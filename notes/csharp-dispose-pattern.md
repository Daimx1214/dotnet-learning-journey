# IDisposable and the Dispose Pattern in C#

Some objects hold resources the garbage collector cannot reclaim on its own: file handles, database connections, network sockets, and unmanaged memory. `IDisposable` is the contract for releasing those resources deterministically, instead of waiting for a finalizer that may run late or never.

## The using Statement

The `using` statement is the normal way to consume a disposable object. It guarantees `Dispose()` runs even when the block throws.

```csharp
using (var writer = new StreamWriter("log.txt"))
{
    writer.WriteLine("hello");
} // Dispose() runs here, flushing and closing the file — even on exception.

// Equivalent modern form: disposed at the end of the enclosing scope.
using var reader = new StreamReader("log.txt");
string? firstLine = reader.ReadLine();
Console.WriteLine(firstLine); // "hello"
```

Without `using`, a forgotten `Dispose()` leaks the handle until the finalizer runs, and an exception between acquisition and disposal leaks it outright. Prefer `using` over manual `try`/`finally` — it is the same guarantee with less code to get wrong.

## Dispose(bool) and the Finalizer

The full pattern separates managed cleanup (other disposable objects) from unmanaged cleanup (raw handles), because the finalizer can only safely do the second — managed objects may already be finalized by then.

```csharp
public class ResourceHolder : IDisposable
{
    private readonly Stream _managedStream;   // Managed resource.
    private IntPtr _unmanagedHandle;          // Unmanaged resource (example handle).
    private bool _disposed;

    public ResourceHolder()
    {
        _managedStream = new FileStream("data.bin", FileMode.Create);
        _unmanagedHandle = new IntPtr(1234); // Placeholder for a real native handle.
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Only here: dispose managed resources.
            _managedStream.Dispose();
        }

        // Always: release unmanaged resources.
        // Real code would call the native free function here, e.g. CloseHandle.
        _unmanagedHandle = IntPtr.Zero;

        _disposed = true;
    }

    ~ResourceHolder()
    {
        Dispose(false);
    }
}
```

The rules in this code are the whole pattern: `Dispose()` calls `Dispose(true)` and then `GC.SuppressFinalize(this)`; `Dispose(bool)` disposes managed resources only when `disposing` is `true`; the finalizer calls `Dispose(false)`; and a `_disposed` flag makes repeated disposal harmless. The finalizer exists only as a safety net for callers who forget `Dispose()` — it must never touch managed objects.

## GC.SuppressFinalize

`GC.SuppressFinalize(this)` tells the runtime the object is already cleaned up, so the finalizer does not need to run. That matters for performance: an object with a finalizer survives one extra garbage-collection generation even after disposal, because the runtime keeps it alive until the finalizer queue is processed. Suppressing finalization lets a properly disposed object be collected in the generation it belongs to. Never call `SuppressFinalize` from the finalizer itself, and never call it on an object whose `Dispose()` was not actually run.

## IAsyncDisposable Note

When cleanup itself is asynchronous — flushing a network stream, for example — `IDisposable` is the wrong shape, because `Dispose()` cannot await. `IAsyncDisposable` and `await using` cover that case:

```csharp
await using (var stream = new FileStream("data.bin", FileMode.Create))
{
    await stream.WriteAsync(new byte[] { 1, 2, 3 });
} // DisposeAsync() is awaited here.

// A class can implement both interfaces when it has both kinds of cleanup,
// but most classes need only one: pick the one that matches the cleanup work.
```

`await using` works on `IAsyncDisposable`; plain `using` works on `IDisposable`. If a class implements both, `await using` is preferred when the caller is already asynchronous.

## Key Takeaways

- Use `using` (or `await using`) so `Dispose()` runs even when the block throws; never rely on the finalizer as the primary cleanup path.
- `Dispose(bool)` disposes managed resources only when `disposing` is `true`; unmanaged resources are released in both paths.
- The finalizer calls `Dispose(false)` and exists only as a safety net for callers who forget to dispose.
- `GC.SuppressFinalize(this)` in `Dispose()` stops the finalizer running on an already-cleaned object and avoids an extra GC generation of lifetime.
- Use `IAsyncDisposable` when cleanup itself needs to await; use `IDisposable` for synchronous cleanup.
- Make disposal idempotent with a `_disposed` flag so calling `Dispose()` twice is harmless.
