using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CarLease.Api.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var settingsPath = Path.Combine(currentDirectory, "appsettings.json");
        if (!File.Exists(settingsPath))
            settingsPath = Path.Combine(currentDirectory, "backend", "appsettings.json");

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var environmentSettingsPath = Path.Combine(
            Path.GetDirectoryName(settingsPath)!,
            $"appsettings.{environmentName}.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: true)
            .AddJsonFile(environmentSettingsPath, optional: true)
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = configuration["Database:Host"];
            var username = configuration["Database:Username"];
            var password = configuration["Database:Password"];

            if (!string.IsNullOrWhiteSpace(host)
                && !string.IsNullOrWhiteSpace(username)
                && !string.IsNullOrWhiteSpace(password))
            {
                var database = configuration["Database:Name"]
                    ?? throw new InvalidOperationException("Database:Name is not configured.");

                if (!int.TryParse(configuration["Database:Port"], out var port))
                    port = 5432;

                connectionString = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                    SslMode = SslMode.Require
                }.ConnectionString;
            }
            else
            {
                // Used only for EF tooling when scaffolding migrations on a developer machine.
                // Migration scaffolding does not open a database connection.
                connectionString = "Host=localhost;Port=5432;Database=carlease_dev;Username=carlease_dev";
            }
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
