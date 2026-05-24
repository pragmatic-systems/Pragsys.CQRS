using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SimpleCQRS.Tests;

public class MediatorTests
{
    private IServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(MediatorTests).Assembly);
        });

        return services.BuildServiceProvider();
    }

    public record GetUserQuery(int Id) : IRequest<string>;

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, string>
    {
        public Task<string> Handle(GetUserQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult($"User-{query.Id}");
    }


    public record SendCommand : IRequest;

    public class SendCommandHandler : IRequestHandler<SendCommand>
    {
        public int CallCount { get; private set; }

        public Task Handle(SendCommand query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }


    [Fact]
    public async Task Send_WithResult_ReturnsHandlerResult()
    {
        var provider = BuildContainer();
        var mediator = provider.GetService<IMediator>();

        var result = await mediator.Send(new GetUserQuery(42));

        Assert.Equal("User-42", result);
    }

    [Fact]
    public async Task Send_WithResult_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = BuildContainer();
        var mediator = provider.GetService<IMediator>();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mediator.Send(new GetUserQuery(1), cts.Token));
        //Assert.IsInstanceOf<OperationCanceledException>(ex);
    }

    [Fact]
    public async Task Send_WithResult_CachesReflection()
    {
        var provider = BuildContainer();
        var mediator = provider.GetService<IMediator>();

        var t1 = await mediator.Send(new GetUserQuery(1), TestContext.Current.CancellationToken);
        var t2 = await mediator.Send(new GetUserQuery(2), TestContext.Current.CancellationToken);

        Assert.Equal("User-1", t1);
        Assert.Equal("User-2", t2);
    }

    [Fact]
    public async Task Send_WithoutResult_CallsHandler()
    {
        var provider = BuildContainer();
        var mediator = provider.GetService<IMediator>();

        await mediator.Send(new SendCommand(), TestContext.Current.CancellationToken);

        var handler = provider.GetService<SendCommandHandler>();
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Send_WithoutResult_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<SendCommand>>(new SendCommandHandler());
        services.AddSingleton(typeof(IRequestHandler<,>), typeof(GetUserQueryHandler));
        var provider = services.BuildServiceProvider();
        var mediator = new Mediator(provider);

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mediator.Send(new SendCommand(), cts.Token));
        //Assert.IsInstanceOf<OperationCanceledException>(ex);
    }

    public record LoggingQuery(int Value) : IRequest<int>;

    public class LoggingQueryHandler : IRequestHandler<LoggingQuery, int>
    {
        public int InvocationCount { get; private set; }

        public Task<int> Handle(LoggingQuery query, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(query.Value * 2);
        }
    }

    public class LoggingBehavior : IPipelineBehavior<LoggingQuery, int>
    {
        public List<string> Log { get; } = new();

        public async Task<int> Handle(LoggingQuery input, Func<Task<int>> next, CancellationToken cancellationToken = default)
        {
            Log.Add("before");
            var result = await next();
            Log.Add("after");
            return result;
        }
    }

    [Fact]
    public async Task Send_WithBehavior_ChainsBehaviorAroundHandler()
    {
        var handler = new LoggingQueryHandler();
        var behavior = new LoggingBehavior();

        var services = new ServiceCollection();
        services.AddSingleton(handler);
        services.AddSingleton(behavior);
        services.AddSingleton(typeof(IRequestHandler<,>), typeof(GetUserQueryHandler));
        services.AddSingleton(typeof(IRequestHandler<>), typeof(SendCommandHandler));
        var provider = services.BuildServiceProvider();
        var mediator = new Mediator(provider);

        var result = await mediator.Send(new LoggingQuery(21), TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
        Assert.Equal(["before", "after"], behavior.Log);
        Assert.Equal(1, handler.InvocationCount);
    }

    public class ReverseOrderBehavior : IPipelineBehavior<LoggingQuery, int>
    {
        public string Name { get; }
        public List<string> Log { get; } = new ();

        public ReverseOrderBehavior(string name) => Name = name;

        public async Task<int> Handle(LoggingQuery input, Func<Task<int>> next, CancellationToken cancellationToken = default)
        {
            Log.Add($"{Name}-before");
            var result = await next();
            Log.Add($"{Name}-after");
            return result;
        }
    }

    [Fact]
    public async Task Send_WithMultipleBehaviors_ChainsInReverseOrder()
    {
        var handler = new LoggingQueryHandler();
        var behaviorA = new ReverseOrderBehavior("A");
        var behaviorB = new ReverseOrderBehavior("B");

        var services = new ServiceCollection();
        services.AddSingleton(handler);
        services.AddSingleton(behaviorA);
        services.AddSingleton(behaviorB);
        services.AddSingleton(typeof(IRequestHandler<,>), typeof(GetUserQueryHandler));
        services.AddSingleton(typeof(IRequestHandler<>), typeof(SendCommandHandler));
        var provider = services.BuildServiceProvider();
        var mediator = new Mediator(provider);

        await mediator.Send(new LoggingQuery(1));

        // Behaviors chain in reverse: B wraps A wraps handler
        Assert.Equal([
            "B-before",
            "A-before",
            "A-after",
            "B-after"
        ], behaviorB.Log);
    }

    public record VoidLoggingCommand : IRequest;

    public class VoidLoggingCommandHandler : IRequestHandler<VoidLoggingCommand>
    {
        public int InvocationCount { get; private set; }

        public Task Handle(VoidLoggingCommand query, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.CompletedTask;
        }
    }

    public class VoidLoggingBehavior : IPipelineBehavior<VoidLoggingCommand>
    {
        public List<string> Log { get; } = new();

        public async Task Handle(VoidLoggingCommand input, Func<Task> next, CancellationToken cancellationToken = default)
        {
            Log.Add("before");
            await next();
            Log.Add("after");
        }
    }

    [Fact]
    public async Task SendVoid_WithBehavior_ChainsBehaviorAroundHandler()
    {
        var handler = new VoidLoggingCommandHandler();
        var behavior = new VoidLoggingBehavior();

        var services = new ServiceCollection();
        services.AddSingleton(handler);
        services.AddSingleton(behavior);
        services.AddSingleton(typeof(IRequestHandler<,>), typeof(GetUserQueryHandler));
        services.AddSingleton(typeof(IRequestHandler<LoggingQuery, int>), typeof(LoggingQueryHandler));
        var provider = services.BuildServiceProvider();
        var mediator = new Mediator(provider);

        await mediator.Send(new VoidLoggingCommand());

        Assert.Equal(1, handler.InvocationCount);
        Assert.Equal(["before", "after"], behavior.Log);
    }

    public record UnknownQuery : IRequest<string>;

    [Fact]
    public async Task Send_WithMissingHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IRequestHandler<>), typeof(SendCommandHandler));
        services.AddSingleton(typeof(IRequestHandler<,>), typeof(GetUserQueryHandler));
        var provider = services.BuildServiceProvider();
        var mediator = new Mediator(provider);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            mediator.Send(new UnknownQuery()));
    }
}
