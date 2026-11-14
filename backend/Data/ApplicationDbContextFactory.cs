using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace CarLease.Api.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = Environment.GetEnvironmentVariable("Database__Host");
            var username = Environment.GetEnvironmentVariable("Database__Username");
            var password = Environment.GetEnvironmentVariable("Database__Password");

            if (!string.IsNullOrWhiteSpace(host)
                && !string.IsNullOrWhiteSpace(username)
                && !string.IsNullOrWhiteSpace(password))
            {
                var database = Environment.GetEnvironmentVariable("Database__Name")
                    ?? throw new InvalidOperationException("Database__Name is not configured.");

                if (!int.TryParse(Environment.GetEnvironmentVariable("Database__Port"), out var port))
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
                // Used only by EF tooling when scaffolding migrations on a developer machine.
                // The tooling does not open a database connection for migrations add.
                connectionString = "Host=localhost;Port=5432;Database=carlease_dev;Username=carlease_dev";
            }
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
