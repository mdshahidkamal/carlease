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

CREATE TABLE "Vehicles" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "VehicleCode" varchar(30) NOT NULL UNIQUE,
    "VehicleName" varchar(100) NOT NULL UNIQUE,
    "Description" varchar(500),
    "PassengerCapacity" integer NOT NULL CHECK ("PassengerCapacity" > 0),
    "LuggageCapacity" integer NOT NULL DEFAULT 0 CHECK ("LuggageCapacity" >= 0),
    "BaseFare" numeric(12,2) NOT NULL DEFAULT 0 CHECK ("BaseFare" >= 0),
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE "Bookings" (
	"Id" uuid PRIMARY KEY,
	"BookingReference" varchar(32) NOT NULL,
	"CustomerId" uuid NOT NULL REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
	"Pickup" varchar(500) NOT NULL,
	"PickupPlaceId" text,
	"PickupLatitude" double precision,
	"PickupLongitude" double precision,
	"Via" varchar(500),
	"Dropoff" varchar(500) NOT NULL,
	"DropoffPlaceId" text,
	"DropoffLatitude" double precision,
	"DropoffLongitude" double precision,
	"EstimatedDistance_Miles" numeric(12,2),
	"EstimatedAmount_Dollars" numeric(12,2) NOT NULL DEFAULT 0,
	"PickupDateTime" timestamp with time zone NOT NULL,
	"VehicleType" text NOT NULL,
	"VehicleId" uuid REFERENCES "Vehicles" ("Id") ON UPDATE CASCADE ON DELETE RESTRICT,
	"PassengerCount" integer NOT NULL CHECK ("PassengerCount" BETWEEN 1 AND 12),
	"KidsCount" integer NOT NULL DEFAULT 0 CHECK ("KidsCount" BETWEEN 0 AND 12),
	"BaggageCount" integer NOT NULL DEFAULT 0 CHECK ("BaggageCount" BETWEEN 0 AND 30),
	"Preferences" varchar(1500),
	"Status" text NOT NULL DEFAULT 'PENDING_REVIEW',
	"CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX "IX_Bookings_BookingReference" ON "Bookings" ("BookingReference");
CREATE INDEX "IX_Bookings_CustomerId" ON "Bookings" ("CustomerId");
CREATE INDEX "IX_Bookings_VehicleId" ON "Bookings" ("VehicleId");
CREATE INDEX "IX_Bookings_AutoReject_PickupDateTime" ON "Bookings" ("PickupDateTime", "Id")
    WHERE "Status" IN ('PENDING_REVIEW', 'CONFIRMED');

CREATE TABLE "BookingStatusHistory" (
    "Id" uuid PRIMARY KEY,
    "BookingId" uuid NOT NULL REFERENCES "Bookings" ("Id") ON DELETE RESTRICT,
    "PreviousStatus" varchar(50) NOT NULL,
    "NewStatus" varchar(50) NOT NULL,
    "Reason" varchar(100) NOT NULL,
    "ChangedBy" varchar(100) NOT NULL,
    "ChangedAtUtc" timestamp with time zone NOT NULL
);
CREATE INDEX "IX_BookingStatusHistory_BookingId" ON "BookingStatusHistory" ("BookingId");

INSERT INTO "Vehicles" ("VehicleCode", "VehicleName", "Description", "PassengerCapacity", "LuggageCapacity", "BaseFare") VALUES
    ('LEXUS_RX350', 'Lexus RX350', 'Lexus RX350', 3, 3, 0),
    ('MITSUBISHI_OUTLANDER', 'Mitsubishi Outlander', 'Mitsubishi Outlander', 4, 3, 0),
    ('CHEVROLET_SUBURBAN', 'Chevrolet Suburban', 'Chevrolet Suburban', 7, 6, 0);
