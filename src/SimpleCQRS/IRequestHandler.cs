namespace SimpleCQRS;

public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    Task Handle(TRequest query, CancellationToken cancellationToken = default);
}

public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest query, CancellationToken cancellationToken = default);
}
