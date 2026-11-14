using CarLease.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<SdlcDemoRecord> SdlcDemoRecords => Set<SdlcDemoRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();
        booking.HasKey(x => x.Id);
        booking.ToTable("Bookings");
        booking.HasIndex(x => x.BookingReference).IsUnique();
        booking.Property(x => x.BookingReference).HasMaxLength(32);
        booking.Property(x => x.Pickup).HasMaxLength(500);
        booking.Property(x => x.Dropoff).HasMaxLength(500);
        booking.Property(x => x.CustomerName).HasMaxLength(120);
        booking.Property(x => x.Phone).HasMaxLength(30);
        booking.Property(x => x.Email).HasMaxLength(160);

        var sdlcDemoRecord = modelBuilder.Entity<SdlcDemoRecord>();
        sdlcDemoRecord.HasKey(x => x.Id);
        sdlcDemoRecord.ToTable("sdlc_demo_records");
        sdlcDemoRecord.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        sdlcDemoRecord.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();
        sdlcDemoRecord.Property(x => x.Environment)
            .HasColumnName("environment")
            .HasMaxLength(32)
            .IsRequired();
        sdlcDemoRecord.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();
    }
}
