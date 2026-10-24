"""Genera database/scripts/03_carga_excel.sql a partir del Excel de insumos.

Copia el contenido de las hojas tal cual (como texto) a las tablas del esquema `carga`.
La limpieza y la normalización se hacen en SQL (04_normalizar_insumos.sql), para que
cada regla quede visible y se pueda repetir.

Uso:
    pip install xlrd
    python database/herramientas/xls_a_sql.py "BASE DE DATOS PARA PRESUPUES-TEN.xls"
"""

import sys
from pathlib import Path

import xlrd

HOJAS = ("MATERIALES", "EQUIPO", "MANO DE OBRA")
COLUMNA_CLASIFICADORES = 13  # columna N de la hoja MATERIALES
SALIDA = Path(__file__).resolve().parent.parent / "scripts" / "03_carga_excel.sql"
FILAS_POR_INSERT = 500


def texto(valor):
    """Convierte una celda a texto sin perder información (los números enteros sin '.0')."""
    if valor is None or valor == "":
        return None
    if isinstance(valor, float):
        return str(int(valor)) if valor.is_integer() else repr(valor)
    return str(valor)


def literal(valor):
    return "NULL" if valor is None else "'" + valor.replace("'", "''") + "'"


def inserts(tabla, columnas, filas):
    for inicio in range(0, len(filas), FILAS_POR_INSERT):
        bloque = filas[inicio:inicio + FILAS_POR_INSERT]
        valores = ",\n".join("(" + ", ".join(literal(v) if not isinstance(v, int) else str(v) for v in fila) + ")" for fila in bloque)
        yield f"INSERT INTO {tabla} ({', '.join(columnas)}) VALUES\n{valores};\n"


def main(ruta_excel):
    libro = xlrd.open_workbook(ruta_excel)

    filas = []
    for nombre_hoja in HOJAS:
        hoja = libro.sheet_by_name(nombre_hoja)
        for fila in range(1, hoja.nrows):
            celdas = [texto(hoja.cell_value(fila, c)) for c in range(5)]
            if celdas[0] is None or not celdas[0].strip():
                continue
            # fila + 1 = número de fila que ve el usuario en Excel
            filas.append([nombre_hoja, fila + 1, *celdas])

    materiales = libro.sheet_by_name("MATERIALES")
    clasificadores = [
        [texto(materiales.cell_value(fila, COLUMNA_CLASIFICADORES))]
        for fila in range(materiales.nrows)
        if texto(materiales.cell_value(fila, COLUMNA_CLASIFICADORES)) not in (None, "CLASIFICADORES")
    ]

    with SALIDA.open("w", encoding="utf-8", newline="\n") as salida:
        salida.write(
            "-- =============================================================================\n"
            "-- 03 · Carga cruda del Excel 'BASE DE DATOS PARA PRESUPUES-TEN.xls'\n"
            "-- GENERADO por database/herramientas/xls_a_sql.py: no editar a mano.\n"
            f"-- {len(filas)} insumos y {len(clasificadores)} clasificadores.\n"
            "-- =============================================================================\n\n"
            "TRUNCATE carga.insumo_excel, carga.clasificador_excel;\n\n"
        )
        for sentencia in inserts("carga.clasificador_excel", ["nombre"], clasificadores):
            salida.write(sentencia + "\n")
        columnas = ["hoja", "fila", "descripcion", "unidad", "valor_unitario", "fecha_actualizacion", "clasificacion"]
        for sentencia in inserts("carga.insumo_excel", columnas, filas):
            salida.write(sentencia + "\n")

    print(f"{SALIDA}: {len(filas)} insumos, {len(clasificadores)} clasificadores")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
