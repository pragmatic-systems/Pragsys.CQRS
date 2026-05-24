namespace SimpleCQRS;

public interface IPipelineBehavior { }

public interface IPipelineBehavior<in TInput, TOutput> : IPipelineBehavior
{
    Task<TOutput> Handle(TInput input, Func<Task<TOutput>> next, CancellationToken cancellationToken = default);
}

public interface IPipelineBehavior<in TInput> : IPipelineBehavior
{
    Task Handle(TInput input, Func<Task> next, CancellationToken cancellationToken = default);
}
