namespace SimpleCQRS.Tests;

public record UnknownQuery : IRequest<string>;

public record LoggingQuery(int Value)
    : IRequest<int>;

public class LoggingQueryHandler : IRequestHandler<LoggingQuery, int>
{
    public int InvocationCount { get; private set; }

    public Task<int> Handle(LoggingQuery query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        InvocationCount++;
        return Task.FromResult(query.Value * 2);
    }
}

public class LoggingBehavior : IPipelineBehavior<LoggingQuery, int>
{
    public string Name { get; }

    public List<string> Log { get; } = new();

    public LoggingBehavior(string name = "A", List<string>? logs = null)
    {
        Name = name;
        Log = logs ?? new List<string>();
    }

    public async Task<int> Handle(LoggingQuery input, Func<Task<int>> next, CancellationToken cancellationToken = default)
    {
        Log.Add($"{Name}-before");
        var result = await next();
        Log.Add($"{Name}-after");
        return result;
    }
}

public record VoidLoggingCommand : IRequest;

public class VoidLoggingCommandHandler : IRequestHandler<VoidLoggingCommand>
{
    public int InvocationCount { get; private set; }

    public Task Handle(VoidLoggingCommand query, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        InvocationCount++;
        return Task.CompletedTask;
    }
}

public class VoidLoggingBehavior : IPipelineBehavior<VoidLoggingCommand>
{
    public VoidLoggingBehavior(string name, List<string> logs)
    {
        Name = name;
        Log = logs;
    }

    public string Name { get; }

    public List<string> Log { get; }

    public async Task Handle(VoidLoggingCommand input, Func<Task> next, CancellationToken cancellationToken = default)
    {
        Log.Add($"{Name}-before");
        await next();
        Log.Add($"{Name}-after");
    }
}
