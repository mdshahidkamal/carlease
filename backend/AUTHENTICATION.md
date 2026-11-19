# Passwordless customer authentication

The API implements the proposed email OTP flow: customers request login or registration codes, verify a six-digit code, and receive a server-side session represented by an HttpOnly cookie. The home page stays public; both the booking route and booking API require a valid session.

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

- `Auth__OtpPepper`: a randomly generated secret with at least 32 characters. Keep it stable while active OTP challenges exist; store it in AWS Secrets Manager in ECS and .NET User Secrets locally. The ECS deploy workflows read it from `AUTH_OTP_PEPPER_SECRET_ARN`.
- `Auth__EmailFrom`: an email identity verified in Amazon SES in the configured region. ECS workflows read the sender from the environment variable `AUTH_EMAIL_FROM`.
- `AWS_REGION`: SES region, for example `ap-southeast-2`.
- Existing database settings (`ConnectionStrings__DefaultConnection` locally or the existing `DB_*` settings in ECS).

The API uses the AWS SDK default credential chain. The ECS **application task role** must allow `ses:SendEmail` for the verified sender identity. SES sandbox accounts can only send to verified recipient addresses until production sending is enabled. No AWS resources or secret values are created by this feature.

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
