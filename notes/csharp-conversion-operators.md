# Conversion Operators in C#

User-defined conversion operators let a type convert to and from another type with cast syntax. They make domain types such as `Money` or `Distance` ergonomic, but a surprising implicit conversion is a bug factory, so the choice between `implicit` and `explicit` matters.

## Implicit vs Explicit

- `implicit`: the conversion happens silently, without a cast. Use it only when the conversion is safe, lossless, and unsurprising.
- `explicit`: the conversion requires a cast, which signals to the reader that something is happening, possibly with loss or failure.

```csharp
public readonly struct Distance
{
    public double Meters { get; }

    public Distance(double meters) => Meters = meters;

    // Safe and lossless: kilometres are just a scaled view of metres
    public static implicit operator double(Distance d) => d.Meters;

    // Lossy: converting metres to whole kilometres discards the remainder
    public static explicit operator int(Distance d) => (int)(d.Meters / 1000);

    // Constructing from a double is unambiguous, so implicit is fine
    public static implicit operator Distance(double meters) => new(meters);
}

Distance d = 1500;            // implicit: double -> Distance
double metres = d;            // implicit: Distance -> double (1500)
int wholeKm = (int)d;         // explicit cast required: 1
```

## When to Define Them

Define a conversion operator when:

- The two types represent the same value in different units or forms (metres/kilometres, cents/dollars).
- The conversion is total: it succeeds for every possible input value.
- The conversion is the obvious one. If two different conversions are both plausible, use a named method (`ToKilometres()`) instead of an operator.

Do not define a conversion just to save typing. A `Money` type that implicitly converts to `decimal` invites code that silently drops the currency, which is exactly the bug the type exists to prevent.

## A Money Example

```csharp
public readonly struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);

    // Explicit only: converting to a bare decimal throws away the currency,
    // so the cast forces the reader to acknowledge it.
    public static explicit operator decimal(Money m) => m.Amount;

    // No implicit conversion from decimal: a bare number has no currency.
    // Construction must name the currency explicitly:
    //   var price = new Money(49.99m, "USD");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add different currencies.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }
}

var price = new Money(49.99m, "USD");
decimal raw = (decimal)price; // 49.99, currency deliberately discarded
```

## Dangers of Implicit Conversions

Implicit conversions compose silently, and the compiler will chain them in ways the author never intended:

```csharp
public readonly struct Percentage
{
    public double Value { get; }
    public Percentage(double value) => Value = value;

    // Dangerous: now any double silently becomes a Percentage,
    // and a Percentage silently becomes a double.
    public static implicit operator Percentage(double v) => new(v);
    public static implicit operator double(Percentage p) => p.Value;
}

void ApplyDiscount(Percentage p) { /* ... */ }

ApplyDiscount(0.2);   // Is 0.2 a fraction (20%) or 0.2 percent? Ambiguous.
ApplyDiscount(20);    // Also compiles. Same call, wildly different meaning.
```

Because both `0.2` and `20` compile, the reader cannot tell which the caller meant, and the compiler will not help. A named factory (`Percentage.FromFraction(0.2)` or `Percentage.FromPercent(20)`) removes the ambiguity entirely.

Other dangers:

- Implicit conversions hide expensive work. If the conversion allocates or parses, a cast-free assignment makes that cost invisible.
- They interfere with overload resolution. Adding an implicit conversion can silently change which overload the compiler picks for existing call sites.
- A type may define at most one implicit conversion to a given target type, and conversions to or from `object` are not allowed.

## Rules of Thumb

- Prefer `explicit` unless the conversion is provably safe, lossless, and unique.
- Never define an implicit conversion that loses information (narrowing, dropping currency, truncating).
- Never define implicit conversions in both directions between two types; the round-trip invites ambiguity.
- If the conversion can fail, do not use an operator at all: use `TryParse` or a named method that returns a `bool`.

## Key Takeaways

- `implicit` converts silently; `explicit` requires a cast and signals loss or risk.
- Define conversions only for total, obvious, same-value-different-form pairs.
- A `Money` type should convert to `decimal` explicitly, never implicitly, so the currency is never dropped by accident.
- Implicit conversions in both directions, or between ambiguous units, create bugs the compiler cannot catch; use named factories instead.
- If a conversion can fail, use `TryParse` or a named method, not an operator.
