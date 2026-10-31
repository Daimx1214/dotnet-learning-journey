# C# async and await

Use `async` and `await` to run work without blocking the calling thread. This is especially useful for I/O, such as reading a file, calling a web API, or querying a database.

## The basic idea

- Mark a method `async` when it awaits asynchronous work.
- `await` pauses the method until the awaited task completes, then resumes it.
- An async method normally returns `Task`, `Task<T>`, or `ValueTask<T>`. Avoid `async void` except for event handlers, because errors are harder to observe.

## Example

```csharp
public async Task<string> GetGreetingAsync()
{
    await Task.Delay(100);
    return "Hello from async C#";
}

public async Task RunAsync()
{
    string greeting = await GetGreetingAsync();
    Console.WriteLine(greeting);
}
```

## Practical tips

- Await tasks instead of blocking with `.Result` or `.Wait()`, which can cause deadlocks.
- Use `Task.WhenAll` when independent tasks can run at the same time.
- Name async methods with an `Async` suffix, for example `GetGreetingAsync`.
