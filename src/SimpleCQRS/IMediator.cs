using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCQRS;

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    Task Send<TRequest>(TRequest query, CancellationToken cancellationToken = default)
        where TRequest : IRequest;
}

public class Mediator(IServiceProvider provider)
    : IMediator
{
    private sealed record MediatorCacheEntry(Type HandlerType, Type BehaviorType, MethodInfo HandlerMethod, MethodInfo BehaviorMethod)
    {
    }

    private readonly ConcurrentDictionary<(Type, Type), MediatorCacheEntry> _cache = new();

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestType = request.GetType();
            var responseType = typeof(TResponse);

            var cacheEntry = _cache.GetOrAdd((requestType, responseType), _ =>
            {
                var ht = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
                var bt = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

                return new MediatorCacheEntry(
                    ht,
                    bt,
                    ht.GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance)!,
                    bt.GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance)!);
            });

            var handler = provider.GetRequiredService(cacheEntry.HandlerType);
            var behaviors = provider.GetServices(cacheEntry.BehaviorType).Reverse();

            Func<Task<TResponse>> handlerDelegate = () =>
            {
                var result = cacheEntry.HandlerMethod.Invoke(handler, new object[] { request, cancellationToken });
                return (Task<TResponse>)result;
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.BehaviorMethod.Invoke(behavior, new object[] { request, next, cancellationToken });
                    return (Task<TResponse>)result;
                };
            }

            return await handlerDelegate();
        }
        catch (TargetInvocationException ex)
        {
            // Unpack the reflection error here.
            throw ex.InnerException ?? ex;
        }
    }

    public async Task Send<TRequest>(TRequest query, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        var handler = provider.GetRequiredService<IRequestHandler<TRequest>>();
        var behaviors = provider.GetServices<IPipelineBehavior<TRequest>>().Reverse();

        Func<Task> handlerDelegate = () => handler.Handle(query, cancellationToken);
        foreach (var behavior in behaviors)
        {
            var next = handlerDelegate;
            handlerDelegate = () => behavior.Handle(query, next, cancellationToken);
        }

        await handlerDelegate();
    }
}