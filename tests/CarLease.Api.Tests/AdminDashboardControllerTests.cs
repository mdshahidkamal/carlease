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

public class AdminDashboardControllerTests
{
    [Fact]
    public async Task GetBookingCounts_ReturnsCountsAcrossCustomersAndIgnoresOtherStatuses()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var first = new Customer { CustomerName = "First", Phone = "111" };
        var second = new Customer { CustomerName = "Second", Phone = "222" };
        db.Bookings.AddRange(
            new Booking { Customer = first, BookingReference = "RLA", Status = "PENDING_REVIEW" },
            new Booking { Customer = second, BookingReference = "RLB", Status = "PENDING_REVIEW" },
            new Booking { Customer = first, BookingReference = "RLC", Status = "CONFIRMED" },
            new Booking { Customer = second, BookingReference = "RLD", Status = "ACTIVE" },
            new Booking { Customer = first, BookingReference = "RLE", Status = "COMPLETED" },
            new Booking { Customer = second, BookingReference = "RLF", Status = "REJECTED" });
        await db.SaveChangesAsync();

        var result = await CreateController(db, "Development", IPAddress.Loopback).GetBookingCounts(CancellationToken.None);

        var counts = Assert.IsType<AdminBookingCounts>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, counts.PendingReview);
        Assert.Equal(1, counts.Confirmed);
        Assert.Equal(1, counts.Active);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task GetBookingCounts_EmptyDatabaseReturnsZerosForLoopback(string address)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var result = await CreateController(db, "Development", IPAddress.Parse(address)).GetBookingCounts(CancellationToken.None);
        var counts = Assert.IsType<AdminBookingCounts>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new AdminBookingCounts(0, 0, 0), counts);
    }

    [Theory]
    [InlineData("Production", "127.0.0.1")]
    [InlineData("Staging", "127.0.0.1")]
    [InlineData("Development", "192.0.2.1")]
    [InlineData("Development", null)]
    public async Task GetBookingCounts_DeniesNonLocalOrNonDevelopmentRequests(string environment, string? address)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var controller = CreateController(db, environment, address is null ? null : IPAddress.Parse(address));
        var result = await controller.GetBookingCounts(CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.IsType<NotFoundResult>((await controller.GetBookings(CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetCompletedBookings(CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetOperationalSummary(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None)).Result);
    }

    [Fact]
    public async Task GetOperationalSummary_UsesHalfOpenLocalDayAndStatusRules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var start = new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(25);
        var customer = new Customer { CustomerName = "Example", Phone = "555" };
        db.Bookings.AddRange(
            new Booking { Customer = customer, BookingReference = "RLA", CreatedAtUtc = start, PickupDateTime = start, Status = "PENDING_REVIEW", EstimatedAmountDollars = 100m },
            new Booking { Customer = customer, BookingReference = "RLB", CreatedAtUtc = end.AddTicks(-1), PickupDateTime = end.AddTicks(-1), Status = "CONFIRMED", EstimatedAmountDollars = 10.12m },
            new Booking { Customer = customer, BookingReference = "RLC", CreatedAtUtc = start.AddTicks(-1), PickupDateTime = start.AddHours(1), Status = "ACTIVE", EstimatedAmountDollars = 20.23m },
            new Booking { Customer = customer, BookingReference = "RLD", CreatedAtUtc = end, PickupDateTime = start.AddHours(2), Status = "COMPLETED", EstimatedAmountDollars = 30.34m },
            new Booking { Customer = customer, BookingReference = "RLE", CreatedAtUtc = start, PickupDateTime = start.AddHours(3), Status = "REJECTED", EstimatedAmountDollars = 40m },
            new Booking { Customer = customer, BookingReference = "RLF", CreatedAtUtc = start, PickupDateTime = end, Status = "CONFIRMED", EstimatedAmountDollars = 50m },
            new Booking { Customer = customer, BookingReference = "RLG", CreatedAtUtc = start, PickupDateTime = start.AddTicks(-1), Status = "ACTIVE", EstimatedAmountDollars = 60m },
            new Booking { Customer = customer, BookingReference = "RLH", CreatedAtUtc = start, PickupDateTime = start.AddHours(4), Status = "NO_SHOW", EstimatedAmountDollars = 70m });
        await db.SaveChangesAsync();
        var result = await CreateController(db, "Development", IPAddress.Loopback).GetOperationalSummary(start, end, CancellationToken.None);
        var summary = Assert.IsType<AdminOperationalSummary>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new AdminOperationalSummary(6, 5, 60.69m), summary);
    }

    [Fact]
    public async Task GetOperationalSummary_EmptyAndInvalidIntervals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var controller = CreateController(db, "Development", IPAddress.Loopback);
        var start = new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero);
        var empty = await controller.GetOperationalSummary(start, start.AddHours(23), CancellationToken.None);
        Assert.Equal(new AdminOperationalSummary(0, 0, 0m), Assert.IsType<AdminOperationalSummary>(Assert.IsType<OkObjectResult>(empty.Result).Value));
        Assert.IsType<BadRequestObjectResult>((await controller.GetOperationalSummary(null, start, CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetOperationalSummary(start, start, CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetOperationalSummary(start, start.AddHours(27), CancellationToken.None)).Result);
    }

    [Fact]
    public async Task GetBookings_ReturnsActualBookingAndCustomerFieldsAcrossStatuses()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var customer = new Customer { CustomerName = "Example", Phone = "555" };
        var time = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        db.Bookings.AddRange(
            new Booking { Customer = customer, BookingReference = "RLA", Pickup = "Origin", Dropoff = "Destination", PickupDateTime = time, VehicleType = "Sedan", Status = "PENDING_REVIEW", PassengerCount = 2, BaggageCount = 1, Preferences = "Child seat" },
            new Booking { Customer = customer, BookingReference = "RLF", PickupDateTime = time.AddDays(1), Status = "PENDING_REVIEW" },
            new Booking { Customer = customer, BookingReference = "RLG", PickupDateTime = time.AddDays(1), Status = "PENDING_REVIEW" },
            new Booking { Customer = customer, BookingReference = "RLB", PickupDateTime = time.AddDays(2), Status = "CONFIRMED" },
            new Booking { Customer = customer, BookingReference = "RLH", PickupDateTime = time.AddDays(3), Status = "CONFIRMED" },
            new Booking { Customer = customer, BookingReference = "RLC", PickupDateTime = time.AddDays(4), Status = "ACTIVE" },
            new Booking { Customer = customer, BookingReference = "RLI", PickupDateTime = time.AddHours(1), Status = "ACTIVE" },
            new Booking { Customer = customer, BookingReference = "RLD", PickupDateTime = time.AddDays(5), Status = "REJECTED" },
            new Booking { Customer = customer, BookingReference = "RLE", PickupDateTime = time.AddDays(6), Status = "COMPLETED" });
        await db.SaveChangesAsync();
        var result = await CreateController(db, "Development", IPAddress.Loopback).GetBookings(CancellationToken.None);
        var bookings = Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new[] { "RLF", "RLG", "RLA", "RLH", "RLB", "RLC", "RLI", "RLD" }, bookings.Select(booking => booking.BookingReference));
        Assert.Equal(new[] { "PENDING_REVIEW", "PENDING_REVIEW", "PENDING_REVIEW", "CONFIRMED", "CONFIRMED", "ACTIVE", "ACTIVE", "REJECTED" }, bookings.Select(booking => booking.Status));
        var pending = bookings.Single(booking => booking.BookingReference == "RLA");
        Assert.Equal("Example", pending.CustomerName);
        Assert.Equal("555", pending.Phone);
        Assert.Equal(time, pending.PickupDateTime);
        Assert.Equal("Origin", pending.Pickup);
        Assert.Equal("Destination", pending.Dropoff);
        Assert.Equal("Sedan", pending.VehicleType);
        Assert.Equal(2, pending.PassengerCount);
        Assert.Equal(1, pending.BaggageCount);
        Assert.Equal("Child seat", pending.Preferences);
        var completed = await CreateController(db, "Development", IPAddress.Loopback).GetCompletedBookings(CancellationToken.None);
        var completedBookings = Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(completed.Result).Value);
        Assert.Equal("RLE", Assert.Single(completedBookings).BookingReference);
        Assert.All(completedBookings, booking => Assert.Equal("COMPLETED", booking.Status));
    }

    [Fact]
    public async Task GetBookings_EmptyDatabaseReturnsEmptyList()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var result = await CreateController(db, "Development", IPAddress.IPv6Loopback).GetBookings(CancellationToken.None);
        Assert.Empty(Assert.IsType<List<AdminBooking>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    private static ApplicationDbContext CreateDatabase(SqliteConnection connection)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AdminDashboardController CreateController(ApplicationDbContext db, string environment, IPAddress? address)
    {
        var controller = new AdminDashboardController(db, new TestEnvironment { EnvironmentName = environment });
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = address;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
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
