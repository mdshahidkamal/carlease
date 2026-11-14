using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarLease.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261114190000_InitialCarLeaseSchema")]
public partial class InitialCarLeaseSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Bookings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BookingReference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Pickup = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                PickupPlaceId = table.Column<string>(type: "text", nullable: true),
                PickupLatitude = table.Column<double>(type: "double precision", nullable: true),
                PickupLongitude = table.Column<double>(type: "double precision", nullable: true),
                Dropoff = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DropoffPlaceId = table.Column<string>(type: "text", nullable: true),
                DropoffLatitude = table.Column<double>(type: "double precision", nullable: true),
                DropoffLongitude = table.Column<double>(type: "double precision", nullable: true),
                PickupDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                VehicleType = table.Column<string>(type: "text", nullable: false),
                PassengerCount = table.Column<int>(type: "integer", nullable: false),
                KidsCount = table.Column<int>(type: "integer", nullable: false),
                BaggageCount = table.Column<int>(type: "integer", nullable: false),
                Preferences = table.Column<string>(type: "text", nullable: true),
                CustomerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Bookings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "sdlc_demo_records",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sdlc_demo_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_BookingReference",
            table: "Bookings",
            column: "BookingReference",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Bookings");
        migrationBuilder.DropTable(name: "sdlc_demo_records");
    }
}
