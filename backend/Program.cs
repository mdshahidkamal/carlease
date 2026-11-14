using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    var databaseHost = builder.Configuration["Database:Host"]
        ?? throw new InvalidOperationException("Database:Host is not configured.");
    var databaseName = builder.Configuration["Database:Name"]
        ?? throw new InvalidOperationException("Database:Name is not configured.");
    var databaseUsername = builder.Configuration["Database:Username"]
        ?? throw new InvalidOperationException("Database:Username is not configured.");
    var databasePassword = builder.Configuration["Database:Password"]
        ?? throw new InvalidOperationException("Database:Password is not configured.");

    if (!int.TryParse(builder.Configuration["Database:Port"], out var databasePort))
        databasePort = 5432;

    var npgsqlConnection = new NpgsqlConnectionStringBuilder
    {
        Host = databaseHost,
        Port = databasePort,
        Database = databaseName,
        Username = databaseUsername,
        Password = databasePassword,
        SslMode = SslMode.Require
    };

    connectionString = npgsqlConnection.ConnectionString;
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

// Serve the compiled React application from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

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

// React Router handles browser-side routes such as /booking.
app.MapFallbackToFile("index.html");

app.Run();
