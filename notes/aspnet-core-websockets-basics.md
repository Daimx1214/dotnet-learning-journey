# Raw WebSockets in ASP.NET Core (ASP.NET Core)

A WebSocket is a persistent, full-duplex connection over a single HTTP request that is upgraded to the WebSocket protocol. Unlike HTTP, where the client must ask for every message, either side can push a message at any time. That makes WebSockets the right primitive for live data: chat, notifications, live dashboards, multiplayer games.

## Enabling WebSockets

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Must be registered before any endpoint that accepts sockets.
app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await EchoLoop(socket);
});

app.Run();
```

`UseWebSockets` registers the middleware that handles the upgrade handshake. `AcceptWebSocketAsync` completes the handshake and returns the socket. Until that call, the request is an ordinary HTTP request; after it, the connection stays open until one side closes it.

## The Echo Loop

Reading and writing are explicit. You receive into a buffer, check the message type, and send the bytes back. A loop keeps the connection alive until the client closes it.

```csharp
static async Task EchoLoop(WebSocket socket)
{
    var buffer = new byte[1024 * 4];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            // Acknowledge the close handshake.
            await socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Closing", CancellationToken.None);
            break;
        }

        // Echo the exact bytes back, preserving text/binary and EndOfMessage.
        await socket.SendAsync(
            new ArraySegment<byte>(buffer, 0, result.Count),
            result.MessageType,
            result.EndOfMessage,
            CancellationToken.None);
    }
}
```

Three details matter. First, `ReceiveAsync` returns one *frame*, not necessarily one whole message — a large message arrives in several frames, and only the last has `EndOfMessage == true`, so real code must accumulate frames until that flag. Second, always handle the `Close` message type and answer with `CloseAsync`, or the connection hangs in a half-closed state. Third, send and receive are not thread-safe on the same socket at the same time — serialize writes with a lock or a channel if multiple producers exist.

## Keeping Connections Alive

Idle connections are killed by proxies and load balancers. Set a keep-alive ping interval so the underlying TCP connection stays open:

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
```

This sends protocol-level ping frames, not application messages. Application-level heartbeats ("ping"/"pong" JSON messages) are still useful to detect a dead peer that the TCP layer has not noticed yet.

## Raw WebSockets vs SignalR

SignalR is built on top of WebSockets and adds everything raw sockets lack: automatic reconnection, fallback transports (long polling when WebSockets are blocked), hub-based routing, groups, strongly typed method calls, and scale-out via a backplane (Redis or Azure SignalR Service). Raw WebSockets are the right choice when the client is not .NET or JavaScript, when you need a non-SignalR protocol, or when you want zero framework overhead and full control of the wire format. For a typical .NET chat or notification feature, SignalR is less code and fewer bugs.

## Key takeaways

- `UseWebSockets()` enables the handshake; `AcceptWebSocketAsync()` upgrades the request to a socket.
- Read in a loop with `ReceiveAsync`, accumulate frames until `EndOfMessage`, and answer close frames with `CloseAsync`.
- Set `KeepAliveInterval` so proxies do not drop idle connections.
- Prefer SignalR for app-level messaging; use raw WebSockets when you need protocol control or a non-SignalR client.
