# ASP.NET Core Session State

Session state stores small pieces of per-user data on the server (or in a distributed cache) and tracks the user with a cookie that holds only a session ID. It is the right tool for a shopping cart ID or a multi-step form draft — and the wrong tool for anything large, secret, or permanent.

## Setup: AddSession and AddDistributedMemoryCache

Session requires a backing `IDistributedCache`. For a single server, the in-memory implementation is enough; for multiple servers, swap it for Redis (see below). Middleware order matters: `UseSession` must come after `UseRouting` and before the endpoints that use it.

```csharp
// Program.cs
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20); // default is 20 minutes
    options.Cookie.HttpOnly = true;                 // default: not readable from JavaScript
    options.Cookie.IsEssential = true;              // needed for GDPR consent flows
    options.Cookie.Name = ".MyApp.Session";
});

var app = builder.Build();

app.UseRouting();
app.UseSession();       // after routing, before endpoints
app.MapControllers();
app.Run();
```

The session cookie carries only an opaque ID; the data itself lives server-side in the cache. The default idle timeout is 20 minutes: each request that touches the session resets the countdown.

## ISession Get and Set

`ISession` stores byte arrays and strings natively. Everything else must be serialized by hand — JSON is the standard choice.

```csharp
public class CartController : Controller
{
    private const string CartIdKey = "CartId";
    private const string CartKey = "Cart";

    public IActionResult AddToCart(int productId)
    {
        // Strings: GetString / SetString
        var cartId = HttpContext.Session.GetString(CartIdKey);
        if (cartId is null)
        {
            cartId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString(CartIdKey, cartId);
        }

        // Integers: GetInt32 / SetInt32
        var count = HttpContext.Session.GetInt32("ItemCount") ?? 0;
        HttpContext.Session.SetInt32("ItemCount", count + 1);

        return Ok(new { cartId, itemCount = count + 1 });
    }
}
```

## Storing Objects via JSON

There is no built-in `SetObject`. Serialize to JSON and store the string, and guard the deserialization against a missing or corrupt entry.

```csharp
public static class SessionExtensions
{
    public static void SetObject<T>(this ISession session, string key, T value)
        => session.SetString(key, JsonSerializer.Serialize(value));

    public static T? GetObject<T>(this ISession session, string key)
    {
        var json = session.GetString(key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }
}

// Usage
var cart = new Cart { Items = new() { new CartItem(42, 2) } };
HttpContext.Session.SetObject("Cart", cart);

var restored = HttpContext.Session.GetObject<Cart>("Cart");
```

Session is loaded lazily and committed at the end of the request, so changes made after the response starts are lost. Keep stored objects small: the whole session is serialized and written on every request that modifies it, so a large object is a per-request tax.

## Worked Example: Counting Requests Against the Timeout

With the default `IdleTimeout` of 20 minutes, trace a user's requests:

- 10:00 — first request creates the session; expiry is 10:20.
- 10:15 — request touches the session; expiry moves to 10:35 (15 + 20 = 35).
- 10:30 — request touches the session; expiry moves to 10:50 (30 + 20 = 50).
- 11:00 — next request arrives; 11:00 is after 10:50, so the session has expired and a new empty session is created.

The session survived 10:00–10:50 (50 minutes of wall-clock time) because each access reset the 20-minute idle window. The arithmetic to check is the last step: 10:30 + 20 minutes = 10:50, and 11:00 > 10:50, so the data is gone. Anything the app needs after a gap longer than the timeout must live in a database, not in session.

## Distributed Session with Redis

On more than one server, the in-memory cache breaks: a request that lands on a different server finds no session. Replace the cache registration and the rest of the code is unchanged.

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MyApp:";
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```

Session data must now be serializable and small enough to round-trip to Redis on every request. Sticky sessions are an alternative, but they defeat load balancing and are a workaround, not a design.

## Cookie Options

| Option | Default | Why it matters |
|---|---|---|
| `Cookie.HttpOnly` | `true` | Blocks JavaScript from reading the session ID (XSS defence) |
| `Cookie.SecurePolicy` | `SameAsRequest` | Set to `Always` in production so the ID never travels over HTTP |
| `Cookie.SameSite` | `Lax` | `Strict` blocks the cookie on cross-site navigation; `None` requires `Secure` |
| `Cookie.IsEssential` | `false` | `true` exempts the cookie from GDPR consent checks |
| `IdleTimeout` | 20 minutes | Sliding window, reset on each access |

## Security Notes

- Never store secrets in session: no passwords, tokens, or API keys. The data lives in a cache that other services may read, and the session ID is the only thing protecting it.
- The session ID is a bearer credential: anyone who holds it is the user. Regenerate it on login (sign out and back in, or call `HttpContext.Session.Clear()` plus re-authentication) so a pre-login ID cannot be reused after privilege changes.
- Keep sessions small and short-lived. Session is for transient workflow state, not a database: if the data must survive a server restart, a browser close, or the 20-minute timeout, persist it properly.

## Key Takeaways

- Register `AddDistributedMemoryCache` (or Redis) plus `AddSession`, and place `UseSession` after routing and before endpoints.
- `ISession` stores strings, integers, and byte arrays; serialize objects to JSON with `SetString`/`GetString`.
- The 20-minute idle timeout is a sliding window: each access resets it, and a gap longer than the timeout destroys the session.
- For multi-server apps, use the Redis distributed cache so any server can serve any session.
- Never store secrets in session, and treat the session ID as a credential that must be regenerated on login.
