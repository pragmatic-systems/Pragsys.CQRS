using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Pragsys.CQRS;

public class MediatorCacheMap
{
    public sealed record MediatorMap(Type Type, Delegate Method);

    public sealed record MediatorCacheEntry(MediatorMap Handler, MediatorMap Behaviour);

    private readonly ConcurrentDictionary<(Type, Type?), MediatorCacheEntry> _cache = new();

    public MediatorCacheEntry GetOrAdd(Type requestType, Type responseType)
    {
        return _cache.GetOrAdd((requestType, responseType), _ =>
        {
            var handlerMap = GetHandlerMap(requestType, responseType);
            var behaviourMap = GetBehaviourMap(requestType, responseType);

            return new MediatorCacheEntry(
                handlerMap,
                behaviourMap);
        });
    }

    public MediatorCacheEntry GetOrAdd(Type requestType)
    {
        return _cache.GetOrAdd((requestType, null), _ =>
        {
            var handlerMap = GetHandlerMap(requestType);
            var behaviourMap = GetBehaviourMap(requestType);

            return new MediatorCacheEntry(
                handlerMap,
                behaviourMap);
        });
    }

    private static MediatorMap GetHandlerMap(Type requestType, Type responseType)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handlerParam = Expression.Parameter(handlerType, "handler");
        var requestParam = Expression.Parameter(requestType, "request");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Handler: {handlerType.FullName}", handlerType);

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
            ?? throw new CqrsException($"Cannot resolve Handle method for Handler: {handlerType.FullName}", handlerType);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, ctParam).Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType, Type responseType)
    {
        var behaviourType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var nextType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        var handlerParam = Expression.Parameter(behaviourType, "handler");
        var requestParam = Expression.Parameter(requestType, "input");
        var nextParam = Expression.Parameter(nextType, "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = behaviourType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Behaviour: {behaviourType.FullName}", behaviourType);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, nextParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, nextParam, ctParam).Compile();

        return new MediatorMap(behaviourType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType)
    {
        var behaviourType = typeof(IPipelineBehavior<>).MakeGenericType(requestType);
        var nextType = typeof(Func<>).MakeGenericType(typeof(Task));

        var handlerParam = Expression.Parameter(behaviourType, "handler");
        var requestParam = Expression.Parameter(requestType, "input");
        var nextParam = Expression.Parameter(nextType, "next");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var handleMethod = behaviourType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Behaviour: {behaviourType.FullName}", behaviourType);

        MethodCallExpression expr = Expression.Call(handlerParam, handleMethod, requestParam, nextParam, ctParam);
        var handlerDelegate = Expression.Lambda(expr, handlerParam, requestParam, nextParam, ctParam).Compile();

        return new MediatorMap(behaviourType, handlerDelegate);
    }
}
