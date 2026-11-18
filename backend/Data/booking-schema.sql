-- Schema for a new PostgreSQL database. Apply before deploying the booking API.
CREATE TABLE "Customers" (
	"Id" uuid PRIMARY KEY,
	"CustomerName" varchar(120) NOT NULL,
	"Phone" varchar(30) NOT NULL,
	"Email" varchar(160),
	"CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX "IX_Customers_Phone" ON "Customers" ("Phone");
CREATE UNIQUE INDEX "IX_Customers_Email" ON "Customers" ("Email") WHERE "Email" IS NOT NULL;

CREATE TABLE "Bookings" (
	"Id" uuid PRIMARY KEY,
	"BookingReference" varchar(32) NOT NULL,
	"CustomerId" uuid NOT NULL REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
	"Pickup" varchar(500) NOT NULL,
	"PickupPlaceId" text,
	"PickupLatitude" double precision,
	"PickupLongitude" double precision,
	"Dropoff" varchar(500) NOT NULL,
	"DropoffPlaceId" text,
	"DropoffLatitude" double precision,
	"DropoffLongitude" double precision,
	"PickupDateTime" timestamp with time zone NOT NULL,
	"VehicleType" text NOT NULL,
	"PassengerCount" integer NOT NULL CHECK ("PassengerCount" BETWEEN 1 AND 12),
	"KidsCount" integer NOT NULL DEFAULT 0 CHECK ("KidsCount" BETWEEN 0 AND 12),
	"BaggageCount" integer NOT NULL DEFAULT 0 CHECK ("BaggageCount" BETWEEN 0 AND 30),
	"Preferences" varchar(1500),
	"Status" text NOT NULL DEFAULT 'PENDING_REVIEW',
	"CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX "IX_Bookings_BookingReference" ON "Bookings" ("BookingReference");
CREATE INDEX "IX_Bookings_CustomerId" ON "Bookings" ("CustomerId");
