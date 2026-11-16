using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarLease.Api.Tests;

public class SdlcDemoControllerTests
{
    [Fact]
    public async Task Get_ReturnsDatabaseRecords_AsOrderedJsonPayload()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE sdlc_demo_records
            (
                id INTEGER NOT NULL,
                message TEXT NOT NULL,
                environment TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );

            INSERT INTO sdlc_demo_records (id, message, environment, created_at_utc)
            VALUES
                (2, 'Second test record', 'Nonprod', '2026-11-10T12:01:00.0000000Z'),
                (1, 'First test record', 'Nonprod', '2026-11-10T12:00:00.0000000Z');
            """);

        var controller = new SdlcDemoController(db);
        var response = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var records = Assert.IsAssignableFrom<IReadOnlyList<SdlcDemoRecord>>(ok.Value);

        Assert.Equal(2, records.Count);
        Assert.Equal(1, records[0].Id);
        Assert.Equal("First test record", records[0].Message);
        Assert.Equal("Nonprod", records[0].Environment);
        Assert.Equal(2, records[1].Id);
    }
}
