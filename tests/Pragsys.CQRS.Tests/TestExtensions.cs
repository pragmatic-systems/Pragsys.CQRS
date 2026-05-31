using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Pragsys.CQRS.Tests;

public static class TestExtensions
{
    public static IServiceCollection InitializeTestServices(this IServiceCollection services, params IPipelineBehavior[] pipelines)
    {
        services.AddCqrs(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                new[] { typeof(MediatorTests).Assembly }, ServiceLifetime.Singleton);
        });

        foreach (var pipeline in pipelines.Reverse())
        {
            var type = pipeline.GetType();
            var interfaces = type.GetInterfaces();

            var targetInterface = interfaces
                .Where(i => i.IsGenericType)
                .Single(i => i.Implements(typeof(IPipelineBehavior)));

            var rootDef = targetInterface.GetGenericTypeDefinition();

            if (rootDef == typeof(IPipelineBehavior<>))
            {
                var requestType = targetInterface.GetGenericArguments()[0];
                services.AddSingleton(
                    typeof(IPipelineBehavior<>).MakeGenericType(requestType),
                    pipeline);
            }
            else if (rootDef == typeof(IPipelineBehavior<,>))
            {
                var requestType = targetInterface.GetGenericArguments()[0];
                var resultType = targetInterface.GetGenericArguments()[1];

                services.AddSingleton(
                    typeof(IPipelineBehavior<,>).MakeGenericType(requestType, resultType),
                    pipeline);
            }
        }

        return services;
    }
}
