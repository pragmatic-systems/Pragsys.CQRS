namespace Pragsys.CQRS;

public interface IBaseRequestHandler { }

public interface IRequestHandler<in TRequest> : IBaseRequestHandler
    where TRequest : IRequest
{
    Task Handle(TRequest query, CancellationToken cancellationToken = default);
}

public interface IRequestHandler<in TRequest, TResult> : IBaseRequestHandler
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest query, CancellationToken cancellationToken = default);
}
