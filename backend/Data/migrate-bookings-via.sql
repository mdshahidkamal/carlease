-- Apply before deploying the API version that reads or writes Via.
ALTER TABLE "Bookings" ADD COLUMN IF NOT EXISTS "Via" varchar(500);
