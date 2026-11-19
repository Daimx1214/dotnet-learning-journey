# Server-Sent Events in ASP.NET Core

Server-Sent Events (SSE) is a simple HTTP standard for pushing updates from server to client over a single long-lived connection. The client opens one GET request, and the server streams text events until either side closes it.

## How SSE Works

The server responds with `Content-Type: text/event-stream` and sends events as plain text lines: `data: <payload>` followed by a blank line. An optional `event:` line names the event type, and `id:` lets the client resume after a disconnect.

```csharp
app.MapGet("/prices", async (HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "text/event-stream";
    response.Headers.CacheControl = "no-cache";

    await foreach (var price in GetPriceStream(ct))
    {
        await response.WriteAsync($"data: {price}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
});

static async IAsyncEnumerable<string> GetPriceStream(
    [EnumeratorCancellation] CancellationToken ct)
{
    var random = new Random();
    while (!ct.IsCancellationRequested)
    {
        yield return $"AAPL: {100 + random.NextDouble() * 5:F2}";
        await Task.Delay(1000, ct);
    }
}
```

`IAsyncEnumerable` is the natural fit: each `yield return` produces one event, and the loop ends when the client disconnects (the `CancellationToken` is triggered). Flushing after each write matters — without it, the response may sit buffered and the client sees nothing.

## The Client Side

On the browser, the built-in `EventSource` API consumes an SSE endpoint with automatic reconnection:

```javascript
const source = new EventSource("/prices");
source.onmessage = (e) => console.log("Price update:", e.data);
source.onerror = () => console.log("Connection lost — EventSource will retry automatically");
```

`EventSource` only supports GET requests and sends no custom headers or body, so authentication usually travels via a cookie or a query-string token — a real limitation to plan around.

## SSE vs SignalR vs WebSockets

| Technology   | Direction      | Protocol          | Best for |
|--------------|----------------|-------------------|----------|
| SSE          | Server → client only | HTTP, text/event-stream | Live feeds: prices, logs, progress bars |
| WebSockets   | Bidirectional  | ws://, framed messages | Chat, games, anything needing client → server messages |
| SignalR      | Bidirectional  | WebSockets with fallbacks (SSE, long polling) | Real-time .NET apps with hubs, groups, and reconnection |

Choose SSE when the flow is strictly one-way and you want the simplest possible implementation — plain HTTP, no extra libraries, automatic reconnection built into the browser. Choose WebSockets or SignalR when the client also needs to send messages, or when you need binary data.

## Key Takeaways

- SSE streams server-to-client events over one HTTP connection with `Content-Type: text/event-stream` and `data: ...` lines.
- `IAsyncEnumerable` plus `Response.Body.FlushAsync()` is the clean minimal SSE endpoint in ASP.NET Core.
- The browser's `EventSource` API consumes SSE with automatic reconnection, but only supports GET requests with no custom headers.
- SSE is one-way and text-only; WebSockets are bidirectional, and SignalR adds hubs, groups, and transport fallback on top.
- Use SSE for live feeds and progress updates; reach for SignalR when the client must talk back.
