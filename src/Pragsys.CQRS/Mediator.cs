using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Pragsys.CQRS;

public class Mediator(IServiceProvider provider, MediatorCacheMap cacheMap)
    : IMediator
{
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var requestType = request.GetType();
            var responseType = typeof(TResponse);

            var cacheEntry = cacheMap.GetOrAdd(requestType, responseType);

            var handler = provider.GetRequiredService(cacheEntry.Handler.Type);
            var behaviors = provider.GetServices(cacheEntry.Behaviour.Type).Reverse();

            RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            {
                var result = cacheEntry.Handler.Method.DynamicInvoke(handler, request, cancellationToken)
                    ?? throw new CqrsException($"Cannot resolve handler method for Handler: {cacheEntry.Handler.Type.FullName}", cacheEntry.Handler.Type);

                return (Task<TResponse>)result;
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.Behaviour.Method.DynamicInvoke(behavior, request, next, cancellationToken)
                        ?? throw new CqrsException($"Cannot resolve handler method for Behaviour: {cacheEntry.Behaviour.Type.FullName}", cacheEntry.Behaviour.Type);

                    return (Task<TResponse>)result;
                };
            }

            return await handlerDelegate();
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException;
            if (inner is OperationCanceledException oce)
            {
                throw oce;  // preserve exact cancellation semantics
            }

            // Unpack the reflection error here.
            ExceptionDispatchInfo.Capture(inner ?? ex).Throw();
            throw;
        }
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var requestType = request.GetType();

            var cacheEntry = cacheMap.GetOrAdd(requestType);

            var handler = provider.GetRequiredService(cacheEntry.Handler.Type);
            var behaviors = provider.GetServices(cacheEntry.Behaviour.Type).Reverse();

            RequestHandlerDelegate handlerDelegate = () =>
            {
                var result = cacheEntry.Handler.Method.DynamicInvoke(handler, request, cancellationToken)
                    ?? throw new CqrsException($"Cannot resolve handler method for Handler: {cacheEntry.Handler.Type.FullName}", cacheEntry.Handler.Type);

                return (Task)result;
            };

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                {
                    var result = cacheEntry.Behaviour.Method.DynamicInvoke(behavior, request, next, cancellationToken)
                        ?? throw new CqrsException($"Cannot resolve handler method for Behaviour: {cacheEntry.Behaviour.Type.FullName}", cacheEntry.Behaviour.Type);

                    return (Task)result;
                };
            }

            await handlerDelegate();
        }
        catch (TargetInvocationException ex)
        {
            // Unpack the reflection error here.
            var inner = ex.InnerException;
            if (inner is OperationCanceledException oce)
            {
                throw oce;  // preserve exact cancellation semantics
            }

            // Unpack the reflection error here.
            ExceptionDispatchInfo.Capture(inner ?? ex).Throw();
            throw;
        }
    }
}
