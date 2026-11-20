using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarLease.Api.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenNoSessionCookieIsSent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var controller = CreateController(db);

        var result = await controller.Create(CreateRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Empty(await db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_AssociatesBookingWithAuthenticatedCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var customer = new Customer { CustomerName = "Alex", Email = "alex@example.com", Phone = "+1 404 555 0100" };
        const string sessionToken = "test-session-token";
        db.CustomerSessions.Add(new CustomerSession
        {
            Customer = customer,
            TokenHash = AuthTokenHash.HashSessionToken(sessionToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie =
            $"{AuthController.SessionCookieName}={sessionToken}";

        var result = await controller.Create(CreateRequest(), CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        var booking = Assert.Single(await db.Bookings.ToListAsync());
        Assert.Equal(customer.Id, booking.CustomerId);
        Assert.Equal("alex@example.com", (await db.Customers.SingleAsync()).Email);
        Assert.Equal("PENDING_REVIEW", booking.Status);
    }

    private static ApplicationDbContext CreateDatabase(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static BookingsController CreateController(ApplicationDbContext db)
    {
        var controller = new BookingsController(db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static CreateBookingRequest CreateRequest() => new()
    {
        Pickup = "Pickup address",
        Dropoff = "Dropoff address",
        PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        PickupTime = new TimeOnly(12, 0),
        VehicleType = "Executive Sedan",
        PassengerCount = 1
    };
}
