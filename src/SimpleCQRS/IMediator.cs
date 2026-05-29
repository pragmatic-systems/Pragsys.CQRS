namespace SimpleCQRS;

public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    Task Send<TRequest>(TRequest query, CancellationToken cancellationToken = default)
        where TRequest : IRequest;
}
