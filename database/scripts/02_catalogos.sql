-- =============================================================================
-- 02 · Catálogos globales. Idempotente.
-- =============================================================================

INSERT INTO presupuesto.tipo_insumo (id, codigo, nombre, prefijo) VALUES
    (1, 'MATERIAL',  'Material',     'MAT'),
    (2, 'EQUIPO',    'Equipo',       'EQU'),
    (3, 'MANO_OBRA', 'Mano de obra', 'MO')
ON CONFLICT (id) DO UPDATE
    SET codigo = EXCLUDED.codigo, nombre = EXCLUDED.nombre, prefijo = EXCLUDED.prefijo;

INSERT INTO presupuesto.unidad_medida (codigo, nombre) VALUES
    ('UN',    'Unidad'),
    ('ML',    'Metro lineal'),
    ('M2',    'Metro cuadrado'),
    ('M3',    'Metro cúbico'),
    ('CM3',   'Centímetro cúbico'),
    ('INCH2', 'Pulgada cuadrada'),
    ('KG',    'Kilogramo'),
    ('LB',    'Libra'),
    ('L',     'Litro'),
    ('GAL',   'Galón'),
    ('HR',    'Hora'),
    ('DIA',   'Día'),
    ('MES',   'Mes'),
    ('GLB',   'Global'),
    ('TN/KM', 'Tonelada-kilómetro'),
    ('M3/KM', 'Metro cúbico-kilómetro')
ON CONFLICT (codigo) DO UPDATE SET nombre = EXCLUDED.nombre;
