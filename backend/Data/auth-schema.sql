-- CarLease passwordless customer authentication schema.
-- Run in pgAdmin against the application database before deploying the auth API.
-- Safe to re-run. Review duplicate emails/phones before applying unique indexes to existing data.

CREATE TABLE IF NOT EXISTS "Customers" (
	"Id" uuid PRIMARY KEY,
	"CustomerName" varchar(120) NOT NULL,
	"Phone" varchar(30) NOT NULL,
	"Email" varchar(160),
	"ConsentAcceptedAtUtc" timestamp with time zone,
	"CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

ALTER TABLE "Customers"
	ADD COLUMN IF NOT EXISTS "ConsentAcceptedAtUtc" timestamp with time zone;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Phone"
	ON "Customers" ("Phone");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Email_CI"
	ON "Customers" (lower("Email"))
	WHERE "Email" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "AuthOtpChallenges" (
	"Id" uuid PRIMARY KEY,
	"Email" varchar(160) NOT NULL,
	"Purpose" varchar(16) NOT NULL CHECK ("Purpose" IN ('LOGIN', 'REGISTER')),
	"OtpHash" bytea NOT NULL,
	"RegistrationName" varchar(120),
	"RegistrationPhone" varchar(30),
	"ConsentAcceptedAtUtc" timestamp with time zone,
	"RequestedAtUtc" timestamp with time zone NOT NULL,
	"ExpiresAtUtc" timestamp with time zone NOT NULL,
	"FailedAttempts" integer NOT NULL DEFAULT 0 CHECK ("FailedAttempts" BETWEEN 0 AND 5),
	"ConsumedAtUtc" timestamp with time zone,
	CHECK (("Purpose" = 'LOGIN' AND "RegistrationName" IS NULL AND "RegistrationPhone" IS NULL AND "ConsentAcceptedAtUtc" IS NULL)
		OR ("Purpose" = 'REGISTER' AND "RegistrationName" IS NOT NULL AND "RegistrationPhone" IS NOT NULL AND "ConsentAcceptedAtUtc" IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS "IX_AuthOtpChallenges_Email_RequestedAtUtc"
	ON "AuthOtpChallenges" ("Email", "RequestedAtUtc" DESC);

CREATE TABLE IF NOT EXISTS "CustomerSessions" (
	"Id" uuid PRIMARY KEY,
	"CustomerId" uuid NOT NULL REFERENCES "Customers" ("Id") ON DELETE CASCADE,
	"TokenHash" bytea NOT NULL UNIQUE,
	"CreatedAtUtc" timestamp with time zone NOT NULL,
	"ExpiresAtUtc" timestamp with time zone NOT NULL,
	"RevokedAtUtc" timestamp with time zone
);

CREATE INDEX IF NOT EXISTS "IX_CustomerSessions_CustomerId_ExpiresAtUtc"
	ON "CustomerSessions" ("CustomerId", "ExpiresAtUtc");
