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

    private sealed record MediatorMap(Type HandlerType, Delegate HandlerMethod);

    private sealed record MediatorCacheEntry(Type HandlerType, Type BehaviorType, Delegate HandlerMethod, MethodInfo BehaviorMethod)
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
                var requestMap = GetRequestMap(requestType, responseType);
                //var behaviourMap = GetBehaviourMap(requestType, responseType);

                var bt = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

                return new MediatorCacheEntry(
                    requestMap.HandlerType,
                    bt,
                    requestMap.HandlerMethod,
                    bt.GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance)!);
            });

            var handler = provider.GetRequiredService(cacheEntry.HandlerType);
            var behaviors = provider.GetServices(cacheEntry.BehaviorType).Reverse();

            Func<Task<TResponse>> handlerDelegate = () =>
            {
                return (Task<TResponse>)cacheEntry.HandlerMethod.DynamicInvoke(handler, request, cancellationToken);
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

    private static MediatorMap GetRequestMap(Type requestType, Type responseType)
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
        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "request");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) });
        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, ct);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, ct).Compile();

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