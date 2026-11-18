# Parallel LINQ (PLINQ)

PLINQ — Parallel LINQ — is the parallel version of LINQ to Objects. Add `.AsParallel()` to a query and the work is split across multiple threads, using the thread pool to process elements concurrently. For CPU-bound work over large collections, it can cut execution time dramatically; for small collections or I/O-bound work, it can make things slower.

It matters because "just add AsParallel" is tempting and often wrong. Knowing when parallelism helps, what it costs, and how ordering and side effects behave is the difference between a real speedup and a slower, nondeterministic bug.

## The Basics

```csharp
var numbers = Enumerable.Range(1, 1_000_000);

// Sequential: one thread, in order.
var sequential = numbers
    .Where(n => IsPrime(n))
    .ToList();

// Parallel: split across cores.
var parallel = numbers
    .AsParallel()
    .Where(n => IsPrime(n))
    .ToList();
```

`AsParallel()` returns a `ParallelQuery<T>`. The operators after it (`Where`, `Select`, `OrderBy`, `Sum`, etc.) run in parallel; the operators before it run sequentially. The split point matters:

```csharp
// Only the Select runs in parallel; the Where already ran sequentially.
var result = numbers
    .Where(n => n > 1000)
    .AsParallel()
    .Select(n => ExpensiveTransform(n))
    .ToList();
```

## When Parallelism Helps — and When It Hurts

Parallelism helps when:

- The collection is **large** (thousands of items or more).
- The per-item work is **CPU-bound** and non-trivial (image processing, prime checks, hashing, parsing).
- The work is **independent** — each item can be processed without knowing about the others.

Parallelism hurts when:

- The collection is **small** — partitioning and merging cost more than the work itself.
- The work is **I/O-bound** — threads just wait on the network or disk. Use `async` / `Task.WhenAll` instead.
- The per-item work is **trivial** — adding two numbers is faster sequentially than paying the parallel overhead.

```csharp
// Worth parallelising: expensive per-item CPU work over a large set.
var hashes = files
    .AsParallel()
    .Select(f => (f, Hash: ComputeSha256(f)))
    .ToList();

// NOT worth it: trivial work, small collection.
var doubled = new[] { 1, 2, 3, 4, 5 }
    .AsParallel()          // overhead exceeds the work
    .Select(n => n * 2)
    .ToList();

// NOT the tool for I/O: use async instead.
var pages = await Task.WhenAll(urls.Select(u => _http.GetStringAsync(u)));
```

A good rule of thumb: measure with `Stopwatch` or BenchmarkDotNet before and after. PLINQ's overhead is real — on a 100-item collection with cheap work, sequential is usually faster.

## Controlling the Degree of Parallelism

By default PLINQ uses as many threads as there are processor cores. Override it with `WithDegreeOfParallelism` — for example, to leave cores free for other work, or to match an external limit (a downstream service that only tolerates 4 concurrent calls).

```csharp
var result = numbers
    .AsParallel()
    .WithDegreeOfParallelism(4) // use at most 4 threads
    .Where(n => IsPrime(n))
    .ToList();
```

Other useful options:

```csharp
var result = numbers
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .WithExecutionMode(ParallelExecutionMode.ForceParallelism) // don't fall back to sequential
    .WithCancellation(cancellationToken)                       // support cancellation
    .Where(n => IsPrime(n))
    .ToList();
```

`WithExecutionMode(ParallelExecutionMode.ForceParallelism)` forces parallel execution even when PLINQ's heuristics think sequential would be faster — useful when you know better than the heuristic, dangerous when you don't.

## Ordering

By default, PLINQ does **not** preserve order — results come back in whatever order threads finish. If order matters, add `AsOrdered()`:

```csharp
// Order is NOT guaranteed — results arrive in completion order.
var unordered = numbers.AsParallel().Where(n => IsPrime(n)).ToList();

// Order IS preserved — but preserving order costs performance.
var ordered = numbers.AsParallel().AsOrdered().Where(n => IsPrime(n)).ToList();
```

`AsOrdered()` adds buffering overhead, so use it only when the consumer genuinely needs the original sequence order. If you only need *a* deterministic order (any order, as long as it is stable), sort the results afterwards instead.

```csharp
// Cheaper than AsOrdered when any stable order will do:
var sorted = numbers.AsParallel().Where(n => IsPrime(n)).ToList();
sorted.Sort();
```

## Thread Safety of Side Effects

This is where PLINQ bites. The query body runs on multiple threads at once, so anything shared must be thread-safe.

```csharp
// BUG: List<T> is not thread-safe — concurrent Add calls corrupt it.
var results = new List<int>();
numbers.AsParallel().ForAll(n =>
{
    if (IsPrime(n)) results.Add(n); // race condition!
});

// Correct: let PLINQ build the result, or use a thread-safe collection.
var primes = numbers.AsParallel().Where(n => IsPrime(n)).ToList();

// Also correct: a concurrent collection, when you truly need side effects.
var bag = new ConcurrentBag<int>();
numbers.AsParallel().ForAll(n =>
{
    if (IsPrime(n)) bag.Add(n);
});
```

Prefer the functional style — `Where`, `Select`, `Aggregate` — and let PLINQ own the result. `ForAll` with side effects is a code smell: if you find yourself writing it, check whether the query operators already do what you need.

The same applies to shared counters, random number generators, and database contexts: none of them are safe to share across PLINQ threads without synchronization, and synchronizing usually destroys the speedup. Give each partition its own state with thread-local values instead:

```csharp
// Each thread gets its own Random — Random is not thread-safe.
var samples = numbers
    .AsParallel()
    .Select(n => ThreadLocalRandom.Value.Next())
    .ToList();

private static readonly ThreadLocal<Random> ThreadLocalRandom =
    new(() => new Random(Guid.NewGuid().GetHashCode()));
```

## Exceptions and Cancellation

If one partition throws, PLINQ aggregates the exceptions and rethrows them as an `AggregateException` once all partitions finish (or are cancelled). Handle it like any parallel failure:

```csharp
try
{
    var result = numbers
        .AsParallel()
        .WithCancellation(cts.Token)
        .Select(n => RiskyTransform(n))
        .ToList();
}
catch (OperationCanceledException)
{
    Console.WriteLine("Query was cancelled.");
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
        Console.WriteLine($"Failed: {inner.Message}");
}
```

## PLINQ vs Parallel.ForEach

PLINQ is a query: it transforms and filters data and returns results. `Parallel.ForEach` is a loop: it performs actions, usually with side effects. Prefer PLINQ when you want a result collection; prefer `Parallel.ForEach` when the work itself is the point (writing files, sending messages) and there is no result to collect.

## Key Takeaways

- `AsParallel()` splits LINQ to Objects work across thread pool threads; operators before it run sequentially, operators after it run in parallel.
- Parallelism pays off for large collections with CPU-bound, independent per-item work; it hurts for small collections, trivial work, or I/O-bound work (use `async` there).
- `WithDegreeOfParallelism` caps the thread count; `WithCancellation` adds cooperative cancellation support.
- Results are unordered by default — add `AsOrdered()` only when the original order is genuinely required.
- Query bodies run on multiple threads: shared mutable state (lists, counters, `Random`, EF contexts) is unsafe unless you use concurrent collections or thread-local values.
- Prefer the functional style (`Where` / `Select` / `ToList`) and let PLINQ build the result instead of mutating shared state inside `ForAll`.
