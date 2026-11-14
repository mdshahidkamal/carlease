using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
        await db.Database.EnsureCreatedAsync();

        db.SdlcDemoRecords.AddRange(
            new SdlcDemoRecord
            {
                Id = 2,
                Message = "Second test record",
                Environment = "NonProduction",
                CreatedAtUtc = new DateTime(2026, 11, 10, 12, 1, 0, DateTimeKind.Utc)
            },
            new SdlcDemoRecord
            {
                Id = 1,
                Message = "First test record",
                Environment = "NonProduction",
                CreatedAtUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();

        var controller = new SdlcDemoController(db, new TestHostEnvironment("NonProduction"));
        var response = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var records = Assert.IsAssignableFrom<IReadOnlyList<SdlcDemoRecord>>(ok.Value);

        Assert.Equal(2, records.Count);
        Assert.Equal("First test record", records[0].Message);
        Assert.Equal("NonProduction", records[0].Environment);
        Assert.Equal("Second test record", records[1].Message);
    }

    [Fact]
    public async Task Create_SavesRecord_UsingCurrentApplicationEnvironment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var controller = new SdlcDemoController(db, new TestHostEnvironment("NonProduction"));
        var response = await controller.Create(
            new CreateSdlcDemoRecordRequest("SDLC deployment smoke test"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var record = Assert.IsType<SdlcDemoRecord>(created.Value);

        Assert.True(record.Id > 0);
        Assert.Equal("SDLC deployment smoke test", record.Message);
        Assert.Equal("NonProduction", record.Environment);
        Assert.Single(await db.SdlcDemoRecords.ToListAsync());
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CarLease.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
