-- =============================================================================
-- 01 · Esquema del catálogo de insumos para presupuestos (PostgreSQL 13+)
-- Idempotente: se puede ejecutar varias veces.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- búsqueda por texto parcial en descripciones

CREATE SCHEMA IF NOT EXISTS presupuesto;  -- modelo de negocio
CREATE SCHEMA IF NOT EXISTS carga;        -- datos crudos importados (staging)

COMMENT ON SCHEMA presupuesto IS 'Catálogo de insumos y precios para presupuestos de obra.';
COMMENT ON SCHEMA carga IS 'Copia cruda de los archivos importados, antes de normalizar.';

-- -----------------------------------------------------------------------------
-- Catálogos globales (iguales para todos los tenants)
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS presupuesto.tipo_insumo (
    id      smallint    PRIMARY KEY,
    codigo  varchar(10) NOT NULL UNIQUE,
    nombre  varchar(50) NOT NULL UNIQUE,
    prefijo varchar(5)  NOT NULL UNIQUE   -- prefijo del código de insumo (MAT-0001)
);
COMMENT ON TABLE presupuesto.tipo_insumo IS 'Material, equipo o mano de obra: una hoja del Excel original.';

CREATE TABLE IF NOT EXISTS presupuesto.unidad_medida (
    id     smallint    GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo varchar(10) NOT NULL UNIQUE,
    nombre varchar(50) NOT NULL,
    CONSTRAINT ck_unidad_medida_codigo CHECK (codigo = upper(btrim(codigo)) AND codigo <> '')
);
COMMENT ON TABLE presupuesto.unidad_medida IS 'Unidades de medida (UN, ML, M2, HR, ...).';

-- -----------------------------------------------------------------------------
-- Datos por tenant. tenant_id usa el mismo formato que la API (X-Tenant-Id).
-- Las FK compuestas (tenant_id, id) impiden relacionar filas de tenants distintos.
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS presupuesto.clasificacion (
    id        integer     GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id varchar(64) NOT NULL,
    nombre    varchar(60) NOT NULL,
    activo    boolean     NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_clasificacion_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_clasificacion_nombre CHECK (btrim(nombre) <> ''),
    CONSTRAINT uq_clasificacion_tenant_id UNIQUE (tenant_id, id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_clasificacion_tenant_nombre
    ON presupuesto.clasificacion (tenant_id, upper(nombre));
COMMENT ON TABLE presupuesto.clasificacion IS 'Agrupación de insumos (ELECTRICOS, HIERROS, MANO DE OBRA, ...).';

CREATE TABLE IF NOT EXISTS presupuesto.insumo (
    id               uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        varchar(64)  NOT NULL,
    codigo           varchar(20)  NOT NULL,
    tipo_insumo_id   smallint     NOT NULL REFERENCES presupuesto.tipo_insumo (id),
    clasificacion_id integer,
    unidad_medida_id smallint     NOT NULL REFERENCES presupuesto.unidad_medida (id),
    descripcion      varchar(200) NOT NULL,
    activo           boolean      NOT NULL DEFAULT true,
    creado_en        timestamptz  NOT NULL DEFAULT now(),
    actualizado_en   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_insumo_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_insumo_descripcion CHECK (descripcion = btrim(descripcion) AND descripcion <> ''),
    CONSTRAINT uq_insumo_tenant_codigo UNIQUE (tenant_id, codigo),
    CONSTRAINT uq_insumo_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_insumo_clasificacion FOREIGN KEY (tenant_id, clasificacion_id)
        REFERENCES presupuesto.clasificacion (tenant_id, id)
);
-- Un mismo insumo no puede repetirse con la misma unidad dentro del tenant.
CREATE UNIQUE INDEX IF NOT EXISTS ux_insumo_tenant_tipo_descripcion_unidad
    ON presupuesto.insumo (tenant_id, tipo_insumo_id, upper(descripcion), unidad_medida_id);
CREATE INDEX IF NOT EXISTS ix_insumo_clasificacion ON presupuesto.insumo (clasificacion_id);
CREATE INDEX IF NOT EXISTS ix_insumo_unidad ON presupuesto.insumo (unidad_medida_id);
CREATE INDEX IF NOT EXISTS ix_insumo_descripcion_trgm ON presupuesto.insumo USING gin (descripcion gin_trgm_ops);
COMMENT ON TABLE presupuesto.insumo IS 'Material, equipo o mano de obra que se usa en un análisis de precios unitarios.';
COMMENT ON COLUMN presupuesto.insumo.codigo IS 'Código legible por tenant: MAT-0001, EQU-0001, MO-0001.';

CREATE TABLE IF NOT EXISTS presupuesto.insumo_precio (
    id             bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id      varchar(64)   NOT NULL,
    insumo_id      uuid          NOT NULL,
    valor_unitario numeric(18,2) NOT NULL,
    moneda         char(3)       NOT NULL DEFAULT 'COP',
    fecha_vigencia date          NOT NULL,
    fuente         varchar(120),
    creado_en      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_insumo_precio_valor CHECK (valor_unitario > 0),
    CONSTRAINT ck_insumo_precio_moneda CHECK (moneda ~ '^[A-Z]{3}$'),
    CONSTRAINT uq_insumo_precio_fecha UNIQUE (insumo_id, fecha_vigencia),
    CONSTRAINT fk_insumo_precio_insumo FOREIGN KEY (tenant_id, insumo_id)
        REFERENCES presupuesto.insumo (tenant_id, id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_insumo_precio_tenant ON presupuesto.insumo_precio (tenant_id);
COMMENT ON TABLE presupuesto.insumo_precio IS 'Historial de precios: una fila por insumo y fecha de vigencia. Nunca se sobrescribe un precio.';

-- -----------------------------------------------------------------------------
-- actualizado_en se mantiene solo
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION presupuesto.fn_actualizado_en() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    NEW.actualizado_en := now();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tg_insumo_actualizado_en ON presupuesto.insumo;
CREATE TRIGGER tg_insumo_actualizado_en
    BEFORE UPDATE ON presupuesto.insumo
    FOR EACH ROW EXECUTE FUNCTION presupuesto.fn_actualizado_en();

-- -----------------------------------------------------------------------------
-- Vista de consulta: cada insumo con su precio vigente (el de fecha más reciente)
-- -----------------------------------------------------------------------------

CREATE OR REPLACE VIEW presupuesto.v_insumo_vigente AS
SELECT i.tenant_id,
       i.id,
       i.codigo,
       t.nombre    AS tipo,
       c.nombre    AS clasificacion,
       i.descripcion,
       u.codigo    AS unidad,
       p.valor_unitario,
       p.moneda,
       p.fecha_vigencia,
       i.activo
FROM presupuesto.insumo i
JOIN presupuesto.tipo_insumo t   ON t.id = i.tipo_insumo_id
JOIN presupuesto.unidad_medida u ON u.id = i.unidad_medida_id
LEFT JOIN presupuesto.clasificacion c ON c.id = i.clasificacion_id
LEFT JOIN LATERAL (
    SELECT ip.valor_unitario, ip.moneda, ip.fecha_vigencia
    FROM presupuesto.insumo_precio ip
    WHERE ip.insumo_id = i.id
    ORDER BY ip.fecha_vigencia DESC
    LIMIT 1
) p ON true;
COMMENT ON VIEW presupuesto.v_insumo_vigente IS 'Insumos con su último precio. Filtrar siempre por tenant_id.';

-- -----------------------------------------------------------------------------
-- Staging: copia literal del Excel, todo como texto
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS carga.insumo_excel (
    hoja                text    NOT NULL,
    fila                integer NOT NULL,
    descripcion         text,
    unidad              text,
    valor_unitario      text,
    fecha_actualizacion text,   -- número de serie de Excel (días desde 1899-12-30)
    clasificacion       text,
    PRIMARY KEY (hoja, fila)
);

CREATE TABLE IF NOT EXISTS carga.clasificador_excel (
    nombre text PRIMARY KEY
);

-- Reglas de limpieza aplicadas a la copia cruda. 04_normalizar_insumos.sql y
-- carga.v_observacion leen de aquí, así las dos usan exactamente las mismas reglas.
CREATE OR REPLACE VIEW carga.v_insumo_normalizado AS
SELECT e.hoja,
       e.fila,
       regexp_replace(btrim(e.descripcion), '\s+', ' ', 'g')                   AS descripcion,
       upper(btrim(e.unidad))                                                   AS unidad,
       CASE e.hoja WHEN 'MATERIALES' THEN 1 WHEN 'EQUIPO' THEN 2 WHEN 'MANO DE OBRA' THEN 3 END::smallint
                                                                                AS tipo_insumo_id,
       CASE e.hoja
           WHEN 'EQUIPO'       THEN 'EQUIPOS'       -- la hoja dice EQUIPO; el clasificador oficial es EQUIPOS
           WHEN 'MANO DE OBRA' THEN 'MANO DE OBRA'
           ELSE CASE WHEN btrim(e.clasificacion) ~ '^[0-9.]+$' THEN NULL
                     ELSE nullif(upper(regexp_replace(btrim(e.clasificacion), '\s+', ' ', 'g')), '') END
       END                                                                      AS clasificacion,
       CASE WHEN btrim(e.valor_unitario) ~ '^[0-9]+(\.[0-9]+)?$'
            THEN round(btrim(e.valor_unitario)::numeric, 2) END                AS valor_unitario,
       CASE WHEN btrim(e.fecha_actualizacion) ~ '^[0-9]+$'
            THEN date '1899-12-30' + btrim(e.fecha_actualizacion)::integer END AS fecha_vigencia,
       e.clasificacion                                                          AS clasificacion_original,
       e.valor_unitario                                                         AS valor_original
FROM carga.insumo_excel e
WHERE nullif(btrim(e.descripcion), '') IS NOT NULL;

-- Filas del Excel que necesitan revisión, con el motivo y lo que se hizo con cada una.
CREATE OR REPLACE VIEW carga.v_observacion AS
WITH n AS (
    SELECT v.*,
           count(*) OVER (PARTITION BY tipo_insumo_id, upper(descripcion), unidad)                 AS repeticiones,
           count(*) OVER (PARTITION BY tipo_insumo_id, upper(descripcion), unidad, fecha_vigencia) AS repeticiones_fecha,
           max(valor_unitario) OVER (PARTITION BY tipo_insumo_id, upper(descripcion), unidad, fecha_vigencia) AS valor_elegido
    FROM carga.v_insumo_normalizado v
)
SELECT hoja, fila, descripcion, 'SIN_PRECIO' AS motivo, 'Se crea el insumo sin precio' AS accion
FROM n WHERE valor_unitario IS NULL
UNION ALL
SELECT hoja, fila, descripcion, 'PRECIO_CERO', 'Se crea el insumo sin precio'
FROM n WHERE valor_unitario = 0
UNION ALL
SELECT hoja, fila, descripcion, 'SIN_CLASIFICACION', 'Se crea el insumo sin clasificación'
FROM n WHERE clasificacion IS NULL AND nullif(btrim(clasificacion_original), '') IS NULL
UNION ALL
SELECT hoja, fila, descripcion, 'CLASIFICACION_INVALIDA', 'Se ignora el valor ' || clasificacion_original
FROM n WHERE btrim(clasificacion_original) ~ '^[0-9.]+$'
UNION ALL
SELECT hoja, fila, descripcion, 'UNIDAD_DESCONOCIDA', 'No se carga: agregar ' || unidad || ' a unidad_medida'
FROM n WHERE NOT EXISTS (SELECT 1 FROM presupuesto.unidad_medida u WHERE u.codigo = n.unidad)
UNION ALL
SELECT hoja, fila, descripcion, 'DUPLICADO_MISMA_FECHA',
       'Se unifica en un insumo; precio elegido ' || valor_elegido || ' (el mayor de la fecha)'
FROM n WHERE repeticiones_fecha > 1
UNION ALL
SELECT hoja, fila, descripcion, 'DUPLICADO_OTRA_FECHA', 'Se unifica en un insumo; el precio queda en el historial'
FROM n WHERE repeticiones > 1 AND repeticiones_fecha = 1;
