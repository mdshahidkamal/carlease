-- Add estimate columns to existing databases without changing columns already present.
-- Verify existing column types and nullability before deploying the booking API.
BEGIN;
ALTER TABLE public."Bookings" ADD COLUMN IF NOT EXISTS "EstimatedDistance_Miles" numeric(12,2);
ALTER TABLE public."Bookings" ADD COLUMN IF NOT EXISTS "EstimatedAmount_Dollars" numeric(12,2) NOT NULL DEFAULT 0;
COMMIT;
