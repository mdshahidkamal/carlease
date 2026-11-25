-- Review the active fleet and any name/code conflicts before applying.
-- Retain historical bookings and custom vehicle records; retire only the original seed vehicles.
BEGIN;
UPDATE public."Vehicles"
SET "IsActive" = false, "UpdatedAt" = CURRENT_TIMESTAMP
WHERE ("VehicleCode", "VehicleName") IN (('SEDAN', 'Sedan'), ('SUV', 'SUV'), ('LUXURY', 'Luxury Sedan'), ('VAN', 'Passenger Van'));

INSERT INTO public."Vehicles" ("VehicleCode", "VehicleName", "Description", "PassengerCapacity", "LuggageCapacity", "BaseFare") VALUES
	('LEXUS_RX350', 'Lexus RX350', 'Lexus RX350', 3, 3, 0),
	('MITSUBISHI_OUTLANDER', 'Mitsubishi Outlander', 'Mitsubishi Outlander', 4, 3, 0),
	('CHEVROLET_SUBURBAN', 'Chevrolet Suburban', 'Chevrolet Suburban', 7, 6, 0)
ON CONFLICT ("VehicleCode") DO UPDATE SET
	"VehicleName" = EXCLUDED."VehicleName",
	"PassengerCapacity" = EXCLUDED."PassengerCapacity",
	"LuggageCapacity" = EXCLUDED."LuggageCapacity",
	"IsActive" = true,
	"UpdatedAt" = CURRENT_TIMESTAMP;
COMMIT;
