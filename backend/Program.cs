using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Prefer an explicitly configured connection string (local development).
// In ECS, construct it from the individual Secrets Manager JSON-key values.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var dbHost = builder.Configuration["DB_HOST"];
    var dbName = builder.Configuration["DB_NAME"];
    var dbUsername = builder.Configuration["DB_USERNAME"];
    var dbPassword = builder.Configuration["DB_PASSWORD"];
    var dbPortValue = builder.Configuration["DB_PORT"] ?? "5432";

    if (string.IsNullOrWhiteSpace(dbHost) ||
        string.IsNullOrWhiteSpace(dbName) ||
        string.IsNullOrWhiteSpace(dbUsername) ||
        string.IsNullOrWhiteSpace(dbPassword))
    {
        throw new InvalidOperationException(
            "Database configuration is incomplete. Configure ConnectionStrings:DefaultConnection " +
            "or DB_HOST, DB_NAME, DB_USERNAME and DB_PASSWORD.");
    }

    if (!int.TryParse(dbPortValue, out var dbPort))
    {
        throw new InvalidOperationException("DB_PORT must be a valid TCP port number.");
    }

    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = dbHost,
        Port = dbPort,
        Database = dbName,
        Username = dbUsername,
        Password = dbPassword,
        SslMode = SslMode.Require
    }.ConnectionString;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

const string frontendPolicy = "FrontendPolicy";
builder.Services.AddCors(options => options.AddPolicy(frontendPolicy, policy =>
    policy.WithOrigins(
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            builder.Configuration["FrontendUrl"] ?? "https://localhost")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(frontendPolicy);
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/checkconnectionstring", CarLease.Api.Endpoints.DatabaseConnectionEndpoint.Check);
app.MapControllers();

app.Run();
