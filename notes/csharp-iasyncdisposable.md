# IAsyncDisposable

`IAsyncDisposable` is the asynchronous counterpart of `IDisposable`. It exists for cleanup work that itself needs to wait for something — flushing a buffered writer to a network stream, closing a database connection gracefully, or releasing a remote lock — where a synchronous `Dispose()` would have to block a thread or skip the work.

## When Async Disposal Is Needed

Use it when the cleanup step is genuinely asynchronous. A `StreamWriter` over a network stream may still hold unwritten bytes; flushing them requires an async write. A synchronous `Dispose` would either block the calling thread while the flush completes or silently drop the data. If the cleanup is only releasing memory or closing a local handle, plain `IDisposable` is enough and simpler.

```csharp
public sealed class AsyncLogger : IAsyncDisposable
{
    private readonly StreamWriter _writer;

    public AsyncLogger(string path) => _writer = new StreamWriter(path);

    public async Task LogAsync(string message) =>
        await _writer.WriteLineAsync(message);

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync(); // async flush — the reason this is IAsyncDisposable
        _writer.Dispose();
    }
}

await using var logger = new AsyncLogger("app.log");
await logger.LogAsync("started");
// DisposeAsync runs here, at the end of the scope, flushing before closing.
```

## await using and Contrast with IDisposable

`await using` calls `DisposeAsync()` at the end of the scope and awaits it, just as `using` calls `Dispose()`. A type can implement both interfaces when it has a cheap synchronous path and a fuller async path; `await using` prefers `DisposeAsync()`, while `using` prefers `Dispose()`. Do not implement `IAsyncDisposable` just because a class has async methods — implement it only when disposal itself awaits something. And never block inside `DisposeAsync` with `.Result` or `.Wait()`; that defeats the purpose and can deadlock.

## Key Takeaways

- Implement `IAsyncDisposable` when cleanup itself must await — async flush, graceful close, remote release.
- `await using` disposes asynchronously at scope exit; `using` is for synchronous `IDisposable` cleanup.
- If disposal is only freeing memory or a local handle, `IDisposable` is the right, simpler choice.
- A class may implement both interfaces; `await using` picks the async path, `using` picks the sync path.
- Never block with `.Result` or `.Wait()` inside `DisposeAsync()` — it can deadlock and wastes the async design.
