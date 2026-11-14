using CarLease.Api.Data;
using CarLease.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarLease.Api.Tests;

public class DatabaseConnectionEndpointTests
{
    [Fact]
    public async Task Check_ReturnsConnected_WhenDatabaseIsReachable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);

        var result = await DatabaseConnectionEndpoint.Check(
            db,
            NullLogger<ApplicationDbContext>.Instance,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);
        var response = Assert.IsType<DatabaseConnectionEndpoint.ConnectionStatus>(((IValueHttpResult)result).Value);
        Assert.Equal("connected", response.Status);
    }
}
