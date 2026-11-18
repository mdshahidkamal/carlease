using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
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

        var phone = request.Phone.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var customerByPhone = await db.Customers.SingleOrDefaultAsync(x => x.Phone == phone, cancellationToken);
        var customerByEmail = email is null
            ? null
            : await db.Customers.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (customerByPhone is not null && customerByEmail is not null && customerByPhone.Id != customerByEmail.Id)
            return Conflict(new { message = "The provided phone number and email belong to different customer records." });

        var customer = customerByEmail ?? customerByPhone;
        if (customer is null)
        {
            customer = new Customer { CustomerName = request.CustomerName.Trim(), Phone = phone, Email = email };
            db.Customers.Add(customer);
        }
        else
        {
            customer.CustomerName = request.CustomerName.Trim();
            customer.Phone = phone;
            if (email is not null)
                customer.Email = email;
        }

        var booking = new Booking
        {
            BookingReference = CreateReference(), Customer = customer, Pickup = request.Pickup.Trim(), PickupPlaceId = request.PickupPlaceId,
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
