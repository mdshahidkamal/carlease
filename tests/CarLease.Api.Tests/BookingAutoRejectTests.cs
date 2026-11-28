using CarLease.Api.Data;
using CarLease.BookingAutoReject;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CarLease.Api.Tests;

public class BookingAutoRejectTests
{
    [Theory]
    [InlineData(null, 100)]
    [InlineData("1", 1)]
    [InlineData("10000", 10000)]
    public void BatchSizeAcceptsValidValues(string? value, int expected) => Assert.Equal(expected, Function.ReadBatchSize(value));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("10001")]
    [InlineData("abc")]
    public void BatchSizeRejectsInvalidValues(string value) => Assert.Throws<InvalidOperationException>(() => Function.ReadBatchSize(value));

    [Fact]
    public void ConnectionStringUsesSecretCredentialsAndTls()
    {
        var result = new NpgsqlConnectionStringBuilder(Function.BuildConnectionString("{\"username\":\"user\",\"password\":\"a;b\"}", "host", "db", null));
        Assert.Equal("user", result.Username);
        Assert.Equal("a;b", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
        Assert.Equal(5432, result.Port);
    }

    [Fact]
    public void ConnectionStringRejectsMissingCredentialsAndInvalidPort()
    {
        Assert.Throws<InvalidOperationException>(() => Function.BuildConnectionString("{}", "host", "db", "5432"));
        Assert.Throws<InvalidOperationException>(() => Function.BuildConnectionString("{\"username\":\"u\",\"password\":\"p\"}", "host", "db", "65536"));
    }

    [PostgresFact]
    public async Task ProcessesOnlyExpiredEligibleBookingsInBatchesAndIsIdempotent()
    {
        await using var fixture = await PostgresFixture.CreateAsync();
        var cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await fixture.InsertAsync("pending", "PENDING_REVIEW", cutoff.AddSeconds(-2));
        await fixture.InsertAsync("confirmed", "CONFIRMED", cutoff.AddSeconds(-1));
        await fixture.InsertAsync("boundary", "PENDING_REVIEW", cutoff);
        await fixture.InsertAsync("future", "CONFIRMED", cutoff.AddSeconds(1));
        await fixture.InsertAsync("active", "ACTIVE", cutoff.AddSeconds(-1));
        await using var db = fixture.CreateContext();
        var worker = new BookingAutoRejectWorker(db);
        Assert.Equal(1, await worker.ProcessBatchAsync(cutoff, 1, CancellationToken.None));
        Assert.Equal(1, await worker.ProcessBatchAsync(cutoff, 1, CancellationToken.None));
        Assert.Equal(0, await worker.ProcessBatchAsync(cutoff, 1, CancellationToken.None));
        var histories = await db.BookingStatusHistories.OrderBy(x => x.PreviousStatus).ToListAsync();
        Assert.Equal(new[] { "CONFIRMED", "PENDING_REVIEW" }, histories.Select(x => x.PreviousStatus));
        Assert.All(histories, h => { Assert.Equal("AUTO_REJECTED", h.NewStatus); Assert.Equal("RIDE_DATE_EXPIRED", h.Reason); Assert.Equal("SYSTEM", h.ChangedBy); Assert.Equal(DateTimeKind.Utc, h.ChangedAtUtc.Kind); });
        var statuses = await db.Bookings.ToDictionaryAsync(x => x.BookingReference, x => x.Status);
        Assert.Equal("AUTO_REJECTED", statuses["pending"]);
        Assert.Equal("AUTO_REJECTED", statuses["confirmed"]);
        Assert.Equal("PENDING_REVIEW", statuses["boundary"]);
        Assert.Equal("CONFIRMED", statuses["future"]);
        Assert.Equal("ACTIVE", statuses["active"]);
    }

    [PostgresFact]
    public async Task AuditFailureRollsBackStatusUpdate()
    {
        await using var fixture = await PostgresFixture.CreateAsync();
        var cutoff = DateTime.UtcNow;
        await fixture.InsertAsync("pending", "PENDING_REVIEW", cutoff.AddMinutes(-1));
        await fixture.ExecuteAsync("ALTER TABLE \"BookingStatusHistories\" ADD CONSTRAINT reject_audit CHECK (\"Reason\" <> 'RIDE_DATE_EXPIRED')");
        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<DbUpdateException>(() => new BookingAutoRejectWorker(db).ProcessBatchAsync(cutoff, 10, CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal("PENDING_REVIEW", (await db.Bookings.SingleAsync()).Status);
        Assert.Empty(await db.BookingStatusHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task AdminTransitionWinsWithoutBeingOverwritten()
    {
        await using var fixture = await PostgresFixture.CreateAsync();
        var cutoff = DateTime.UtcNow;
        await fixture.InsertAsync("confirmed", "CONFIRMED", cutoff.AddMinutes(-1));
        await using var admin = fixture.CreateContext();
        await using var transaction = await admin.Database.BeginTransactionAsync();
        Assert.Equal(1, await admin.Bookings.Where(x => x.Status == "CONFIRMED")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "ACTIVE")));
        await using var workerDb = fixture.CreateContext();
        var worker = new BookingAutoRejectWorker(workerDb).ProcessBatchAsync(cutoff, 10, CancellationToken.None);
        await transaction.CommitAsync();
        Assert.Equal(0, await worker);
        Assert.Equal("ACTIVE", (await workerDb.Bookings.SingleAsync()).Status);
        Assert.Empty(await workerDb.BookingStatusHistories.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentWorkersDoNotDuplicateAudits()
    {
        await using var fixture = await PostgresFixture.CreateAsync();
        var cutoff = DateTime.UtcNow;
        await fixture.InsertAsync("pending", "PENDING_REVIEW", cutoff.AddMinutes(-1));
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var counts = await Task.WhenAll(new BookingAutoRejectWorker(first).ProcessBatchAsync(cutoff, 10, CancellationToken.None), new BookingAutoRejectWorker(second).ProcessBatchAsync(cutoff, 10, CancellationToken.None));
        Assert.Equal(1, counts.Sum());
        Assert.Single(await first.BookingStatusHistories.AsNoTracking().ToListAsync());
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CARLEASE_TEST_POSTGRES")))
                Skip = "Set CARLEASE_TEST_POSTGRES to a disposable PostgreSQL test database to run integration tests.";
        }
    }

    private sealed class PostgresFixture : IAsyncDisposable
    {
        private readonly string schema = "auto_reject_test_" + Guid.NewGuid().ToString("N");
        private readonly string connectionString;
        private PostgresFixture(string connectionString) => this.connectionString = connectionString;
        public static async Task<PostgresFixture> CreateAsync()
        {
            var fixture = new PostgresFixture(Environment.GetEnvironmentVariable("CARLEASE_TEST_POSTGRES")!);
            await using var connection = new NpgsqlConnection(fixture.connectionString);
            await connection.OpenAsync();
            await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{fixture.schema}\"", connection)) await command.ExecuteNonQueryAsync();
            await fixture.ExecuteAsync("""
                CREATE TABLE "Bookings" (
                  "Id" uuid PRIMARY KEY, "BookingReference" text NOT NULL, "CustomerId" uuid NOT NULL,
                  "Pickup" text NOT NULL, "PickupPlaceId" text, "PickupLatitude" double precision, "PickupLongitude" double precision,
                  "Via" text, "Dropoff" text NOT NULL, "DropoffPlaceId" text, "DropoffLatitude" double precision, "DropoffLongitude" double precision,
                  "EstimatedDistance_Miles" numeric, "EstimatedAmount_Dollars" numeric NOT NULL, "PickupDateTime" timestamptz NOT NULL,
                  "VehicleType" text NOT NULL, "VehicleId" uuid, "PassengerCount" integer NOT NULL, "KidsCount" integer NOT NULL,
                  "BaggageCount" integer NOT NULL, "Preferences" text, "Status" text NOT NULL, "CreatedAtUtc" timestamptz NOT NULL
                );
                CREATE TABLE "BookingStatusHistories" (
                  "Id" uuid PRIMARY KEY, "BookingId" uuid NOT NULL REFERENCES "Bookings" ("Id"), "PreviousStatus" text NOT NULL,
                  "NewStatus" text NOT NULL, "Reason" text NOT NULL, "ChangedBy" text NOT NULL, "ChangedAtUtc" timestamptz NOT NULL
                );
                """);
            return fixture;
        }
        private string ScopedConnectionString => new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
        public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ScopedConnectionString).Options);
        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ScopedConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        public async Task InsertAsync(string reference, string status, DateTime pickup)
        {
            await using var db = CreateContext();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Bookings" ("Id", "BookingReference", "CustomerId", "Pickup", "Dropoff", "EstimatedAmount_Dollars", "PickupDateTime", "VehicleType", "PassengerCount", "KidsCount", "BaggageCount", "Status", "CreatedAtUtc")
                VALUES ({Guid.NewGuid()}, {reference}, {Guid.NewGuid()}, '', '', 0, {pickup}, '', 1, 0, 0, {status}, {DateTime.UtcNow})
                """);
        }
        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
