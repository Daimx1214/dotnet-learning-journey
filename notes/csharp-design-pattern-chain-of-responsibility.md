# Chain of Responsibility Pattern in C#

Chain of Responsibility passes a request along a chain of handlers until one of them handles it. The sender does not know which handler will act — it just hands the request to the first link in the chain.

## The Problem It Solves

An expense-approval system has several authority levels: a manager can approve up to $1,000, a director up to $10,000, and a VP any amount. Without the pattern, the caller needs a chain of `if/else` checks that must change every time a level is added or a limit moves. With the pattern, each handler knows only its own limit and the next handler in line.

## The Handler Base Class

Every handler shares the same shape: a reference to the next handler and a method that either handles the request or passes it on.

```csharp
public abstract class Approver
{
    private Approver? _next;

    // Link this handler to the next one in the chain.
    public Approver SetNext(Approver next)
    {
        _next = next;
        return next;
    }

    public void Approve(decimal amount)
    {
        if (CanApprove(amount))
        {
            Console.WriteLine($"{GetType().Name} approved ${amount:N0}");
        }
        else if (_next is not null)
        {
            _next.Approve(amount);
        }
        else
        {
            Console.WriteLine($"No approver could handle ${amount:N0}");
        }
    }

    protected abstract bool CanApprove(decimal amount);
}
```

`SetNext` returns the handler it was given, so links can be chained fluently: `manager.SetNext(director).SetNext(vp)`.

## Concrete Handlers

```csharp
public sealed class Manager : Approver
{
    // A manager may approve expenses up to $1,000.
    protected override bool CanApprove(decimal amount) => amount <= 1_000m;
}

public sealed class Director : Approver
{
    // A director may approve expenses up to $10,000.
    protected override bool CanApprove(decimal amount) => amount <= 10_000m;
}

public sealed class VicePresident : Approver
{
    // A VP may approve any amount.
    protected override bool CanApprove(decimal amount) => true;
}
```

## Building and Using the Chain

```csharp
var manager = new Manager();
var director = new Director();
var vp = new VicePresident();

manager.SetNext(director).SetNext(vp);

manager.Approve(500m);
manager.Approve(5_000m);
manager.Approve(25_000m);
```

The caller always starts at the head of the chain (`manager`) and never asks which level approved the expense.

## Worked Traces

Three requests, three different handlers — the trace for each one:

```text
Manager approved $500
Director approved $5,000
VicePresident approved $25,000
```

Step by step:

- **$500** — Manager checks `500 <= 1000` → true. Approved by Manager. The chain stops; Director and VP never see it.
- **$5,000** — Manager checks `5000 <= 1000` → false, passes to Director. Director checks `5000 <= 10000` → true. Approved by Director.
- **$25,000** — Manager rejects it, Director checks `25000 <= 10000` → false, passes to VP. VP accepts any amount. Approved by VicePresident.

The boundary values are exact: $1,000 is approved by the Manager (the check is `<=`), and $10,000 is approved by the Director. $1,000.01 would move to the Director, and $10,000.01 would move to the VP.

## When to Use It

- More than one object may handle a request, and the handler is not known in advance.
- You want to issue a request without coupling the sender to a specific receiver.
- Handlers should be addable, removable, or reorderable without touching the sender — for example, inserting a `SeniorManager` between Manager and Director is a one-line change.

## Trade-Offs

A request can fall off the end of the chain unhandled if no handler accepts it, so the base class should handle that case (as the `else` branch above does). Debugging can also be harder: the request's path is decided at runtime, not visible in the source. Keep chains short and log which handler acted, as the example does.

## Key Takeaways

- Chain of Responsibility decouples the sender from the receiver: the sender hands a request to the chain and one handler deals with it.
- Each handler either handles the request or forwards it via `SetNext`; the base class owns the forwarding logic.
- Worked traces: $500 is approved by Manager, $5,000 by Director, and $25,000 by VicePresident.
- Boundary checks use `<=`, so exactly $1,000 stays with the Manager and exactly $10,000 stays with the Director.
- New handlers slot into the chain without changing the sender, but always handle the "nobody handled it" case.
