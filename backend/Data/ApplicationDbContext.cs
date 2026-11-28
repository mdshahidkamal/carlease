using CarLease.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<AuthOtpChallenge> AuthOtpChallenges => Set<AuthOtpChallenge>();

    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();

    public DbSet<SdlcDemoRecord> SdlcDemoRecords => Set<SdlcDemoRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var booking = modelBuilder.Entity<Booking>();
        booking.HasKey(x => x.Id);
        booking.HasIndex(x => x.BookingReference).IsUnique();
        booking.Property(x => x.BookingReference).HasMaxLength(32);
        booking.Property(x => x.Pickup).HasMaxLength(500);
        booking.Property(x => x.Via).HasMaxLength(500);
        booking.Property(x => x.Dropoff).HasMaxLength(500);
        booking.Property(x => x.Preferences).HasMaxLength(1500);
        booking.Property(x => x.EstimatedDistanceMiles).HasColumnName("EstimatedDistance_Miles").HasPrecision(12, 2);
        booking.Property(x => x.EstimatedAmountDollars).HasColumnName("EstimatedAmount_Dollars").HasPrecision(12, 2);
        booking.HasIndex(x => x.CustomerId);
        booking.HasOne(x => x.Customer)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        booking.HasIndex(x => x.VehicleId);
        booking.HasOne(x => x.Vehicle)
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        var history = modelBuilder.Entity<BookingStatusHistory>();
        history.ToTable("BookingStatusHistory");
        history.HasKey(x => x.Id);
        history.Property(x => x.PreviousStatus).HasMaxLength(50).IsRequired();
        history.Property(x => x.NewStatus).HasMaxLength(50).IsRequired();
        history.Property(x => x.Reason).HasMaxLength(100).IsRequired();
        history.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired();
        history.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);

        var vehicle = modelBuilder.Entity<Vehicle>();
        vehicle.HasKey(x => x.Id);
        vehicle.Property(x => x.VehicleName).HasMaxLength(100).IsRequired();

        var customer = modelBuilder.Entity<Customer>();
        customer.HasKey(x => x.Id);
        customer.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
        customer.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        customer.Property(x => x.Email).HasMaxLength(160);
        customer.HasIndex(x => x.Phone).IsUnique();
        customer.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");

        var otpChallenge = modelBuilder.Entity<AuthOtpChallenge>();
        otpChallenge.HasKey(x => x.Id);
        otpChallenge.Property(x => x.Email).HasMaxLength(160).IsRequired();
        otpChallenge.Property(x => x.Purpose).HasMaxLength(16).IsRequired();
        otpChallenge.Property(x => x.OtpHash).IsRequired();
        otpChallenge.Property(x => x.RegistrationName).HasMaxLength(120);
        otpChallenge.Property(x => x.RegistrationPhone).HasMaxLength(30);
        otpChallenge.HasIndex(x => new { x.Email, x.RequestedAtUtc });

        var customerSession = modelBuilder.Entity<CustomerSession>();
        customerSession.HasKey(x => x.Id);
        customerSession.Property(x => x.TokenHash).IsRequired();
        customerSession.HasIndex(x => x.TokenHash).IsUnique();
        customerSession.HasIndex(x => new { x.CustomerId, x.ExpiresAtUtc });
        customerSession.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        var sdlcDemoRecord = modelBuilder.Entity<SdlcDemoRecord>();
        sdlcDemoRecord.HasNoKey();
        sdlcDemoRecord.ToTable("sdlc_demo_records");
        sdlcDemoRecord.Property(x => x.Id).HasColumnName("id");
        sdlcDemoRecord.Property(x => x.Message).HasColumnName("message");
        sdlcDemoRecord.Property(x => x.Environment).HasColumnName("environment");
        sdlcDemoRecord.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}
