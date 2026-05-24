using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Numerics;
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

    private sealed record MediatorMap(Type Type, Delegate Method);

    private sealed record MediatorCacheEntry(MediatorMap Handler, MediatorMap Behaviour)
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
                var handlerMap = GetHanderMap(requestType, responseType);
                var behaviourMap = GetBehaviourMap(requestType, responseType);

                var bt = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

                return new MediatorCacheEntry(
                    handlerMap,
                    behaviourMap);
            });

            var handler = provider.GetRequiredService(cacheEntry.Handler.Type);
            var behaviors = provider.GetServices(cacheEntry.Behaviour.Type).Reverse();

            Func<Task<TResponse>> handlerDelegate = () =>
            {
                return (Task<TResponse>)cacheEntry.Handler.Method.DynamicInvoke(handler, request, cancellationToken);
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.Behaviour.Method.DynamicInvoke(behavior, request, next, cancellationToken);
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

    private static MediatorMap GetHanderMap(Type requestType, Type responseType)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "request");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) });
        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, ct);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, ct).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType, Type responseType)
    {
        var handlerType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var nextType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "input");
        var nextParam = Expression.Parameter(nextType, "next");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) });
        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, nextParam, ct);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, nextParam, ct).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
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