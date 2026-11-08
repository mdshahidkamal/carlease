using CarLease.Api.Data;
using CarLease.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarLease.Api.Tests;

public class DatabaseConnectionEndpointTests
{
    [Fact]
    public async Task Check_ReturnsConnected_WhenDatabaseIsReachable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        var result = await DatabaseConnectionEndpoint.Check(db, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);
        var response = Assert.IsType<DatabaseConnectionEndpoint.ConnectionStatus>(((IValueHttpResult)result).Value);
        Assert.Equal("connected", response.Status);
    }
}
