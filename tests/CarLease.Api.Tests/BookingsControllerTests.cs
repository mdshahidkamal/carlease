using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarLease.Api.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task Create_ReusesCustomer_WhenPhoneAndEmailMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var controller = new BookingsController(db);

        var firstResponse = await controller.Create(CreateRequest("Alex", "+1 404 555 0100", "Alex@Example.com"), CancellationToken.None);
        var secondResponse = await controller.Create(CreateRequest("Alex Updated", "+1 404 555 0100", "alex@example.com"), CancellationToken.None);

        var firstResult = Assert.IsType<ObjectResult>(firstResponse.Result);
        var secondResult = Assert.IsType<ObjectResult>(secondResponse.Result);
        Assert.Equal(StatusCodes.Status201Created, firstResult.StatusCode);
        Assert.Equal(StatusCodes.Status201Created, secondResult.StatusCode);
        var customer = Assert.Single(await db.Customers.ToListAsync());
        var bookings = await db.Bookings.OrderBy(x => x.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, bookings.Count);
        Assert.All(bookings, booking => Assert.Equal(customer.Id, booking.CustomerId));
        var responseBody = Assert.IsType<CreateBookingResponse>(secondResult.Value);
        Assert.Equal(bookings[1].Id, responseBody.BookingId);
        Assert.Equal(bookings[1].BookingReference, responseBody.BookingReference);
        Assert.Equal("PENDING_REVIEW", responseBody.Status);
        Assert.All(bookings, booking => Assert.Equal("PENDING_REVIEW", booking.Status));
        Assert.Equal("Alex Updated", customer.CustomerName);
        Assert.Equal("alex@example.com", customer.Email);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPhoneAndEmailIdentifyDifferentCustomers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var controller = new BookingsController(db);

        await controller.Create(CreateRequest("First Customer", "111", "first@example.com"), CancellationToken.None);
        await controller.Create(CreateRequest("Second Customer", "222", "second@example.com"), CancellationToken.None);
        var result = await controller.Create(CreateRequest("Mismatch", "111", "second@example.com"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(2, await db.Bookings.CountAsync());
        Assert.Equal(2, await db.Customers.CountAsync());
    }

    private static CreateBookingRequest CreateRequest(string name, string phone, string? email) => new()
    {
        Pickup = "Pickup address",
        Dropoff = "Dropoff address",
        PickupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        PickupTime = new TimeOnly(12, 0),
        VehicleType = "Executive Sedan",
        PassengerCount = 1,
        CustomerName = name,
        Phone = phone,
        Email = email
    };
}
