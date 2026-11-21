-- Apply to existing PostgreSQL databases before deploying the vehicle-aware API.
BEGIN;

CREATE TABLE IF NOT EXISTS public."Vehicles" (
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

ALTER TABLE public."Bookings" ADD COLUMN IF NOT EXISTS "VehicleId" uuid;
DO $$
BEGIN
	IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Bookings_Vehicles_VehicleId' AND conrelid = 'public."Bookings"'::regclass) THEN
		ALTER TABLE public."Bookings" ADD CONSTRAINT "FK_Bookings_Vehicles_VehicleId"
			FOREIGN KEY ("VehicleId") REFERENCES public."Vehicles" ("Id") ON UPDATE CASCADE ON DELETE RESTRICT;
	END IF;
END $$;
CREATE INDEX IF NOT EXISTS "IX_Bookings_VehicleId" ON public."Bookings" ("VehicleId");

INSERT INTO public."Vehicles" ("VehicleCode", "VehicleName", "Description", "PassengerCapacity", "LuggageCapacity", "BaseFare") VALUES
	('SEDAN', 'Sedan', 'Standard executive sedan', 3, 2, 0),
	('SUV', 'SUV', 'Premium SUV', 6, 4, 0),
	('LUXURY', 'Luxury Sedan', 'Luxury executive sedan', 3, 2, 0),
	('VAN', 'Passenger Van', 'Large passenger van', 10, 8, 0)
ON CONFLICT ("VehicleCode") DO NOTHING;

COMMIT;
