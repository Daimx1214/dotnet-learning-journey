# TaskCompletionSource in C#

`TaskCompletionSource<T>` bridges callback-based and event-based APIs to the `async`/`await` world. It creates a `Task<T>` that you complete manually by calling `TrySetResult`, `TrySetException`, or `TrySetCanceled`. It is the tool for wrapping anything that signals completion with an event or a callback instead of returning a task.

## The Basic Pattern

```csharp
var tcs = new TaskCompletionSource<string>();

someService.OperationCompleted += (_, result) =>
{
    tcs.TrySetResult(result);
};

someService.StartOperation();

string result = await tcs.Task;
```

The `await` suspends until something calls one of the `TrySet*` methods. The `Try` prefix matters: it returns `false` instead of throwing if the task is already completed, which protects against an event firing twice or a timeout racing with a result.

## Wrapping an Event: A Real Example

Suppose a legacy downloader raises `Downloaded` when it finishes and `Failed` on error:

```csharp
public static Task<byte[]> DownloadAsync(LegacyDownloader downloader, string url)
{
    var tcs = new TaskCompletionSource<byte[]>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    void OnDownloaded(object? sender, byte[] data)
    {
        Cleanup();
        tcs.TrySetResult(data);
    }

    void OnFailed(object? sender, Exception error)
    {
        Cleanup();
        tcs.TrySetException(error);
    }

    void Cleanup()
    {
        downloader.Downloaded -= OnDownloaded;
        downloader.Failed -= OnFailed;
    }

    downloader.Downloaded += OnDownloaded;
    downloader.Failed += OnFailed;
    downloader.Start(url);

    return tcs.Task;
}
```

Unsubscribing in `Cleanup` is important: without it, the downloader holds a reference to the handlers, and the `TaskCompletionSource` (plus anything it captures) is never garbage collected. Note that the handlers are attached before `Start` is called, so a fast completion cannot be missed.

## RunContinuationsAsynchronously

By default, the continuation after `await tcs.Task` runs synchronously on the thread that called `TrySetResult`. If that thread holds a lock or is a UI thread, the continuation can deadlock or freeze the UI. Passing `TaskCreationOptions.RunContinuationsAsynchronously` forces continuations onto the thread pool:

```csharp
var tcs = new TaskCompletionSource<int>(
    TaskCreationOptions.RunContinuationsAsynchronously);
```

Use it by default when wrapping external events, since you do not control which thread raises the event.

## Adding a Timeout

A `TaskCompletionSource` has no built-in timeout. Combine it with a `CancellationToken` or a delay task:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

string result = await tcs.Task;
```

`TrySetCanceled` with the token makes the awaited task throw `OperationCanceledException` when the timeout fires. Registering the cancellation before starting the operation avoids a race where the timeout fires first.

## Common Mistakes

- Forgetting to unsubscribe event handlers, which leaks the `TaskCompletionSource`.
- Using `SetResult` instead of `TrySetResult`, which throws if the task already completed.
- Creating the `TaskCompletionSource` after starting the operation, so a fast completion is missed.
- Calling `TrySetResult` from a thread that holds a lock, without `RunContinuationsAsynchronously`, which can deadlock the continuation.

## Where It Shows Up

`TaskCompletionSource` is behind many familiar APIs: `Task.Delay` with cancellation, `HttpClient` internals, `Channel<T>` readers, and test doubles that need to control when an async operation completes. In tests it is the simplest way to make a fake async method wait until the test says so.

## Key Takeaways

- `TaskCompletionSource<T>` turns an event or callback into an awaitable `Task<T>`.
- Use `TrySetResult`, `TrySetException`, and `TrySetCanceled`: they return false instead of throwing on a second completion.
- Pass `RunContinuationsAsynchronously` so continuations do not run on the event-raising thread.
- Always unsubscribe event handlers when the task completes, or the source leaks.
- Attach handlers before starting the operation, and add a timeout with a `CancellationToken`.
