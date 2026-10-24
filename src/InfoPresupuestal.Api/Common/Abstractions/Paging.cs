using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Common.Abstractions;

/// <summary>Parámetros de listado: <c>?page=1&amp;pageSize=20&amp;search=texto</c>.</summary>
public record PageRequest(int Page = 1, int PageSize = 20, string? Search = null)
{
    public const int MaxPageSize = 100;

    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    /// <summary>Texto de búsqueda listo para <c>EF.Functions.ILike</c>, o null si no hay búsqueda.</summary>
    public string? SearchPattern => string.IsNullOrWhiteSpace(Search) ? null : $"%{LikePattern.Escape(Search.Trim())}%";
}

/// <summary>Patrones para <c>EF.Functions.ILike</c> que tratan el texto del usuario como literal.</summary>
public static class LikePattern
{
    /// <summary>Escapa <c>\</c>, <c>%</c> y <c>_</c> (el carácter de escape por defecto de PostgreSQL es <c>\</c>).</summary>
    public static string Escape(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    /// <summary>Igualdad sin distinguir mayúsculas: <c>ILIKE 'texto'</c> sin comodines.</summary>
    public static string Exact(string value) => Escape(value);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>Respuesta de los comandos que no devuelven datos (eliminar, cambiar contraseña).</summary>
public readonly record struct Done;

public static class PagingExtensions
{
    public static async Task<PagedResponse<T>> ToPagedAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<T>(items, request.SafePage, request.SafePageSize, total);
    }
}
