using FluentAssertions.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace SimpleCQRS.Tests;

public class MediatorTests
{
    private IServiceProvider BuildContainer(params IPipelineBehavior[] pipelines)
    {
        var services = new ServiceCollection();
        services.InitializeServices(pipelines);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_WithResult_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = BuildContainer();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mediator.Send(new LoggingQuery(1), cts.Token));
    }

    [Fact]
    public async Task Send_WithResult_CachesReflection()
    {
        var provider = BuildContainer();
        var mediator = provider.GetRequiredService<IMediator>();

        var t1 = await mediator.Send(new LoggingQuery(1), TestContext.Current.CancellationToken);
        var t2 = await mediator.Send(new LoggingQuery(2), TestContext.Current.CancellationToken);
        var handler = (LoggingQueryHandler)provider.GetRequiredService<IRequestHandler<LoggingQuery, int>>();

        Assert.Equal(2, t1);
        Assert.Equal(4, t2);

        Assert.Equal(2, handler.InvocationCount);
    }

    [Fact]
    public async Task Send_WithoutResult_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = BuildContainer();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mediator.Send(new VoidLoggingCommand(), cts.Token));
    }

    [Fact]
    public async Task Send_WithMultipleBehaviors_ChainsInReverseOrder()
    {
        var logs = new List<string>();
        var behaviorA = new LoggingBehavior("A", logs);
        var behaviorB = new LoggingBehavior("B", logs);

        var provider = BuildContainer(behaviorA, behaviorB);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new LoggingQuery(1));
        var handler = (LoggingQueryHandler)provider.GetRequiredService<IRequestHandler<LoggingQuery, int>>();

        // Behaviors chain in reverse: B wraps A wraps handler
        Assert.Equal([
            "B-before",
            "A-before",
            "A-after",
            "B-after"
        ], logs);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Fact]
    public async Task SendVoid_WithBehavior_ChainsBehaviorAroundHandler()
    {
        var logs = new List<string>();
        var behaviorA = new VoidLoggingBehavior("A", logs);
        var behaviorB = new VoidLoggingBehavior("B", logs);

        var provider = BuildContainer(behaviorA, behaviorB);
        var mediator = provider.GetRequiredService<IMediator>();

        var handler = (VoidLoggingCommandHandler)provider.GetRequiredService<IRequestHandler<VoidLoggingCommand>>();
        await mediator.Send(new VoidLoggingCommand());

        Assert.Equal(1, handler.InvocationCount);

        // Behaviors chain in reverse: B wraps A wraps handler
        Assert.Equal([
            "B-before",
            "A-before",
            "A-after",
            "B-after"
        ], logs);
    }

    public record UnknownQuery : IRequest<string>;

    [Fact]
    public async Task Send_WithMissingHandler_ThrowsInvalidOperationException()
    {
        var provider = BuildContainer();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            mediator.Send(new UnknownQuery()));
    }
}
