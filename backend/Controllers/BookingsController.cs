using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        var pickupDateTime = request.PickupDate.ToDateTime(request.PickupTime, DateTimeKind.Utc);
        if (pickupDateTime < DateTime.UtcNow)
            return BadRequest(new { message = "The pickup date and time must be in the future." });

        var vehicleCapacity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Executive Sedan"] = 3,
            ["Luxury SUV"] = 6,
            ["Stretch Limousine"] = 8,
            ["Luxury Van"] = 12
        };
        if (!vehicleCapacity.TryGetValue(request.VehicleType, out var capacity))
            return BadRequest(new { message = "Please select a valid vehicle type." });
        if (request.PassengerCount > capacity)
            return BadRequest(new { message = $"The selected vehicle supports no more than {capacity} passengers." });

        var booking = new Booking
        {
            BookingReference = CreateReference(), Customer = session.Customer, Pickup = request.Pickup.Trim(), PickupPlaceId = request.PickupPlaceId,
            PickupLatitude = request.PickupLatitude, PickupLongitude = request.PickupLongitude,
            Dropoff = request.Dropoff.Trim(), DropoffPlaceId = request.DropoffPlaceId,
            DropoffLatitude = request.DropoffLatitude, DropoffLongitude = request.DropoffLongitude,
            PickupDateTime = pickupDateTime, VehicleType = request.VehicleType,
            PassengerCount = request.PassengerCount, KidsCount = request.KidsCount, BaggageCount = request.BaggageCount,
            Preferences = request.Preferences?.Trim()
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CreateBookingResponse(booking.Id, booking.BookingReference, booking.Status));
    }

    private static string CreateReference()
    {
        var code = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..6]);
        return $"RLS-{code[..12]}";
    }
}
