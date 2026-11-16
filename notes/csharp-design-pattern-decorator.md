# C# Design Pattern: Decorator (C#)

The Decorator pattern lets you add behaviour to an object without changing its class and without subclassing it. You wrap the original object in another object that implements the same interface, forwards the calls it does not care about, and adds its own behaviour before or after delegating. Because decorators compose — you can wrap a decorator in another decorator — you get combinations of behaviour (logging + caching + retry) that would need a combinatorial explosion of subclasses with inheritance.

## The Classic Shape

The core is an interface, a concrete component, and an abstract decorator that holds a reference to the wrapped component:

```csharp
// The shared contract. Both the real component and every decorator implement it.
public interface INotifier
{
    void Send(string message);
}
public class EmailNotifier : INotifier
{
    public void Send(string message) => Console.WriteLine($"Email sent: {message}");
}
// Abstract decorator: implements the interface and forwards to the wrapped component.
public abstract class NotifierDecorator : INotifier
{
    protected readonly INotifier _inner;
    protected NotifierDecorator(INotifier inner) => _inner = inner;
    public virtual void Send(string message) => _inner.Send(message);
}
// Concrete decorators add behaviour around the delegated call.
public class SmsNotifierDecorator : NotifierDecorator
{
    public SmsNotifierDecorator(INotifier inner) : base(inner) { }
    public override void Send(string message)
    {
        base.Send(message);
        Console.WriteLine($"SMS sent: {message}");
    }
}
public class LoggingNotifierDecorator : NotifierDecorator
{
    public LoggingNotifierDecorator(INotifier inner) : base(inner) { }
    public override void Send(string message)
    {
        Console.WriteLine($"[LOG] Sending notification at {DateTime.Now:HH:mm:ss}");
        base.Send(message);
        Console.WriteLine("[LOG] Notification delivered.");
    }
}
```

Stacking decorators is just nesting constructors — the outermost runs first:

```csharp
INotifier notifier = new LoggingNotifierDecorator(
    new SmsNotifierDecorator(new EmailNotifier()));
notifier.Send("Your order has shipped.");
// [LOG] Sending notification at 14:32:05
// Email sent: Your order has shipped.
// SMS sent: Your order has shipped.
// [LOG] Notification delivered.
```

## Decorating a Service: Logging and Caching

The most common real-world use is wrapping a service interface — a repository, an API client, a pricing service — with cross-cutting concerns. The consumer only ever sees the interface, so the decorators are invisible to callers and trivial to register in a DI container.

```csharp
public interface IProductService
{
    Product? GetById(int id);
}

public class ProductService : IProductService
{
    // Pretend this hits a database or an external API.
    public Product? GetById(int id) { Thread.Sleep(50); return new Product(id, $"Product {id}"); }
}

public record Product(int Id, string Name);

// Caching decorator: short-circuits the call when the result is already known.
public class CachingProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly Dictionary<int, Product> _cache = new();

    public CachingProductService(IProductService inner) => _inner = inner;

    public Product? GetById(int id)
    {
        if (_cache.TryGetValue(id, out Product? cached)) return cached;
        Product? product = _inner.GetById(id);
        if (product is not null) _cache[id] = product;
        return product;
    }
}

// Logging decorator: measures the call and records failures.
public class LoggingProductService : IProductService
{
    private readonly IProductService _inner;
    public LoggingProductService(IProductService inner) => _inner = inner;
    public Product? GetById(int id)
    {
        var sw = Stopwatch.StartNew();
        Product? product = _inner.GetById(id);
        Console.WriteLine($"GetById({id}) took {sw.ElapsedMilliseconds}ms");
        return product;
    }
}
```

## Decorator vs Inheritance

Inheritance bakes behaviour into the class hierarchy at compile time: a `LoggedEmailNotifier` subclass can never be reused for SMS, and adding a third behaviour means writing a new subclass for every combination. Decorators are composed at runtime instead, so the same logging decorator wraps any `INotifier`, and the combination is chosen by the caller. The price is indirection: a long decorator chain is harder to debug, and identity checks like `is EmailNotifier` fail on the wrapper. Decorators also only work well when the wrapped type is behind an interface — wrapping a concrete class with many methods means forwarding all of them. The .NET framework itself is built on decorators: `Stream` wrappers such as `BufferedStream`, `GZipStream`, and `CryptoStream` all decorate another `Stream`, and ASP.NET Core middleware is the same idea applied to the request pipeline.

## Key Takeaways

- A decorator implements the same interface as the object it wraps and delegates to it, adding behaviour before or after the call.
- Decorators compose at runtime: logging, caching, and retry can be stacked in any order without new subclasses.
- The pattern shines for cross-cutting concerns around a service interface, and it is how `Stream` wrappers and ASP.NET Core middleware work.
- Prefer it over inheritance when behaviours must be combined freely; accept the extra indirection and the loss of concrete-type identity.
- Keep decorators thin and single-purpose — one concern per decorator, so chains stay readable and testable.
