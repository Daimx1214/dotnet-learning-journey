# SignalR Scale-Out

A single SignalR server holds its connections in memory, so a client connected to server A cannot receive a message sent from server B. Scale-out solves this with a backplane: when one server publishes a message, the backplane delivers it to every other server, which forwards it to its own connected clients. The two standard backplanes are Redis and Azure SignalR Service.

## How the Redis Backplane Works

With `Microsoft.AspNetCore.SignalR.StackExchangeRedis`, each server subscribes to Redis pub/sub channels. When server A calls `Clients.All.SendAsync("ReceiveMessage", ...)`, it publishes the message to Redis once; servers B and C receive it and deliver it to their local connections. The message crosses the network twice (server A to Redis, Redis to server B), so latency rises by roughly 1–3 ms on a local network and more across regions. Redis also stores nothing durable — if a server is down when a message is published, its clients simply miss that message.

```csharp
// Program.cs: add the Redis backplane to SignalR.
builder.Services.AddSignalR()
    .AddStackExchangeRedis("localhost:6379", options =>
    {
        // Channel prefix isolates this app when Redis is shared.
        options.Configuration.ChannelPrefix = "ChatApp";
    });

// Hub code is unchanged — scale-out is transparent to hubs.
public sealed class ChatHub : Hub
{
    public async Task SendMessage(string user, string message) =>
        await Clients.All.SendAsync("ReceiveMessage", user, message);
}
```

With 3 servers and 10,000 connections each, one `Clients.All` broadcast publishes 1 message to Redis, and each server delivers it to its 10,000 local connections — 30,000 deliveries from a single publish. The Redis publish itself is cheap; the cost is in the per-server fan-out, which is the same work a single server would do for 30,000 connections.

## Sticky Sessions

SignalR negotiates a connection and then upgrades to WebSockets. With multiple servers behind a load balancer, the negotiate request and the WebSocket upgrade must land on the same server, and all subsequent requests on that connection must keep landing there. That is what sticky sessions (session affinity) provide: the load balancer routes by a cookie or by client IP.

- **Azure App Service**: enable ARR affinity (a cookie named `ARRAffinity`) — on by default for SignalR scenarios.
- **NGINX**: `ip_hash` in the upstream block, or the `sticky` directive with a cookie.
- **Kubernetes**: an ingress with `nginx.ingress.kubernetes.io/affinity: "cookie"`.
- **Application Gateway / ALB**: enable cookie-based affinity on the backend pool.

Without sticky sessions, the WebSocket upgrade hits a different server than the negotiate call, the connection fails, and the client falls back to long polling — or fails outright. Sticky sessions are required for the Redis backplane but not for Azure SignalR Service: the service terminates the client connections itself, so any app server can handle any message.

## Azure SignalR Service

Azure SignalR Service replaces both the backplane and the connection handling: clients connect directly to the service, and app servers connect to the service as hubs. App servers become stateless — they hold no client connections at all — so sticky sessions are unnecessary and servers can scale to zero.

```csharp
// Program.cs: replace the Redis backplane with Azure SignalR Service.
builder.Services.AddSignalR()
    .AddAzureSignalR("Endpoint=https://myapp.service.signalr.net;AccessKey=...;Version=1.0;");

// Hub code is identical. Clients connect to the service endpoint,
// which the service redirects to the app server for negotiation.
```

Pricing is per unit (1 unit = 1,000 concurrent connections, roughly $50/month for Standard tier at the time of writing). A deployment with 5,000 concurrent connections needs 5 units. The service also handles scale-out across regions with geo-replication, which the Redis backplane cannot do without custom work.

## Choosing Between the Two

| | Redis backplane | Azure SignalR Service |
|---|---|---|
| Client connections live on | App servers | The service |
| Sticky sessions | Required | Not required |
| Ops burden | You run Redis | Managed |
| Cross-region | Manual | Built-in geo-replication |
| Cost at 5,000 connections | Redis hosting (~$15–50/mo) | ~5 units (~$250/mo) |
| Message loss on server restart | Missed messages | Missed messages |

Choose Redis when you already run Redis, traffic stays in one region, and cost matters. Choose Azure SignalR Service when you want zero connection state on app servers, automatic scaling, or multi-region delivery.

## Common Pitfalls

- Forgetting sticky sessions after adding a second server: connections fail intermittently, only under load, which makes it look like a SignalR bug.
- Publishing large messages (megabytes) through the backplane: every server receives every message, so a 1 MB broadcast to 10 servers moves 10 MB through Redis.
- Assuming at-least-once delivery: the backplane is fire-and-forget; clients that disconnect miss messages. Use a persistent store (database, Service Bus) for messages that must not be lost.
- Scaling the backplane but not the servers: Redis handles the fan-out, but each server still serializes and sends to its own connections — CPU on the app servers is usually the bottleneck.
- Mixing `Clients.All` with per-connection state stored in hub instance fields: hub instances are transient, so state must live in a shared store.

## Key Takeaways

- Scale-out needs a backplane because connections live in server memory; Redis pub/sub delivers each message to every server, which fans out locally.
- The Redis backplane requires sticky sessions so negotiate and WebSocket upgrade land on the same server; without them connections fail intermittently.
- Azure SignalR Service terminates client connections itself, making app servers stateless and sticky sessions unnecessary.
- A broadcast to 30,000 connections across 3 servers is 1 Redis publish plus 30,000 local deliveries; the fan-out cost stays on the app servers.
- The backplane is fire-and-forget — disconnected clients miss messages, so persist anything that must not be lost.
