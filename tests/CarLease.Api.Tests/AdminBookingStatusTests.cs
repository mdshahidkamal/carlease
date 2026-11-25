using System.Net;
using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace CarLease.Api.Tests;

public class AdminBookingStatusTests
{
    [Theory]
    [InlineData("PENDING_REVIEW", "CONFIRMED")]
    [InlineData("PENDING_REVIEW", "REJECTED")]
    [InlineData("CONFIRMED", "ACTIVE")]
    [InlineData("CONFIRMED", "REJECTED")]
    [InlineData("ACTIVE", "COMPLETED")]
    public async Task UpdateStatus_PersistsAllowedTransition(string initial, string next)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Bookings.Add(new Booking { BookingReference = "RLTEST", Status = initial, Customer = new Customer { CustomerName = "Example", Phone = "555" } });
        await db.SaveChangesAsync();
        var result = await Controller(db).UpdateStatus("RLTEST", new(initial, next), CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(next, (await db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateStatus_CompletedBookingDisappearsFromDashboard()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Bookings.Add(new Booking { BookingReference = "RLTEST", Status = "ACTIVE", Customer = new Customer { CustomerName = "Example", Phone = "555" } });
        await db.SaveChangesAsync();
        var controller = Controller(db);
        var before = await controller.GetBookings(CancellationToken.None);
        Assert.Single(Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(before.Result).Value));
        Assert.IsType<NoContentResult>(await controller.UpdateStatus("RLTEST", new("ACTIVE", "COMPLETED"), CancellationToken.None));
        var after = await controller.GetBookings(CancellationToken.None);
        Assert.Empty(Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(after.Result).Value));
        var completed = await controller.GetCompletedBookings(CancellationToken.None);
        Assert.Equal("RLTEST", Assert.Single(Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(completed.Result).Value)).BookingReference);
        db.ChangeTracker.Clear();
        Assert.Equal("COMPLETED", (await db.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("PENDING_REVIEW", "ACTIVE")]
    [InlineData("CONFIRMED", "COMPLETED")]
    [InlineData("ACTIVE", "REJECTED")]
    [InlineData("COMPLETED", "ACTIVE")]
    [InlineData("REJECTED", "CONFIRMED")]
    [InlineData("NO_SHOW", "COMPLETED")]
    [InlineData("PENDING_REVIEW", "DECLINED")]
    public async Task UpdateStatus_RejectsInvalidTransitions(string initial, string next)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Bookings.Add(new Booking { BookingReference = "RLTEST", Status = initial, Customer = new Customer { CustomerName = "Example", Phone = "555" } });
        await db.SaveChangesAsync();
        var result = await Controller(db).UpdateStatus("RLTEST", new(initial, next), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal(initial, (await db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateStatus_RejectsStaleAndMissingBooking()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Bookings.Add(new Booking { BookingReference = "RLTEST", Status = "REJECTED", Customer = new Customer { CustomerName = "Example", Phone = "555" } });
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsType<ConflictObjectResult>(await controller.UpdateStatus("RLTEST", new("PENDING_REVIEW", "CONFIRMED"), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.UpdateStatus("MISSING", new("PENDING_REVIEW", "CONFIRMED"), CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal("REJECTED", (await db.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("Staging", "127.0.0.1", true)]
    [InlineData("Production", "127.0.0.1", true)]
    [InlineData("Development", "192.0.2.1", true)]
    [InlineData("Development", null, true)]
    [InlineData("Development", "127.0.0.1", false)]
    public async Task UpdateStatus_RejectsDisallowedRequests(string environment, string? address, bool header)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Bookings.Add(new Booking { BookingReference = "RLTEST", Status = "PENDING_REVIEW", Customer = new Customer { CustomerName = "Example", Phone = "555" } });
        await db.SaveChangesAsync();
        var controller = Controller(db, environment, address, header);
        var result = await controller.UpdateStatus("RLTEST", new("PENDING_REVIEW", "CONFIRMED"), CancellationToken.None);
        if (header) Assert.IsType<NotFoundResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.Equal("PENDING_REVIEW", (await db.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("evil.example", "http://localhost:5173")]
    [InlineData("localhost", "https://evil.example")]
    [InlineData("localhost", "null")]
    public async Task UpdateStatus_RejectsNonLocalHostOrOrigin(string host, string origin)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var controller = Controller(db);
        controller.Request.Host = new HostString(host);
        controller.Request.Headers.Origin = origin;
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateStatus("RLTEST", new("PENDING_REVIEW", "CONFIRMED"), CancellationToken.None));
    }

    private static ApplicationDbContext CreateDatabase(SqliteConnection connection)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AdminDashboardController Controller(ApplicationDbContext db, string environment = "Development", string? address = "127.0.0.1", bool header = true)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        http.Request.Host = new HostString("localhost", 5292);
        http.Request.Headers.Origin = "http://localhost:5173";
        if (header) http.Request.Headers["X-Admin-Request"] = "booking-status";
        return new AdminDashboardController(db, new TestEnvironment { EnvironmentName = environment }) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
