using System.Security.Cryptography;
using System.Text;
using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CarLease.Api.Tests;

public class AdminControllerTests
{
    [Theory]
    [InlineData("PENDING_REVIEW", "CONFIRMED", true)]
    [InlineData("PENDING_REVIEW", "REJECTED", true)]
    [InlineData("CONFIRMED", "ACTIVE", true)]
    [InlineData("CONFIRMED", "REJECTED", true)]
    [InlineData("ACTIVE", "COMPLETED", true)]
    [InlineData("PENDING_REVIEW", "ACTIVE", false)]
    [InlineData("ACTIVE", "REJECTED", false)]
    [InlineData("COMPLETED", "ACTIVE", false)]
    [InlineData("REJECTED", "CONFIRMED", false)]
    public async Task UpdateStatus_EnforcesAllowedTransitionsAndAuditsSuccess(
        string current, string next, bool allowed)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, booking) = await CreateAuthenticatedController(db, current);

        var result = await controller.UpdateStatus(booking.Id,
            new UpdateBookingStatusRequest(next, next == "REJECTED" ? "Customer request could not be fulfilled" : null),
            CancellationToken.None);

        if (allowed)
        {
            Assert.IsType<OkObjectResult>(result);
            db.ChangeTracker.Clear();
            Assert.Equal(next, (await db.Bookings.SingleAsync()).Status);
            var audit = Assert.Single(await db.AdminAuditLogs.ToListAsync());
            Assert.Equal(current, audit.PreviousStatus);
            Assert.Equal(next, audit.NewStatus);
            Assert.Equal("admin@example.com", audit.AdminEmail);
        }
        else
        {
            Assert.IsType<ConflictObjectResult>(result);
            Assert.Empty(await db.AdminAuditLogs.ToListAsync());
            Assert.Equal(current, (await db.Bookings.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task UpdateStatus_RequiresReasonForRejection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, booking) = await CreateAuthenticatedController(db, "PENDING_REVIEW");

        var result = await controller.UpdateStatus(booking.Id,
            new UpdateBookingStatusRequest("REJECTED", "  "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.AdminAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task UpdateStatus_RequiresAdministratorSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var booking = new Booking { BookingReference = "RLTEST1", Status = "PENDING_REVIEW" };
        db.Customers.Add(new Customer { Id = booking.CustomerId, CustomerName = "Test", Phone = "+14045550100" });
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.UpdateStatus(booking.Id,
            new UpdateBookingStatusRequest("CONFIRMED", null), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal("PENDING_REVIEW", (await db.Bookings.SingleAsync()).Status);
    }

    private static async Task<(AdminController Controller, Booking Booking)> CreateAuthenticatedController(
        ApplicationDbContext db, string status)
    {
        const string token = "admin-test-session-token";
        db.AdminSessions.Add(new AdminSession
        {
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        var customer = new Customer { CustomerName = "Test Customer", Phone = "+14045550100" };
        var booking = new Booking { Customer = customer, BookingReference = "RLTEST1", Status = status };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"{AdminAuthController.CookieName}={token}";
        return (controller, booking);
    }

    private static AdminController CreateController(ApplicationDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Email"] = "admin@example.com" })
            .Build();
        var controller = new AdminController(db, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    private static ApplicationDbContext CreateDatabase(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
