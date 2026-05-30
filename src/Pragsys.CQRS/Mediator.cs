using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Pragsys.CQRS;

public class Mediator(IServiceProvider provider)
    : IMediator
{
    private sealed record MediatorMap(Type Type, Delegate Method);

    private sealed record MediatorCacheEntry(MediatorMap Handler, MediatorMap Behaviour);

    private readonly ConcurrentDictionary<(Type, Type?), MediatorCacheEntry> _cache = new();

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var requestType = request.GetType();
            var responseType = typeof(TResponse);

            var cacheEntry = _cache.GetOrAdd((requestType, responseType), _ =>
            {
                var handlerMap = GetHandlerMap(requestType, responseType);
                var behaviourMap = GetBehaviourMap(requestType, responseType);

                return new MediatorCacheEntry(
                    handlerMap,
                    behaviourMap);
            });

            var handler = provider.GetRequiredService(cacheEntry.Handler.Type);
            var behaviors = provider.GetServices(cacheEntry.Behaviour.Type).Reverse();

            Func<Task<TResponse>> handlerDelegate = () =>
            {
                var result = cacheEntry.Handler.Method.DynamicInvoke(handler, request, cancellationToken)
                    ?? throw new InvalidOperationException("Cannot resolve handler for " + cacheEntry.Handler.Type);

                return (Task<TResponse>)result;
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.Behaviour.Method.DynamicInvoke(behavior, request, next, cancellationToken)
                        ?? throw new InvalidOperationException("Cannot resolve handler for " + cacheEntry.Behaviour.Type);

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

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var requestType = request.GetType();

            var cacheEntry = _cache.GetOrAdd((requestType, null), _ =>
            {
                var handlerMap = GetHandlerMap(requestType);
                var behaviourMap = GetBehaviourMap(requestType);

                return new MediatorCacheEntry(
                    handlerMap,
                    behaviourMap);
            });

            var handler = provider.GetRequiredService(cacheEntry.Handler.Type);
            var behaviors = provider.GetServices(cacheEntry.Behaviour.Type).Reverse();

            Func<Task> handlerDelegate = () =>
            {
                var result = cacheEntry.Handler.Method.DynamicInvoke(handler, request, cancellationToken)
                    ?? throw new InvalidOperationException("Cannot resolve handler for " + cacheEntry.Handler.Type);

                return (Task)result;
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.Behaviour.Method.DynamicInvoke(behavior, request, next, cancellationToken)
                        ?? throw new InvalidOperationException("Cannot resolve handler for " + cacheEntry.Behaviour.Type);

                    return (Task)result;
                };
            }

            await handlerDelegate();
        }
        catch (TargetInvocationException ex)
        {
            // Unpack the reflection error here.
            throw ex.InnerException ?? ex;
        }
    }

    private static MediatorMap GetHandlerMap(Type requestType, Type responseType)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException("Cannot resolve Handle method for type: " + handlerType.FullName);
        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, ctParam).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetHandlerMap(Type requestType)
    {
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException("Cannot resolve Handle method for type: " + handlerType.FullName);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, ctParam).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType, Type responseType)
    {
        var handlerType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var nextType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "input");
        var nextParam = Expression.Parameter(nextType, "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException("Cannot resolve Handle method for type: " + handlerType.FullName);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, nextParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, nextParam, ctParam).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType)
    {
        var handlerType = typeof(IPipelineBehavior<>).MakeGenericType(requestType);
        var nextType = typeof(Func<>).MakeGenericType(typeof(Task));

        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "input");
        var nextParam = Expression.Parameter(nextType, "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException("Cannot resolve Handle method for type: " + handlerType.FullName);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, nextParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, nextParam, ctParam).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }
}
