# Observer Pattern (C# Design Patterns)

The observer pattern defines a one-to-many relationship: when one object (the *subject*) changes state, all its dependents (the *observers*) are notified automatically. It is the pattern behind event-driven UI, stock-price feeds, and pub/sub messaging. In C# you rarely need to build it from scratch — the language has events and the `IObserver<T>` / `IObservable<T>` interfaces built in — but understanding the classic form makes the built-in options far easier to use well.

## The Classic Form: IObserver and IObservable

.NET ships the observer pattern as two interfaces in `System`. The subject implements `IObservable<T>` and hands out a disposable subscription; observers implement `IObserver<T>` and receive `OnNext`, `OnError`, and `OnCompleted` calls.

```csharp
public class StockTicker : IObservable<decimal>
{
    private readonly List<IObserver<decimal>> _observers = new();
    private decimal _price;

    public decimal Price
    {
        get => _price;
        set
        {
            _price = value;
            foreach (var observer in _observers.ToList())
                observer.OnNext(value); // Push the new price to every observer.
        }
    }

    public IDisposable Subscribe(IObserver<decimal> observer)
    {
        if (!_observers.Contains(observer)) _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }

    private sealed class Unsubscriber(List<IObserver<decimal>> observers, IObserver<decimal> observer)
        : IDisposable
    {
        public void Dispose() => observers.Remove(observer);
    }
}

public class PriceAlert : IObserver<decimal>
{
    private readonly decimal _threshold;
    public PriceAlert(decimal threshold) => _threshold = threshold;

    public void OnNext(decimal price)
    {
        if (price >= _threshold)
            Console.WriteLine($"Alert: price hit {price:C} (threshold {_threshold:C})");
    }

    public void OnError(Exception error) => Console.WriteLine($"Feed error: {error.Message}");
    public void OnCompleted() => Console.WriteLine("Feed ended.");
}
```

## The Idiomatic C# Form: Events

For most .NET code, events are the simpler observer. The subject exposes an event, observers attach handlers with `+=`, and the subject raises the event when state changes. There is no interface to implement and no subscription object to manage — the delegate list *is* the observer list.

```csharp
public class StockTickerEvents
{
    private decimal _price;
    public event EventHandler<decimal>? PriceChanged;

    public decimal Price
    {
        get => _price;
        set
        {
            if (_price == value) return; // No change, no notification.
            _price = value;
            PriceChanged?.Invoke(this, value);
        }
    }
}

// Usage:
var ticker = new StockTickerEvents();
ticker.PriceChanged += (sender, price) => Console.WriteLine($"New price: {price:C}");
ticker.Price = 142.50m; // Prints: New price: $142.50
```

The guard `if (_price == value) return;` matters: observers should only be woken when something actually changed. Events also need care around lifetime — a subscriber that never unsubscribes (`-=`) keeps the publisher alive through the delegate, which is a classic source of memory leaks in long-lived UI code.

## Events vs IObserver

| Aspect | Events | IObserver / IObservable |
|--------|--------|-------------------------|
| Setup | Minimal — declare an event, attach with `+=` | More ceremony — interfaces, subscription, unsubscriber |
| Completion and errors | No built-in channel | `OnCompleted` and `OnError` are part of the contract |
| Composition | Hard to chain or transform | Composes well (the basis of Reactive Extensions / Rx) |
| Unsubscribe | `-=` | Dispose the returned `IDisposable` |
| Best for | UI callbacks, simple notifications | Data streams, push-based pipelines |

Use events for straightforward "something happened" notifications. Reach for `IObservable<T>` when the stream needs operators — filtering, throttling, combining multiple sources — which is where Rx (`System.Reactive`) takes over.

## A Real Use Case: Notification System

A stock-price feed is the textbook example because it shows why the pattern earns its keep: the ticker knows nothing about its consumers. A price alert, a logging service, and a dashboard widget can all subscribe independently, and adding a fourth observer touches no existing code — the open/closed principle in action. The same shape fits order-status updates, file-system watchers (`FileSystemWatcher` raises events), and UI data binding, where a view observes a view-model and re-renders on change.

## Key Takeaways

- The observer pattern decouples the subject from its dependents: the subject only knows observers through an interface or an event, never their concrete types.
- `IObservable<T>` / `IObserver<T>` is the formal .NET form, with built-in error and completion channels; events are the lighter, idiomatic form for simple notifications.
- Always notify only on real change, and always unsubscribe — a forgotten event subscription keeps the publisher alive and leaks memory.
- Adding a new observer should never require changing the subject; if it does, the pattern is not doing its job.
- Reach for Rx when the stream needs filtering, throttling, or composition; plain events cover everything else.
