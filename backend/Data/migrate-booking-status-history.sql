-- Apply to the non-production database before deploying the auto-rejection worker.
BEGIN;
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
CREATE INDEX "IX_Bookings_AutoReject_PickupDateTime" ON "Bookings" ("PickupDateTime", "Id")
	WHERE "Status" IN ('PENDING_REVIEW', 'CONFIRMED');
COMMIT;
