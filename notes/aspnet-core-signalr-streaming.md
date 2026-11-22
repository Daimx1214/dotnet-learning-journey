# SignalR Streaming

A normal SignalR hub method returns one value per call. Streaming hub
methods return a sequence of values over time instead: the server pushes
items as they become available, and the client processes each one as it
arrives. SignalR supports streaming in both directions — server to
client and client to server — using `IAsyncEnumerable<T>` or
`ChannelReader<T>`.

## Server-to-Client Streaming

A hub method that returns `IAsyncEnumerable<T>` streams its items to
the caller. The client reads them with `await foreach` (or an async
iterator in JavaScript), so a long-running feed never blocks waiting
for the whole result.

```csharp
public class StockHub : Hub
{
    public async IAsyncEnumerable<decimal> StreamPrices(
        string symbol,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var random = new Random();
        decimal price = 100m;
        while (!cancellationToken.IsCancellationRequested)
        {
            price += (decimal)(random.NextDouble() - 0.5);
            yield return Math.Round(price, 2);
            await Task.Delay(1000, cancellationToken);
        }
    }
}
```

The `[EnumeratorCancellation]` attribute wires the client's cancellation
into the token: when the client stops listening (or disconnects), the
loop ends instead of streaming forever. On the client:

```csharp
await foreach (var price in connection.StreamAsync<decimal>(
    "StreamPrices", "MSFT", cancellationToken))
{
    Console.WriteLine(price);
}
```

## Client-to-Server Streaming

The client can stream values up to the hub, one at a time. The hub
method takes an `IAsyncEnumerable<T>` (or `ChannelReader<T>`) and
consumes it as items arrive — useful for uploads, live sensor feeds,
or chunked data.

```csharp
public async Task<int> UploadReadings(IAsyncEnumerable<int> readings)
{
    int count = 0;
    await foreach (var reading in readings)
    {
        count++;
        // store or process each reading as it arrives
    }
    return count;
}
```

From the client, pass an `IAsyncEnumerable<T>` (or a `ChannelWriter<T>`
backed channel) as the argument:

```csharp
async IAsyncEnumerable<int> ReadSensor()
{
    for (int i = 0; i < 10; i++)
    {
        yield return i * 2;
        await Task.Delay(500);
    }
}

int received = await connection.InvokeAsync<int>(
    "UploadReadings", ReadSensor());
```

## ChannelReader<T> vs IAsyncEnumerable<T>

Both shapes work on either side of the connection; they differ in who
controls production:

- `IAsyncEnumerable<T>` — the producer is a simple async iterator
  (`yield return`); best when items are produced by a loop.
- `ChannelReader<T>` — backed by a `Channel<T>`, so any code holding
  the writer can push items from anywhere (another thread, an event
  handler, a background service). The reader side sees a `ChannelReader`
  and reads until the writer completes it.

```csharp
public ChannelReader<string> StreamLogs()
{
    var channel = Channel.CreateUnbounded<string>();
    _ = Task.Run(async () =>
    {
        for (int i = 0; i < 5; i++)
        {
            await channel.Writer.WriteAsync($"log line {i}");
            await Task.Delay(200);
        }
        channel.Writer.Complete();
    });
    return channel.Reader;
}
```

The hub method returns immediately with the reader; SignalR drains the
channel and sends each item, then closes the stream when the writer
completes.

## Cancellation and Errors

- **Client stops listening:** the server's `CancellationToken` (via
  `[EnumeratorCancellation]`) is cancelled and the iterator's `finally`
  blocks run — use them to release resources.
- **Server throws mid-stream:** the exception is sent to the client and
  the stream ends; the client sees it when awaiting the next item.
- **Hub lifetime:** a streaming method runs on the hub instance for the
  duration of the stream, so do not hold a hub method open for hours
  without a timeout; prefer short-lived streams the client re-opens.

## When Streaming Fits — and When It Does Not

- **Fits:** live prices, progress of a long job, log tailing, chunked
  uploads, sensor feeds — anything where values arrive over time.
- **Does not fit:** one-shot request/response (use a normal hub method),
  broadcasting the same stream to many clients (use
  `Clients.All.SendAsync` per event instead), or very high-frequency
  data where per-message overhead matters (batch items into arrays).

## Key Takeaways

- Streaming hub methods return `IAsyncEnumerable<T>` or
  `ChannelReader<T>` and push items over time instead of one result.
- Server-to-client streaming uses `StreamAsync<T>` on the client and
  `await foreach` to consume items as they arrive.
- Client-to-server streaming passes an `IAsyncEnumerable<T>` argument
  that the hub consumes item by item.
- `[EnumeratorCancellation]` links client cancellation to the server
  iterator so abandoned streams stop cleanly.
- `ChannelReader<T>` fits when items are produced from outside the
  iterator — events, threads or background services.
- Use streaming for live feeds and long jobs, not for one-shot calls or
  fan-out broadcasts.
