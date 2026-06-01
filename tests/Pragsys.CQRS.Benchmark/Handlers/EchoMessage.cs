namespace Pragsys.CQRS.Benchmark.Handlers;

public class EchoMessage : IRequest<int>
{
    public EchoMessage(int count)
        => Count = count;

    public int Count { get; set; }
}

public class EchoMessageHandler : IRequestHandler<EchoMessage, int>
{
    public Task<int> Handle(EchoMessage request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Count);
    }
}

public class EchoPipelineMessage : IRequest<int>
{
    public EchoPipelineMessage(int count)
        => Count = count;

    public int Count { get; set; }
}

public class EchoPipelineMessageHandler : IRequestHandler<EchoPipelineMessage, int>
{
    public Task<int> Handle(EchoPipelineMessage request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Count);
    }
}

public class EchoPipelineBehaviourHandler : IPipelineBehavior<EchoPipelineMessage, int>
{
    public Task<int> Handle(EchoPipelineMessage input, RequestHandlerDelegate<int> next, CancellationToken cancellationToken = default)
    {
        return next();
    }
}
