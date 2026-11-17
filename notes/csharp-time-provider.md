# TimeProvider Abstraction (C#)

`DateTime.Now` and `DateTime.UtcNow` are static calls that read the system clock directly, which makes any code that uses them untestable: a cache-expiry check, a token lifetime, or a "is this coupon still valid?" rule cannot be exercised at a chosen instant without waiting for real time to pass or resorting to fragile hacks. .NET 8 introduced the `TimeProvider` abstraction to fix this. Instead of calling the static clock, you inject a `TimeProvider` and ask it for the current time — and in tests you substitute a fake clock you can move forward at will.

## The Problem with DateTime.UtcNow

Consider a simple in-memory cache entry that expires after 30 minutes. Written against the static clock, the only way to test expiry is to wait 30 minutes, or to expose the expiry time as a parameter and hope the production call site passes the right value. Both are bad: the first is slow, the second leaks an implementation detail into every caller.

```csharp
// Hard to test: the clock is baked in.
public class CacheEntry<T>
{
    public T Value { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public bool IsExpired(TimeSpan lifetime) =>
        DateTime.UtcNow - CreatedAtUtc > lifetime;
}
```

## Injecting TimeProvider

`TimeProvider` is an abstract class with `GetUtcNow()` (returning `DateTimeOffset`), `GetLocalNow()`, and a `TimeZoneInfo` property. The framework registers `TimeProvider.System` — the real clock — in DI by default in .NET 8, so you just take it as a constructor dependency.

```csharp
public class CacheEntry<T>
{
    private readonly TimeProvider _time;

    public CacheEntry(T value, TimeProvider time)
    {
        Value = value;
        _time = time;
        CreatedAtUtc = time.GetUtcNow();
    }

    public T Value { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public bool IsExpired(TimeSpan lifetime) =>
        _time.GetUtcNow() - CreatedAtUtc > lifetime;
}

// Registration — TimeProvider.System is already registered by the host,
// so this is all the wiring you need:
builder.Services.AddSingleton(typeof(CacheEntry<>));
```

Production code behaves exactly as before, because `TimeProvider.System` reads the real clock. The difference is that the dependency is now explicit and replaceable.

## Testing with FakeTimeProvider

The `Microsoft.Extensions.TimeProvider.Testing` package provides `FakeTimeProvider`, a clock whose time you set and advance manually. Tests become deterministic and instant: create an entry, advance the fake clock past the lifetime, and assert expiry — no `Thread.Sleep`, no flaky timing.

```csharp
using Microsoft.Extensions.Time.Testing;

[Fact]
public void Entry_Expires_After_Lifetime()
{
    var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
    var entry = new CacheEntry<string>("hello", fakeTime);

    Assert.False(entry.IsExpired(TimeSpan.FromMinutes(30)));

    fakeTime.Advance(TimeSpan.FromMinutes(29));
    Assert.False(entry.IsExpired(TimeSpan.FromMinutes(30))); // 29 < 30: still fresh.

    fakeTime.Advance(TimeSpan.FromMinutes(2)); // Now 31 minutes have passed.
    Assert.True(entry.IsExpired(TimeSpan.FromMinutes(30)));  // 31 > 30: expired.
}
```

Hand-trace: the entry is created at 10:00. After advancing 29 minutes the clock reads 10:29, so `10:29 − 10:00 = 29 minutes`, which is not greater than 30 — not expired. Advancing 2 more minutes makes the clock 10:31, so `10:31 − 10:00 = 31 minutes`, which is greater than 30 — expired. The boundary is exact because the fake clock moves in whole, controlled steps.

`FakeTimeProvider` also has `SetUtcNow` to jump to an absolute instant, and it can auto-advance on a timer for code that awaits delays — useful when testing retry loops or background services that use `TimeProvider.CreateTimer` instead of `Task.Delay`.

## Where TimeProvider Pays Off

The pattern is worth adopting anywhere time is a *decision input*, not just a timestamp: cache and token expiry, rate-limit windows, scheduled jobs, "trial ends in N days" logic, and `DateOnly`/`TimeOnly` calculations. It is overkill for pure logging timestamps, where the exact value never changes behaviour. A good rule: if a test would need to control the clock to verify the behaviour, inject `TimeProvider`; if the time is only recorded, `DateTimeOffset.UtcNow` at the call site is fine.

## Key Takeaways

- `DateTime.Now` / `DateTime.UtcNow` are static, untestable calls; code that decides things based on time should take a `TimeProvider` instead.
- `TimeProvider` is abstract with `GetUtcNow()`, `GetLocalNow()` and `TimeZoneInfo`; `TimeProvider.System` is the real clock and is registered in DI by default in .NET 8.
- `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) lets tests set and advance the clock deterministically — no sleeps, no flakiness.
- Inject `TimeProvider` wherever time is a decision input (expiry, rate limits, trial windows); plain logging timestamps do not need it.
- Prefer `DateTimeOffset` over `DateTime` when storing instants: it carries the offset, so expiry math stays correct across time zones and DST changes.
- `TimeProvider` also covers timers via `CreateTimer`, so background services and retry loops can be tested with a fake clock too.
