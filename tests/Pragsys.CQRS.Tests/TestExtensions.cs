using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Pragsys.CQRS.Tests;

public static class TestExtensions
{
    public static IServiceCollection InitializeServices(this IServiceCollection services, params IPipelineBehavior[] pipelines)
    {
        services.AddCqrs(cfg =>
        {
            cfg.RegisterSingletonServicesFromAssemblies(
                typeof(MediatorTests).Assembly);
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
