using System.Net;
using CarLease.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController(ApplicationDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("booking-counts")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<AdminBookingCounts>> GetBookingCounts(CancellationToken cancellationToken)
    {
        if (!IsLocalDevelopment()) return NotFound();

        var counts = await db.Bookings.AsNoTracking()
            .Where(booking => booking.Status == "PENDING_REVIEW" || booking.Status == "CONFIRMED" || booking.Status == "ACTIVE")
            .GroupBy(booking => booking.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);

        return Ok(new AdminBookingCounts(
            counts.GetValueOrDefault("PENDING_REVIEW"),
            counts.GetValueOrDefault("CONFIRMED"),
            counts.GetValueOrDefault("ACTIVE")));
    }

    [HttpGet("operational-summary")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<AdminOperationalSummary>> GetOperationalSummary(DateTimeOffset? startUtc, DateTimeOffset? endUtc, CancellationToken cancellationToken)
    {
        if (!IsLocalDevelopment()) return NotFound();
        if (startUtc is null || endUtc is null || endUtc <= startUtc || endUtc - startUtc > TimeSpan.FromHours(26))
            return BadRequest(new { message = "A valid local-day interval is required." });

        var start = startUtc.Value.UtcDateTime;
        var end = endUtc.Value.UtcDateTime;
        var bookings = await db.Bookings.AsNoTracking()
            .CountAsync(booking => booking.CreatedAtUtc >= start && booking.CreatedAtUtc < end, cancellationToken);
        var pickups = 0;
        var revenue = 0m;
        var scheduled = db.Bookings.AsNoTracking()
            .Where(booking => booking.PickupDateTime >= start && booking.PickupDateTime < end && booking.Status != "REJECTED")
            .Select(booking => new { booking.Status, booking.EstimatedAmountDollars });
        await foreach (var booking in scheduled.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            pickups++;
            if (booking.Status is "CONFIRMED" or "ACTIVE" or "COMPLETED") revenue += booking.EstimatedAmountDollars;
        }
        return Ok(new AdminOperationalSummary(bookings, pickups, revenue));
    }

    [HttpGet("bookings")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<List<AdminBooking>>> GetBookings(CancellationToken cancellationToken) => ListBookings(false, cancellationToken);

    [HttpGet("completed-bookings")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<List<AdminBooking>>> GetCompletedBookings(CancellationToken cancellationToken) => ListBookings(true, cancellationToken);

    private async Task<ActionResult<List<AdminBooking>>> ListBookings(bool completed, CancellationToken cancellationToken)
    {
        if (!IsLocalDevelopment()) return NotFound();

        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => completed ? booking.Status == "COMPLETED" : booking.Status != "COMPLETED")
            .OrderBy(booking => booking.Status == "PENDING_REVIEW" ? 0 : booking.Status == "CONFIRMED" ? 1 : booking.Status == "ACTIVE" ? 2 : 3)
            .ThenByDescending(booking => booking.PickupDateTime)
            .ThenBy(booking => booking.BookingReference)
            .Select(booking => new AdminBooking(booking.BookingReference, booking.Customer.CustomerName,
                booking.Customer.Phone, booking.PickupDateTime, booking.Pickup, booking.Dropoff,
                booking.VehicleType, booking.Status, booking.PassengerCount, booking.BaggageCount, booking.Preferences))
            .ToListAsync(cancellationToken);
        return Ok(bookings);
    }

    [HttpPut("bookings/{bookingReference}/status")]
    [Consumes("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> UpdateStatus(string bookingReference, UpdateAdminBookingStatus request, CancellationToken cancellationToken)
    {
        if (!IsLocalDevelopment()) return NotFound();
        if (!IsLoopbackHost(Request.Host.Host) || !Uri.TryCreate(Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps) || !IsLoopbackHost(origin.Host) ||
            Request.Headers["X-Admin-Request"] != "booking-status")
            return BadRequest(new { message = "A local admin request is required." });

        var allowed = (request.ExpectedStatus, request.Status) switch
        {
            ("PENDING_REVIEW", "CONFIRMED" or "REJECTED") => true,
            ("CONFIRMED", "ACTIVE" or "REJECTED") => true,
            ("ACTIVE", "COMPLETED") => true,
            _ => false
        };
        if (!allowed) return BadRequest(new { message = "This booking status transition is not allowed." });

        var updated = await db.Bookings
            .Where(booking => booking.BookingReference == bookingReference && booking.Status == request.ExpectedStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(booking => booking.Status, request.Status), cancellationToken);
        if (updated == 1) return NoContent();
        if (!await db.Bookings.AsNoTracking().AnyAsync(booking => booking.BookingReference == bookingReference, cancellationToken))
            return NotFound(new { message = "Booking not found." });
        return Conflict(new { message = "The booking status has changed. Refresh and try again." });
    }

    private bool IsLocalDevelopment()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp?.IsIPv4MappedToIPv6 == true) remoteIp = remoteIp.MapToIPv4();
        return environment.IsDevelopment() && remoteIp is not null && IPAddress.IsLoopback(remoteIp);
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
}

public record AdminBookingCounts(int PendingReview, int Confirmed, int Active);
public record AdminOperationalSummary(int Bookings, int Pickups, decimal EstimatedRevenueDollars);
public record AdminBooking(string BookingReference, string CustomerName, string Phone, DateTime PickupDateTime,
    string Pickup, string Dropoff, string VehicleType, string Status, int PassengerCount, int BaggageCount, string? Preferences);
public record UpdateAdminBookingStatus(string ExpectedStatus, string Status);
