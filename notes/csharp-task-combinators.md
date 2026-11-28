# Task Combinators in C#

Task combinators turn many tasks into one: wait for all of them, wait for the first, add a timeout, or process results as they arrive. They are the building blocks of concurrent async code in C#.

## Task.WhenAll

`WhenAll` returns a task that completes when every supplied task completes. It is the standard way to run independent operations concurrently:

```csharp
Task<string> users = GetUsersAsync();
Task<string> orders = GetOrdersAsync();
Task<string> products = GetProductsAsync();

string[] results = await Task.WhenAll(users, orders, products);
// results[0] is users, results[1] is orders, results[2] is products
```

The results arrive in the order the tasks were passed, not the order they finished. `WhenAll` starts nothing itself: it only observes tasks that are already running. Calling `GetUsersAsync()` starts the work; `WhenAll` just waits for it.

For tasks with different result types, await them individually after `WhenAll`:

```csharp
Task<int> countTask = CountAsync();
Task<string> nameTask = GetNameAsync();
await Task.WhenAll(countTask, nameTask);
int count = await countTask;   // already complete, returns immediately
string name = await nameTask;
```

### Exception Aggregation

If one task faults, `WhenAll` faults with the first exception when awaited, but the returned task's `Exception` property holds an `AggregateException` with **all** of them:

```csharp
Task t1 = Task.Run(() => throw new InvalidOperationException("first"));
Task t2 = Task.Run(() => throw new ArgumentException("second"));

try
{
    await Task.WhenAll(t1, t2);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message); // first
}

Task all = Task.WhenAll(t1, t2);
Console.WriteLine(all.Exception!.InnerExceptions.Count); // 2
```

Awaiting `WhenAll` throws only the first exception, which is usually what you want. Inspect `all.Exception.InnerExceptions` when you need the full list, for example to log every failure.

## Task.WhenAny

`WhenAny` completes as soon as any one task completes, and returns that task so you can inspect its result or status:

```csharp
Task<string> fast = GetFromCacheAsync();
Task<string> slow = GetFromDatabaseAsync();

Task<string> first = await Task.WhenAny(fast, slow);
string result = await first; // the winner's result
```

The losers keep running. `WhenAny` does not cancel them; if you need that, pass a `CancellationToken` into the operations themselves. Common uses: racing a cache against a database, or implementing a "first response wins" fallback.

```csharp
// Timeout pattern with WhenAny and a delay task
Task<string> work = FetchAsync();
Task<string> winner = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(5)));
if (winner != work)
    throw new TimeoutException("Fetch took too long.");
string result = await work;
```

## Task.WaitAsync (Timeout and Cancellation)

`WaitAsync` (.NET 6+) is the cleaner timeout primitive: it wraps a task with a timeout or a cancellation token, and throws `TimeoutException` or `OperationCanceledException` when the limit hits:

```csharp
try
{
    string result = await FetchAsync().WaitAsync(TimeSpan.FromSeconds(5));
}
catch (TimeoutException)
{
    Console.WriteLine("Fetch timed out after 5 seconds.");
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
string result2 = await FetchAsync(cts.Token).WaitAsync(cts.Token);
```

Important: `WaitAsync` does not cancel the underlying operation. The original task keeps running in the background unless it observes the same token. A timeout on a fire-and-forget operation therefore leaks work; always pass the token through to the operation when you can.

## Task.WhenEach (.NET 9)

`WhenEach` (.NET 9+) returns an async stream of tasks in completion order, so you can process each result as soon as it arrives instead of waiting for all of them:

```csharp
Task<int>[] downloads = { DownloadAsync(1), DownloadAsync(2), DownloadAsync(3) };

await foreach (Task<int> finished in Task.WhenEach(downloads))
{
    int bytes = await finished; // already complete
    Console.WriteLine($"Finished a download: {bytes} bytes");
}
```

Contrast with `WhenAll`, which gives you all results at once, in input order. `WhenEach` is the right choice for progress reporting, streaming results to a client, or starting dependent work on the first available result.

## Practical Patterns

**Fan-out, then aggregate:**

```csharp
string[] urls = { "https://a.example", "https://b.example", "https://c.example" };
using var http = new HttpClient();
string[] pages = await Task.WhenAll(urls.Select(u => http.GetStringAsync(u)));
```

**Bounded concurrency:** `WhenAll` on 1,000 tasks starts 1,000 operations at once. For I/O-bound work that is often fine, but for rate-limited APIs use `Parallel.ForEachAsync` or a `SemaphoreSlim` to cap concurrency.

**Do not mix blocking waits:** never call `.Result` or `.Wait()` on a task in async code; it can deadlock. Always `await` the combinator.

## Key Takeaways

- `Task.WhenAll` waits for every task and returns results in input order; awaiting it throws the first exception, while its `Exception` property holds all of them.
- `Task.WhenAny` returns the first task to finish; the others keep running unless cancelled.
- `Task.WaitAsync` adds a timeout or cancellation to any task, throwing `TimeoutException` or `OperationCanceledException`.
- `Task.WhenEach` (.NET 9) streams completed tasks in finish order for processing results as they arrive.
- Combinators observe already-running tasks; they do not start work, and they do not cancel losers by themselves.
