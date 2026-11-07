using CarLease.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();
        booking.HasKey(x => x.Id);
        booking.HasIndex(x => x.BookingReference).IsUnique();
        booking.Property(x => x.BookingReference).HasMaxLength(32);
        booking.Property(x => x.Pickup).HasMaxLength(500);
        booking.Property(x => x.Dropoff).HasMaxLength(500);
        booking.Property(x => x.CustomerName).HasMaxLength(120);
        booking.Property(x => x.Phone).HasMaxLength(30);
        booking.Property(x => x.Email).HasMaxLength(160);
    }
}
