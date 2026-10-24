-- =============================================================================
-- Instala el modelo y carga el Excel en una sola transacción (psql).
--   psql -h localhost -U postgres -d infopresupuestal -f database/instalar.sql
--   psql ... -v tenant=acme -f database/instalar.sql      (otro tenant)
-- En pgAdmin: ejecutar scripts/01..04 en orden en la misma pestaña.
-- =============================================================================
\set ON_ERROR_STOP on
\if :{?tenant}
\else
    \set tenant demo
\endif

BEGIN;
SET LOCAL client_min_messages = warning;
SELECT set_config('app.tenant_id', :'tenant', false);
\ir scripts/01_esquema.sql
\ir scripts/02_catalogos.sql
\ir scripts/03_carga_excel.sql
\ir scripts/04_normalizar_insumos.sql
\ir scripts/05_apu_presupuesto.sql
COMMIT;

\echo 'Filas a revisar: SELECT motivo, count(*) FROM carga.v_observacion GROUP BY motivo;'
SELECT motivo, count(*) AS filas FROM carga.v_observacion GROUP BY motivo ORDER BY motivo;
