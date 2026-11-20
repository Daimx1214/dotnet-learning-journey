# C# Async Best Practices

Async code is easy to write and easy to get subtly wrong. Most async bugs are not crashes — they are deadlocks, thread-pool starvation, or exceptions that vanish silently. These rules cover the mistakes that actually happen in real code.

## async void: Only for Event Handlers

`async void` cannot be awaited, so its caller cannot observe failures. An exception thrown inside an `async void` method crashes the process (it is raised on the synchronisation context), and there is no `Task` to catch it with. The only legitimate use is an event handler, where the signature forces `void`.

```csharp
// BAD: exceptions escape and crash the process; callers can't await it
public async void SaveDataAsync() { ... }

// GOOD: return Task, even if the caller doesn't await it (fire-and-forget deliberately)
public async Task SaveDataAsync() { ... }

// The one exception: event handlers must be void
private async void OnButtonClick(object sender, EventArgs e)
{
    await SaveDataAsync();
}
```

If you genuinely need fire-and-forget, store the `Task` somewhere observable or attach a continuation that logs failures — never just drop it.

## ConfigureAwait(false) in Libraries

`await` captures the current synchronisation context by default and resumes on it. In UI apps that is what you want (resume on the UI thread). In library code, capturing the context is pure overhead and can deadlock a caller that blocks on the task. Library code should use `ConfigureAwait(false)` on every await.

```csharp
// Library code: don't capture the caller's context
public async Task<string> FetchAsync(HttpClient http, string url)
{
    var response = await http.GetAsync(url).ConfigureAwait(false);
    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
}
```

Application code (ASP.NET Core, console apps) has no synchronisation context by default, so `ConfigureAwait(false)` is harmless but unnecessary there. The rule is about libraries: code you do not know the caller of.

## Never Block on Async Code

`.Result`, `.Wait()`, and `GetAwaiter().GetResult()` on a task that needs the captured context to complete will deadlock: the blocking thread holds the context the continuation is waiting for. This is the classic ASP.NET (classic) and UI deadlock.

```csharp
// BAD: blocks the thread; deadlocks when a sync context is present
var data = FetchAsync().Result;

// GOOD: async all the way down
var data = await FetchAsync();
```

If you are in genuinely synchronous code that must call an async API, run it on a thread-pool thread without a context (`Task.Run(() => FetchAsync()).Result`) — or better, make the caller async. Async all the way down is the real fix.

## Async Disposal and ValueTask

Types that hold async resources implement `IAsyncDisposable`. Use `await using` so disposal can await flushes and network closes instead of blocking.

```csharp
await using var stream = new FileStream("data.bin", FileMode.Create);
await stream.WriteAsync(buffer);
```

`ValueTask<T>` avoids a `Task` allocation when the result is often already available (e.g. a cache hit). But it has rules: await it only once, never store it in a field, and never await it twice — a second await on a `ValueTask` backed by a pooled source is undefined behaviour. Prefer plain `Task<T>` unless profiling shows the allocation matters.

## Cancellation Tokens

Pass `CancellationToken` through your whole call chain and honour it in loops and I/O. It is how a request abort, a timeout, or a shutdown propagates instead of leaving work running after nobody cares.

```csharp
public async Task ProcessAsync(CancellationToken ct)
{
    await foreach (var item in GetItemsAsync(ct).WithCancellation(ct))
    {
        ct.ThrowIfCancellationRequested();
        await HandleAsync(item, ct);
    }
}
```

## Key Takeaways

- `async void` is only for event handlers; everywhere else return `Task` so failures are observable.
- Library code should use `ConfigureAwait(false)`; application code with no sync context does not need it.
- Never block on async code with `.Result` or `.Wait()` — go async all the way down.
- Use `await using` for `IAsyncDisposable`; use `ValueTask` only when profiling justifies it, and await it exactly once.
- Thread `CancellationToken` through the whole call chain and check it in loops.
- Fire-and-forget is a deliberate choice: observe the task or log its failure, never silently drop it.
