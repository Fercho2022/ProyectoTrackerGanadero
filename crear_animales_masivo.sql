-- ============================================================================
-- CREACIÓN MASIVA DE ANIMALES - TRACKER GANADERO
-- Genera 120 animales con datos variados y realistas para PostgreSQL
-- ============================================================================

-- Array de nombres para machos y hembras
WITH
nombres_machos AS (
    SELECT unnest(ARRAY[
        'Pampa', 'Gaucho', 'Toro', 'Macho', 'Bravo', 'Rojo', 'Negro', 'Blanco',
        'Fortín', 'Potro', 'Relincho', 'Bandido', 'Caudillo', 'Rebelde', 'Centauro',
        'Chacarero', 'Pulpero', 'Tropero', 'Resero', 'Jinete', 'Domador', 'Palenque',
        'Facón', 'Lazo', 'Boleador', 'Mancarrón', 'Overo', 'Zaino', 'Moro', 'Tordillo'
    ]) AS nombre
),
nombres_hembras AS (
    SELECT unnest(ARRAY[
        'Lola', 'Manchada', 'Pegada', 'Tristes', 'Cafe', 'Luna', 'Estrella', 'Aurora',
        'Flor', 'Rosa', 'Margarita', 'Violeta', 'Azucena', 'Dalia', 'Hortensia',
        'Mimosa', 'Amapola', 'Gardenia', 'Jazmín', 'Magnolia', 'Petunia', 'Begonia',
        'Caléndula', 'Camelia', 'Clavel', 'Delfina', 'Gladiola', 'Iris', 'Jacinta',
        'Lila', 'Orquídea', 'Primavera', 'Tulipán', 'Verbena', 'Zinnia', 'Alegría'
    ]) AS nombre
),
prefijos AS (
    SELECT unnest(ARRAY[
        'Ojo', 'Pinta', 'Cara', 'Pata', 'Cola', 'Oreja', 'Frente', 'Lomo',
        'Costado', 'Cuerno', 'Hocico', 'Pecho', 'Anca', 'Paleta', 'Corvejón'
    ]) AS prefijo
),
razas AS (
    SELECT unnest(ARRAY[
        'Angus', 'Brangus', 'Hereford', 'Braford', 'Shorthorn',
        'Charolais', 'Limousin', 'Holando Argentino', 'Jersey', 'Criollo'
    ]) AS raza
),
-- Generar 120 animales
animales_generados AS (
    SELECT
        i AS numero,
        -- Género: 60% hembras, 40% machos
        CASE
            WHEN (i % 10) < 6 THEN 'Female'
            ELSE 'Male'
        END AS genero,
        -- Raza aleatoria usando módulo
        (SELECT raza FROM razas OFFSET (i % 10) LIMIT 1) AS raza,
        -- Fecha de nacimiento: entre 6 meses y 5 años atrás
        (CURRENT_DATE - ((180 + (i * 13) % 1645) || ' days')::interval) AS fecha_nacimiento,
        -- Estado: mayoría saludables
        CASE
            WHEN (i % 10) < 7 THEN 'Saludable'
            WHEN (i % 10) = 7 THEN 'En tratamiento'
            WHEN (i % 10) = 8 THEN 'Recuperación'
            ELSE 'Gestante'
        END AS estado
    FROM generate_series(1, 120) AS i
),
-- Generar nombres únicos
animales_con_nombres AS (
    SELECT
        ag.*,
        CASE
            -- 1 de cada 3: nombre simple
            WHEN (ag.numero % 3) = 0 THEN
                CASE
                    WHEN ag.genero = 'Male' THEN
                        (SELECT nombre FROM nombres_machos OFFSET ((ag.numero / 3) % 30) LIMIT 1)
                    ELSE
                        (SELECT nombre FROM nombres_hembras OFFSET ((ag.numero / 3) % 36) LIMIT 1)
                END
            -- 2 de cada 3: prefijo + nombre
            ELSE
                (SELECT prefijo FROM prefijos OFFSET (ag.numero % 15) LIMIT 1) ||
                CASE
                    WHEN ag.genero = 'Male' THEN
                        (SELECT nombre FROM nombres_machos OFFSET (ag.numero % 30) LIMIT 1)
                    ELSE
                        (SELECT nombre FROM nombres_hembras OFFSET (ag.numero % 36) LIMIT 1)
                END
        END AS nombre,
        'Rebo' || LPAD((1000 + ag.numero)::text, 4, '0') AS tag
    FROM animales_generados ag
),
-- Calcular peso según edad y género
animales_completos AS (
    SELECT
        acn.*,
        CASE
            -- Machos: 300-800 kg según edad
            WHEN acn.genero = 'Male' THEN
                CASE
                    WHEN EXTRACT(YEAR FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) < 1 THEN
                        200 + (EXTRACT(MONTH FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) * 15) + (acn.numero % 50)
                    ELSE
                        400 + ((EXTRACT(YEAR FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) - 1) * 96) + (acn.numero % 80)
                END
            -- Hembras: 250-600 kg según edad
            ELSE
                CASE
                    WHEN EXTRACT(YEAR FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) < 1 THEN
                        180 + (EXTRACT(MONTH FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) * 12) + (acn.numero % 40)
                    ELSE
                        350 + ((EXTRACT(YEAR FROM AGE(CURRENT_DATE, acn.fecha_nacimiento)) - 1) * 60) + (acn.numero % 60)
                END
        END AS peso
    FROM animales_con_nombres acn
)
-- INSERT final
INSERT INTO "Animals" (
    "Name",
    "Tag",
    "BirthDate",
    "Gender",
    "Breed",
    "Weight",
    "Status",
    "FarmId",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    nombre,
    tag,
    fecha_nacimiento,
    genero,
    raza,
    LEAST(peso, CASE WHEN genero = 'Male' THEN 800 ELSE 600 END)::numeric(10,2), -- Limitar peso máximo
    estado,
    1, -- FarmId: 1 = Granja Norte (ajustar si es diferente)
    NOW(),
    NOW()
FROM animales_completos
ORDER BY numero;

-- Mostrar resumen
SELECT
    'Animales creados exitosamente!' AS mensaje,
    COUNT(*) AS total_creados,
    SUM(CASE WHEN "Gender" = 'Male' THEN 1 ELSE 0 END) AS machos,
    SUM(CASE WHEN "Gender" = 'Female' THEN 1 ELSE 0 END) AS hembras,
    COUNT(DISTINCT "Breed") AS razas_diferentes
FROM "Animals"
WHERE "Tag" LIKE 'Rebo%'
  AND "Tag" >= 'Rebo1001';
