-- =============================================================================
-- 06 · Catálogo propio de proveedores
-- Un proveedor es una empresa (tenant) con seguridad.empresa.tipo = 'PROVEEDOR'.
-- Publica sus productos y una lista de precios por ciudad, con o sin IVA, que
-- todas las constructoras pueden consultar.
-- Idempotente: se puede ejecutar varias veces. Requiere 01_esquema.sql.
--
--   proveedor_producto ──< proveedor_precio   (historial por ciudad y fecha)
--
-- Reglas de diseño:
--   · tenant_id = el proveedor dueño del producto; FK compuestas (tenant_id, id).
--   · ciudad NULL = el precio aplica a todo el país.
--   · Un precio nuevo agrega una fila (fecha de vigencia); no sobrescribe.
--   · El IVA se guarda como fracción (0.19 = 19 %) junto a si el valor lo incluye.
-- =============================================================================

CREATE TABLE IF NOT EXISTS presupuesto.proveedor_producto (
    id               uuid         PRIMARY KEY,
    tenant_id        varchar(64)  NOT NULL,
    codigo           varchar(40)  NOT NULL,
    descripcion      varchar(200) NOT NULL,
    tipo_insumo_id   smallint     NOT NULL REFERENCES presupuesto.tipo_insumo (id),
    unidad_medida_id smallint     NOT NULL REFERENCES presupuesto.unidad_medida (id),
    marca            varchar(80),
    categoria        varchar(80),
    activo           boolean      NOT NULL DEFAULT true,
    creado_en        timestamptz  NOT NULL DEFAULT now(),
    actualizado_en   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_proveedor_producto_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_proveedor_producto_descripcion CHECK (descripcion = btrim(descripcion) AND descripcion <> ''),
    CONSTRAINT ck_proveedor_producto_codigo CHECK (codigo = btrim(codigo) AND codigo <> ''),
    CONSTRAINT uq_proveedor_producto_tenant_codigo UNIQUE (tenant_id, codigo),
    CONSTRAINT uq_proveedor_producto_tenant_id UNIQUE (tenant_id, id)
);
CREATE INDEX IF NOT EXISTS ix_proveedor_producto_descripcion_trgm
    ON presupuesto.proveedor_producto USING gin (descripcion gin_trgm_ops);
COMMENT ON TABLE presupuesto.proveedor_producto IS 'Producto que vende un proveedor, con su propio código (SKU).';

CREATE TABLE IF NOT EXISTS presupuesto.proveedor_precio (
    id             bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id      varchar(64)   NOT NULL,
    producto_id    uuid          NOT NULL,
    ciudad         varchar(80),
    valor          numeric(18,2) NOT NULL,
    incluye_iva    boolean       NOT NULL DEFAULT false,
    iva_pct        numeric(5,4)  NOT NULL DEFAULT 0.19,
    moneda         char(3)       NOT NULL DEFAULT 'COP',
    fecha_vigencia date          NOT NULL,
    creado_en      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_proveedor_precio_valor CHECK (valor > 0),
    CONSTRAINT ck_proveedor_precio_iva CHECK (iva_pct BETWEEN 0 AND 1),
    CONSTRAINT ck_proveedor_precio_moneda CHECK (moneda ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_proveedor_precio_ciudad CHECK (ciudad IS NULL OR (ciudad = upper(btrim(ciudad)) AND ciudad <> '')),
    CONSTRAINT fk_proveedor_precio_producto FOREIGN KEY (tenant_id, producto_id)
        REFERENCES presupuesto.proveedor_producto (tenant_id, id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_proveedor_precio_producto_ciudad_fecha
    ON presupuesto.proveedor_precio (producto_id, coalesce(ciudad, ''), fecha_vigencia);
COMMENT ON TABLE presupuesto.proveedor_precio IS 'Lista de precios del proveedor: historial por ciudad (NULL = nacional) y fecha de vigencia.';
COMMENT ON COLUMN presupuesto.proveedor_precio.iva_pct IS 'Tarifa de IVA como fracción (0.19 = 19 %).';

-- Precio vigente de cada producto en cada ciudad: la fila con la fecha más reciente.
CREATE OR REPLACE VIEW presupuesto.v_proveedor_precio_vigente AS
SELECT DISTINCT ON (p.producto_id, coalesce(p.ciudad, ''))
       p.tenant_id, p.producto_id, p.ciudad, p.valor, p.incluye_iva, p.iva_pct, p.moneda, p.fecha_vigencia,
       CASE WHEN p.incluye_iva THEN round(p.valor / (1 + p.iva_pct), 2) ELSE p.valor END AS valor_sin_iva,
       CASE WHEN p.incluye_iva THEN p.valor ELSE round(p.valor * (1 + p.iva_pct), 2) END AS valor_con_iva
FROM presupuesto.proveedor_precio p
ORDER BY p.producto_id, coalesce(p.ciudad, ''), p.fecha_vigencia DESC;
