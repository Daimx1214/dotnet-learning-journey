# C# Weak References

A `WeakReference<T>` holds a reference that does not stop the garbage collector from reclaiming the object. It is the tool for caches and event subscriptions that must not keep objects alive forever.

## How WeakReference<T> Works

A normal (strong) reference keeps its target alive. A weak reference lets you *try* to reach the object, and tells you honestly when it is gone.

```csharp
var target = new LargeObject();
var weak = new WeakReference<LargeObject>(target);
target = null;                       // drop the strong reference

GC.Collect();                        // for demo purposes only — never do this in real code

if (weak.TryGetTarget(out var alive))
    Console.WriteLine("Still alive");
else
    Console.WriteLine("Collected");
```

`TryGetTarget` returns `false` once the GC has collected the object. Until then, the object is usable — but between the check and the use, another collection could occur, so hold the returned strong reference in a local and use that, not the weak reference repeatedly.

## When GC Collects the Target

Weak references come in two flavours. A short weak reference (the default) is cleared as soon as the object becomes unreachable, even before finalisation. A long weak reference (`new WeakReference<T>(target, trackResurrection: true)`) survives finalisation and only clears when the object is actually reclaimed — useful when you need to run cleanup after the finaliser.

```csharp
var shortWeak = new WeakReference<LargeObject>(target);                    // default
var longWeak  = new WeakReference<LargeObject>(target, trackResurrection: true);
```

Do not call `GC.Collect()` in production to test this — forcing collections hurts performance. In real code, the weak reference simply becomes invalid whenever the GC next runs and finds the object unreachable.

## Caches and Event Handlers

The classic use is a cache that may hold objects but must not prevent their collection when memory is tight. A `ConditionalWeakTable<TKey, TValue>` is the companion type: it attaches extra data to an object without keeping it alive, and the entry disappears automatically when the key is collected.

```csharp
// A cache that doesn't leak: values are collectible when nothing else uses them
private readonly Dictionary<string, WeakReference<byte[]>> _cache = new();

public byte[]? GetImage(string key)
{
    if (_cache.TryGetValue(key, out var weak) && weak.TryGetTarget(out var data))
        return data;
    return null;
}
```

The other classic use is events: a publisher holding a strong reference to a subscriber's handler keeps the subscriber alive for as long as the publisher lives — a common leak in long-lived services. A weak-event pattern (or `WeakEventManager` in WPF) stores the handler weakly so the subscriber can be collected. `ConditionalWeakTable` is also how frameworks attach state to objects they do not own.

## Key Takeaways

- `WeakReference<T>` lets you reach an object without keeping it alive; `TryGetTarget` tells you if it is gone.
- Short weak references clear when the object becomes unreachable; long ones (trackResurrection) survive finalisation.
- Use weak references for caches that must yield to memory pressure, not for data you need to keep.
- Events are a common leak: a long-lived publisher holding a strong handler reference keeps the subscriber alive.
- `ConditionalWeakTable<TKey, TValue>` attaches data to an object without extending its lifetime.
- Never call `GC.Collect()` in production to manage weak references — let the GC decide when to collect.
