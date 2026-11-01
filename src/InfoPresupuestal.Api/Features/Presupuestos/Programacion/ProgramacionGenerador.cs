using InfoPresupuestal.Api.Features.Presupuestos.Obras;

namespace InfoPresupuestal.Api.Features.Presupuestos.Programacion;

/// <summary>
/// Arma una programación inicial desde el presupuesto, para que el usuario solo la ajuste en el Gantt:
/// <list type="bullet">
/// <item>cada capítulo es un resumen (<c>project</c>) y sus ítems son actividades;</item>
/// <item>el plazo se reparte entre capítulos según su peso en el costo directo y los capítulos van uno tras otro (fin-inicio);</item>
/// <item>dentro del capítulo, los ítems se escalonan según su peso y pueden traslaparse;</item>
/// <item>al final queda el hito "Entrega de obra".</item>
/// </list>
/// </summary>
internal static class ProgramacionGenerador
{
    public const int DuracionPorDefecto = 30;
    public const string TextoEntrega = "ENTREGA DE OBRA";

    public static (List<TareaDatos> Tareas, List<VinculoDatos> Vinculos) Generar(
        IReadOnlyList<PresupuestoCapitulo> capitulos, DateOnly inicio, int duracionTotal, Func<Guid> nuevoId)
    {
        var tareas = new List<TareaDatos>();
        var vinculos = new List<VinculoDatos>();
        var ordenados = capitulos.OrderBy(c => c.Numero).ToList();
        if (ordenados.Count == 0)
        {
            return (tareas, vinculos);
        }

        var subtotales = ordenados.Select(c => c.Items.Sum(i => i.ValorTotal)).ToList();
        var duraciones = Repartir(subtotales, Math.Max(duracionTotal, ordenados.Count));
        var fecha = inicio;
        Guid? anterior = null;

        for (var c = 0; c < ordenados.Count; c++)
        {
            var capitulo = ordenados[c];
            var duracion = duraciones[c];
            var capituloId = nuevoId();
            tareas.Add(new TareaDatos(
                capituloId, null, tareas.Count, TipoTarea.Capitulo, $"{capitulo.Numero}. {capitulo.Nombre}", capitulo.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
                fecha, duracion, 0, null, null, null, subtotales[c], null, null, true));

            var items = capitulo.Items.OrderBy(i => i.Numero).ToList();
            var valores = items.Select(i => i.ValorTotal).ToList();
            var total = valores.Sum();
            decimal acumulado = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var peso = total == 0 ? 1m / items.Count : valores[i] / total;
                var desplazamiento = (int)Math.Floor(acumulado * duracion);
                desplazamiento = Math.Min(desplazamiento, duracion - 1);
                var dias = Math.Clamp((int)Math.Round(peso * duracion, MidpointRounding.AwayFromZero), 1, duracion - desplazamiento);
                acumulado += peso;
                tareas.Add(new TareaDatos(
                    nuevoId(), capituloId, tareas.Count, TipoTarea.Tarea, item.Descripcion, PresupuestoMapping.Codigo(capitulo.Numero, item.Numero),
                    fecha.AddDays(desplazamiento), dias, 0, null, null, null, item.ValorTotal, null, null, true));
            }

            if (anterior is { } previo)
            {
                vinculos.Add(new VinculoDatos(nuevoId(), previo, capituloId, 0, 0));
            }

            anterior = capituloId;
            fecha = fecha.AddDays(duracion);
        }

        var entrega = nuevoId();
        tareas.Add(new TareaDatos(entrega, null, tareas.Count, TipoTarea.Hito, TextoEntrega, null, fecha, 0, 0, null, null, null, null, null, null, true));
        vinculos.Add(new VinculoDatos(nuevoId(), anterior!.Value, entrega, 0, 0));
        return (tareas, vinculos);
    }

    /// <summary>Reparte <paramref name="total"/> días según los pesos, con al menos un día por parte y suma exacta.</summary>
    public static List<int> Repartir(IReadOnlyList<decimal> pesos, int total)
    {
        var suma = pesos.Sum();
        var exactos = pesos.Select(p => suma == 0 ? (decimal)total / pesos.Count : p / suma * total).ToList();
        var dias = exactos.Select(e => Math.Max(1, (int)Math.Floor(e))).ToList();

        // Los días que faltan van a las partes con mayor fracción descartada; los que sobran salen de las más largas.
        var restante = total - dias.Sum();
        foreach (var i in Enumerable.Range(0, dias.Count).OrderByDescending(i => exactos[i] - Math.Floor(exactos[i])).ThenBy(i => i))
        {
            if (restante <= 0)
            {
                break;
            }

            dias[i]++;
            restante--;
        }

        while (restante < 0)
        {
            var mayor = dias.IndexOf(dias.Max());
            if (dias[mayor] <= 1)
            {
                break;
            }

            dias[mayor]--;
            restante++;
        }

        return dias;
    }
}
