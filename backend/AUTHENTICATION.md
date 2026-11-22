# Passwordless customer authentication

The API implements email OTP verification by default: customers request login or registration codes, verify a six-digit code, and receive a server-side session represented by an HttpOnly cookie. The home page stays public; both the booking route and booking API require a valid session.

## Non-production email-verification switch

`Auth:EmailVerificationEnabled` defaults to `true` and is explicitly enabled in Development. `appsettings.Staging.json` sets it to `false` for non-production testing; with verification disabled, registration still requires all fields and consent, checks email/phone uniqueness, creates the customer and session, while login requires a matching existing email and creates a session. Neither flow sends email or creates OTP challenges. Booking authorization, sessions, and request rate limits remain in force. This mode does not prove control of the email address and must never be enabled in Production; startup rejects `false` in the Production environment. Set the Staging value to `true` to restore OTP verification. Environment variables and .NET User Secrets can override the file settings; restart the API after changing configuration.

For local testing, use the Development launch profile and open the frontend consistently at `http://localhost:5173` or `http://127.0.0.1:5173`. Development builds align a loopback API hostname with the frontend hostname so a SameSite cookie can be sent. Changing between hostnames uses a different cookie, so sign in again after switching. The API needs to listen on the chosen host. The 30-day session survives refreshes and API restarts when the browser keeps its cookie and the same database remains available; logout, browser cookie deletion, expiry or revocation ends it. If the session check returns HTTP 500, inspect API logs and database connectivity rather than assuming the cookie expired.

## PostgreSQL setup

1. Back up the target database. Before applying the script to existing data, run these checks and resolve any duplicate groups:

   ```sql
   SELECT lower("Email") AS normalized_email, count(*)
   FROM "Customers"
   WHERE "Email" IS NOT NULL
   GROUP BY lower("Email")
   HAVING count(*) > 1;

   SELECT "Phone", count(*)
   FROM "Customers"
   GROUP BY "Phone"
   HAVING count(*) > 1;
   ```

2. Run `Data/auth-schema.sql` in pgAdmin against the application database. The script creates `Customers` if it is absent, adds a nullable consent timestamp if needed, and creates the OTP/session tables. It does not migrate or delete existing customer or booking data.
3. This script does not migrate an existing `Bookings` table to the new `CustomerId` relationship. Prepare and apply a separate data-preserving booking migration before deploying a version that requires that relationship.

## Application configuration

Configure these values outside source control:

- `Auth__OtpPepper`: required only when email OTP verification is enabled. Use a randomly generated secret with at least 32 characters; keep it stable while active OTP challenges exist. Store it in AWS Secrets Manager for ECS and .NET User Secrets locally. Nonprod ECS deployments inject it when `AUTH_OTP_PEPPER_SECRET_ARN` is configured.
- `Auth__EmailFrom`: an email identity verified in Amazon SES in the configured region. ECS workflows read the sender from the environment variable `AUTH_EMAIL_FROM`.
- `AWS_REGION`: SES region, for example `ap-southeast-2`.
- Existing database settings (`ConnectionStrings__DefaultConnection` locally or the existing `DB_*` settings in ECS).

The API uses the AWS SDK credential chain. The ECS **application task role** must allow `ses:SendEmail` for the verified sender identity. For local AWS CLI login credentials that the .NET SDK cannot read directly, create a process-backed profile (it fetches short-lived credentials from the CLI without storing keys):

```powershell
aws configure set credential_process "aws configure export-credentials --profile default --format process" --profile carlease-sdk
[Environment]::SetEnvironmentVariable("AWS_PROFILE", "carlease-sdk", "User")
```

Restart Visual Studio or the terminal after setting `AWS_PROFILE`. The default AWS CLI profile must already be logged in and its identity needs `ses:SendEmail` permission. SES sandbox accounts can send only to verified recipient addresses until production access is enabled. Do not put AWS credentials in source control or User Secrets.

Example local configuration (use your own generated pepper and verified sender):

```powershell
dotnet user-secrets set "Auth:OtpPepper" "<random-secret-at-least-32-characters>" --project backend\CarLease.Api.csproj
dotnet user-secrets set "Auth:EmailFrom" "<verified-ses-sender@example.com>" --project backend\CarLease.Api.csproj
```

## Flow and controls

- OTP codes are generated with a cryptographic random-number generator, stored only as an HMAC hash, expire after five minutes, and allow at most five attempts.
- A 60-second per-email cooldown applies to OTP requests and resends; an in-memory limit of 100 auth requests per minute per app instance bounds SES usage without depending on client-IP forwarding through the load balancer.
- Sessions store only a SHA-256 token hash, expire after 30 days, and are revoked on logout. The cookie is HttpOnly, SameSite=Lax, and Secure outside Development.
- Serve the frontend and API on the same site (as the existing ECS deployment does), or deliberately configure cookie, CORS, and SameSite behavior before splitting them across sites.
- The registration form requires explicit terms/privacy consent. Review and publish the actual policy pages before accepting production customers.
- Schedule routine cleanup of expired OTP challenges and expired/revoked sessions; the schema intentionally does not delete records automatically.

## Administrator access

The Admin Console is served at `/admin`. It uses a separate administrator session cookie and does not reuse customer sessions. The initial implementation supports one full-access administrator configured through server-side secrets:

- `Admin__Email`: administrator sign-in email.
- `Admin__Password`: a long, unique administrator password (use a password manager-generated value).

Set these as .NET User Secrets for local development and as protected ECS task environment values sourced from AWS Secrets Manager for deployed environments. Never add the credentials to appsettings files, frontend environment variables, GitHub source, or browser code. The API stores only a hash of each random administrator session token. The session cookie is HttpOnly, SameSite=Lax, Secure outside Development, and expires after eight days. Sign-out revokes the session. Admin sign-in shares the API authentication rate limit.

Before using the Admin Console against a database, back it up and execute `Data/admin-console-schema.sql` using pgAdmin or psql. It creates `AdminSessions` and `AdminAuditLogs`; it does not alter existing bookings. The API requires these tables for administrator sign-in and audit history.

Admin API routes:
- `POST /api/admin/auth/login`, `GET /api/admin/auth/me`, `POST /api/admin/auth/logout`
- `GET /api/admin/bookings` and `GET /api/admin/bookings/{id}`
- `PATCH /api/admin/bookings/{id}/status`
- `GET /api/admin/reports?period=daily|monthly|ytd|fy&date=YYYY-MM-DD`

Permitted transitions are `PENDING_REVIEW -> CONFIRMED|REJECTED`, `CONFIRMED -> ACTIVE|REJECTED`, and `ACTIVE -> COMPLETED`. A rejection reason is mandatory. Every successful transition writes an audit record with the booking reference, previous/new status, administrator email, reason and UTC timestamp. Completed/rejected bookings have no further status transitions.

Reporting periods use the calendar year (January–December). Daily, monthly and YTD use the supplied reference date; FY means January 1 through December 31 of that reference year. Booking counts are grouped by booking creation timestamp. Revenue is summed from completed bookings' `EstimatedAmountDollars`; the current booking flow initializes this amount to zero, so meaningful revenue reporting requires pricing to be implemented/populated.
