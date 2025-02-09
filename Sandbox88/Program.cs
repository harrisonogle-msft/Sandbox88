using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Management.Services.Common;
using System.Collections.Immutable;
using System.Diagnostics;

// The `BaseObject` type is an abstract class, so we need to
// choose which implementation to benchmark. We could iteratively
// benchmark each of them in turn, but I'm not going to.
//
// Of the possible `BaseObject`s we can benchmark, let's choose
// the one that seems most expensive to compare (`GetHashCode`).
//
// Every time two `TwoGuidTwoStringKeyBaseObject`s are compared,
// both objects need to compute the combined hash of 2 guids and
// 2 strings, which seems the most expensive of any implementation.
//
// Note: In general, computing the hash of a string is more expensive
// than computing the hash of a guid, so I chose the one with the
// most strings in the hash.
//
// Note (2): It probably doesn't matter at all which one is chosen.
// The benchmark will still scale according to the number of items.

var summary = BenchmarkRunner.Run<Benchmarks>();


public readonly struct TestCase<T> where T : BaseObject
{
    public TestCase(ImmutableArray<T> dataset)
    {
        if (dataset.Length == 0)
        {
            throw new InvalidOperationException("Benchmark dataset is empty.");
        }

        Dataset = dataset;
    }

    public readonly ImmutableArray<T> Dataset;

    public void Run<TServerContext>(int iterations = 1)
        where TServerContext : struct, IBenchmarkTarget
    {
        TServerContext target = new TServerContext();
        for (int i = 0; i < iterations; ++i)
        {
            Run(ref target, in Dataset);
        }
    }

    private static void Run<TServerContext, TDataset>(ref TServerContext target, in TDataset dataset)
        where TServerContext : struct, IBenchmarkTarget
        where TDataset : struct, IReadOnlyList<T>
    {
        target.BaseObjects = dataset;
    }
}

[MemoryDiagnoser]
public class Benchmarks
{
    private static readonly ImmutableArray<TestCase<TwoGuidTwoStringKeyObject>> TestCases;

    static Benchmarks()
    {
        const int M = 1000;
        const int N = 10_000;

        var testCases = new List<TestCase<TwoGuidTwoStringKeyObject>>(capacity: M);

        for (int i = 0; i < M; ++i)
        {
            ImmutableArray<TwoGuidTwoStringKeyObject> dataset = CreateMany(N);
            testCases.Add(new TestCase<TwoGuidTwoStringKeyObject>(dataset));
        }

        TestCases = testCases.ToImmutableArray();

        static ImmutableArray<TwoGuidTwoStringKeyObject> CreateMany(int count)
        {
            return Enumerable.Range(0, count).Select(static i => CreateNew()).ToImmutableArray();
            static TwoGuidTwoStringKeyObject CreateNew() =>
                new TwoGuidTwoStringKeyObject(key1: Guid.NewGuid(), key2: Guid.NewGuid(), key3: $"key3_{Guid.NewGuid()}", key4: $"key4_{Guid.NewGuid()}");
        }
    }

    [Benchmark(Baseline = true)]
    public long Baseline() => Benchmark<Baseline>();

    [Benchmark]
    public long Sorted() => Benchmark<Sorted>();

    [Benchmark]
    public long ZeroAlloc() => Benchmark<ZeroAlloc>();

    [Benchmark]
    public long LinqDistinct() => Benchmark<LinqDistinct>();

    [Benchmark]
    public long PooledLinqDistinct() => Benchmark<PooledLinqDistinct>();

    public long Benchmark<TBenchmark>()
        where TBenchmark : struct, IBenchmarkTarget
    {
        ref readonly ImmutableArray<TestCase<TwoGuidTwoStringKeyObject>> testCases = ref TestCases;
        ReadOnlySpan<TestCase<TwoGuidTwoStringKeyObject>> span = testCases.AsSpan();

        for (int i = 0; i < span.Length; ++i)
        {
            ref readonly TestCase<TwoGuidTwoStringKeyObject> testCase = ref span[i];
            testCase.Run<TBenchmark>(iterations: 2);
        }

        return PreventCompilerOptimization();

        static long PreventCompilerOptimization() => Stopwatch.GetTimestamp();
    }
}
