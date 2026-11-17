# Keyed Services in .NET 8 (C#)

Dependency injection in .NET traditionally resolves one implementation per service type: register `IPaymentProvider` once, and every consumer gets the same implementation. That breaks down the moment you have two real implementations of one interface — Stripe and PayPal, or two database providers — and different parts of the app need different ones. The old workaround was a factory or a named string lookup, both of which pushed the selection logic into your code. .NET 8 added *keyed services*: register multiple implementations under different keys, and ask for the one you want by key.

## Registering Keyed Services

`AddKeyedSingleton`, `AddKeyedScoped` and `AddKeyedTransient` mirror the normal lifetime methods but take a key — usually a string, though any object works. The key is part of the registration, so two implementations of the same interface coexist peacefully.

```csharp
public interface IPaymentProvider
{
    string Name { get; }
    Task<string> ChargeAsync(decimal amount, string currency);
}

public sealed class StripeProvider : IPaymentProvider
{
    public string Name => "Stripe";
    public Task<string> ChargeAsync(decimal amount, string currency) =>
        Task.FromResult($"stripe-ch-{Guid.NewGuid():N}");
}

public sealed class PayPalProvider : IPaymentProvider
{
    public string Name => "PayPal";
    public Task<string> ChargeAsync(decimal amount, string currency) =>
        Task.FromResult($"paypal-txn-{Guid.NewGuid():N}");
}

builder.Services.AddKeyedSingleton<IPaymentProvider, StripeProvider>("stripe");
builder.Services.AddKeyedSingleton<IPaymentProvider, PayPalProvider>("paypal");
```

## Resolving by Key

Consumers ask for a specific key with the `[FromKeyedServices]` attribute on a constructor parameter, or resolve it manually from the provider. The attribute keeps the selection declarative — the class says which implementation it needs, and the container supplies it.

```csharp
public sealed class CheckoutService
{
    private readonly IPaymentProvider _provider;

    // This checkout flow always uses Stripe.
    public CheckoutService([FromKeyedServices("stripe")] IPaymentProvider provider)
    {
        _provider = provider;
    }

    public Task<string> CheckoutAsync(decimal amount) =>
        _provider.ChargeAsync(amount, "USD");
}

// Resolving manually, e.g. when the key comes from a request at runtime:
public sealed class PaymentRouter
{
    private readonly IServiceProvider _services;

    public PaymentRouter(IServiceProvider services) => _services = services;

    public IPaymentProvider GetProvider(string key) =>
        _services.GetRequiredKeyedService<IPaymentProvider>(key);
    // GetProvider("paypal") returns the PayPalProvider; an unknown key throws.
}
```

There are also non-throwing variants (`GetKeyedService`, returning `null` for a missing key) and an enumerable form: `GetKeyedServices<IPaymentProvider>()` returns every registered implementation, which is handy for a page that lists all available payment methods.

## Choosing the Key at Runtime

The `[FromKeyedServices]` attribute is compile-time, so it fits when a consumer always uses the same implementation. When the choice depends on runtime data — the customer picked PayPal at checkout — resolve from `IServiceProvider` inside a small router, or register a factory that reads the request. A clean shape is to keep the router thin and let it delegate:

```csharp
public sealed class PaymentService
{
    private readonly IServiceProvider _services;

    public PaymentService(IServiceProvider services) => _services = services;

    public async Task<string> ChargeAsync(string providerKey, decimal amount)
    {
        IPaymentProvider provider = _services.GetRequiredKeyedService<IPaymentProvider>(providerKey);
        return await provider.ChargeAsync(amount, "USD");
    }
}

// Usage: await paymentService.ChargeAsync("stripe", 49.99m);
//        await paymentService.ChargeAsync("paypal", 49.99m);
```

Resolving directly from `IServiceProvider` is the service-locator pattern, so keep it confined to one small router class rather than sprinkling it through the codebase — the rest of the app should still receive its dependencies by constructor injection.

## Key Takeaways

- Keyed services (.NET 8) let you register several implementations of one interface under different keys, e.g. `"stripe"` and `"paypal"` for `IPaymentProvider`.
- Register with `AddKeyedSingleton` / `AddKeyedScoped` / `AddKeyedTransient`; resolve with `[FromKeyedServices("key")]` or `GetRequiredKeyedService<T>(key)`.
- Use `[FromKeyedServices]` when a consumer always needs the same implementation; resolve from `IServiceProvider` when the key depends on runtime data.
- `GetKeyedServices<T>()` returns every registered implementation — useful for listing all available options.
- Keys can be any object, but strings are the clearest choice; an unknown key throws with `GetRequiredKeyedService` and returns `null` with `GetKeyedService`.
- Confine direct `IServiceProvider` resolution to a thin router — the rest of the app should keep constructor injection.
