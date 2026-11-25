using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController(ApplicationDbContext db) : ControllerBase
{
    private const int MinimumLeadHours = 24;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<List<CustomerBookingSummary>>> GetMyBookings(CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        if (session is null)
            return Unauthorized(new { message = "Please sign in to view your bookings." });

        var bookings = await db.Bookings.AsNoTracking()
            .Where(x => x.CustomerId == session.CustomerId && x.Status != "COMPLETED")
            .OrderBy(x => x.Status == "PENDING_REVIEW" ? 0 : 1)
            .ThenBy(x => x.PickupDateTime).ThenBy(x => x.BookingReference)
            .Take(5)
            .Select(x => new CustomerBookingSummary(x.BookingReference, x.PickupDateTime, x.Status))
            .ToListAsync(cancellationToken);
        return Ok(bookings);
    }

    [HttpGet("{bookingReference}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CustomerBookingDetails>> GetMyBooking(string bookingReference, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        if (session is null)
            return Unauthorized(new { message = "Please sign in to view your bookings." });

        var booking = await db.Bookings.AsNoTracking()
            .Where(x => x.CustomerId == session.CustomerId && x.BookingReference == bookingReference)
            .Select(x => new CustomerBookingDetails(x.BookingReference, x.Pickup, x.Dropoff, x.PickupDateTime, x.Status,
                x.VehicleType, x.PassengerCount, x.KidsCount, x.BaggageCount,
                x.EstimatedDistanceMiles, x.EstimatedAmountDollars, x.Preferences))
            .SingleOrDefaultAsync(cancellationToken);
        return booking is null ? NotFound(new { message = "Booking not found." }) : Ok(booking);
    }

    [HttpPost]
    public async Task<ActionResult<CreateBookingResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        if (session is null)
            return Unauthorized(new { message = "Please sign in before creating a booking." });
        var now = DateTime.UtcNow;

        if (request.EstimatedDistanceMiles is null or < 0 or > 9999999999.99m)
            return BadRequest(new { message = "A valid driving distance is required." });

        if (request.PickupAtUtc is null)
            return BadRequest(new { message = "Select a pickup date and time." });
        var pickupDateTime = request.PickupAtUtc.Value.UtcDateTime;
        var earliest = now.AddHours(MinimumLeadHours);
        var earliestMinute = new DateTime(earliest.Year, earliest.Month, earliest.Day, earliest.Hour, earliest.Minute, 0, DateTimeKind.Utc);
        if (pickupDateTime < earliestMinute)
            return BadRequest(new { message = $"Pickups must be booked at least {MinimumLeadHours} hours in advance." });

        if (request.PassengerCount is < 1 or > 12)
            return BadRequest(new { message = "Please enter a valid passenger count." });
        if (request.BaggageCount is null or < 0 or > 30)
            return BadRequest(new { message = "Please enter a valid baggage count." });
        var vehicle = await db.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.VehicleId && x.IsActive, cancellationToken);
        if (vehicle is null)
            return BadRequest(new { message = "Please select an available vehicle." });
        if (request.PassengerCount > vehicle.PassengerCapacity)
            return BadRequest(new { message = $"The selected vehicle supports no more than {vehicle.PassengerCapacity} passengers." });
        if (request.BaggageCount.Value > vehicle.LuggageCapacity)
            return BadRequest(new { message = $"The selected vehicle supports no more than {vehicle.LuggageCapacity} bags." });

        var booking = new Booking
        {
            Customer = session.Customer, Pickup = request.Pickup.Trim(), PickupPlaceId = request.PickupPlaceId,
            PickupLatitude = request.PickupLatitude, PickupLongitude = request.PickupLongitude,
            Dropoff = request.Dropoff.Trim(), DropoffPlaceId = request.DropoffPlaceId,
            DropoffLatitude = request.DropoffLatitude, DropoffLongitude = request.DropoffLongitude,
            EstimatedDistanceMiles = Math.Round(request.EstimatedDistanceMiles.Value, 2, MidpointRounding.AwayFromZero),
            EstimatedAmountDollars = 0m,
            PickupDateTime = pickupDateTime, VehicleId = vehicle.Id, VehicleType = vehicle.VehicleName,
            PassengerCount = request.PassengerCount, KidsCount = request.KidsCount, BaggageCount = request.BaggageCount.Value,
            Preferences = request.Preferences?.Trim()
        };

        db.Bookings.Add(booking);
        const int maxReferenceAttempts = 12;
        for (var attempt = 0; attempt < maxReferenceAttempts; attempt++)
        {
            booking.BookingReference = CreateReference();
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return StatusCode(StatusCodes.Status201Created, new CreateBookingResponse(booking.Id, booking.BookingReference, booking.Status));
            }
            catch (DbUpdateException ex) when (IsBookingReferenceConflict(ex) && attempt < maxReferenceAttempts - 1)
            {
                db.Entry(booking).State = EntityState.Added;
            }
        }

        return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not generate a unique booking reference. Please try again." });
    }

    private Task<CustomerSession?> FindSessionAsync(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[AuthController.SessionCookieName];
        if (string.IsNullOrEmpty(token)) return Task.FromResult<CustomerSession?>(null);
        var hash = AuthTokenHash.HashSessionToken(token);
        var now = DateTime.UtcNow;
        return db.CustomerSessions.Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
    }

    private static string CreateReference()
    {
        const string prefix = "RL";
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[6];
        chars[0] = prefix[0];
        chars[1] = prefix[1];
        for (var index = 2; index < chars.Length; index++)
            chars[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    private static bool IsBookingReferenceConflict(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException postgresException)
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(postgresException.ConstraintName, "IX_Bookings_BookingReference", StringComparison.Ordinal);

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_Bookings_BookingReference", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("BookingReference", StringComparison.OrdinalIgnoreCase)
                && message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase));
    }
}
