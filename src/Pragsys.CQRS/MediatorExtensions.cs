using Microsoft.Extensions.DependencyInjection;

namespace Pragsys.CQRS;

public static class MediatorExtensions
{
    public static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        services.AddTransient<IMediator, Mediator>();
        services.AddSingleton<MediatorCacheMap>();
        return services;
    }

    public static IServiceCollection AddCqrs(this IServiceCollection services, Action<MediatorConfig> config)
    {
        services.AddTransient<IMediator, Mediator>();
        services.AddSingleton<MediatorCacheMap>();

        var configurationBuilder = new MediatorConfig(services);
        config(configurationBuilder);
        return services;
    }
}
