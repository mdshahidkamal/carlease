-- One-time migration for an existing CarLease database that has the original
-- denormalized Bookings.CustomerName / Phone / Email columns.
-- Run during a maintenance window after taking a database snapshot/backup.
-- This script preserves existing booking rows and keeps the legacy customer
-- columns nullable for audit/rollback purposes.

BEGIN;

CREATE TABLE IF NOT EXISTS "Customers" (
    "Id" uuid PRIMARY KEY,
    "CustomerName" varchar(120) NOT NULL,
    "Phone" varchar(30) NOT NULL,
    "Email" varchar(160),
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

-- The phone index must exist before the INSERT ... ON CONFLICT below.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Phone" ON "Customers" ("Phone");

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "Bookings"
        WHERE NULLIF(BTRIM("Phone"), '') IS NULL
    ) THEN
        RAISE EXCEPTION 'Migration stopped: at least one booking has an empty phone number. Resolve these rows before retrying.';
    END IF;

    IF EXISTS (
        SELECT LOWER(BTRIM("Email"))
        FROM "Bookings"
        WHERE NULLIF(BTRIM("Email"), '') IS NOT NULL
        GROUP BY LOWER(BTRIM("Email"))
        HAVING COUNT(DISTINCT BTRIM("Phone")) > 1
    ) THEN
        RAISE EXCEPTION 'Migration stopped: the same email is associated with multiple phone numbers. Resolve duplicate customer identities before retrying.';
    END IF;
END $migration$;

INSERT INTO "Customers" ("Id", "CustomerName", "Phone", "Email", "CreatedAtUtc")
SELECT
    gen_random_uuid(),
    COALESCE(NULLIF(BTRIM(source."CustomerName"), ''), 'Customer'),
    source.phone,
    source.email,
    now()
FROM (
    SELECT DISTINCT ON (BTRIM("Phone"))
        BTRIM("Phone") AS phone,
        "CustomerName",
        LOWER(NULLIF(BTRIM("Email"), '')) AS email
    FROM "Bookings"
    ORDER BY BTRIM("Phone"), "CreatedAtUtc" DESC, "Id"
) AS source
ON CONFLICT ("Phone") DO NOTHING;

ALTER TABLE "Bookings"
    ADD COLUMN IF NOT EXISTS "CustomerId" uuid;

UPDATE "Bookings" AS booking
SET "CustomerId" = customer."Id"
FROM "Customers" AS customer
WHERE customer."Phone" = BTRIM(booking."Phone")
  AND booking."CustomerId" IS NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "Bookings" WHERE "CustomerId" IS NULL) THEN
        RAISE EXCEPTION 'Migration stopped: one or more bookings could not be linked to a customer.';
    END IF;
END $$;

ALTER TABLE "Bookings" ALTER COLUMN "CustomerId" SET NOT NULL;
ALTER TABLE "Bookings" ALTER COLUMN "CustomerName" DROP NOT NULL;
ALTER TABLE "Bookings" ALTER COLUMN "Phone" DROP NOT NULL;
ALTER TABLE "Bookings" ALTER COLUMN "Email" DROP NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Phone" ON "Customers" ("Phone");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Email"
    ON "Customers" ("Email") WHERE "Email" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Bookings_CustomerId" ON "Bookings" ("CustomerId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Bookings_Customers_CustomerId'
    ) THEN
        ALTER TABLE "Bookings"
            ADD CONSTRAINT "FK_Bookings_Customers_CustomerId"
            FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id")
            ON DELETE RESTRICT;
    END IF;
END $$;

COMMIT;
