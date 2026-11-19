using System.Security.Cryptography;
using Amazon.Runtime;
using CarLease.Api.Data;
using CarLease.Api.DTOs;
using CarLease.Api.Models;
using CarLease.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CarLease.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(
    ApplicationDbContext db,
    IOtpEmailSender emailSender,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string SessionCookieName = "carlease_session";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaximumAttempts = 5;
    private const int SessionDays = 30;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!request.ConsentAccepted)
            return BadRequest(new AuthMessageResponse("Please accept the Terms of Service and Privacy Policy to register."));

        var email = NormalizeEmail(request.Email);
        var phone = request.Phone.Trim();
        if (await db.Customers.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, cancellationToken))
            return Conflict(new AuthMessageResponse("An account already exists for this email. Switch to Login."));
        if (await db.Customers.AnyAsync(x => x.Phone == phone, cancellationToken))
            return Conflict(new AuthMessageResponse("This phone number is already associated with an account. Sign in with its registered email."));

        return await CreateChallengeAsync(email, "REGISTER", request.Name.Trim(), phone, DateTime.UtcNow, cancellationToken);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(EmailRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (!await db.Customers.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, cancellationToken))
            return NotFound(new AuthMessageResponse("No account was found for this email. Please register first."));

        return await CreateChallengeAsync(email, "LOGIN", null, null, null, cancellationToken);
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(EmailRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var challenge = await db.AuthOtpChallenges
            .Where(x => x.Email == email && x.ConsumedAtUtc == null)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null)
            return NotFound(new AuthMessageResponse("No active verification request was found. Start login or registration again."));

        var now = DateTime.UtcNow;
        if (now - challenge.RequestedAtUtc < ResendCooldown)
            return StatusCode(StatusCodes.Status429TooManyRequests, new AuthMessageResponse("Please wait 60 seconds between verification code requests."));

        challenge.OtpHash = CreateOtpHash(email, out var otp);
        challenge.RequestedAtUtc = now;
        challenge.ExpiresAtUtc = now.Add(OtpLifetime);
        challenge.FailedAttempts = 0;
        await db.SaveChangesAsync(cancellationToken);

        if (!await TrySendOtpAsync(email, otp, cancellationToken))
            return StatusCode(StatusCodes.Status502BadGateway, new AuthMessageResponse("We could not deliver the email. Please try again after the resend cooldown."));

        return Ok(new AuthMessageResponse("A new verification code has been sent."));
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var challenge = await db.AuthOtpChallenges
            .Where(x => x.Email == email && x.ConsumedAtUtc == null)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null)
            return BadRequest(new AuthMessageResponse("No active verification code was found. Request a new code."));

        var now = DateTime.UtcNow;
        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.ConsumedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            return BadRequest(new AuthMessageResponse("This verification code has expired. Request a new code."));
        }
        if (challenge.FailedAttempts >= MaximumAttempts)
            return BadRequest(new AuthMessageResponse("The maximum number of attempts was reached. Request a new code."));

        var expectedHash = AuthTokenHash.HashOtp(email, request.Code, GetOtpPepper());
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, challenge.OtpHash))
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= MaximumAttempts)
                challenge.ConsumedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = MaximumAttempts - challenge.FailedAttempts;
            return BadRequest(new AuthMessageResponse(remaining > 0
                ? $"The verification code is incorrect. {remaining} attempt(s) remain."
                : "The maximum number of attempts was reached. Request a new code."));
        }

        Customer? customer;
        if (challenge.Purpose == "REGISTER")
        {
            if (await db.Customers.AnyAsync(x => x.Email != null && x.Email.ToLower() == email, cancellationToken))
            {
                challenge.ConsumedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                return Conflict(new AuthMessageResponse("An account already exists for this email. Switch to Login."));
            }
            if (await db.Customers.AnyAsync(x => x.Phone == challenge.RegistrationPhone, cancellationToken))
            {
                challenge.ConsumedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                return Conflict(new AuthMessageResponse("This phone number is already associated with an account."));
            }

            customer = new Customer
            {
                CustomerName = challenge.RegistrationName!,
                Email = email,
                Phone = challenge.RegistrationPhone!,
                ConsentAcceptedAtUtc = challenge.ConsentAcceptedAtUtc
            };
            db.Customers.Add(customer);
        }
        else
        {
            customer = await db.Customers.SingleOrDefaultAsync(
                x => x.Email != null && x.Email.ToLower() == email,
                cancellationToken);
            if (customer is null)
            {
                challenge.ConsumedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                return NotFound(new AuthMessageResponse("No account was found for this email. Please register first."));
            }
        }

        challenge.ConsumedAtUtc = now;
        var sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.CustomerSessions.Add(new CustomerSession
        {
            Customer = customer,
            TokenHash = AuthTokenHash.HashSessionToken(sessionToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(SessionDays)
        });
        await db.SaveChangesAsync(cancellationToken);
        SetSessionCookie(sessionToken);

        return Ok(new { customer = ToResponse(customer) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        return session is null
            ? Unauthorized(new AuthMessageResponse("Please sign in to continue."))
            : Ok(new { customer = ToResponse(session.Customer) });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(cancellationToken);
        if (session is not null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return Ok(new AuthMessageResponse("You have been signed out."));
    }

    private async Task<IActionResult> CreateChallengeAsync(
        string email,
        string purpose,
        string? name,
        string? phone,
        DateTime? consentAcceptedAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lastRequestAt = await db.AuthOtpChallenges
            .Where(x => x.Email == email)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => (DateTime?)x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastRequestAt.HasValue && now - lastRequestAt.Value < ResendCooldown)
            return StatusCode(StatusCodes.Status429TooManyRequests, new AuthMessageResponse("Please wait 60 seconds between verification code requests."));

        var previousChallenges = await db.AuthOtpChallenges
            .Where(x => x.Email == email && x.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var previousChallenge in previousChallenges)
            previousChallenge.ConsumedAtUtc = now;

        var otpHash = CreateOtpHash(email, out var otp);
        db.AuthOtpChallenges.Add(new AuthOtpChallenge
        {
            Email = email,
            Purpose = purpose,
            OtpHash = otpHash,
            RegistrationName = name,
            RegistrationPhone = phone,
            ConsentAcceptedAtUtc = consentAcceptedAtUtc,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.Add(OtpLifetime)
        });
        await db.SaveChangesAsync(cancellationToken);

        if (!await TrySendOtpAsync(email, otp, cancellationToken))
            return StatusCode(StatusCodes.Status502BadGateway, new AuthMessageResponse("We could not deliver the email. Please try again after the resend cooldown."));

        return Ok(new AuthMessageResponse("A six-digit verification code has been sent to your email address."));
    }

    private async Task<bool> TrySendOtpAsync(string email, string otp, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(email, otp, cancellationToken);
            return true;
        }
        catch (AmazonServiceException exception)
        {
            logger.LogError(exception, "Amazon SES failed to send an authentication code.");
            return false;
        }
    }

    private async Task<CustomerSession?> FindSessionAsync(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[SessionCookieName];
        if (string.IsNullOrEmpty(token)) return null;
        var tokenHash = AuthTokenHash.HashSessionToken(token);
        var now = DateTime.UtcNow;
        return await db.CustomerSessions
            .Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
    }

    private void SetSessionCookie(string token) => Response.Cookies.Append(SessionCookieName, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromDays(SessionDays)
    });

    private byte[] CreateOtpHash(string email, out string otp)
    {
        otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return AuthTokenHash.HashOtp(email, otp, GetOtpPepper());
    }

    private string GetOtpPepper()
    {
        var pepper = configuration["Auth:OtpPepper"];
        if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < 32)
            throw new InvalidOperationException("Configure Auth:OtpPepper with a secret of at least 32 characters.");
        return pepper;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static AuthCustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.CustomerName, customer.Email!, customer.Phone);
}
