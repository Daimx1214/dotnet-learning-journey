# Parallel Programming in C# (C#)

Modern machines have many cores, and .NET makes it easy to use them — but parallelism is a tool with sharp edges. The three main APIs are `Parallel.For` / `Parallel.ForEach` for CPU-bound loops, PLINQ for parallel LINQ queries, and `Task` / `async` for concurrency. Choosing the wrong one, or parallelizing the wrong workload, can make code slower and buggier than the sequential version.

## Parallel.For and Parallel.ForEach

`Parallel.ForEach` splits a collection into partitions and processes them on thread-pool threads. It is the right choice for independent CPU-bound work over a large collection, such as image processing or number crunching. The loop returns only after all partitions finish, and `ParallelLoopResult.IsCompleted` tells you whether it ran to completion or stopped early via `Break()` / `Stop()`.

```csharp
using System.Threading.Tasks;

// Square a million numbers in parallel.
int[] numbers = Enumerable.Range(0, 1_000_000).ToArray();
int[] squares = new int[numbers.Length];

Parallel.ForEach(numbers, (n, state, index) =>
{
    squares[index] = n * n;
});

// With a degree-of-parallelism cap and a shared, thread-safe accumulator.
long total = 0;
Parallel.For(0, numbers.Length,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    i => Interlocked.Add(ref total, numbers[i]));

Console.WriteLine($"Sum: {total}");
```

Never write to a shared variable with a plain `+=` inside a parallel loop — that is a race condition, and the result will silently be wrong on some runs. Use `Interlocked` for counters, a `ConcurrentBag<T>` / `ConcurrentDictionary<K, V>` for shared collections, or the `localInit` / `localFinally` overloads of `Parallel.ForEach` to accumulate per-partition and merge at the end.

## PLINQ

PLINQ (`AsParallel()`) parallelizes a LINQ query. It is ideal when the work is already expressed as a query and each element is independent. `AsOrdered()` preserves the source order (at a cost); without it, results come back in whatever order partitions finished.

```csharp
// Find all primes below 1,000,000, in parallel.
List<int> primes = Enumerable.Range(2, 1_000_000)
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .Where(IsPrime)
    .ToList();

bool IsPrime(int n)
{
    if (n < 2) return false;
    for (int i = 2; i * i <= n; i++)
        if (n % i == 0) return false;
    return true;
}
```

PLINQ chooses a partitioning strategy and a merge strategy automatically, and for small collections the overhead of partitioning outweighs the benefit — it may even run slower than plain LINQ. `WithDegreeOfParallelism` caps the threads; `WithExecutionMode(ParallelExecutionMode.ForceParallelism)` overrides PLINQ's decision to fall back to sequential execution, which you should only do after measuring.

## Task.WhenAll vs Parallel

`Task.WhenAll` and `Parallel.ForEach` solve different problems. `Task.WhenAll` awaits many asynchronous operations that are mostly waiting — HTTP calls, database queries, file reads. The threads are free while the work waits, so thousands of concurrent operations are cheap. `Parallel.ForEach` occupies a thread per partition for CPU-bound work; using it for I/O-bound work wastes threads that just sit blocked.

```csharp
// I/O-bound: await many HTTP calls concurrently — use Task.WhenAll.
string[] urls = { "https://example.com", "https://dotnet.microsoft.com" };
using var http = new HttpClient();
string[] pages = await Task.WhenAll(urls.Select(url => http.GetStringAsync(url)));

// CPU-bound: use Parallel.ForEach, never Task.Run in a loop for this.
// (Wrapping CPU work in Task.Run just to await it adds scheduling overhead.)
```

A common mistake is `await Task.WhenAll(items.Select(i => Task.Run(() => CpuWork(i))))` — that is a poor man's `Parallel.ForEach` with extra allocations and no partitioning control.

## Thread-Safety Pitfalls

- **Shared mutable state.** Any variable written by more than one thread without synchronization is a race. `List<T>` and `Dictionary<K, V>` are not thread-safe for concurrent writes — use the `Concurrent*` collections or partition the work so each thread writes to its own slice.
- **Random is not thread-safe.** A shared `Random` instance called from parallel code can return 0 repeatedly or throw. Use `Random.Shared` (.NET 6+), which is thread-safe, or one `Random` per thread.
- **Captured loop variables.** In `Parallel.ForEach`, the loop variable is a fresh parameter per iteration, so this pitfall is mostly gone — but in a hand-rolled `Task.Run` loop over a `for` index, capture a local copy or the index will have moved on by the time the task runs.
- **Exceptions.** An exception in one partition does not stop the others immediately; `Parallel.ForEach` aggregates them into an `AggregateException`. Catch it and inspect `InnerExceptions` — a bare `catch (Exception)` will not see the individual failures.

## When NOT to Parallelize

- The workload is small: partitioning and thread scheduling cost more than the work itself.
- The work is I/O-bound: use `async`/`await` and `Task.WhenAll` instead.
- The iterations depend on each other (each step needs the previous result) — that is inherently sequential.
- The order of side effects matters (writing lines to a file in order, updating a UI).
- The code already saturates the machine: parallelizing a loop inside an already-parallel web request just adds contention.

## Key Takeaways

- `Parallel.For` / `Parallel.ForEach` for CPU-bound loops; PLINQ for parallel LINQ queries; `Task.WhenAll` for concurrent async I/O.
- Shared mutable state needs `Interlocked`, concurrent collections, or per-partition accumulators — a plain `+=` is a race.
- Exceptions from parallel loops arrive wrapped in `AggregateException`.
- Measure before parallelizing: small, I/O-bound, or order-dependent work is slower in parallel.
