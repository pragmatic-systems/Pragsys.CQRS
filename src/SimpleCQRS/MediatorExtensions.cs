using Microsoft.Extensions.DependencyInjection;

namespace SimpleCQRS;

public static class MediatorExtensions
{
    public static IServiceCollection AddMediatR(this IServiceCollection services, Action<MediatorConfig> config)
    {
        services.AddSingleton<IMediator, Mediator>();

        var configurationBuilder = new MediatorConfig(services);
        config(configurationBuilder);
        return services;
    }
}
