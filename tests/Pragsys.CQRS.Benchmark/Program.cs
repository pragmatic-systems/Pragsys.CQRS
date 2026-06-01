using BenchmarkDotNet.Running;
using Pragsys.CQRS.Benchmark.Benchmarks;

namespace Pragsys.CQRS.Benchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly, args: args);
    }
}
