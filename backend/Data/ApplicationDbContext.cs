using CarLease.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<SdlcDemoRecord> SdlcDemoRecords => Set<SdlcDemoRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();
        booking.HasKey(x => x.Id);
        booking.HasIndex(x => x.BookingReference).IsUnique();
        booking.Property(x => x.BookingReference).HasMaxLength(32);
        booking.Property(x => x.Pickup).HasMaxLength(500);
        booking.Property(x => x.Dropoff).HasMaxLength(500);
        booking.Property(x => x.Preferences).HasMaxLength(1500);
        booking.HasIndex(x => x.CustomerId);
        booking.HasOne(x => x.Customer)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        var customer = modelBuilder.Entity<Customer>();
        customer.HasKey(x => x.Id);
        customer.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
        customer.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        customer.Property(x => x.Email).HasMaxLength(160);
        customer.HasIndex(x => x.Phone).IsUnique();
        customer.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");

        var sdlcDemoRecord = modelBuilder.Entity<SdlcDemoRecord>();
        sdlcDemoRecord.HasNoKey();
        sdlcDemoRecord.ToTable("sdlc_demo_records");
        sdlcDemoRecord.Property(x => x.Id).HasColumnName("id");
        sdlcDemoRecord.Property(x => x.Message).HasColumnName("message");
        sdlcDemoRecord.Property(x => x.Environment).HasColumnName("environment");
        sdlcDemoRecord.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}
