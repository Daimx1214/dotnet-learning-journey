# C# Performance Profiling

Guessing where time goes is how performance work fails. Profiling measures it: which methods consume CPU, how much memory is allocated, and where threads wait. .NET ships the tools; the skill is reading what they say.

## dotnet-trace and dotnet-counters

`dotnet-trace` records an EventPipe trace of a running process (CPU samples, GC events, exceptions), which you open in PerfView or Visual Studio. `dotnet-counters` shows live metrics — CPU, working set, GC heap size, exception rate, thread-pool queue length — while the app runs.

```bash
dotnet tool install -g dotnet-trace dotnet-counters dotnet-dump

dotnet-counters monitor --process-id 1234 \
    System.Runtime                      # cpu, heap, GC, exceptions, thread pool

dotnet-trace collect --process-id 1234 --duration 00:00:30
```

Counters are the first stop for "is it CPU, memory, or waiting?": high CPU with low GC time points at compute; a growing Gen 2 heap points at a leak or pinned objects; a long thread-pool queue points at blocking calls starving the pool.

## dotnet-dump and PerfView

When a process is stuck or leaking, `dotnet-dump collect` captures a memory dump without stopping the process for long, and `dotnet-dump analyze` (or SOS commands in `lldb`) inspects it: heap stats, which types dominate memory, and what roots keep them alive.

```bash
dotnet-dump collect --process-id 1234
dotnet-dump analyze core_20261120_1234
> dumpheap -stat          # which types use the most memory
> gcroot <address>        # why is this object still alive?
```

PerfView is the heavyweight Windows tool for `.nettrace` and ETW data: CPU stacks, allocation profiles, and wall-clock time (where threads block). Its "By Name" and "CallTree" views turn thousands of samples into "method X costs 40% of CPU, called from Y". On Linux, `dotnet-trace` plus PerfView (or Speedscope for flame graphs) covers the same ground.

## BenchmarkDotNet vs Profiling

BenchmarkDotNet measures a small piece of code precisely — mean time, allocations, GC counts — with warmup, statistics, and outlier handling. Profiling measures a whole running app under real load. They answer different questions.

```csharp
[MemoryDiagnoser]
public class ParserBenchmarks
{
    private const string Input = "2026-11-20T19:47:00";

    [Benchmark(Baseline = true)]
    public DateTime Parse() => DateTime.Parse(Input);

    [Benchmark]
    public DateTime ParseExact() =>
        DateTime.ParseExact(Input, "yyyy-MM-ddTHH:mm:ss", null);
}
```

Use BenchmarkDotNet to choose between two implementations of a hot method. Use profiling to find which method is hot in the first place — optimising a method that costs 0.1% of runtime is wasted effort, however elegant the optimisation.

## Reading a CPU Sample

A CPU profile is a stack of samples. Read it top-down: the methods at the top of the call tree with the largest *inclusive* time are where the time goes. A method with high inclusive time but low *exclusive* time is just a caller — the cost is in its children. Watch for these signatures: a large share under `Monitor.Enter` or lock contention (threads waiting, not working), heavy `string` allocations under a hot loop (fix with `Span<char>` or `StringBuilder`), and JSON serialisation dominating a request (consider source-generated serializers).

## Key Takeaways

- Profile before optimising: `dotnet-counters` first (CPU vs memory vs waiting), then `dotnet-trace` for detail.
- `dotnet-dump` captures a process for offline heap analysis; `gcroot` shows why an object is still alive.
- PerfView turns traces into call trees — follow inclusive time to find the real hotspot.
- BenchmarkDotNet measures micro-benchmarks precisely; profiling finds the hotspot in a real app — use both, for different questions.
- High inclusive time with low exclusive time means the cost is in the children, not the method itself.
- Common finds: lock contention, string allocations in hot loops, and serialisation dominating request time.
