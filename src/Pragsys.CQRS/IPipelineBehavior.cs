namespace Pragsys.CQRS;

public interface IPipelineBehavior { }

public interface IPipelineBehavior<in TInput, TOutput> : IPipelineBehavior
{
    Task<TOutput> Handle(TInput input, RequestHandlerDelegate<TOutput> next, CancellationToken cancellationToken = default);
}

public interface IPipelineBehavior<in TInput> : IPipelineBehavior
{
    Task Handle(TInput input, RequestHandlerDelegate next, CancellationToken cancellationToken = default);
}
