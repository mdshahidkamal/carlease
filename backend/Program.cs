using Amazon;
using Amazon.SimpleEmailV2;
using CarLease.Api.Data;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var otpPepper = builder.Configuration["Auth:OtpPepper"];
if (string.IsNullOrWhiteSpace(otpPepper) || otpPepper.Length < 32)
    throw new InvalidOperationException("Configure Auth:OtpPepper with a secret of at least 32 characters.");
if (string.IsNullOrWhiteSpace(builder.Configuration["Auth:EmailFrom"]))
    throw new InvalidOperationException("Configure Auth:EmailFrom with a verified Amazon SES sender address.");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client(
    new AmazonSimpleEmailServiceV2Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(
            builder.Configuration["AWS_REGION"] ?? builder.Configuration["AWS:Region"] ?? "ap-southeast-2")
    }));
builder.Services.AddScoped<IOtpEmailSender, SesOtpEmailSender>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many authentication requests. Please wait a minute and try again." },
            cancellationToken);
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

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
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();
var exceptionLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledApiException");
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var traceId = context.TraceIdentifier;
    exceptionLogger.LogError(exception, "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
        context.Request.Method, context.Request.Path, traceId);

    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "The request could not be completed.",
        Detail = app.Environment.IsDevelopment()
            ? exception?.GetBaseException().Message
            : $"An unexpected error occurred. Reference: {traceId}",
        Instance = context.Request.Path
    };
    problem.Extensions["traceId"] = traceId;

    context.Response.StatusCode = problem.Status.Value;
    await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
}));

// Serve React frontend files.
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(frontendPolicy);
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/checkconnectionstring", CarLease.Api.Endpoints.DatabaseConnectionEndpoint.Check);
app.MapControllers();
// React Router fallback: serve index.html for frontend routes.
app.MapFallbackToFile("index.html");

app.Run();
