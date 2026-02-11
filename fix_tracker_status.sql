-- Script para verificar y arreglar el estado del tracker COW_GPS_ER_01

-- 1. Ver el estado actual del tracker
SELECT
    t."Id",
    t."DeviceId",
    t."Status",
    t."IsOnline",
    t."LastSeen",
    a."Id" as "AnimalId",
    a."Name" as "AnimalName",
    ct."Id" as "CustomerTrackerId",
    ct."Status" as "CustomerTrackerStatus"
FROM "Trackers" t
LEFT JOIN "Animals" a ON a."TrackerId" = t."Id"
LEFT JOIN "CustomerTrackers" ct ON ct."TrackerId" = t."Id"
WHERE t."DeviceId" = 'COW_GPS_ER_01';

-- 2. Cambiar el estado del tracker a "Active" si está asignado a un animal
UPDATE "Trackers"
SET "Status" = 'Active'
WHERE "DeviceId" = 'COW_GPS_ER_01'
AND "Id" IN (
    SELECT t."Id"
    FROM "Trackers" t
    INNER JOIN "Animals" a ON a."TrackerId" = t."Id"
    WHERE t."DeviceId" = 'COW_GPS_ER_01'
);

-- 3. Verificar el resultado
SELECT
    t."Id",
    t."DeviceId",
    t."Status",
    t."IsOnline",
    t."LastSeen",
    a."Id" as "AnimalId",
    a."Name" as "AnimalName"
FROM "Trackers" t
LEFT JOIN "Animals" a ON a."TrackerId" = t."Id"
WHERE t."DeviceId" = 'COW_GPS_ER_01';

-- 4. Ver las últimas ubicaciones guardadas
SELECT
    lh."Id",
    lh."DeviceId",
    lh."Latitude",
    lh."Longitude",
    lh."Timestamp",
    lh."Speed"
FROM "LocationHistories" lh
WHERE lh."DeviceId" = 'COW_GPS_ER_01'
ORDER BY lh."Timestamp" DESC
LIMIT 10;
