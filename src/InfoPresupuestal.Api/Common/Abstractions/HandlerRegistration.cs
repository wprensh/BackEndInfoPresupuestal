using System.Reflection;
using InfoPresupuestal.Api.Common.Caching;

namespace InfoPresupuestal.Api.Common.Abstractions;

public static class HandlerRegistration
{
    /// <summary>
    /// Registra todos los handlers del ensamblado. Las consultas que implementan
    /// <see cref="ICachedQuery{TResponse}"/> se envuelven en <see cref="CachedQueryHandler{TQuery, TResponse}"/>.
    /// Agregar un slice nuevo no requiere tocar este archivo.
    /// </summary>
    public static IServiceCollection AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.DefinedTypes.Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var handlerType in handlerTypes)
        {
            foreach (var contract in handlerType.ImplementedInterfaces.Where(IsHandlerContract))
            {
                var arguments = contract.GetGenericArguments();
                var requestType = arguments[0];

                if (contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>) &&
                    requestType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICachedQuery<>)))
                {
                    var decoratorType = typeof(CachedQueryHandler<,>).MakeGenericType(arguments);
                    services.AddScoped(handlerType);
                    services.AddScoped(contract, sp => ActivatorUtilities.CreateInstance(sp, decoratorType, sp.GetRequiredService(handlerType)));
                }
                else
                {
                    services.AddScoped(contract, handlerType);
                }
            }
        }

        return services;
    }

    private static bool IsHandlerContract(Type type) =>
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
         type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));
}
