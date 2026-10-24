-- =============================================================================
-- 05 · Análisis de Precios Unitarios (APU) y Presupuestos de obra
-- Modelo de las hojas APU's y PRESUPUESTO de "FORMATO PRESUPUES-TENV3v3.xlsm".
-- Idempotente: se puede ejecutar varias veces. Requiere 01_esquema.sql.
--
--   apu ──< apu_detalle >── insumo            (un APU reutilizable por empresa)
--   presupuesto ──< presupuesto_capitulo ──< presupuesto_item >── apu
--
-- Reglas de diseño:
--   · tenant_id en todas las tablas y FK compuestas (tenant_id, id): una fila
--     nunca puede apuntar a datos de otra empresa.
--   · Los valores calculados son columnas GENERATED: la base garantiza que
--     el total de cada línea siempre corresponde a sus factores.
--   · El ítem del presupuesto guarda una copia (descripción, unidad, valor
--     unitario) del APU al momento de agregarlo: si el APU cambia después,
--     el presupuesto ya emitido no cambia solo. La API ofrece recalcular.
--   · Porcentajes como fracción (0.05 = 5 %), igual que el Excel.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- APU: costo directo de una actividad por unidad de medida
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS presupuesto.apu (
    id               uuid         PRIMARY KEY,
    tenant_id        varchar(64)  NOT NULL,
    codigo           varchar(20)  NOT NULL,
    descripcion      varchar(200) NOT NULL,
    unidad_medida_id smallint     NOT NULL REFERENCES presupuesto.unidad_medida (id),
    fecha            date         NOT NULL DEFAULT current_date,
    observaciones    varchar(500),
    activo           boolean      NOT NULL DEFAULT true,
    creado_en        timestamptz  NOT NULL DEFAULT now(),
    actualizado_en   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_apu_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_apu_descripcion CHECK (descripcion = btrim(descripcion) AND descripcion <> ''),
    CONSTRAINT uq_apu_tenant_codigo UNIQUE (tenant_id, codigo),
    CONSTRAINT uq_apu_tenant_id UNIQUE (tenant_id, id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_apu_tenant_descripcion_unidad
    ON presupuesto.apu (tenant_id, upper(descripcion), unidad_medida_id);
CREATE INDEX IF NOT EXISTS ix_apu_descripcion_trgm ON presupuesto.apu USING gin (descripcion gin_trgm_ops);
COMMENT ON TABLE presupuesto.apu IS 'Análisis de precios unitarios: actividad reutilizable en varios presupuestos.';
COMMENT ON COLUMN presupuesto.apu.codigo IS 'Consecutivo legible por empresa: APU-0001.';

-- Una sola tabla para las tres secciones (materiales, equipo, mano de obra):
-- comparten casi todas las columnas y el CHECK de cada tipo exige las suyas.
CREATE TABLE IF NOT EXISTS presupuesto.apu_detalle (
    id                  bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id           varchar(64)   NOT NULL,
    apu_id              uuid          NOT NULL,
    tipo_insumo_id      smallint      NOT NULL REFERENCES presupuesto.tipo_insumo (id),
    orden               smallint      NOT NULL,
    insumo_id           uuid,
    descripcion         varchar(200)  NOT NULL,
    unidad_medida_id    smallint      REFERENCES presupuesto.unidad_medida (id),
    -- Material: cantidad. Equipo: incidencia. Mano de obra: no aplica (1).
    cantidad            numeric(18,6) NOT NULL DEFAULT 1,
    -- Material: precio unitario. Equipo: tarifa. Mano de obra: valor del jornal.
    precio_unitario     numeric(18,2) NOT NULL,
    desperdicio         numeric(7,4)  NOT NULL DEFAULT 0,  -- solo material
    factor_prestacional numeric(6,4)  NOT NULL DEFAULT 1,  -- solo mano de obra (1.7 = 70 %)
    rendimiento         numeric(18,6) NOT NULL DEFAULT 1,  -- solo mano de obra: unidades por día
    valor_parcial       numeric(20,4) GENERATED ALWAYS AS (
        CASE tipo_insumo_id
            WHEN 1 THEN cantidad * precio_unitario * (1 + desperdicio)   -- =E*G*F+E*G
            WHEN 2 THEN cantidad * precio_unitario                       -- =E*G
            WHEN 3 THEN precio_unitario * factor_prestacional / rendimiento  -- =(D*E)/G
        END) STORED,
    CONSTRAINT ck_apu_detalle_descripcion CHECK (btrim(descripcion) <> ''),
    CONSTRAINT ck_apu_detalle_cantidad CHECK (cantidad > 0),
    CONSTRAINT ck_apu_detalle_precio CHECK (precio_unitario >= 0),
    CONSTRAINT ck_apu_detalle_desperdicio CHECK (desperdicio BETWEEN 0 AND 1),
    CONSTRAINT ck_apu_detalle_factor CHECK (factor_prestacional BETWEEN 1 AND 3),
    CONSTRAINT ck_apu_detalle_rendimiento CHECK (rendimiento > 0),
    CONSTRAINT ck_apu_detalle_unidad CHECK (tipo_insumo_id = 3 OR unidad_medida_id IS NOT NULL),
    CONSTRAINT uq_apu_detalle_orden UNIQUE (apu_id, tipo_insumo_id, orden),
    CONSTRAINT fk_apu_detalle_apu FOREIGN KEY (tenant_id, apu_id)
        REFERENCES presupuesto.apu (tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_apu_detalle_insumo FOREIGN KEY (tenant_id, insumo_id)
        REFERENCES presupuesto.insumo (tenant_id, id) ON DELETE SET NULL (insumo_id)
);
CREATE INDEX IF NOT EXISTS ix_apu_detalle_insumo ON presupuesto.apu_detalle (insumo_id) WHERE insumo_id IS NOT NULL;
COMMENT ON TABLE presupuesto.apu_detalle IS 'Líneas de materiales, equipo y mano de obra de un APU.';
COMMENT ON COLUMN presupuesto.apu_detalle.insumo_id IS 'Insumo del catálogo de origen; la línea guarda su propia descripción y precio.';

-- -----------------------------------------------------------------------------
-- Presupuesto de obra
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS presupuesto.presupuesto (
    id                  uuid          PRIMARY KEY,
    tenant_id           varchar(64)   NOT NULL,
    codigo              varchar(20)   NOT NULL,
    nombre_obra         varchar(200)  NOT NULL,
    cliente             varchar(200),
    ubicacion           varchar(200),
    fecha               date          NOT NULL DEFAULT current_date,
    propuesta_numero    varchar(30),
    estado              varchar(12)   NOT NULL DEFAULT 'BORRADOR',
    moneda              char(3)       NOT NULL DEFAULT 'COP',
    administracion_pct  numeric(6,4)  NOT NULL DEFAULT 0.09,
    imprevistos_pct     numeric(6,4)  NOT NULL DEFAULT 0.03,
    utilidad_pct        numeric(6,4)  NOT NULL DEFAULT 0.08,
    iva_utilidad_pct    numeric(6,4)  NOT NULL DEFAULT 0.19,
    duracion_dias       integer,
    anticipo_pct        numeric(6,4),
    observaciones       varchar(1000),
    creado_en           timestamptz   NOT NULL DEFAULT now(),
    actualizado_en      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_presupuesto_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_presupuesto_obra CHECK (btrim(nombre_obra) <> ''),
    CONSTRAINT ck_presupuesto_estado CHECK (estado IN ('BORRADOR', 'APROBADO', 'ANULADO')),
    CONSTRAINT ck_presupuesto_moneda CHECK (moneda ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_presupuesto_pct CHECK (
        administracion_pct BETWEEN 0 AND 1 AND imprevistos_pct BETWEEN 0 AND 1
        AND utilidad_pct BETWEEN 0 AND 1 AND iva_utilidad_pct BETWEEN 0 AND 1
        AND (anticipo_pct IS NULL OR anticipo_pct BETWEEN 0 AND 1)),
    CONSTRAINT ck_presupuesto_duracion CHECK (duracion_dias IS NULL OR duracion_dias > 0),
    CONSTRAINT uq_presupuesto_tenant_codigo UNIQUE (tenant_id, codigo),
    CONSTRAINT uq_presupuesto_tenant_id UNIQUE (tenant_id, id)
);
CREATE INDEX IF NOT EXISTS ix_presupuesto_tenant_fecha ON presupuesto.presupuesto (tenant_id, fecha DESC);
CREATE INDEX IF NOT EXISTS ix_presupuesto_obra_trgm ON presupuesto.presupuesto USING gin (nombre_obra gin_trgm_ops);
COMMENT ON TABLE presupuesto.presupuesto IS 'Presupuesto de obra: encabezado, porcentajes AIU y condiciones.';
COMMENT ON COLUMN presupuesto.presupuesto.codigo IS 'Consecutivo legible por empresa: PRE-0001.';
COMMENT ON COLUMN presupuesto.presupuesto.iva_utilidad_pct IS 'IVA sobre la utilidad (régimen AIU).';

CREATE TABLE IF NOT EXISTS presupuesto.presupuesto_capitulo (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id      varchar(64)  NOT NULL,
    presupuesto_id uuid         NOT NULL,
    numero         smallint     NOT NULL,
    nombre         varchar(200) NOT NULL,
    CONSTRAINT ck_presupuesto_capitulo_numero CHECK (numero > 0),
    CONSTRAINT ck_presupuesto_capitulo_nombre CHECK (btrim(nombre) <> ''),
    CONSTRAINT uq_presupuesto_capitulo_numero UNIQUE (presupuesto_id, numero),
    CONSTRAINT uq_presupuesto_capitulo_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_presupuesto_capitulo_presupuesto FOREIGN KEY (tenant_id, presupuesto_id)
        REFERENCES presupuesto.presupuesto (tenant_id, id) ON DELETE CASCADE
);
COMMENT ON TABLE presupuesto.presupuesto_capitulo IS 'Capítulo del presupuesto (CIMENTACION, ESTRUCTURA, ...), numerado 1, 2, 3...';

CREATE TABLE IF NOT EXISTS presupuesto.presupuesto_item (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id        varchar(64)   NOT NULL,
    capitulo_id      bigint        NOT NULL,
    numero           smallint      NOT NULL,
    apu_id           uuid,
    descripcion      varchar(200)  NOT NULL,
    unidad_medida_id smallint      NOT NULL REFERENCES presupuesto.unidad_medida (id),
    cantidad         numeric(18,4) NOT NULL,
    valor_unitario   numeric(18,2) NOT NULL,
    valor_total      numeric(20,2) GENERATED ALWAYS AS (round(cantidad * valor_unitario, 2)) STORED,
    CONSTRAINT ck_presupuesto_item_numero CHECK (numero > 0),
    CONSTRAINT ck_presupuesto_item_descripcion CHECK (btrim(descripcion) <> ''),
    CONSTRAINT ck_presupuesto_item_cantidad CHECK (cantidad > 0),
    CONSTRAINT ck_presupuesto_item_valor CHECK (valor_unitario >= 0),
    CONSTRAINT uq_presupuesto_item_numero UNIQUE (capitulo_id, numero),
    CONSTRAINT fk_presupuesto_item_capitulo FOREIGN KEY (tenant_id, capitulo_id)
        REFERENCES presupuesto.presupuesto_capitulo (tenant_id, id) ON DELETE CASCADE,
    -- Un APU usado en un presupuesto no se puede borrar (se desactiva).
    CONSTRAINT fk_presupuesto_item_apu FOREIGN KEY (tenant_id, apu_id)
        REFERENCES presupuesto.apu (tenant_id, id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_presupuesto_item_apu ON presupuesto.presupuesto_item (apu_id) WHERE apu_id IS NOT NULL;
COMMENT ON TABLE presupuesto.presupuesto_item IS 'Ítem del presupuesto; su consecutivo es capítulo.número (1.1, 1.2, 2.1).';
COMMENT ON COLUMN presupuesto.presupuesto_item.valor_unitario IS 'Costo directo del APU copiado al agregar el ítem.';

-- -----------------------------------------------------------------------------
-- actualizado_en automático
-- -----------------------------------------------------------------------------

DROP TRIGGER IF EXISTS tg_apu_actualizado_en ON presupuesto.apu;
CREATE TRIGGER tg_apu_actualizado_en
    BEFORE UPDATE ON presupuesto.apu
    FOR EACH ROW EXECUTE FUNCTION presupuesto.fn_actualizado_en();

DROP TRIGGER IF EXISTS tg_presupuesto_actualizado_en ON presupuesto.presupuesto;
CREATE TRIGGER tg_presupuesto_actualizado_en
    BEFORE UPDATE ON presupuesto.presupuesto
    FOR EACH ROW EXECUTE FUNCTION presupuesto.fn_actualizado_en();

-- -----------------------------------------------------------------------------
-- Vistas de consulta (filtrar siempre por tenant_id)
-- -----------------------------------------------------------------------------

CREATE OR REPLACE VIEW presupuesto.v_apu_costo AS
SELECT a.tenant_id,
       a.id,
       a.codigo,
       a.descripcion,
       u.codigo AS unidad,
       round(coalesce(sum(d.valor_parcial) FILTER (WHERE d.tipo_insumo_id = 1), 0), 2) AS materiales,
       round(coalesce(sum(d.valor_parcial) FILTER (WHERE d.tipo_insumo_id = 2), 0), 2) AS equipo,
       round(coalesce(sum(d.valor_parcial) FILTER (WHERE d.tipo_insumo_id = 3), 0), 2) AS mano_obra,
       round(coalesce(sum(d.valor_parcial), 0), 2) AS costo_directo,
       a.activo
FROM presupuesto.apu a
JOIN presupuesto.unidad_medida u ON u.id = a.unidad_medida_id
LEFT JOIN presupuesto.apu_detalle d ON d.apu_id = a.id
GROUP BY a.tenant_id, a.id, a.codigo, a.descripcion, u.codigo, a.activo;
COMMENT ON VIEW presupuesto.v_apu_costo IS 'Costo directo de cada APU por sección.';

-- Mismas fórmulas del Excel (y de la API): AIU sobre el costo directo, IVA sobre la
-- utilidad, cada valor redondeado a centavos y el total truncado a pesos.
CREATE OR REPLACE VIEW presupuesto.v_presupuesto_resumen AS
WITH cd AS (
    SELECT c.presupuesto_id, coalesce(sum(i.valor_total), 0) AS costo_directo
    FROM presupuesto.presupuesto_capitulo c
    LEFT JOIN presupuesto.presupuesto_item i ON i.capitulo_id = c.id
    GROUP BY c.presupuesto_id
), aiu AS (
    SELECT p.*,
           coalesce(cd.costo_directo, 0)                                   AS costo_directo,
           round(coalesce(cd.costo_directo, 0) * p.administracion_pct, 2) AS administracion,
           round(coalesce(cd.costo_directo, 0) * p.imprevistos_pct, 2)    AS imprevistos,
           round(coalesce(cd.costo_directo, 0) * p.utilidad_pct, 2)       AS utilidad
    FROM presupuesto.presupuesto p
    LEFT JOIN cd ON cd.presupuesto_id = p.id
)
SELECT tenant_id,
       id,
       codigo,
       nombre_obra,
       estado,
       costo_directo,
       administracion,
       imprevistos,
       utilidad,
       round(utilidad * iva_utilidad_pct, 2) AS iva_utilidad,
       trunc(costo_directo + administracion + imprevistos + utilidad + round(utilidad * iva_utilidad_pct, 2)) AS valor_total
FROM aiu;
COMMENT ON VIEW presupuesto.v_presupuesto_resumen IS 'Costo directo, AIU, IVA sobre utilidad y valor total de cada presupuesto.';
