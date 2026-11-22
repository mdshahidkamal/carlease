using System.Security.Cryptography;
using System.Text;
using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController(ApplicationDbContext db, IConfiguration configuration) : ControllerBase
{
    private async Task<bool> IsAdminAsync(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[AdminAuthController.CookieName];
        if (string.IsNullOrWhiteSpace(token)) return false;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var now = DateTime.UtcNow;
        return await db.AdminSessions.AnyAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings([FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(cancellationToken)) return Unauthorized();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Bookings.AsNoTracking().Include(x => x.Customer).Include(x => x.Vehicle).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim().ToUpperInvariant());
        if (from.HasValue) query = query.Where(x => x.CreatedAtUtc >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
        if (to.HasValue) query = query.Where(x => x.CreatedAtUtc < DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.BookingReference.Contains(term) || x.Customer.CustomerName.Contains(term) ||
                x.Customer.Phone.Contains(term) || (x.Customer.Email != null && x.Customer.Email.Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new {
                x.Id, x.BookingReference, x.Status, x.CreatedAtUtc, x.PickupDateTime, x.Pickup, x.Dropoff,
                x.VehicleType, x.PassengerCount, x.KidsCount, x.BaggageCount, x.EstimatedDistanceMiles,
                x.EstimatedAmountDollars, CustomerName = x.Customer.CustomerName, CustomerPhone = x.Customer.Phone,
                CustomerEmail = x.Customer.Email
            }).ToListAsync(cancellationToken);
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("bookings/{id:guid}")]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(cancellationToken)) return Unauthorized();
        var item = await db.Bookings.AsNoTracking().Include(x => x.Customer).Include(x => x.Vehicle)
            .Where(x => x.Id == id).Select(x => new {
                x.Id, x.BookingReference, x.Status, x.CreatedAtUtc, x.PickupDateTime, x.Pickup, x.PickupPlaceId,
                x.PickupLatitude, x.PickupLongitude, x.Dropoff, x.DropoffPlaceId, x.DropoffLatitude, x.DropoffLongitude,
                x.VehicleType, x.VehicleId, x.PassengerCount, x.KidsCount, x.BaggageCount, x.Preferences,
                x.EstimatedDistanceMiles, x.EstimatedAmountDollars, CustomerName = x.Customer.CustomerName,
                CustomerPhone = x.Customer.Phone, CustomerEmail = x.Customer.Email
            }).SingleOrDefaultAsync(cancellationToken);
        if (item is null) return NotFound();
        var history = await db.AdminAuditLogs.AsNoTracking().Where(x => x.BookingId == id)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return Ok(new { booking = item, history });
    }

    [HttpPatch("bookings/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateBookingStatusRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(cancellationToken)) return Unauthorized();
        var target = request.Status?.Trim().ToUpperInvariant();
        var allowed = target is "CONFIRMED" or "REJECTED" or "ACTIVE" or "COMPLETED";
        if (!allowed) return BadRequest(new { message = "Status must be CONFIRMED, REJECTED, ACTIVE or COMPLETED." });
        if (target == "REJECTED" && string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A rejection reason is required." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (booking is null) return NotFound();
        var previous = booking.Status;
        var valid = previous switch
        {
            "PENDING_REVIEW" => target is "CONFIRMED" or "REJECTED",
            "CONFIRMED" => target is "ACTIVE" or "REJECTED",
            "ACTIVE" => target == "COMPLETED",
            _ => false
        };
        if (!valid) return Conflict(new { message = $"Cannot change a booking from {previous} to {target}." });

        booking.Status = target!;
        db.AdminAuditLogs.Add(new AdminAuditLog {
            BookingId = booking.Id, BookingReference = booking.BookingReference, Action = target!,
            PreviousStatus = previous, NewStatus = target!, Reason = request.Reason?.Trim(),
            AdminEmail = configuration["Admin:Email"] ?? "admin", CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { booking.Id, booking.BookingReference, previousStatus = previous, status = booking.Status });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] string period = "daily", [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(cancellationToken)) return Unauthorized();
        var timeZoneId = configuration["Reporting:TimeZone"] ?? "America/New_York";
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { return StatusCode(500, new { message = $"Reporting time zone '{timeZoneId}' is not available on the API host." }); }
        catch (InvalidTimeZoneException) { return StatusCode(500, new { message = $"Reporting time zone '{timeZoneId}' is invalid." }); }

        var selected = (date ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone)).Date;
        DateTime localStart, localEnd;
        switch (period.Trim().ToLowerInvariant())
        {
            case "daily": localStart = selected; localEnd = localStart.AddDays(1); break;
            case "monthly": localStart = new DateTime(selected.Year, selected.Month, 1); localEnd = localStart.AddMonths(1); break;
            case "ytd": localStart = new DateTime(selected.Year, 1, 1); localEnd = selected.AddDays(1); break;
            case "fy": localStart = new DateTime(selected.Year, 1, 1); localEnd = new DateTime(selected.Year + 1, 1, 1); break;
            default: return BadRequest(new { message = "Period must be daily, monthly, ytd or fy." });
        }
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), timeZone);
        var query = db.Bookings.AsNoTracking().Where(x => x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc);
        var total = await query.CountAsync(cancellationToken);
        var pending = await query.CountAsync(x => x.Status == "PENDING_REVIEW", cancellationToken);
        var confirmed = await query.CountAsync(x => x.Status == "CONFIRMED", cancellationToken);
        var rejected = await query.CountAsync(x => x.Status == "REJECTED", cancellationToken);
        var active = await query.CountAsync(x => x.Status == "ACTIVE", cancellationToken);
        var completed = await query.CountAsync(x => x.Status == "COMPLETED", cancellationToken);
        var revenue = await query.Where(x => x.Status == "COMPLETED").SumAsync(x => (decimal?)x.EstimatedAmountDollars, cancellationToken) ?? 0m;
        return Ok(new { period = period.ToLowerInvariant(), timeZone = timeZoneId, startDate = localStart, endDateExclusive = localEnd, totalBookings = total,
            pendingReview = pending, confirmed, rejected, active, completed, completedRideRevenue = revenue,
            averageCompletedRideValue = completed == 0 ? 0m : Math.Round(revenue / completed, 2) });
    }
}

public sealed record UpdateBookingStatusRequest(string Status, string? Reason);
