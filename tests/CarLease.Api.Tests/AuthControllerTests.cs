using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;
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
    public async Task Register_ReturnsSesExceptionDetailInDevelopment_WhenEmailDeliveryFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var emailSender = new FakeOtpEmailSender(new AmazonClientException("Failed to resolve AWS credentials for the test."));
        var controller = CreateController(db, emailSender);

        var result = await controller.Register(new RegisterRequest
        {
            Name = "Test Customer",
            Email = "registration@example.com",
            Phone = "+1 404 555 0188",
            ConsentAccepted = true
        }, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, response.StatusCode);
        var detail = response.Value?.GetType().GetProperty("detail")?.GetValue(response.Value) as string;
        Assert.Equal("Failed to resolve AWS credentials for the test.", detail);
    }

    [Fact]
    public async Task Register_ReturnsSesHttpStatusAndErrorMessage_WhenSesRejectsRequest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var response = new FakeWebResponseData(
            HttpStatusCode.Forbidden,
            "{\"message\":\"Email address is not verified.\",\"code\":\"MessageRejected\"}");
        var emailSender = new FakeOtpEmailSender(new HttpErrorResponseException("AWS HTTP error.", response));
        var controller = CreateController(db, emailSender);

        var result = await controller.Register(new RegisterRequest
        {
            Name = "SES Rejection Test",
            Email = "ses-rejection@example.com",
            Phone = "+1 404 555 0186",
            ConsentAccepted = true
        }, CancellationToken.None);

        var apiResponse = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, apiResponse.StatusCode);
        var detail = apiResponse.Value?.GetType().GetProperty("detail")?.GetValue(apiResponse.Value) as string;
        Assert.Equal("SES returned HTTP 403 (MessageRejected): Email address is not verified.", detail);
    }

    [Fact]
    public async Task Register_ReturnsGatewayTimeout_WhenEmailProviderTimesOut()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var emailSender = new FakeOtpEmailSender(new TimeoutException("The operation has timed out."));
        var controller = CreateController(db, emailSender);

        var result = await controller.Register(new RegisterRequest
        {
            Name = "Timeout Test Customer",
            Email = "timeout@example.com",
            Phone = "+1 404 555 0187",
            ConsentAccepted = true
        }, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, response.StatusCode);
        var detail = response.Value?.GetType().GetProperty("detail")?.GetValue(response.Value) as string;
        Assert.Equal("The operation has timed out.", detail);
    }

    [Fact]
    public async Task LoginAndVerify_CreatesSessionForExistingCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDatabase(connection);
        var customer = new Customer
        {
            CustomerName = "Test Login Customer",
            Email = "login@example.com",
            Phone = "+1 404 555 0199"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var emailSender = new FakeOtpEmailSender();
        var controller = CreateController(db, emailSender);
        var loginResult = await controller.Login(
            new EmailRequest { Email = "LOGIN@example.com" },
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(loginResult).StatusCode);
        Assert.NotNull(emailSender.LastCode);
        var challenge = Assert.Single(await db.AuthOtpChallenges.ToListAsync());
        Assert.Equal("login@example.com", challenge.Email);
        Assert.Equal("LOGIN", challenge.Purpose);

        var verifyResult = await controller.VerifyOtp(new VerifyOtpRequest
        {
            Email = "login@example.com",
            Code = emailSender.LastCode!
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(verifyResult).StatusCode);
        Assert.Equal(customer.Id, Assert.Single(await db.Customers.ToListAsync()).Id);
        var session = Assert.Single(await db.CustomerSessions.ToListAsync());
        Assert.Equal(customer.Id, session.CustomerId);
        Assert.Contains(AuthController.SessionCookieName, controller.Response.Headers.SetCookie.ToString());
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

    private sealed class FakeWebResponseData(HttpStatusCode statusCode, string body) : IWebResponseData
    {
        public long ContentLength => Encoding.UTF8.GetByteCount(body);
        public string ContentType => "application/json";
        public HttpStatusCode StatusCode => statusCode;
        public bool IsSuccessStatusCode => (int)statusCode < 400;
        public IHttpResponseBody ResponseBody => new FakeHttpResponseBody(body);
        public string[] GetHeaderNames() => [];
        public bool IsHeaderPresent(string headerName) => false;
        public string GetHeaderValue(string headerName) => string.Empty;
    }

    private sealed class FakeHttpResponseBody(string body) : IHttpResponseBody
    {
        public Stream OpenResponse() => new MemoryStream(Encoding.UTF8.GetBytes(body));
        public Task<Stream> OpenResponseAsync() => Task.FromResult(OpenResponse());
        public void Dispose() { }
    }

    private sealed class FakeOtpEmailSender(Exception? failure = null) : IOtpEmailSender
    {
        public string? LastCode { get; private set; }

        public Task SendAsync(string recipient, string otp, CancellationToken cancellationToken)
        {
            if (failure is not null)
                return Task.FromException(failure);

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
