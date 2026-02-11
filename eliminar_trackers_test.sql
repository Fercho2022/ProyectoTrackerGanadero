-- Script para eliminar trackers de prueba creados durante debugging
-- Ejecutar en la base de datos PostgreSQL

BEGIN;

-- 1. Eliminar registros de LocationHistory relacionados con estos trackers
DELETE FROM "LocationHistories"
WHERE "DeviceId" IN ('TEST_001', 'TEST_002', 'TEST_FULL', 'CONCURRENT_TEST_1', 'CONCURRENT_TEST_2', 'CONCURRENT_TEST_3');

-- 2. Eliminar los trackers de prueba
DELETE FROM "Trackers"
WHERE "DeviceId" IN ('TEST_001', 'TEST_002', 'TEST_FULL', 'CONCURRENT_TEST_1', 'CONCURRENT_TEST_2', 'CONCURRENT_TEST_3');

-- Mostrar cuántos registros se eliminaron
SELECT 'Trackers de prueba eliminados correctamente' AS mensaje;

COMMIT;
