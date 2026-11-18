# ConfigureAwait in C#

`ConfigureAwait` controls whether an `await` resumes on the captured synchronization context. `await task.ConfigureAwait(false)` tells the awaiter *not* to capture the current context, so the continuation runs on whatever thread pool thread is available. `ConfigureAwait(true)` — the default when you write a plain `await` — captures the context and resumes on it.

It matters because resuming on the wrong context causes deadlocks in UI and legacy ASP.NET apps, while capturing a context you do not need costs performance and limits scalability in library code.

## What Is a SynchronizationContext?

A `SynchronizationContext` is an abstraction over "where continuations run". Different hosts install different ones:

- **WPF / WinForms / MAUI:** a context that posts continuations back to the single UI thread. UI controls can only be touched from that thread, so this is essential.
- **Legacy ASP.NET (pre-Core):** a context that preserves the request context (culture, `HttpContext`, identity) and allows only one thread in the request at a time.
- **ASP.NET Core:** **no** `SynchronizationContext` by default. Continuations run on thread pool threads.
- **Console apps, background services, tests:** usually no context at all.

When you `await` without `ConfigureAwait(false)`, the runtime captures `SynchronizationContext.Current` (if there is one) and schedules the continuation on it. When you pass `false`, it skips the capture entirely.

```csharp
// Captures the UI context: continuation runs on the UI thread.
private async void LoadButton_Click(object sender, RoutedEventArgs e)
{
    var data = await LoadDataAsync(); // resumes on the UI thread
    ResultTextBox.Text = data;        // safe: we are on the UI thread
}

// Does NOT capture: continuation runs on a thread pool thread.
private async Task<string> LoadDataAsync()
{
    var json = await _http.GetStringAsync(url).ConfigureAwait(false);
    return Parse(json); // runs on a thread pool thread — do NOT touch UI here
}
```

## The Classic Deadlock

The famous deadlock happens when synchronous code blocks on an async method in a context that only allows one thread at a time — the UI thread, or a legacy ASP.NET request.

```csharp
// UI event handler (or legacy ASP.NET controller action):
public void Button_Click(object sender, RoutedEventArgs e)
{
    // Blocks the UI thread waiting for the task.
    var result = LoadDataAsync().Result; // DEADLOCK
}

public async Task<string> LoadDataAsync()
{
    // The await captures the UI context...
    var json = await _http.GetStringAsync(url);
    // ...and the continuation needs the UI thread to run.
    return json;
}
```

Step by step: the UI thread calls `.Result` and blocks. The HTTP call completes, and the continuation is queued to the UI thread — but the UI thread is blocked waiting for `.Result`. Neither side can proceed. Deadlock, forever.

Adding `.ConfigureAwait(false)` breaks the cycle, because the continuation no longer needs the UI thread:

```csharp
public async Task<string> LoadDataAsync()
{
    var json = await _http.GetStringAsync(url).ConfigureAwait(false);
    return json; // runs on a thread pool thread — the UI thread is free
}
```

The real fix, of course, is to not block at all — make the caller `async` and `await` instead of using `.Result` or `.Wait()`. `ConfigureAwait(false)` is a safety net for library code, not a licence to block.

## Library Code vs Application Code

The guidance splits cleanly:

**Library code — use `ConfigureAwait(false)` on every await.** A library does not know who will call it. If a UI app calls your library and blocks on it, a captured context deadlocks. `ConfigureAwait(false)` also avoids the small cost of posting back to the context, which adds up in hot paths.

```csharp
// Library method: no UI to update, no context needed.
public async Task<string> FetchWeatherAsync(string city)
{
    var response = await _http.GetAsync($"weather/{city}").ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
}
```

**Application code (UI event handlers, controllers) — plain `await` is fine.** You usually *want* the context: a WPF handler needs the UI thread to update controls, and a legacy ASP.NET action may need the request context. In ASP.NET Core there is no context anyway, so `ConfigureAwait(false)` changes nothing there.

**ASP.NET Core — `ConfigureAwait(false)` is a no-op.** There is no `SynchronizationContext`, so capturing or not capturing makes no difference. You will still see it in libraries targeting multiple frameworks, but in a pure ASP.NET Core app you can leave it out.

## Common Misconceptions

**"ConfigureAwait(false) makes code run on a background thread."**
No. It only says "do not resume on the captured context". The continuation runs on a thread pool thread, but the work itself was always going to run somewhere — `ConfigureAwait` says nothing about *where* the awaited operation executes, only where the code *after* the await resumes.

**"ConfigureAwait(false) makes code faster."**
Marginally. Skipping the context post avoids one queue hop. Measurable in tight loops, irrelevant in a single database call. The real reason to use it is deadlock safety in libraries, not speed.

**"I should add ConfigureAwait(false) everywhere in my app."**
In ASP.NET Core it does nothing, and in UI code it actively breaks the pattern of updating controls after an await. Use it in libraries; leave application code alone unless you have a measured reason.

**"ConfigureAwait(false) fixes all deadlocks."**
It fixes the specific deadlock where a continuation needs a blocked context. It does not fix deadlocks caused by lock ordering, `.Result` on genuinely incomplete work, or a thread pool starved by too many blocked threads. The correct fix for blocking is `await`, not configuration.

## The async All the Way Down Rule

The deadlock above only exists because something blocked. The durable fix is to make the whole call chain async:

```csharp
// Bad: blocking on async code.
public void Button_Click(object sender, RoutedEventArgs e)
{
    var result = LoadDataAsync().Result; // blocks, can deadlock
}

// Good: async all the way down.
public async void Button_Click(object sender, RoutedEventArgs e)
{
    var result = await LoadDataAsync(); // no blocking, no deadlock
    ResultTextBox.Text = result;
}
```

`async void` is acceptable for event handlers (it is the only place it is), because the UI framework needs a `void` signature. Everywhere else, return `Task` so callers can await and exceptions can be observed.

## Key Takeaways

- `ConfigureAwait(false)` tells an await not to capture the current `SynchronizationContext`, so the continuation runs on a thread pool thread.
- Blocking on async code (`.Result`, `.Wait()`) in a single-threaded context (UI thread, legacy ASP.NET) deadlocks, because the continuation needs the blocked thread.
- Library code should use `ConfigureAwait(false)` on every await: libraries cannot know their caller's context, and it prevents deadlocks for consumers.
- Application UI code usually wants the default (`true`) so it can update controls after an await; ASP.NET Core has no context, so `ConfigureAwait(false)` is a no-op there.
- `ConfigureAwait(false)` does not move work to a background thread — it only controls where the code after the await resumes.
- The real fix for blocking deadlocks is async all the way down: `await` instead of `.Result`, never the reverse.
