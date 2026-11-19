using CarLease.Api.Controllers;
using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarLease.Api.Tests;

public class AuthControllerTests
{
    private const string OtpPepper = "test-auth-pepper-value-with-at-least-32-chars";

    [Fact]
    public async Task RegisterAndVerify_CreatesCustomerAndSetsSessionCookie()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var emailSender = new FakeOtpEmailSender();
        var controller = CreateController(db, emailSender);

        var startResult = await controller.Register(new RegisterRequest
        {
            Name = "Alex Customer",
            Email = "Alex@example.com",
            Phone = "+1 404 555 0100",
            ConsentAccepted = true
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(startResult).StatusCode);
        Assert.Empty(await db.Customers.ToListAsync());
        var challenge = Assert.Single(await db.AuthOtpChallenges.ToListAsync());
        Assert.Equal("alex@example.com", challenge.Email);
        Assert.Equal("REGISTER", challenge.Purpose);
        Assert.Equal(AuthTokenHash.HashOtp(challenge.Email, emailSender.LastCode!, OtpPepper), challenge.OtpHash);
        Assert.Equal(32, challenge.OtpHash.Length);

        var verifyResult = await controller.VerifyOtp(new VerifyOtpRequest
        {
            Email = "alex@example.com",
            Code = emailSender.LastCode!
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(verifyResult).StatusCode);
        var customer = Assert.Single(await db.Customers.ToListAsync());
        Assert.Equal("Alex Customer", customer.CustomerName);
        Assert.Equal("+1 404 555 0100", customer.Phone);
        Assert.NotNull(customer.ConsentAcceptedAtUtc);
        var session = Assert.Single(await db.CustomerSessions.ToListAsync());
        Assert.Equal(customer.Id, session.CustomerId);
        Assert.Equal(32, session.TokenHash.Length);
        Assert.Contains(AuthController.SessionCookieName, controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("HttpOnly", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyOtp_ConsumesChallengeAfterFiveIncorrectAttempts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var emailSender = new FakeOtpEmailSender();
        var controller = CreateController(db, emailSender);
        await controller.Register(new RegisterRequest
        {
            Name = "Alex Customer",
            Email = "alex@example.com",
            Phone = "+1 404 555 0100",
            ConsentAccepted = true
        }, CancellationToken.None);
        var wrongCode = emailSender.LastCode == "000000" ? "000001" : "000000";

        IActionResult? result = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await controller.VerifyOtp(new VerifyOtpRequest
            {
                Email = "alex@example.com",
                Code = wrongCode
            }, CancellationToken.None);
        }

        Assert.IsType<BadRequestObjectResult>(result);
        var challenge = Assert.Single(await db.AuthOtpChallenges.ToListAsync());
        Assert.Equal(5, challenge.FailedAttempts);
        Assert.NotNull(challenge.ConsumedAtUtc);
        Assert.Empty(await db.Customers.ToListAsync());
    }

    [Fact]
    public async Task Login_ReturnsNotFound_ForUnregisteredEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var emailSender = new FakeOtpEmailSender();
        var controller = CreateController(db, emailSender);

        var result = await controller.Login(new EmailRequest { Email = "unknown@example.com" }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Null(emailSender.LastCode);
    }

    private static ApplicationDbContext CreateDatabase(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AuthController CreateController(ApplicationDbContext db, FakeOtpEmailSender emailSender)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:OtpPepper"] = OtpPepper })
            .Build();
        var controller = new AuthController(
            db,
            emailSender,
            configuration,
            new TestWebHostEnvironment(),
            NullLogger<AuthController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private sealed class FakeOtpEmailSender : IOtpEmailSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(string recipient, string otp, CancellationToken cancellationToken)
        {
            LastCode = otp;
            return Task.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CarLease.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
