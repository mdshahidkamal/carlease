using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarLease.BookingAutoReject;

public sealed class BookingAutoRejectWorker(ApplicationDbContext db)
{
    public async Task<int> ProcessBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Cutoff must be UTC.", nameof(cutoffUtc));
        if (batchSize is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(batchSize));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var bookings = await db.Bookings.FromSqlInterpolated($"""
            SELECT * FROM "Bookings"
            WHERE "Status" IN ({BookingStatuses.PendingReview}, {BookingStatuses.Confirmed})
              AND "PickupDateTime" < {cutoffUtc}
            ORDER BY "PickupDateTime", "Id"
            LIMIT {batchSize}
            FOR UPDATE
            """).ToListAsync(cancellationToken);

        var changedAtUtc = DateTime.UtcNow;
        foreach (var booking in bookings)
        {
            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = booking.Status,
                NewStatus = BookingStatuses.AutoRejected,
                Reason = "RIDE_DATE_EXPIRED",
                ChangedBy = "SYSTEM",
                ChangedAtUtc = changedAtUtc
            });
            booking.Status = BookingStatuses.AutoRejected;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return bookings.Count;
    }
}
