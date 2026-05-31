namespace Pragsys.CQRS.Benchmark.Handlers;

public class VoidMessage : IRequest
{
    public VoidMessage(int count)
        => Count = count;

    public int Count { get; set; }
}

public class VoidMessageHandler : IRequestHandler<VoidMessage>
{
    public Task Handle(VoidMessage request, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class VoidPipelineMessage : IRequest<int>
{
    public VoidPipelineMessage(int count)
        => Count = count;

    public int Count { get; set; }
}

public class VoidPipelineMessageHandler : IRequestHandler<VoidPipelineMessage, int>
{
    public Task<int> Handle(VoidPipelineMessage request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Count);
    }
}

public class VoidPipelineBehaviourHandler : IPipelineBehavior<VoidPipelineMessage, int>
{
    public Task<int> Handle(VoidPipelineMessage input, Func<Task<int>> next, CancellationToken cancellationToken = default)
    {
        return next();
    }
}
