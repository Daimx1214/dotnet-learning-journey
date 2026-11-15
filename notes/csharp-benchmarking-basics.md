# C# Benchmarking Basics (C#)

Guessing which implementation is faster is unreliable — the JIT, CPU caches, and garbage collection all confound intuition. BenchmarkDotNet is the standard tool for measuring .NET code: it runs each benchmark many times, warms up the JIT, measures statistically, and reports allocation and GC data alongside timing. A benchmark is a small class with methods marked `[Benchmark]`; the tool handles the rest.

## Setup

Install the `BenchmarkDotNet` NuGet package into a console project and call `BenchmarkRunner.Run<T>()` from `Main`. Benchmarks run in Release configuration — running them in Debug measures unoptimized code and produces meaningless numbers.

```bash
dotnet add package BenchmarkDotNet
```

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public class StringConcatBenchmarks
{
    private readonly string[] _words = Enumerable.Range(0, 100)
        .Select(i => $"word{i}").ToArray();

    [Benchmark(Baseline = true)]
    public string WithPlusOperator()
    {
        string result = string.Empty;
        foreach (string word in _words) result += word;
        return result;
    }

    [Benchmark]
    public string WithStringBuilder()
    {
        var sb = new StringBuilder();
        foreach (string word in _words) sb.Append(word);
        return sb.ToString();
    }
}

// Program.cs
BenchmarkRunner.Run<StringConcatBenchmarks>();
```

## [Params] and the Memory Diagnoser

`[Params]` runs the same benchmark across several input sizes, which shows how performance scales instead of giving one data point. `[MemoryDiagnoser]` adds allocations and GC counts to the report — often the more important number, since an allocation-heavy method pressures the GC long after the benchmark ends.

```csharp
[MemoryDiagnoser]
public class ListSearchBenchmarks
{
    [Params(100, 10_000, 1_000_000)]
    public int Size { get; set; }

    private List<int> _list = null!;
    private HashSet<int> _set = null!;

    [GlobalSetup]
    public void Setup()
    {
        _list = Enumerable.Range(0, Size).ToList();
        _set = _list.ToHashSet();
    }

    [Benchmark(Baseline = true)]
    public bool ListContains() => _list.Contains(Size - 1);

    [Benchmark]
    public bool HashSetContains() => _set.Contains(Size - 1);
}
```

`[GlobalSetup]` runs once per parameter combination, before timing starts, so setup cost never pollutes the measurement. Use `[IterationSetup]` only when each iteration genuinely needs fresh state.

## Reading the Results

A typical result table looks like this:

| Method | Size | Mean | Error | StdDev | Ratio | Allocated |
|--------|------|------|-------|--------|-------|-----------|
| ListContains | 100 | 120.5 ns | 2.1 ns | 1.9 ns | 1.00 | 0 B |
| HashSetContains | 100 | 18.3 ns | 0.4 ns | 0.3 ns | 0.15 | 0 B |
| ListContains | 1000000 | 412.5 us | 8.2 us | 7.7 us | 1.00 | 0 B |
| HashSetContains | 1000000 | 19.1 ns | 0.5 ns | 0.4 ns | 0.00 | 0 B |

`Mean` is the average time per invocation; `Error` is the 99.9% confidence interval half-width. `Ratio` compares each method to the baseline (`[Benchmark(Baseline = true)]`), so 0.15 means "6.7x faster than baseline". `Allocated` shows bytes allocated per invocation — 0 B means no GC pressure. A result is only trustworthy when `Error` is small relative to `Mean` and the run was not disturbed; BenchmarkDotNet warns about outliers and unstable environments.

## Common Mistakes

- **Benchmarking in Debug** — always run Release; Debug disables JIT optimizations.
- **Dead code elimination** — a benchmark whose result is never used gets optimized away; always return the result from the method.
- **Measuring setup** — put expensive setup in `[GlobalSetup]`, not in the benchmark method.
- **Allocating inside the benchmark** — allocations inside the measured method are real, but allocating the test data there measures the wrong thing.
- **Running on a busy machine** — close other apps; background load inflates variance and can flip a close result.
- **Micro-optimizing noise** — a 2 ns difference with a 5 ns error bar is not a finding; look for order-of-magnitude differences or allocation differences that matter at scale.
- **Benchmarking `DateTime.Now` or random data** — nondeterministic inputs make runs incomparable; seed random data in setup.

## Key Takeaways

- BenchmarkDotNet handles warmup, JIT, statistics, and GC reporting; mark methods with `[Benchmark]` and run in Release.
- `[Params]` shows scaling across input sizes; `[MemoryDiagnoser]` reports allocations and GC counts per call.
- `[GlobalSetup]` prepares data once per parameter set so setup cost never enters the measurement.
- Read `Mean` with its `Error`, use `Ratio` against a baseline, and treat `Allocated` as a first-class result.
- Always return the computed value, benchmark in Release on a quiet machine, and distrust differences smaller than the error bar.
