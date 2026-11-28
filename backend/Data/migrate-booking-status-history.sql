-- Apply to the non-production database before deploying the auto-rejection worker.
BEGIN;
CREATE TABLE "BookingStatusHistories" (
	"Id" uuid PRIMARY KEY,
	"BookingId" uuid NOT NULL REFERENCES "Bookings" ("Id") ON DELETE RESTRICT,
	"PreviousStatus" text NOT NULL,
	"NewStatus" text NOT NULL,
	"Reason" text NOT NULL,
	"ChangedBy" text NOT NULL,
	"ChangedAtUtc" timestamp with time zone NOT NULL
);
CREATE INDEX "IX_BookingStatusHistories_BookingId" ON "BookingStatusHistories" ("BookingId");
CREATE INDEX "IX_Bookings_AutoReject_PickupDateTime" ON "Bookings" ("PickupDateTime", "Id")
	WHERE "Status" IN ('PENDING_REVIEW', 'CONFIRMED');
COMMIT;
