using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarLease.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
public partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.15")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("CarLease.Api.Models.Booking", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<string>("BookingReference")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("CustomerName")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("Dropoff")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<double?>("DropoffLatitude")
                .HasColumnType("double precision");

            b.Property<double?>("DropoffLongitude")
                .HasColumnType("double precision");

            b.Property<string>("DropoffPlaceId")
                .HasColumnType("text");

            b.Property<string>("Email")
                .HasMaxLength(160)
                .HasColumnType("character varying(160)");

            b.Property<int>("KidsCount")
                .HasColumnType("integer");

            b.Property<int>("BaggageCount")
                .HasColumnType("integer");

            b.Property<int>("PassengerCount")
                .HasColumnType("integer");

            b.Property<string>("Phone")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("character varying(30)");

            b.Property<string>("Pickup")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<DateTime>("PickupDateTime")
                .HasColumnType("timestamp with time zone");

            b.Property<double?>("PickupLatitude")
                .HasColumnType("double precision");

            b.Property<double?>("PickupLongitude")
                .HasColumnType("double precision");

            b.Property<string>("PickupPlaceId")
                .HasColumnType("text");

            b.Property<string>("Preferences")
                .HasColumnType("text");

            b.Property<string>("Status")
                .IsRequired()
                .HasColumnType("text");

            b.Property<string>("VehicleType")
                .IsRequired()
                .HasColumnType("text");

            b.HasKey("Id");

            b.HasIndex("BookingReference")
                .IsUnique();

            b.ToTable("Bookings");
        });

        modelBuilder.Entity("CarLease.Api.Models.SdlcDemoRecord", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasColumnName("id")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            b.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at_utc");

            b.Property<string>("Environment")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)")
                .HasColumnName("environment");

            b.Property<string>("Message")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("character varying(500)")
                .HasColumnName("message");

            b.HasKey("Id");

            b.ToTable("sdlc_demo_records");
        });
#pragma warning restore 612, 618
    }
}
