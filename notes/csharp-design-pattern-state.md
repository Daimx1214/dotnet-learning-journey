# State Pattern

The State pattern lets an object change its behavior when its internal state changes, by moving each state's behavior into its own class. The object appears to change its class at runtime. It replaces long chains of `if` and `switch` statements that check the current state before every action.

## The Problem It Solves

An order moves through Pending, Paid, Shipped, and Delivered. A naive implementation puts a `switch (order.Status)` inside every method: `Pay()`, `Ship()`, `Cancel()`. Adding a fifth state, Refunded, means finding and editing every switch, and forgetting one produces a bug where a delivered order can still be cancelled. With the State pattern, each state is a class that only allows the transitions that make sense for it.

## Example: Order State Machine

The state interface declares every action an order can take:

```csharp
public interface IOrderState
{
    void Pay(Order order);
    void Ship(Order order);
    void Deliver(Order order);
    void Cancel(Order order);
}
```

The context object holds the current state and delegates to it:

```csharp
public sealed class Order
{
    public IOrderState State { get; private set; } = new PendingState();
    public string Status => State.GetType().Name.Replace("State", string.Empty);

    internal void ChangeState(IOrderState state) => State = state;

    public void Pay() => State.Pay(this);
    public void Ship() => State.Ship(this);
    public void Deliver() => State.Deliver(this);
    public void Cancel() => State.Cancel(this);
}
```

Each state allows only its own valid transitions and rejects the rest:

```csharp
public sealed class PendingState : IOrderState
{
    public void Pay(Order order) => order.ChangeState(new PaidState());
    public void Ship(Order order) => throw new InvalidOperationException("Cannot ship an unpaid order.");
    public void Deliver(Order order) => throw new InvalidOperationException("Cannot deliver an unpaid order.");
    public void Cancel(Order order) => order.ChangeState(new CancelledState());
}

public sealed class PaidState : IOrderState
{
    public void Pay(Order order) => throw new InvalidOperationException("Order is already paid.");
    public void Ship(Order order) => order.ChangeState(new ShippedState());
    public void Deliver(Order order) => throw new InvalidOperationException("Cannot deliver an order that has not shipped.");
    public void Cancel(Order order) => order.ChangeState(new CancelledState());
}

public sealed class ShippedState : IOrderState
{
    public void Pay(Order order) => throw new InvalidOperationException("Order is already paid.");
    public void Ship(Order order) => throw new InvalidOperationException("Order is already shipped.");
    public void Deliver(Order order) => order.ChangeState(new DeliveredState());
    public void Cancel(Order order) => throw new InvalidOperationException("Cannot cancel an order that has shipped.");
}

public sealed class DeliveredState : IOrderState
{
    public void Pay(Order order) => throw new InvalidOperationException("Order is already paid.");
    public void Ship(Order order) => throw new InvalidOperationException("Order already delivered.");
    public void Deliver(Order order) => throw new InvalidOperationException("Order already delivered.");
    public void Cancel(Order order) => throw new InvalidOperationException("Cannot cancel a delivered order.");
}

public sealed class CancelledState : IOrderState
{
    public void Pay(Order order) => throw new InvalidOperationException("Order is cancelled.");
    public void Ship(Order order) => throw new InvalidOperationException("Order is cancelled.");
    public void Deliver(Order order) => throw new InvalidOperationException("Order is cancelled.");
    public void Cancel(Order order) => throw new InvalidOperationException("Order is already cancelled.");
}
```

Tracing the happy path by hand: a new `Order` starts in `PendingState`. `Pay()` moves it to `PaidState`. `Ship()` moves it to `ShippedState`. `Deliver()` moves it to `DeliveredState`. Calling `Ship()` before `Pay()` throws, because `PendingState.Ship` rejects it. No `if` in `Order` itself ever checks the status.

## State vs Switch Statements

- A switch scatters transition rules across every method. A new state means editing every method.
- A state class keeps one state's rules in one file. A new state means adding one file and one transition.
- Invalid transitions fail loudly at the exact place they are attempted, instead of silently doing nothing.

## State vs Strategy

The two patterns look identical in structure: a context delegates to an object it holds. The difference is intent:

- **State**: the object changes its own state as a result of its own behavior, and the set of states is fixed by the domain.
- **Strategy**: the caller picks an algorithm, and the context never changes it by itself.

## Key Takeaways

- State moves each state's behavior into its own class, so transitions live with the state that allows them.
- Invalid transitions become compile-time structure plus a clear runtime exception, not a forgotten switch case.
- The context object stays thin: it holds the current state and delegates every action to it.
- Use it when an object has three or more states with different rules, such as orders, payments, or document approval.
- Keep it for behavior that changes with state; if only a single value differs per state, a simple enum is enough.
