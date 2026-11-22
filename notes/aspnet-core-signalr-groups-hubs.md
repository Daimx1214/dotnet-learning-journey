# SignalR Groups and Hubs

Basic SignalR sends messages to all clients or back to the caller. Real
applications need more control: send to one chat room, one user, or one
team. Groups and hub context give you that control, and strongly-typed
hubs make the client contract compile-time checked.

## Groups

A group is a named set of connections. Clients join and leave groups,
and the server sends to a group by name — the classic chat room or live
dashboard per tenant.

```csharp
public class ChatHub : Hub
{
    public async Task JoinRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        await Clients.Group(roomName).SendAsync(
            "UserJoined", Context.ConnectionId);
    }

    public async Task LeaveRoom(string roomName)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId, roomName);
    }

    public async Task SendToRoom(string roomName, string message)
    {
        await Clients.Group(roomName).SendAsync(
            "ReceiveMessage", Context.User?.Identity?.Name, message);
    }
}
```

Groups are connection-scoped, not user-scoped: if a user reconnects
they get a new connection id and must rejoin their groups. A common
pattern is to rejoin groups in `OnConnectedAsync` by looking up the
user's rooms from the database.

## Sending to Users

`Clients.User(userId)` targets every connection of one user (all their
tabs and devices), using the `NameIdentifier` claim from authentication.
`Clients.Client(connectionId)` targets a single connection.

```csharp
// Notify one user on all their devices.
await Clients.User(userId).SendAsync("OrderShipped", orderId);

// Reply only to the connection that called the hub method.
await Clients.Caller.SendAsync("Ack", "Message received");

// Everyone except the caller.
await Clients.Others.SendAsync("UserTyping", Context.ConnectionId);
```

## HubContext: Sending from Outside a Hub

Controllers and background services cannot use `Clients` directly, but
they can inject `IHubContext<T>` and send to clients from anywhere:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IHubContext<ChatHub> _hub;

    public OrdersController(IHubContext<ChatHub> hub) => _hub = hub;

    [HttpPost("{id}/ship")]
    public async Task<IActionResult> Ship(int id)
    {
        // ... update the order in the database ...
        await _hub.Clients.Group($"order-{id}").SendAsync(
            "OrderShipped", id);
        return Ok();
    }
}
```

A background service can do the same — for example, a hosted service
that watches a queue and pushes progress updates to the relevant group.

## Strongly-Typed Hubs

A strongly-typed hub replaces magic strings with an interface, so a
renamed client method becomes a compile error instead of a silent miss:

```csharp
public interface IChatClient
{
    Task ReceiveMessage(string user, string message);
    Task UserJoined(string connectionId);
    Task OrderShipped(int orderId);
}

public class ChatHub : Hub<IChatClient>
{
    public async Task SendToRoom(string roomName, string message)
    {
        await Clients.Group(roomName).ReceiveMessage(
            Context.User?.Identity?.Name ?? "Anonymous", message);
    }
}

// From a controller:
await _hub.Clients.Group(room).OrderShipped(orderId);
```

The JavaScript or .NET client still registers handlers by string name,
so the interface only guards the server side — but that is where most
typos happen.

## Scale-Out

A single server keeps group membership in memory. With multiple servers
behind a load balancer, a message sent from server A must reach clients
connected to server B. That requires a backplane: the Redis backplane
(`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) or Azure SignalR
Service. With a backplane, every server publishes to the shared channel
and every server delivers to its own connections. Sticky sessions are
still recommended so a client's reconnects land on the same server.

## Key Takeaways

- Groups (`AddToGroupAsync` / `RemoveFromGroupAsync`) target a named set
  of connections, such as a chat room.
- `Clients.User(...)` targets all connections of one user;
  `Clients.Client(...)` targets one connection.
- Inject `IHubContext<T>` to send from controllers and background
  services, outside a hub method.
- Strongly-typed hubs (`Hub<IChatClient>`) turn client method names
  into compile-time-checked calls.
- Scale-out needs a backplane (Redis or Azure SignalR Service) so a
  message from one server reaches clients on another.
