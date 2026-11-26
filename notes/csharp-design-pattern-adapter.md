# Adapter Pattern

The Adapter pattern converts the interface of one class into an interface a client expects. It lets classes work together that otherwise could not because their interfaces are incompatible, without changing either side. In .NET you meet it constantly: a legacy library, a third-party SDK, or an old file format that must plug into new code.

## The Problem It Solves

Imagine an app that logs through `ILogger<T>` everywhere, but an old internal library only exposes a `LegacyLogger` class with a `WriteLog(string message, int severity)` method. Rewriting every call site is risky, and rewriting the legacy class may break other consumers. An adapter wraps the legacy class and exposes the interface the rest of the app already uses.

## Class Adapter vs Object Adapter

There are two forms, and C# strongly favors one of them:

- **Object adapter (composition)**: the adapter holds an instance of the adaptee and forwards calls to it. This is the standard form in C#. It works with any adaptee, even sealed classes, and lets one adapter wrap several adaptees.
- **Class adapter (inheritance)**: the adapter inherits from the adaptee and implements the target interface. C# only allows single inheritance, so this form is rare and only possible when the adaptee is a non-sealed class and the adapter has no other base class to inherit from.

Prefer the object adapter unless there is a specific reason to inherit.

## Example: Adapting a Legacy Logger

The target interface is what the new code expects:

```csharp
public interface IAppLogger
{
    void LogInfo(string message);
    void LogError(string message);
}
```

The legacy class we cannot change:

```csharp
// Third-party or legacy code, do not modify.
public sealed class LegacyLogger
{
    public void WriteLog(string message, int severity)
    {
        // severity: 1 = info, 2 = error
        Console.WriteLine($"[{severity}] {message}");
    }
}
```

The adapter translates between the two:

```csharp
public sealed class LegacyLoggerAdapter : IAppLogger
{
    private readonly LegacyLogger _legacy;

    public LegacyLoggerAdapter(LegacyLogger legacy)
    {
        _legacy = legacy;
    }

    public void LogInfo(string message) => _legacy.WriteLog(message, 1);

    public void LogError(string message) => _legacy.WriteLog(message, 2);
}
```

Client code only ever sees `IAppLogger`:

```csharp
IAppLogger logger = new LegacyLoggerAdapter(new LegacyLogger());
logger.LogInfo("Order placed");
logger.LogError("Payment failed");
```

Swapping the legacy logger for a real `ILogger<T>`-based implementation later means changing one registration in DI, not every call site.

## When to Use It

- Integrating a third-party library or legacy code whose interface does not match yours.
- Making two independently developed systems work together.
- Adding a new interface on top of an existing class without touching that class.
- Wrapping an external API (payment gateway, SMS provider) behind your own interface so the provider can be swapped.

## When Not to Use It

- When you own both sides and can simply change the interface: an adapter then adds a useless layer.
- When the two interfaces differ in behavior, not just shape. If the adaptee cannot do what the target promises, an adapter hides a real mismatch instead of fixing it.
- As a permanent excuse to avoid refactoring genuinely dead legacy code.

## Adapter vs Related Patterns

- **Adapter** changes an existing interface to one a client expects. **Facade** simplifies a whole subsystem behind one easy interface. **Decorator** adds behavior while keeping the same interface. **Proxy** controls access to an object with the same interface.

## Key Takeaways

- Adapter translates one interface into another so incompatible classes can work together.
- In C#, prefer the object adapter (composition) over the class adapter (inheritance).
- It is the standard way to wrap legacy or third-party code behind your own interface.
- Use it when the mismatch is in shape, not in behavior: if the adaptee cannot do the job, no adapter will fix that.
- Keep the adapter thin: translate the call, map the parameters, and nothing else.
