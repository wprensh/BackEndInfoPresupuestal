-- =============================================================================
-- 07 · Formato de impresión por empresa y programación de obra (Gantt)
-- Idempotente: se puede ejecutar varias veces. Requiere 05_apu_presupuesto.sql.
--
--   configuracion_impresion          (una fila por empresa / tenant)
--   presupuesto ──1 programacion ──< programacion_tarea
--                               └──< programacion_vinculo (dependencias)
--
-- Reglas de diseño:
--   · El logo se guarda en la base (bytea, máx. 512 KB) con su tipo MIME.
--   · Las tareas usan uuid generados por el cliente: el diagrama crea tareas sin
--     esperar al servidor y la API guarda el conjunto completo en una transacción.
--   · referencia = consecutivo del capítulo o ítem del presupuesto ("2", "2.3")
--     del que salió la tarea; no es FK porque los ítems se renumeran al editar.
--   · vínculo tipo: 0 fin-inicio, 1 inicio-inicio, 2 fin-fin, 3 inicio-fin.
--   · Las fechas de la línea base son opcionales (se fijan desde el Gantt).
-- =============================================================================

CREATE TABLE IF NOT EXISTS presupuesto.configuracion_impresion (
    tenant_id             varchar(64)   PRIMARY KEY,
    razon_social          varchar(200),
    nit                   varchar(20),
    direccion             varchar(200),
    ciudad                varchar(80),
    telefono              varchar(30),
    email                 varchar(200),
    sitio_web             varchar(200),
    logo                  bytea,
    logo_tipo             varchar(40),
    color_primario        char(7)       NOT NULL DEFAULT '#006a6a',
    encabezado            varchar(500),
    pie_pagina            varchar(500),
    firma_nombre          varchar(120),
    firma_cargo           varchar(120),
    firma_matricula       varchar(60),
    mostrar_apu           boolean       NOT NULL DEFAULT true,
    mostrar_participacion boolean       NOT NULL DEFAULT true,
    mostrar_valor_letras  boolean       NOT NULL DEFAULT true,
    actualizado_en        timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_configuracion_impresion_tenant CHECK (tenant_id ~ '^[a-z0-9][a-z0-9-]{0,62}$'),
    CONSTRAINT ck_configuracion_impresion_color CHECK (color_primario ~ '^#[0-9a-f]{6}$'),
    CONSTRAINT ck_configuracion_impresion_logo CHECK (
        (logo IS NULL AND logo_tipo IS NULL)
        OR (logo IS NOT NULL AND octet_length(logo) <= 524288 AND logo_tipo IN ('image/png', 'image/jpeg', 'image/webp')))
);
COMMENT ON TABLE presupuesto.configuracion_impresion IS 'Membrete con que cada empresa imprime sus presupuestos: logo, razón social, datos de contacto y firma.';
COMMENT ON COLUMN presupuesto.configuracion_impresion.razon_social IS 'Razón social impresa; NULL = la de seguridad.empresa.';

CREATE TABLE IF NOT EXISTS presupuesto.programacion (
    presupuesto_id   uuid         PRIMARY KEY,
    tenant_id        varchar(64)  NOT NULL,
    fecha_inicio     date         NOT NULL,
    escala           varchar(12)  NOT NULL DEFAULT 'semana',
    creado_en        timestamptz  NOT NULL DEFAULT now(),
    actualizado_en   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_programacion_escala CHECK (escala IN ('dia', 'semana', 'mes', 'trimestre', 'anio')),
    CONSTRAINT fk_programacion_presupuesto FOREIGN KEY (presupuesto_id)
        REFERENCES presupuesto.presupuesto (id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_programacion_tenant ON presupuesto.programacion (tenant_id);
COMMENT ON TABLE presupuesto.programacion IS 'Programación de obra (diagrama de Gantt) de un presupuesto.';

CREATE TABLE IF NOT EXISTS presupuesto.programacion_tarea (
    id                 uuid          PRIMARY KEY,
    tenant_id          varchar(64)   NOT NULL,
    presupuesto_id     uuid          NOT NULL REFERENCES presupuesto.programacion (presupuesto_id) ON DELETE CASCADE,
    parent_id          uuid,
    orden              integer       NOT NULL,
    tipo               varchar(10)   NOT NULL DEFAULT 'task',
    texto              varchar(200)  NOT NULL,
    referencia         varchar(20),
    fecha_inicio       date          NOT NULL,
    duracion           integer       NOT NULL,
    progreso           numeric(5,4)  NOT NULL DEFAULT 0,
    responsable        varchar(120),
    color              char(7),
    notas              varchar(1000),
    valor              numeric(18,2),
    linea_base_inicio  date,
    linea_base_fin     date,
    abierta            boolean       NOT NULL DEFAULT true,
    CONSTRAINT ck_programacion_tarea_tipo CHECK (tipo IN ('task', 'project', 'milestone')),
    CONSTRAINT ck_programacion_tarea_duracion CHECK (duracion >= 0 AND duracion <= 3650),
    CONSTRAINT ck_programacion_tarea_progreso CHECK (progreso BETWEEN 0 AND 1),
    CONSTRAINT ck_programacion_tarea_color CHECK (color IS NULL OR color ~ '^#[0-9a-f]{6}$'),
    CONSTRAINT ck_programacion_tarea_linea_base CHECK (linea_base_inicio IS NULL OR linea_base_fin >= linea_base_inicio)
);
CREATE INDEX IF NOT EXISTS ix_programacion_tarea_presupuesto ON presupuesto.programacion_tarea (presupuesto_id, orden);
CREATE INDEX IF NOT EXISTS ix_programacion_tarea_tenant ON presupuesto.programacion_tarea (tenant_id);
COMMENT ON TABLE presupuesto.programacion_tarea IS 'Actividad del Gantt: capítulo (project), ítem (task) o hito (milestone). Duración en días calendario.';

CREATE TABLE IF NOT EXISTS presupuesto.programacion_vinculo (
    id              uuid         PRIMARY KEY,
    tenant_id       varchar(64)  NOT NULL,
    presupuesto_id  uuid         NOT NULL REFERENCES presupuesto.programacion (presupuesto_id) ON DELETE CASCADE,
    origen_id       uuid         NOT NULL REFERENCES presupuesto.programacion_tarea (id) ON DELETE CASCADE,
    destino_id      uuid         NOT NULL REFERENCES presupuesto.programacion_tarea (id) ON DELETE CASCADE,
    tipo            smallint     NOT NULL DEFAULT 0,
    desfase         integer      NOT NULL DEFAULT 0,
    CONSTRAINT ck_programacion_vinculo_tipo CHECK (tipo BETWEEN 0 AND 3),
    CONSTRAINT ck_programacion_vinculo_distintos CHECK (origen_id <> destino_id)
);
CREATE INDEX IF NOT EXISTS ix_programacion_vinculo_presupuesto ON presupuesto.programacion_vinculo (presupuesto_id);
CREATE INDEX IF NOT EXISTS ix_programacion_vinculo_tenant ON presupuesto.programacion_vinculo (tenant_id);
COMMENT ON TABLE presupuesto.programacion_vinculo IS 'Dependencia entre actividades: 0 fin-inicio, 1 inicio-inicio, 2 fin-fin, 3 inicio-fin; desfase en días.';
