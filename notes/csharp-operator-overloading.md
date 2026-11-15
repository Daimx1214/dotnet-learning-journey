# Operator Overloading in C# (C#)

Operator overloading lets a type define what `+`, `==`, `<`, and other operators mean for its instances. Used well, it makes value types such as money, vectors, and fractions read naturally; used badly, it makes code surprising. The rule is simple: an overloaded operator must do what a reader would expect it to do.

## Overloading Arithmetic Operators

Operators are declared `public static` and must take at least one parameter of the containing type. They are usually defined in pairs.

```csharp
public readonly record struct Money(decimal Amount, string Currency)
{
    // Addition only makes sense for the same currency.
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add different currencies.");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot subtract different currencies.");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal factor) =>
        new(money.Amount * factor, money.Currency);
}
```

`var total = new Money(10, "USD") + new Money(5, "USD");` now gives 15 USD. Note the `*` overload above only works as `money * factor`; for `factor * money` you must declare a second overload with the parameters swapped.

## Comparison Operators and Equality

Comparison operators must be overloaded in pairs: `==` with `!=`, `<` with `>`, `<=` with `>=`. And `==` must agree with `Equals` and `GetHashCode` — if two objects are `==`, they must be `Equals` and produce the same hash code, or dictionaries and hash sets will behave inconsistently.

```csharp
public readonly struct Fraction : IEquatable<Fraction>
{
    public int Numerator { get; }
    public int Denominator { get; }

    public Fraction(int numerator, int denominator)
    {
        if (denominator == 0) throw new ArgumentException("Denominator cannot be zero.");
        Numerator = numerator;
        Denominator = denominator;
    }

    public static bool operator ==(Fraction left, Fraction right) =>
        left.Numerator * right.Denominator == right.Numerator * left.Denominator;

    public static bool operator !=(Fraction left, Fraction right) => !(left == right);

    public static bool operator <(Fraction left, Fraction right) =>
        left.Numerator * right.Denominator < right.Numerator * left.Denominator;

    public static bool operator >(Fraction left, Fraction right) => right < left;

    public bool Equals(Fraction other) => this == other;
    public override bool Equals(object? obj) => obj is Fraction other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
}
```

Comparing by cross-multiplication avoids floating-point error: `1/3 == 2/6` is true here, while `(double)1/3 == (double)2/6` can surprise you. Records and record structs generate `==`, `Equals`, and `GetHashCode` for you, which is one reason to prefer them for value types.

## Implicit and Explicit Conversions

Conversion operators let a type convert to and from other types:

```csharp
public readonly struct Celsius
{
    public double Degrees { get; }
    public Celsius(double degrees) => Degrees = degrees;

    // Implicit: always safe, never loses information or throws.
    public static implicit operator Fahrenheit(Celsius c) =>
        new(c.Degrees * 9.0 / 5.0 + 32);

    // Explicit: may lose precision, so the caller must cast.
    public static explicit operator int(Celsius c) => (int)Math.Round(c.Degrees);
}

Celsius c = new(100);
Fahrenheit f = c;        // implicit, no cast needed
int whole = (int)c;      // explicit, cast required
```

Use `implicit` only for conversions that cannot fail and cannot lose data. Anything lossy, narrowing, or throwing belongs in `explicit`. Never define both an implicit and an explicit conversion between the same two types.

## Rules and Pitfalls

- You cannot overload `=`, `&&`, `||`, `?:`, `?.`, `=>`, or member access. Overloading `&&` and `||` is impossible, but overloading `&`, `|`, `true`, and `false` together enables conditional-operator behaviour — rarely worth it.
- You cannot change precedence or arity: `+` stays binary, `-` can be unary or binary, and `+` always binds tighter than `==`.
- Overloads must be symmetric in behaviour: `a == b` and `b == a` must agree, and `a < b` must be the opposite of `a >= b`.
- Do not overload operators to do something unrelated to their name. A `+` that deletes a record or a `==` that writes to a database will confuse every reader and every tool.
- Overloading is resolved at compile time from the static type, not the runtime type, so it does not behave polymorphically like virtual methods.

## Key Takeaways

- Overload operators in pairs (`==`/`!=`, `<`/`>`), and always keep `==`, `Equals`, and `GetHashCode` consistent.
- Prefer records or record structs for value types — they generate the equality members for you.
- Use `implicit` only for safe, lossless conversions; everything else is `explicit`.
- An operator must do the obvious thing: never overload `+` or `==` for an unrelated side effect.
