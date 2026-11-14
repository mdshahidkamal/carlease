using CarLease.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Sockets;

namespace CarLease.Api.Endpoints;

public static class DatabaseConnectionEndpoint
{
    public static async Task<IResult> Check(
        ApplicationDbContext db,
        ILogger<ApplicationDbContext> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            return Results.Ok(new ConnectionStatus("connected"));
        }
        catch (NpgsqlException exception)
        {
            logger.LogWarning(
                "Database connection check failed. PostgreSQL SQLSTATE {SqlState}: {ErrorMessage}",
                exception.SqlState,
                exception.Message);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database connection failed.");
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Database connection check failed because the configured connection string is malformed.");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database connection failed.");
        }
        catch (SocketException exception)
        {
            logger.LogWarning(
                "Database connection check failed because the database host could not be reached: {ErrorMessage}",
                exception.Message);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database connection failed.");
        }
    }

    public sealed record ConnectionStatus(string Status);
}
