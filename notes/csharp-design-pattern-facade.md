# Facade Pattern

The Facade pattern provides a single, simple interface to a whole subsystem. It hides the subsystem's complexity behind one class with a few easy methods, so callers do not need to know about the dozen classes, the order they must be called in, or the details of how they connect.

## The Problem It Solves

Checking out an order touches inventory, payment, shipping, and notifications. Without a facade, every controller or service that places an order must call all four in the right order, handle the failure of each step, and know which classes to inject. That logic gets copied everywhere, and changing the checkout flow means editing every caller. A facade owns the sequence once and exposes one method: `PlaceOrder()`.

## Example: Order Checkout Facade

The subsystem classes each do one job and stay independent:

```csharp
public sealed class InventoryService
{
    public bool ReserveItems(int orderId)
    {
        Console.WriteLine($"Reserving items for order {orderId}.");
        return true;
    }
}

public sealed class PaymentService
{
    public bool Charge(int orderId, decimal amount)
    {
        Console.WriteLine($"Charging {amount:C} for order {orderId}.");
        return true;
    }
}

public sealed class ShippingService
{
    public string CreateShipment(int orderId)
    {
        Console.WriteLine($"Creating shipment for order {orderId}.");
        return "TRK-1001";
    }
}

public sealed class NotificationService
{
    public void SendOrderConfirmation(int orderId)
        => Console.WriteLine($"Sending confirmation for order {orderId}.");
}
```

The facade coordinates them and hides the order of operations:

```csharp
public sealed class CheckoutFacade
{
    private readonly InventoryService _inventory;
    private readonly PaymentService _payment;
    private readonly ShippingService _shipping;
    private readonly NotificationService _notifications;

    public CheckoutFacade(InventoryService inventory, PaymentService payment,
        ShippingService shipping, NotificationService notifications)
    {
        _inventory = inventory;
        _payment = payment;
        _shipping = shipping;
        _notifications = notifications;
    }

    public string PlaceOrder(int orderId, decimal amount)
    {
        if (!_inventory.ReserveItems(orderId))
            throw new InvalidOperationException("Items are out of stock.");
        if (!_payment.Charge(orderId, amount))
            throw new InvalidOperationException("Payment failed.");
        var trackingNumber = _shipping.CreateShipment(orderId);
        _notifications.SendOrderConfirmation(orderId);
        return trackingNumber;
    }
}
```

The caller now needs one line instead of four services and the failure rules:

```csharp
var checkout = new CheckoutFacade(
    new InventoryService(), new PaymentService(),
    new ShippingService(), new NotificationService());
var tracking = checkout.PlaceOrder(orderId: 42, amount: 59.99m);
Console.WriteLine($"Order shipped, tracking: {tracking}");
```

Registering the facade and its services in DI keeps the same shape in a real app: the controller injects only `CheckoutFacade`.

## Facade vs Mediator

The two patterns look similar because both sit between the caller and several other objects, but their purpose differs:

- **Facade** simplifies a subsystem for its clients. The subsystem classes do not know about the facade, and they can still be used directly. The facade is a convenient front door.
- **Mediator** centralizes communication between colleagues that need to talk to each other. The colleagues know about the mediator and use it instead of talking directly. The mediator owns the interaction rules.

Rule of thumb: if the classes behind the front door never talk to each other, it is a facade. If they must coordinate with each other, it is a mediator.

## Facade vs Adapter

- **Facade** simplifies an interface to a subsystem and may define a brand-new, easier API.
- **Adapter** converts one existing interface into another existing interface so incompatible code can work together.

A facade is often written fresh for your own subsystem; an adapter is written to bridge code you did not design.

## When to Use It

- A subsystem has many classes and a fixed call order that callers should not need to memorize.
- You want to layer an application: the web layer talks to facades, and facades talk to the domain services.
- You are wrapping a third-party SDK whose API is awkward, and you want one clean entry point for your team.
- Do not use it when the subsystem is already simple, or as a hiding place for business logic that belongs in the domain. The facade coordinates; it should not contain pricing rules or validation logic.
## Key Takeaways

- Facade gives a complex subsystem one simple entry point and hides the call order.
- The subsystem classes stay independent and usable on their own; the facade is only a front door.
- Facade simplifies for the caller, while Mediator coordinates between colleagues that talk to each other.
- Keep the facade thin: coordinate calls and handle step failures, but put business rules in the domain services.
- In ASP.NET Core, a facade is a natural shape for an application service that orchestrates several domain services behind one method.
