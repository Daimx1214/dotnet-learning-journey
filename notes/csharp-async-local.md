# C# AsyncLocal<T>

`AsyncLocal<T>` carries a value along the logical flow of async code. Set it before an `await`, and the continuation — and anything it calls — sees the same value, even after the work resumes on a different thread.

## How It Flows

`AsyncLocal<T>` is backed by `ExecutionContext`, which the runtime captures at every `await` and restores when the continuation runs. The value follows the async call chain, not the thread.

```csharp
public static class RequestContext
{
    public static AsyncLocal<string?> CorrelationId { get; } = new();
}

RequestContext.CorrelationId.Value = Guid.NewGuid().ToString("N");
await DoWorkAsync();   // DoWorkAsync sees the same correlation ID
// even if it resumes on a different thread pool thread
```

This is the standard way to carry a correlation ID, tenant ID, or current user through a request without threading it through every method signature. ASP.NET Core itself uses the same mechanism for `HttpContext` in some paths, and logging scopes (`ILogger.BeginScope`) rely on it.

## AsyncLocal vs ThreadLocal

`ThreadLocal<T>` is tied to the physical thread. After an `await`, the continuation usually runs on a different thread, so a `ThreadLocal` value set before the await is gone — or worse, belongs to whatever ran on that thread before.

```csharp
private static readonly ThreadLocal<string?> ThreadValue = new();
private static readonly AsyncLocal<string?> AsyncValue = new();

ThreadValue.Value = "A";
AsyncValue.Value = "A";
await Task.Delay(10);
// ThreadValue.Value is now null (or another request's value) on this thread
// AsyncValue.Value is still "A"
```

Use `ThreadLocal` only for genuinely thread-bound resources (like a per-thread buffer). For anything that must survive `await`, use `AsyncLocal`.

## Pitfalls: Mutation and Cost

`AsyncLocal` values flow *down* the call chain, not back up. A child async method that changes the value does not change it for its caller — the change is scoped to that child's execution context. But if the value is a mutable reference type (a `List`, a `Dictionary`), mutating the object itself *is* visible to the caller, because both sides hold the same reference. Prefer immutable values, or replace the whole value instead of mutating it.

```csharp
// Prefer replacing the value over mutating a shared object
RequestContext.CorrelationId.Value = newId;   // safe: new value, scoped to this flow

// Avoid this: mutating a shared mutable object leaks across flows
// context.Items["key"] = value;  // visible to the caller too
```

There is also a cost: every `await` captures the `ExecutionContext`, and a large or frequently-changing `AsyncLocal` graph makes that capture more expensive. Keep the stored value small and change it rarely on hot paths. Clearing a value when a request ends is good hygiene, though the context is discarded with the request anyway.

## Key Takeaways

- `AsyncLocal<T>` flows with the async call chain across awaits and thread switches; `ThreadLocal<T>` does not.
- Use it for ambient context: correlation IDs, tenant IDs, logging scopes — not for passing data as a substitute for parameters.
- Changes flow downward only: a child changing the value does not affect the caller, but mutating a shared reference-type value does leak.
- Prefer immutable values; replace the whole value instead of mutating a shared object.
- `ExecutionContext` is captured at every await, so keep stored values small on hot paths.
- Never use `AsyncLocal` to smuggle data between unrelated requests — it is per logical flow, not global state.
