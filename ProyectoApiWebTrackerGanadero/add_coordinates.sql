-- Agregar columnas de coordenadas a la tabla Farms si no existen
ALTER TABLE "Farms"
ADD COLUMN IF NOT EXISTS "Latitude" double precision DEFAULT 0,
ADD COLUMN IF NOT EXISTS "Longitude" double precision DEFAULT 0;

-- Actualizar las granjas de Entre Ríos con las coordenadas correctas
UPDATE "Farms"
SET "Latitude" = -33.0167, "Longitude" = -58.5167
WHERE "Name" LIKE '%Entre Rios%' OR "Name" LIKE '%Entre Ríos%';

-- Verificar que se actualizaron
SELECT "Id", "Name", "Latitude", "Longitude" FROM "Farms" WHERE "Name" LIKE '%Entre Rios%' OR "Name" LIKE '%Entre Ríos%';
