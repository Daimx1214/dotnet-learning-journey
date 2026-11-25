# SignalR Authentication

SignalR connections are long-lived, so authentication works differently from a normal HTTP request. Browsers cannot set an `Authorization` header on a WebSocket handshake, so the standard approach is to pass the JWT access token in the query string and teach the JWT handler to read it there.

## Passing the Token in the Query String

The JavaScript client supplies the token through `accessTokenFactory`:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chat", {
        accessTokenFactory: () => localStorage.getItem("accessToken")
    })
    .build();
```

The client sends the token as `?access_token=...` on the negotiate request and the WebSocket handshake. The token must be short-lived, because query strings can end up in server logs. Keep access tokens to minutes, and refresh them with a refresh token flow.

## Reading the Token on the Server

Configure the JWT bearer handler to pick the token up from the query string for hub paths:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
```

The path check matters: only hub requests should accept a token from the query string. Ordinary API requests must keep using the `Authorization` header, so a leaked token in a log cannot be reused against the whole API.

## Authorizing Hubs

Apply `[Authorize]` to the hub class or to individual methods, exactly as with controllers:

```csharp
[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string message)
    {
        var userId = Context.UserIdentifier;
        var userName = Context.User?.Identity?.Name;
        await Clients.All.SendAsync("ReceiveMessage", userName, message);
    }

    [Authorize(Roles = "Admin")]
    public Task Announce(string message) =>
        Clients.All.SendAsync("Announcement", message);
}
```

`Context.User` is the authenticated `ClaimsPrincipal`. `Context.UserIdentifier` is the user id claim by default (the `NameIdentifier` claim, or the value configured in `ClaimTypes.NameIdentifier`). Use it to target a single user.

## Groups per User

Groups route messages to a set of connections. A common pattern is one group per user, so a message reaches all of the user's devices:

```csharp
public override async Task OnConnectedAsync()
{
    var userId = Context.UserIdentifier;
    if (userId is not null)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception? exception)
{
    var userId = Context.UserIdentifier;
    if (userId is not null)
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

    await base.OnDisconnectedAsync(exception);
}
```

Sending to `Clients.Group($"user-{userId}")` then reaches every connection that user has open. For chat rooms, add the connection to a room group after checking the user is allowed in that room; never trust a group name sent by the client without an authorization check.

## Token Expiry Mid-Connection

A connection can outlive its access token. SignalR does not re-authenticate an open connection, so a token that expires after connect keeps working until the connection drops. Mitigations: keep access tokens short-lived, force reconnects from the client when a refresh happens, and re-check authorization inside sensitive hub methods (for example, verify room membership on every send, not only on connect).

## Key Takeaways

- Browsers cannot set headers on a WebSocket handshake, so the JWT goes in the query string as `access_token`.
- `OnMessageReceived` reads the query token, but only for hub paths; the API keeps using the header.
- `[Authorize]` works on hubs and hub methods, and `Context.UserIdentifier` gives the current user id.
- Groups per user (`user-{userId}`) deliver messages to all of a user's devices.
- Keep tokens short-lived and re-check authorization in sensitive methods, since a connection can outlive its token.
