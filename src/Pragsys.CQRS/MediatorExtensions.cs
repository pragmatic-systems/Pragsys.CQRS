using Microsoft.Extensions.DependencyInjection;

namespace Pragsys.CQRS;

public static class MediatorExtensions
{
    public static IServiceCollection AddCqrs(this IServiceCollection services, Action<MediatorConfig> config)
    {
        services.AddSingleton<IMediator, Mediator>();

        var configurationBuilder = new MediatorConfig(services);
        config(configurationBuilder);
        return services;
    }
}
