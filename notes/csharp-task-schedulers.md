# Task Schedulers

A `TaskScheduler` decides where and when a task's work runs. The default
scheduler queues work to the thread pool, but tasks can also be scheduled
onto a UI thread, a single dedicated thread, or a custom queue. Knowing
which scheduler a task uses explains a whole class of deadlocks and
ordering bugs.

## TaskScheduler.Default

`TaskScheduler.Default` is the thread pool scheduler. `Task.Run` and
`Task.Factory.StartNew` (without a scheduler argument) use it.

```csharp
// Runs on a thread pool thread.
Task task = Task.Run(() => Console.WriteLine("On the thread pool"));

// Explicitly the same thing.
Task same = Task.Factory.StartNew(
    () => Console.WriteLine("Also the thread pool"),
    CancellationToken.None,
    TaskCreationOptions.None,
    TaskScheduler.Default);
```

The thread pool is the right default for CPU-bound and I/O-bound work
that does not need to run on a specific thread.

## FromCurrentSynchronizationContext

`TaskScheduler.FromCurrentSynchronizationContext()` captures the
`SynchronizationContext` of the calling thread and schedules continuations
back onto it — the UI thread in WinForms/WPF, or the ASP.NET request
context in classic ASP.NET.

```csharp
// In a WinForms event handler:
TaskScheduler uiScheduler =
    TaskScheduler.FromCurrentSynchronizationContext();

Task.Run(() => ComputeResult())
    .ContinueWith(t => label.Text = t.Result.ToString(),
        uiScheduler); // back on the UI thread, safe to touch controls
```

Calling `FromCurrentSynchronizationContext()` where there is no current
context (a thread pool thread, or ASP.NET Core, which has no
`SynchronizationContext`) throws `InvalidOperationException`.

## ConfigureAwait(false)

`await task.ConfigureAwait(false)` tells the awaiter not to capture the
current `SynchronizationContext`, so the continuation runs on a thread
pool thread instead of marshaling back to the UI thread. Library code
should use it by default: the library does not know whether its caller
needs the UI thread, and capturing the context can deadlock a caller
that blocks on the task (for example, `.Result` on the UI thread).

```csharp
// Library code: no need for the caller's context.
public async Task<string> FetchAsync()
{
    using var client = new HttpClient();
    return await client.GetStringAsync("https://example.com")
        .ConfigureAwait(false);
}
```

Application code that updates the UI after an await usually omits it, so
the continuation returns to the UI thread.

## A Custom Scheduler Sketch

A custom scheduler can serialize work onto one thread — useful for
interop with a single-threaded library. The core of it is a queue plus
a dedicated thread that drains it:

```csharp
public sealed class SingleThreadScheduler : TaskScheduler, IDisposable
{
    private readonly BlockingCollection<Task> _queue = new();
    private readonly Thread _thread;

    public SingleThreadScheduler()
    {
        _thread = new Thread(() =>
        {
            foreach (Task task in _queue.GetConsumingEnumerable())
                TryExecuteTask(task);
        })
        { IsBackground = true };
        _thread.Start();
    }

    protected override void QueueTask(Task task) => _queue.Add(task);

    protected override bool TryExecuteTaskInline(
        Task task, bool taskWasPreviouslyQueued) => false;

    protected override IEnumerable<Task> GetScheduledTasks() =>
        _queue.ToArray();

    public void Dispose() => _queue.CompleteAdding();
}

// Usage: all tasks queued here run one at a time, in order.
var scheduler = new SingleThreadScheduler();
Task.Factory.StartNew(() => DoWork(), CancellationToken.None,
    TaskCreationOptions.None, scheduler);
```

## Key Takeaways

- `TaskScheduler.Default` is the thread pool; `Task.Run` uses it.
- `FromCurrentSynchronizationContext()` routes continuations back to
  the calling thread (UI thread), and throws where no context exists.
- `ConfigureAwait(false)` skips capturing the context; library code
  should use it, UI code usually should not.
- A custom scheduler is a queue plus a thread that drains it via
  `TryExecuteTask` — the building block for single-threaded execution.
- Picking the wrong scheduler is how UI freezes and deadlocks happen;
  always know which thread your continuation will run on.
