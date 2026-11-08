using CarLease.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Endpoints;

public static class DatabaseConnectionEndpoint
{
    public static async Task<IResult> Check(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new ConnectionStatus("connected"))
            : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database connection failed.");
    }

    public sealed record ConnectionStatus(string Status);
}
