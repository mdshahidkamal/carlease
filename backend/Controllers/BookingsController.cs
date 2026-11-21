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
    [HttpPost]
    public async Task<ActionResult<CreateBookingResponse>> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var sessionToken = Request.Cookies[AuthController.SessionCookieName];
        if (string.IsNullOrEmpty(sessionToken))
            return Unauthorized(new { message = "Please sign in before creating a booking." });

        var sessionTokenHash = AuthTokenHash.HashSessionToken(sessionToken);
        var now = DateTime.UtcNow;
        var session = await db.CustomerSessions
            .Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.TokenHash == sessionTokenHash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
        if (session is null)
            return Unauthorized(new { message = "Your sign-in session has expired. Please sign in again." });

        if (request.EstimatedDistanceMiles is null or < 0 or > 9999999999.99m)
            return BadRequest(new { message = "A valid driving distance is required." });

        if (request.PickupAtUtc is null)
            return BadRequest(new { message = "Select a pickup date and time." });
        var pickupDateTime = request.PickupAtUtc.Value.UtcDateTime;
        var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        if (pickupDateTime < currentMinute)
            return BadRequest(new { message = "The pickup date and time cannot be in the past." });

        var vehicle = await db.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.VehicleId && x.IsActive, cancellationToken);
        if (vehicle is null)
            return BadRequest(new { message = "Please select an available vehicle." });
        if (request.PassengerCount > vehicle.PassengerCapacity)
            return BadRequest(new { message = $"The selected vehicle supports no more than {vehicle.PassengerCapacity} passengers." });
        if (request.BaggageCount > vehicle.LuggageCapacity)
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
            PassengerCount = request.PassengerCount, KidsCount = request.KidsCount, BaggageCount = request.BaggageCount,
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
