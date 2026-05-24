using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCQRS;

public class MediatorConfig(IServiceCollection Services)
{
    public void RegisterServicesFromAssemblies(params Assembly[] targetAssemblies)
    {
        if (Services == null) throw new ArgumentNullException(nameof(Services));
        if (targetAssemblies == null) throw new ArgumentNullException(nameof(targetAssemblies));

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

                        Services.AddTransient(
                            typeof(IRequestHandler<,>).MakeGenericType(requestType, resultType),
                            type);
                    }
                    else if (genericDef == handlerWithoutResultInterface)
                    {
                        var requestType = iface.GetGenericArguments()[0];

                        Services.AddTransient(
                            typeof(IRequestHandler<>).MakeGenericType(requestType),
                            type);
                    }
                }
            }
        }
    }
}
