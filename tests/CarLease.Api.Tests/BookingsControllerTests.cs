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
    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task CustomerBookings_RejectInvalidSession(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, _, _) = await CreateAuthenticatedController(db);
        if (scenario == "missing") controller.Request.Headers.Remove("Cookie");
        if (scenario == "invalid") controller.Request.Headers.Cookie = $"{AuthController.SessionCookieName}=invalid";
        if (scenario == "expired") { (await db.CustomerSessions.SingleAsync()).ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); }
        if (scenario == "revoked") { (await db.CustomerSessions.SingleAsync()).RevokedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); }

        Assert.IsType<UnauthorizedObjectResult>((await controller.GetMyBookings(CancellationToken.None)).Result);
        Assert.IsType<UnauthorizedObjectResult>((await controller.GetMyBooking("RLEARL", CancellationToken.None)).Result);
    }

    [Fact]
    public async Task CustomerBookings_ReturnsOwnBookingsAcrossStatusesAndDetailsOnlyForOwner()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, _, customer) = await CreateAuthenticatedController(db);
        var other = new Customer { CustomerName = "Other", Phone = "+1 404 555 0111", Email = "other@example.com" };
        var pickup = DateTime.UtcNow.AddDays(1);
        db.Bookings.AddRange(
            new Booking { Customer = customer, BookingReference = "RLLATE", Pickup = "A", Dropoff = "B", PickupDateTime = pickup.AddHours(1) },
            new Booking { Customer = customer, BookingReference = "RLEARL", Pickup = "Origin", Dropoff = "Destination", PickupDateTime = pickup, VehicleType = "Sedan", PassengerCount = 2, KidsCount = 1, BaggageCount = 2, EstimatedDistanceMiles = 7.4m, EstimatedAmountDollars = 0m, Preferences = "Child seat" },
            new Booking { Customer = customer, BookingReference = "RLDONE", Status = "CONFIRMED", PickupDateTime = pickup },
            new Booking { Customer = customer, BookingReference = "RLREJX", Status = "REJECTED", PickupDateTime = pickup },
            new Booking { Customer = customer, BookingReference = "RLCOMP", Status = "COMPLETED", PickupDateTime = pickup },
            new Booking { Customer = other, BookingReference = "RLOTHR", PickupDateTime = pickup });
        await db.SaveChangesAsync();

        var bookings = Assert.IsType<List<CustomerBookingSummary>>(Assert.IsType<OkObjectResult>((await controller.GetMyBookings(CancellationToken.None)).Result).Value);
        Assert.Equal(new[] { "RLLATE", "RLCOMP", "RLDONE", "RLEARL", "RLREJX" }, bookings.Select(x => x.BookingReference));
        Assert.Equal(new[] { "PENDING_REVIEW", "COMPLETED", "CONFIRMED", "PENDING_REVIEW", "REJECTED" }, bookings.Select(x => x.Status));
        var detail = Assert.IsType<CustomerBookingDetails>(Assert.IsType<OkObjectResult>((await controller.GetMyBooking("RLEARL", CancellationToken.None)).Result).Value);
        Assert.Equal("RLEARL", detail.BookingReference);
        Assert.Equal("Origin", detail.Pickup);
        Assert.Equal("Destination", detail.Dropoff);
        Assert.Equal(pickup, detail.PickupDateTime);
        Assert.Equal("PENDING_REVIEW", detail.Status);
        Assert.Equal("Sedan", detail.VehicleType);
        Assert.Equal(2, detail.PassengerCount);
        Assert.Equal(1, detail.KidsCount);
        Assert.Equal(2, detail.BaggageCount);
        Assert.Equal(7.4m, detail.EstimatedDistanceMiles);
        Assert.Equal(0m, detail.EstimatedAmountDollars);
        Assert.Equal("Child seat", detail.Preferences);
        Assert.IsType<NotFoundObjectResult>((await controller.GetMyBooking("RLOTHR", CancellationToken.None)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetMyBooking("MISSING", CancellationToken.None)).Result);
    }

    [Fact]
    public async Task GetMyBookings_ReturnsEmptyListWhenNoneExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, _, _) = await CreateAuthenticatedController(db);
        var result = await controller.GetMyBookings(CancellationToken.None);
        Assert.Empty(Assert.IsType<List<CustomerBookingSummary>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }

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
        var (controller, vehicle, customer) = await CreateAuthenticatedController(db);
        var request = CreateRequest();
        request.VehicleId = vehicle.Id;
        request.PickupAtUtc = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(-5));
        request.Pickup = "  Pickup address  ";
        request.PickupPlaceId = "pickup-id";
        request.PickupLatitude = 33.749;
        request.PickupLongitude = -84.388;
        request.Dropoff = "  Dropoff address  ";
        request.DropoffPlaceId = "dropoff-id";
        request.DropoffLatitude = 33.64;
        request.DropoffLongitude = -84.427;
        request.EstimatedDistanceMiles = 7.386m;
        request.PassengerCount = 2;
        request.KidsCount = 1;
        request.BaggageCount = 2;
        request.Preferences = "  Child seat  ";

        var result = await controller.Create(request, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        db.ChangeTracker.Clear();
        var booking = Assert.Single(await db.Bookings.ToListAsync());
        Assert.Equal(customer.Id, booking.CustomerId);
        Assert.Equal(vehicle.Id, booking.VehicleId);
        Assert.Equal(vehicle.VehicleName, booking.VehicleType);
        Assert.Matches("^RL[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$", booking.BookingReference);
        Assert.Equal(request.PickupAtUtc.Value.UtcDateTime, booking.PickupDateTime);
        Assert.Equal("Pickup address", booking.Pickup);
        Assert.Equal(request.PickupPlaceId, booking.PickupPlaceId);
        Assert.Equal(request.PickupLatitude, booking.PickupLatitude);
        Assert.Equal(request.PickupLongitude, booking.PickupLongitude);
        Assert.Equal("Dropoff address", booking.Dropoff);
        Assert.Equal(request.DropoffPlaceId, booking.DropoffPlaceId);
        Assert.Equal(request.DropoffLatitude, booking.DropoffLatitude);
        Assert.Equal(request.DropoffLongitude, booking.DropoffLongitude);
        Assert.Equal(7.39m, booking.EstimatedDistanceMiles);
        Assert.Equal(0m, booking.EstimatedAmountDollars);
        Assert.Equal(2, booking.PassengerCount);
        Assert.Equal(1, booking.KidsCount);
        Assert.Equal(2, booking.BaggageCount);
        Assert.Equal("Child seat", booking.Preferences);
        Assert.Equal("PENDING_REVIEW", booking.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(10000000000d)]
    public async Task Create_RejectsMissingOrInvalidDistance(double? miles)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, vehicle, _) = await CreateAuthenticatedController(db);
        var request = CreateRequest();
        request.VehicleId = vehicle.Id;
        request.EstimatedDistanceMiles = miles is null ? null : (decimal)miles.Value;

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_PersistsManualLocationsAndZeroDistance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, vehicle, _) = await CreateAuthenticatedController(db);
        var request = CreateRequest();
        request.VehicleId = vehicle.Id;
        request.EstimatedDistanceMiles = 0m;

        var result = await controller.Create(request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        db.ChangeTracker.Clear();
        var booking = Assert.Single(await db.Bookings.ToListAsync());
        Assert.Equal(0m, booking.EstimatedDistanceMiles);
        Assert.Equal(0m, booking.EstimatedAmountDollars);
        Assert.Null(booking.PickupPlaceId);
        Assert.Null(booking.PickupLatitude);
        Assert.Null(booking.DropoffPlaceId);
        Assert.Null(booking.DropoffLongitude);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("passengers")]
    [InlineData("baggage")]
    [InlineData("past")]
    [InlineData("no-time")]
    public async Task Create_RejectsInvalidVehicleCapacityOrTime(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, vehicle, _) = await CreateAuthenticatedController(db);
        var request = CreateRequest();
        request.VehicleId = vehicle.Id;
        switch (scenario)
        {
            case "missing": request.VehicleId = Guid.NewGuid(); break;
            case "inactive": vehicle.IsActive = false; await db.SaveChangesAsync(); break;
            case "passengers": request.PassengerCount = vehicle.PassengerCapacity + 1; break;
            case "baggage": request.BaggageCount = vehicle.LuggageCapacity + 1; break;
            case "past": request.PickupAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2); break;
            case "no-time": request.PickupAtUtc = null; break;
        }
        var result = await controller.Create(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_AcceptsCurrentMinuteAndMaximumCapacities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var (controller, vehicle, _) = await CreateAuthenticatedController(db);
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest();
        request.VehicleId = vehicle.Id;
        request.PickupAtUtc = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        request.PassengerCount = vehicle.PassengerCapacity;
        request.BaggageCount = vehicle.LuggageCapacity;
        var result = await controller.Create(request, CancellationToken.None);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Vehicles_ReturnsOnlyActiveVehiclesInNameOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        db.Vehicles.AddRange(
            new Vehicle { VehicleName = "SUV", PassengerCapacity = 6, LuggageCapacity = 4 },
            new Vehicle { VehicleName = "Sedan", PassengerCapacity = 3, LuggageCapacity = 2 },
            new Vehicle { VehicleName = "Inactive", PassengerCapacity = 1, IsActive = false });
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await new VehiclesController(db).Get(CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("Sedan", json);
        Assert.Contains("SUV", json);
        Assert.DoesNotContain("Inactive", json);
        Assert.True(json.IndexOf("SUV", StringComparison.Ordinal) < json.IndexOf("Sedan", StringComparison.Ordinal));
        Assert.Contains("\"PassengerCapacity\":3", json);
        Assert.Contains("\"LuggageCapacity\":2", json);
    }

    private static async Task<(BookingsController Controller, Vehicle Vehicle, Customer Customer)> CreateAuthenticatedController(ApplicationDbContext db)
    {
        var customer = new Customer { CustomerName = "Alex", Email = "alex@example.com", Phone = "+1 404 555 0100" };
        var vehicle = new Vehicle { VehicleName = "Sedan", PassengerCapacity = 3, LuggageCapacity = 2 };
        const string sessionToken = "test-session-token";
        db.CustomerSessions.Add(new CustomerSession
        {
            Customer = customer,
            TokenHash = AuthTokenHash.HashSessionToken(sessionToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        });
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"{AuthController.SessionCookieName}={sessionToken}";
        return (controller, vehicle, customer);
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
        EstimatedDistanceMiles = 7.4m,
        PickupAtUtc = DateTimeOffset.UtcNow.AddDays(1),
        VehicleId = Guid.NewGuid(),
        PassengerCount = 1
    };
}
