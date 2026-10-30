# ASP.NET Core Dependency Injection

ASP.NET Core has dependency injection (DI) built in. Instead of creating its own dependencies, a class declares what it needs in its constructor, and the framework supplies those objects at runtime. This keeps classes loosely coupled and easy to test.

## The three lifetimes

- **Transient** — a new instance every time it is requested. Good for lightweight, stateless services.
- **Scoped** — one instance per HTTP request. This is the usual choice for services used inside a single request, such as a database context.
- **Singleton** — one instance for the whole lifetime of the application. Use it for stateless, thread-safe services only; a singleton must never depend on a scoped service.

## Registering services

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<ICache, MemoryCache>();

var app = builder.Build();
```

## Constructor injection

```csharp
public class OrderController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrderController(IOrderService orders)
    {
        _orders = orders;
    }
}
```

The controller never creates an `OrderService` itself, so in a test we can pass a fake `IOrderService` instead of the real one.

## Key takeaways

1. Depend on interfaces, not concrete classes.
2. Match the lifetime to how the service is used — when in doubt, prefer scoped for request work.
3. Avoid the captive-dependency trap: a long-lived service holding a short-lived one (for example, a singleton depending on a scoped service) causes stale or incorrect state.
