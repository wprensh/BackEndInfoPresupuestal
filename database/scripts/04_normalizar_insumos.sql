-- =============================================================================
-- 04 · Pasa la copia cruda (carga) al modelo (presupuesto) para un tenant.
-- Idempotente: volver a ejecutarlo solo agrega lo que falte.
--
-- Tenant: por defecto 'demo'. Para otro, ejecutar antes en la misma sesión:
--     SET app.tenant_id = 'acme';
-- =============================================================================

SELECT set_config('app.tenant_id', coalesce(nullif(current_setting('app.tenant_id', true), ''), 'demo'), false) AS tenant;

-- 1. Clasificaciones: la lista oficial (columna N de MATERIALES) más las usadas en las filas
INSERT INTO presupuesto.clasificacion (tenant_id, nombre)
SELECT current_setting('app.tenant_id'), nombre
FROM (
    SELECT upper(regexp_replace(btrim(nombre), '\s+', ' ', 'g')) AS nombre FROM carga.clasificador_excel
    UNION
    SELECT clasificacion FROM carga.v_insumo_normalizado WHERE clasificacion IS NOT NULL
) s
ORDER BY nombre
ON CONFLICT (tenant_id, upper(nombre)) DO NOTHING;

-- 2. Insumos: uno por (tipo, descripción, unidad). Los duplicados del Excel se unifican.
WITH grupo AS (
    SELECT tipo_insumo_id,
           unidad,
           (array_agg(descripcion ORDER BY fila))[1]                                          AS descripcion,
           (array_agg(clasificacion ORDER BY fila) FILTER (WHERE clasificacion IS NOT NULL))[1] AS clasificacion,
           min(fila)                                                                          AS primera_fila
    FROM carga.v_insumo_normalizado
    GROUP BY tipo_insumo_id, upper(descripcion), unidad
),
nuevo AS (
    SELECT g.*, u.id AS unidad_medida_id, c.id AS clasificacion_id
    FROM grupo g
    JOIN presupuesto.unidad_medida u ON u.codigo = g.unidad
    LEFT JOIN presupuesto.clasificacion c
           ON c.tenant_id = current_setting('app.tenant_id') AND upper(c.nombre) = g.clasificacion
    WHERE NOT EXISTS (
        SELECT 1 FROM presupuesto.insumo i
        WHERE i.tenant_id = current_setting('app.tenant_id')
          AND i.tipo_insumo_id = g.tipo_insumo_id
          AND upper(i.descripcion) = upper(g.descripcion)
          AND i.unidad_medida_id = u.id)
),
ultimo_codigo AS (
    SELECT i.tipo_insumo_id, max(substring(i.codigo FROM '[0-9]+$')::integer) AS ultimo
    FROM presupuesto.insumo i
    WHERE i.tenant_id = current_setting('app.tenant_id')
    GROUP BY i.tipo_insumo_id
)
INSERT INTO presupuesto.insumo (tenant_id, codigo, tipo_insumo_id, clasificacion_id, unidad_medida_id, descripcion)
SELECT current_setting('app.tenant_id'),
       t.prefijo || '-' || lpad((coalesce(uc.ultimo, 0)
           + row_number() OVER (PARTITION BY n.tipo_insumo_id ORDER BY n.primera_fila))::text, 4, '0'),
       n.tipo_insumo_id,
       n.clasificacion_id,
       n.unidad_medida_id,
       n.descripcion
FROM nuevo n
JOIN presupuesto.tipo_insumo t ON t.id = n.tipo_insumo_id
LEFT JOIN ultimo_codigo uc ON uc.tipo_insumo_id = n.tipo_insumo_id
ORDER BY n.tipo_insumo_id, n.primera_fila;

-- 3. Precios: uno por insumo y fecha. Precios vacíos o en cero no se cargan.
--    Si el Excel repite un insumo con la misma fecha y precios distintos, se toma el mayor.
INSERT INTO presupuesto.insumo_precio (tenant_id, insumo_id, valor_unitario, fecha_vigencia, fuente)
SELECT i.tenant_id, i.id, max(n.valor_unitario), n.fecha_vigencia, 'Excel BASE DE DATOS PARA PRESUPUES-TEN.xls'
FROM carga.v_insumo_normalizado n
JOIN presupuesto.unidad_medida u ON u.codigo = n.unidad
JOIN presupuesto.insumo i
  ON i.tenant_id = current_setting('app.tenant_id')
 AND i.tipo_insumo_id = n.tipo_insumo_id
 AND upper(i.descripcion) = upper(n.descripcion)
 AND i.unidad_medida_id = u.id
WHERE n.valor_unitario > 0 AND n.fecha_vigencia IS NOT NULL
GROUP BY i.tenant_id, i.id, n.fecha_vigencia
ON CONFLICT (insumo_id, fecha_vigencia) DO NOTHING;

-- 4. Resumen
SELECT t.nombre AS tipo,
       count(*)                       AS insumos,
       count(v.valor_unitario)        AS con_precio,
       count(*) FILTER (WHERE v.clasificacion IS NULL) AS sin_clasificacion
FROM presupuesto.v_insumo_vigente v
JOIN presupuesto.tipo_insumo t ON t.nombre = v.tipo
WHERE v.tenant_id = current_setting('app.tenant_id')
GROUP BY t.id, t.nombre
ORDER BY t.id;
