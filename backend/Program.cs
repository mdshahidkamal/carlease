using CarLease.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.")));

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
