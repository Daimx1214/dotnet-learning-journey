# YARP Reverse Proxy in ASP.NET Core

YARP (Yet Another Reverse Proxy) is Microsoft's reverse proxy library for ASP.NET Core. A reverse proxy sits in front of your services and forwards incoming HTTP requests to the right backend — the client talks to the proxy, and the proxy talks to your APIs, websites, or microservices. YARP turns that job into configuration plus a small amount of code, and it is the engine behind the API gateway pattern in modern .NET systems.

It matters because microservices need a single front door: one public URL, one place for TLS, routing, load balancing, and rate limiting, while the individual services stay private and independently deployable.

## Getting Started

Add the YARP package (`Yarp.ReverseProxy`) and register it in `Program.cs`. The simplest setup reads routes and clusters from `appsettings.json`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
```

## Routes and Clusters

Configuration has two halves: **routes** decide which requests match, and **clusters** decide where they go.

```json
{
  "ReverseProxy": {
    "Routes": {
      "products-route": {
        "ClusterId": "products-cluster",
        "Match": {
          "Path": "/api/products/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}",
          "Methods": ["GET", "POST"]
        }
      }
    },
    "Clusters": {
      "products-cluster": {
        "Destinations": {
          "products-1": { "Address": "http://localhost:5001" },
          "products-2": { "Address": "http://localhost:5002" }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "orders-1": { "Address": "http://localhost:5101" }
        }
      }
    }
  }
}
```

A request to `/api/products/42` matches `products-route` and is forwarded to one of the two product-service destinations. The `{**catch-all}` segment captures the rest of the path so `/api/products/42` reaches the backend as `/api/products/42` (or a rewritten path if a transform is configured).

Route matching is richer than paths: you can match on host, headers, query parameters, and methods.

```json
{
  "tenant-route": {
    "ClusterId": "tenant-cluster",
    "Match": {
      "Hosts": ["*.example.com"],
      "Headers": [
        { "Name": "X-Tenant", "Values": ["acme"], "Mode": "ExactHeader" }
      ]
    }
  }
}
```

## Load Balancing

When a cluster has more than one destination, YARP picks one per request. The default policy is `PowerOfTwoChoices` — it samples two healthy destinations and sends the request to the less-loaded one — which is a good default for most systems. Other built-in policies:

| Policy | Behaviour |
|---|---|
| `PowerOfTwoChoices` (default) | Samples two destinations, picks the less loaded |
| `RoundRobin` | Cycles through destinations in order |
| `LeastRequests` | Sends to the destination with the fewest in-flight requests |
| `Random` | Picks a destination at random |
| `First` | Always uses the first healthy destination (active/passive) |

```json
{
  "products-cluster": {
    "LoadBalancingPolicy": "RoundRobin",
    "Destinations": {
      "products-1": { "Address": "http://localhost:5001" },
      "products-2": { "Address": "http://localhost:5002" }
    }
  }
}
```

Session affinity (sticky sessions) is available too, via a cookie that pins a client to one destination — useful when a backend keeps in-memory per-user state, though stateless services should not need it.

## Health Checks

YARP can actively probe destinations and stop sending traffic to unhealthy ones. Configure a health-check path per cluster and YARP re-checks on a schedule.

```json
{
  "products-cluster": {
    "HealthCheck": {
      "Active": {
        "Enabled": true,
        "Interval": "00:00:10",
        "Timeout": "00:00:05",
        "Path": "/health"
      }
    },
    "Destinations": {
      "products-1": { "Address": "http://localhost:5001" },
      "products-2": { "Address": "http://localhost:5002" }
    }
  }
}
```

Passive health checking also exists: if a destination starts failing requests, YARP temporarily removes it from rotation. Combine both — active checks catch a dead service quickly, passive checks react to errors the active probe might miss.

## Transforms

Transforms modify requests and responses as they pass through the proxy. Common built-in transforms: strip or add a path prefix, add or remove headers, and change the request path.

```json
{
  "legacy-route": {
    "ClusterId": "legacy-cluster",
    "Match": { "Path": "/legacy/{**catch-all}" },
    "Transforms": [
      { "PathRemovePrefix": "/legacy" },
      { "RequestHeader": "X-Forwarded-By", "Set": "yarp-gateway" },
      { "ResponseHeader": "Server", "Remove": true }
    ]
  }
}
```

With `PathRemovePrefix`, a request to `/legacy/users/7` reaches the backend as `/users/7`, so the backend never needs to know about the public path prefix. For anything the built-in transforms cannot do, write a custom transform in code — for example, injecting a correlation id or rewriting a body.

```csharp
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            transformContext.ProxyRequest.Headers.Add(
                "X-Correlation-Id",
                Guid.NewGuid().ToString());
            return ValueTask.CompletedTask;
        });
    });
```

## Use Cases

- **API gateway:** one public entry point that routes `/api/products` to the product service, `/api/orders` to the order service, and `/` to the web frontend.
- **Gradual migration:** route new paths to a new service and old paths to a legacy monolith, moving traffic slice by slice.
- **Multiple environments behind one domain:** `/app` to the web app, `/api` to the backend, `/admin` to an internal tool.
- **TLS termination:** the proxy handles HTTPS and forwards plain HTTP internally, simplifying service configuration.

## Rate Limiting and Auth at the Gateway

YARP composes with normal ASP.NET Core middleware, so cross-cutting concerns live in one place. Add rate limiting or authentication before `MapReverseProxy` and it applies to everything behind the gateway.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1)
    });
});

var app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy().RequireRateLimiting("api");
```

Similarly, `RequireAuthorization()` on a route group enforces authentication at the gateway, so unauthenticated requests never reach the backend at all.

## Key Takeaways

- YARP is Microsoft's reverse proxy for ASP.NET Core: one front door that forwards requests to the right backend service.
- Configuration splits into routes (which requests match) and clusters (where they go, with one or more destinations).
- The default load-balancing policy is PowerOfTwoChoices; RoundRobin, LeastRequests, Random, and First are built in.
- Active health checks probe destinations and remove unhealthy ones from rotation; passive checks react to live failures.
- Transforms rewrite paths, add or remove headers, and strip prefixes so backends stay unaware of the public URL shape.
- Rate limiting and authentication compose with YARP at the gateway, giving every service behind it the same protection in one place.
