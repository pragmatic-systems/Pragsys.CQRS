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
        var handlerParamObj = Expression.Parameter(typeof(object), "handlerObj");
        var requestParamObj = Expression.Parameter(typeof(object), "requestObj");
        var ctParamObj = Expression.Parameter(typeof(object), "ctObj");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Handler: {handlerType.FullName}", handlerType);

        var handlerExpr = Expression.Convert(handlerParamObj, handlerType);
        var requestExpr = Expression.Convert(requestParamObj, requestType);
        var ctExpr = Expression.Convert(ctParamObj, typeof(CancellationToken));

        var callExpr = Expression.Call(handlerExpr, handleMethod, requestExpr, ctExpr);

        var lambdaExpr = Expression.Lambda<Func<object, object, object, object>>(
            callExpr,
            handlerParamObj,
            requestParamObj,
            ctParamObj);

        var handlerDelegate = lambdaExpr.Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetHandlerMap(Type requestType)
    {
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
        var handlerParamObj = Expression.Parameter(typeof(object), "handlerObj");
        var requestParamObj = Expression.Parameter(typeof(object), "requestObj");
        var ctParamObj = Expression.Parameter(typeof(object), "ctObj");

        var handleMethod = handlerType.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Handler: {handlerType.FullName}", handlerType);

        var handlerExpr = Expression.Convert(handlerParamObj, handlerType);
        var requestExpr = Expression.Convert(requestParamObj, requestType);
        var ctExpr = Expression.Convert(ctParamObj, typeof(CancellationToken));

        MethodCallExpression callExpr = Expression.Call(handlerExpr, handleMethod, requestExpr, ctExpr);

        var lambdaExpr = Expression.Lambda<Func<object, object, object, object>>(
            callExpr,
            handlerParamObj,
            requestParamObj,
            ctParamObj);

        var handlerDelegate = lambdaExpr.Compile();

        return new MediatorMap(handlerType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType, Type responseType)
    {
        var behaviourType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var nextType = typeof(RequestHandlerDelegate<>).MakeGenericType(responseType);

        var behaviourParamObj = Expression.Parameter(typeof(object), "behaviourObj");
        var inputParamObj = Expression.Parameter(typeof(object), "inputObj");
        var requestNextObj = Expression.Parameter(typeof(object), "nextObj");
        var ctParamObj = Expression.Parameter(typeof(object), "ctObj");

        var behaviourMethod = behaviourType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Behaviour: {behaviourType.FullName}", behaviourType);

        var behaviourExpr = Expression.Convert(behaviourParamObj, behaviourType);
        var requestExpr = Expression.Convert(inputParamObj, requestType);
        var nextExpr = Expression.Convert(requestNextObj, nextType);
        var ctExpr = Expression.Convert(ctParamObj, typeof(CancellationToken));

        MethodCallExpression callExpr = Expression.Call(behaviourExpr, behaviourMethod, requestExpr, nextExpr, ctExpr);

        var lambdaExpr = Expression.Lambda<Func<object, object, object, object, object>>(
            callExpr,
            behaviourParamObj,
            inputParamObj,
            requestNextObj,
            ctParamObj);

        var handlerDelegate = lambdaExpr.Compile();

        return new MediatorMap(behaviourType, handlerDelegate);
    }

    private static MediatorMap GetBehaviourMap(Type requestType)
    {
        var behaviourType = typeof(IPipelineBehavior<>).MakeGenericType(requestType);
        var nextType = typeof(RequestHandlerDelegate);

        var behaviourParamObj = Expression.Parameter(typeof(object), "behaviourObj");
        var inputParamObj = Expression.Parameter(typeof(object), "inputObj");
        var requestNextObj = Expression.Parameter(typeof(object), "nextObj");
        var ctParamObj = Expression.Parameter(typeof(object), "ctObj");

        var behaviourMethod = behaviourType.GetMethod("Handle", new Type[] { requestType, nextType, typeof(CancellationToken) })
            ?? throw new CqrsException($"Cannot resolve Handle method for Behaviour: {behaviourType.FullName}", behaviourType);

        var behaviourExpr = Expression.Convert(behaviourParamObj, behaviourType);
        var requestExpr = Expression.Convert(inputParamObj, requestType);
        var nextExpr = Expression.Convert(requestNextObj, nextType);
        var ctExpr = Expression.Convert(ctParamObj, typeof(CancellationToken));

        MethodCallExpression callExpr = Expression.Call(behaviourExpr, behaviourMethod, requestExpr, nextExpr, ctExpr);

        var lambdaExpr = Expression.Lambda<Func<object, object, object, object, object>>(
            callExpr,
            behaviourParamObj,
            inputParamObj,
            requestNextObj,
            ctParamObj);

        var handlerDelegate = lambdaExpr.Compile();

        return new MediatorMap(behaviourType, handlerDelegate);
    }
}
