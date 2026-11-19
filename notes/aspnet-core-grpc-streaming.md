# gRPC Streaming in ASP.NET Core

gRPC is built on HTTP/2, which supports long-lived bidirectional streams — so a single RPC call can exchange many messages over time instead of just one request and one response.

## Unary vs Streaming

There are four call patterns:

| Pattern | Client sends | Server sends |
|---------|--------------|--------------|
| Unary | 1 message | 1 message |
| Server streaming | 1 message | Many messages |
| Client streaming | Many messages | 1 message |
| Bidirectional streaming | Many messages | Many messages |

Unary is the default and fits most CRUD-style calls. Streaming fits when data arrives over time, or when the response is too large to build in memory at once.

## Server-Streaming Example: Stock-Price Ticker

Define the service in a `.proto` file:

```proto
syntax = "proto3";

service Ticker {
  rpc WatchPrices (WatchRequest) returns (stream PriceUpdate);
}

message WatchRequest {
  repeated string symbols = 1;
}

message PriceUpdate {
  string symbol = 1;
  double price = 2;
  int64 timestamp_unix_ms = 3;
}
```

The `stream` keyword on the return type makes it server-streaming. On the server, the generated base class exposes the call as an `IAsyncEnumerable`-style method using `IServerStreamWriter<T>`:

```csharp
public class TickerService : Ticker.TickerBase
{
    public override async Task WatchPrices(
        WatchRequest request,
        IServerStreamWriter<PriceUpdate> responseStream,
        ServerCallContext context)
    {
        var random = new Random();
        while (!context.CancellationToken.IsCancellationRequested)
        {
            foreach (var symbol in request.Symbols)
            {
                await responseStream.WriteAsync(new PriceUpdate
                {
                    Symbol = symbol,
                    Price = 100 + random.NextDouble() * 5,
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            await Task.Delay(1000, context.CancellationToken);
        }
    }
}
```

The client consumes the stream with `await foreach`, which reads each message as it arrives:

```csharp
using var call = client.WatchPrices(new WatchRequest { Symbols = { "AAPL", "MSFT" } });
await foreach (var update in call.ResponseStream.ReadAllAsync())
{
    Console.WriteLine($"{update.Symbol}: {update.Price:F2}");
}
```

## When Streaming Beats Polling

Polling asks "anything new?" on a timer — every poll is a full HTTP request, and most polls return nothing. Streaming opens one connection and pushes each update the moment it exists: lower latency, far less overhead, and no empty responses. Streaming wins when updates are frequent, latency matters, or the client count is large. Polling is still fine when updates are rare (minutes apart) or the client cannot hold a long-lived connection.

## Key Takeaways

- gRPC supports four patterns: unary, server streaming, client streaming, and bidirectional streaming.
- Server streaming is declared with `returns (stream T)` in the `.proto` file and implemented with `IServerStreamWriter<T>` on the server.
- Clients consume a server stream with `await foreach` over `ReadAllAsync()` — each message arrives as it is written.
- Always respect `context.CancellationToken` so the loop ends when the client disconnects.
- Streaming beats polling for frequent, latency-sensitive updates; polling is fine for rare updates or short-lived clients.
