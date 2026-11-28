using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CarLease.BookingAutoReject;

public sealed class Function
{
    private const string SecretId = "carlease/nonprod/database-credentials";
    private static readonly IAmazonSecretsManager Secrets = new AmazonSecretsManagerClient();

    public async Task<RunResult> FunctionHandler(JsonElement input, ILambdaContext context)
    {
        var cutoff = DateTime.UtcNow;
        var total = 0;
        try
        {
            var batchSize = ReadBatchSize(Environment.GetEnvironmentVariable("BATCH_SIZE"));
            var remaining = context.RemainingTime - TimeSpan.FromSeconds(10);
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("Insufficient time remaining to process bookings.");
            using var timeout = new CancellationTokenSource(remaining);
            var secret = await Secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = SecretId }, timeout.Token);
            var connectionString = BuildConnectionString(secret.SecretString,
                Environment.GetEnvironmentVariable("DB_HOST"), Environment.GetEnvironmentVariable("DB_NAME"),
                Environment.GetEnvironmentVariable("DB_PORT"));
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
            await using var db = new ApplicationDbContext(options);
            var worker = new BookingAutoRejectWorker(db);
            int count;
            do
            {
                count = await worker.ProcessBatchAsync(cutoff, batchSize, timeout.Token);
                total += count;
                Log(context, "batch_completed", total, count);
            } while (count != 0);
            Log(context, "completed", total, 0);
            return new RunResult(total, cutoff);
        }
        catch (Exception ex)
        {
            Log(context, "failed", total, 0, ex.GetType().Name);
            throw;
        }
    }

    public static int ReadBatchSize(string? value)
    {
        if (value is null) return 100;
        if (!int.TryParse(value, out var result) || result is < 1 or > 10000)
            throw new InvalidOperationException("BATCH_SIZE must be an integer between 1 and 10000.");
        return result;
    }

    public static string BuildConnectionString(string? secretJson, string? host, string? database, string? portValue)
    {
        if (string.IsNullOrWhiteSpace(secretJson) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("Database secret, DB_HOST and DB_NAME are required.");
        if (!int.TryParse(portValue ?? "5432", out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("DB_PORT must be a valid TCP port.");
        using var document = JsonDocument.Parse(secretJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("username", out var username) || username.ValueKind != JsonValueKind.String ||
            !document.RootElement.TryGetProperty("password", out var password) || password.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(username.GetString()) || string.IsNullOrWhiteSpace(password.GetString()))
            throw new InvalidOperationException("Database secret must contain non-empty username and password strings.");
        return new NpgsqlConnectionStringBuilder
        {
            Host = host, Port = port, Database = database, Username = username.GetString(), Password = password.GetString(),
            SslMode = SslMode.Require
        }.ConnectionString;
    }

    private static void Log(ILambdaContext context, string eventName, int total, int batchCount, string? errorType = null) =>
        context.Logger.LogLine(JsonSerializer.Serialize(new { eventName, requestId = context.AwsRequestId, total, batchCount, errorType }));
}

public sealed record RunResult(int RejectedCount, DateTime CutoffUtc);
