using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Pragsys.CQRS;

public class MediatorConfig
{
    private readonly IServiceCollection _services;

    internal MediatorConfig(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public void RegisterServicesFromAssemblies(Assembly[] targetAssemblies, ServiceLifetime serviceLifetime)
    {
        ArgumentNullException.ThrowIfNull(targetAssemblies);

        var handlerWithResultInterface = typeof(IRequestHandler<,>);
        var handlerWithoutResultInterface = typeof(IRequestHandler<>);

        foreach (var assembly in targetAssemblies)
        {
            // GetExportedTypes() is faster and safer than GetTypes() as it avoids loading
            // non-exported types into the current AppDomain.
            var types = assembly.GetExportedTypes();

            foreach (var type in types)
            {
                // Skip abstract, interface, and generic type definition types
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                var implementedInterfaces = type.GetInterfaces();
                foreach (var iface in implementedInterfaces)
                {
                    if (!iface.IsGenericType)
                        continue;

                    var genericDef = iface.GetGenericTypeDefinition();

                    if (genericDef == handlerWithResultInterface)
                    {
                        var requestType = iface.GetGenericArguments()[0];
                        var resultType = iface.GetGenericArguments()[1];
                        var genericType = typeof(IRequestHandler<,>).MakeGenericType(requestType, resultType);
                        _services.Add(new ServiceDescriptor(genericType, type, serviceLifetime));
                    }
                    else if (genericDef == handlerWithoutResultInterface)
                    {
                        var requestType = iface.GetGenericArguments()[0];
                        var genericType = typeof(IRequestHandler<>).MakeGenericType(requestType);
                        _services.Add(new ServiceDescriptor(genericType, type, serviceLifetime));
                    }
                }
            }
        }
    }
}

public static class MediatorConfigExtensions
{
    public static void RegisterServicesFromAssemblies(this MediatorConfig config, params Assembly[] targetAssemblies)
    {
        config.RegisterServicesFromAssemblies(targetAssemblies, ServiceLifetime.Transient);
    }
}