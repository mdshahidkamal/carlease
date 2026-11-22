-- CarLease Admin Console schema additions.
CREATE TABLE IF NOT EXISTS "AdminSessions" (
    "Id" uuid PRIMARY KEY,
    "TokenHash" bytea NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "RevokedAtUtc" timestamp with time zone NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminSessions_TokenHash" ON "AdminSessions" ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_AdminSessions_ExpiresAtUtc" ON "AdminSessions" ("ExpiresAtUtc");

CREATE TABLE IF NOT EXISTS "AdminAuditLogs" (
    "Id" uuid PRIMARY KEY,
    "BookingId" uuid NOT NULL,
    "BookingReference" character varying(32) NOT NULL,
    "Action" character varying(24) NOT NULL,
    "PreviousStatus" character varying(32) NOT NULL,
    "NewStatus" character varying(32) NOT NULL,
    "Reason" character varying(1000) NULL,
    "AdminEmail" character varying(160) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AdminAuditLogs_BookingId_CreatedAtUtc" ON "AdminAuditLogs" ("BookingId", "CreatedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_AdminAuditLogs_CreatedAtUtc" ON "AdminAuditLogs" ("CreatedAtUtc");
