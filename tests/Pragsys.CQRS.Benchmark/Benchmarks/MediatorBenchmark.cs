using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Pragsys.CQRS.Benchmark.Handlers;

namespace Pragsys.CQRS.Benchmark.Benchmarks;

public class MediatorBenchmark
{
    private readonly IServiceProvider _provider;

    public MediatorBenchmark()
    {
        var services = new ServiceCollection();
        services.AddCqrs(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(typeof(MediatorBenchmark).Assembly);
        });

        services.AddTransient<IPipelineBehavior<VoidPipelineMessage>, VoidPipelineBehaviourHandler>();
        services.AddTransient<IPipelineBehavior<EchoPipelineMessage, int>, EchoPipelineBehaviourHandler>();

        _provider = services.BuildServiceProvider();
    }

    [Benchmark]
    public void RequestResponseRawBenchmark()
    {
        var mediator = _provider.GetRequiredService<IMediator>();
        mediator.Send(new EchoMessage(1));
    }

    [Benchmark]
    public void RequestResponsePipelineBenchmark()
    {
        var mediator = _provider.GetRequiredService<IMediator>();
        mediator.Send(new EchoPipelineMessage(1));
    }

    [Benchmark]
    public void RequestVoidRawBenchmark()
    {
        var mediator = _provider.GetRequiredService<IMediator>();
        mediator.Send(new VoidMessage(1));
    }

    [Benchmark]
    public void RequestVoidPipelineBenchmark()
    {
        var mediator = _provider.GetRequiredService<IMediator>();
        mediator.Send(new VoidPipelineMessage(1));
    }
}
