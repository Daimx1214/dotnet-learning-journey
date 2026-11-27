# Static Abstract Members in C#

C# 11 introduced **static abstract members in interfaces**. For the first time, an interface can require a type to provide a static member — most famously, operators. That is what makes generic math possible: you can write one `Sum<T>` that works for `int`, `double`, `decimal`, and any other numeric type.

## Why This Was Needed

Before C# 11, a generic method could not add two values of type `T`:

```csharp
// This did NOT compile before C# 11 — there was no way to
// say "T must support the + operator".
T Add<T>(T a, T b) => a + b; // error: operator '+' cannot be applied to T
```

The workaround was `dynamic` (slow, no compile-time safety) or hand-writing one overload per numeric type. Static abstract members fix the problem at the language level.

## The Generic Math Interfaces

.NET 7 shipped a set of numeric interfaces built on static abstract members, in the `System.Numerics` namespace:

- `INumber<T>` — the full numeric contract: addition, subtraction, multiplication, division, comparison, parsing, and more. If a type is a number, it implements this.
- `IAdditionOperators<TSelf, TOther, TResult>` — just the `+` operator. There are matching interfaces for subtraction (`ISubtractionOperators`), multiplication, division, and the rest, so a method can demand exactly the operations it uses and nothing more.
- `INumber<T>` itself refines many of these operator interfaces, so constraining to `INumber<T>` gives you the whole arithmetic toolbox in one constraint.

## A Generic Sum

```csharp
using System.Numerics;

public static T Sum<T>(IEnumerable<T> values) where T : INumber<T>
{
    T total = T.Zero;
    foreach (T value in values)
    {
        total += value;
    }
    return total;
}
```

Two things are new here. `T.Zero` is a static abstract property declared on `INumber<T>` — the additive identity for whatever `T` is. And `total += value` compiles because `INumber<T>` guarantees the `+` operator exists for `T`.

## Worked Example

```csharp
int intSum = Sum(new[] { 1, 2, 3, 4 });
double doubleSum = Sum(new[] { 1.0, 2.0, 3.0, 4.0 });

Console.WriteLine(intSum);    // 10
Console.WriteLine(doubleSum); // 10
```

The arithmetic, checked by hand: 1 + 2 = 3, 3 + 3 = 6, 6 + 4 = 10. Both calls return 10 — as an `int` (printed `10`) and as a `double` (printed `10`, since `Console.WriteLine` prints `10.0` as `10`).

The same method works unchanged for `decimal`, `long`, `float`, and even `BigInteger`, because they all implement `INumber<T>`.

## How It Works Under the Hood

When you write `where T : INumber<T>`, the compiler resolves `T.Zero` and `T + T` through the interface's static abstract members. At runtime the JIT generates a specialised version of `Sum<T>` for each value type, so there is no boxing and no `dynamic` dispatch — the addition is the same native instruction as in hand-written `int` code.

## Requirements

- **C# 11 or later** (language version 11) and **.NET 7 or later** for the `System.Numerics` interfaces. `INumber<T>` does not exist on .NET 6.
- The feature also works in interfaces you write yourself: any interface can declare `static abstract` (or `static virtual`) members, and implementing types must supply them.
- A type implementing the interface must provide the static member explicitly — a class cannot inherit a `static abstract` implementation from a base class the way instance members are inherited.

## Constrain Only What You Need

Prefer the narrowest interface that covers the operations you use. If a method only adds values, `IAdditionOperators<T, T, T>` is a tighter contract than `INumber<T>` and admits more types (a custom vector type might support `+` without being a full number). Reach for `INumber<T>` when the method genuinely needs full numeric behaviour — parsing, comparison, zero/one constants, and arithmetic together.

## Your Own Static Abstract Members

The feature is not limited to the built-in numeric interfaces. Any interface you write can declare static abstract members:

```csharp
public interface IFactory<TSelf>
    where TSelf : IFactory<TSelf>
{
    static abstract TSelf CreateDefault();
}

public sealed class Widget : IFactory<Widget>
{
    public static Widget CreateDefault() => new();

    private Widget() { }
}
```

Here `IFactory<TSelf>` forces every implementing type to supply a `CreateDefault()` factory method. Generic code can then write `TSelf.CreateDefault()` for any `TSelf` satisfying the constraint — the same mechanism generic math uses for `T.Zero` and the `+` operator, applied to your own domain.

## Key Takeaways

- C# 11 static abstract interface members let an interface require static members, including operators — the foundation of generic math.
- `INumber<T>` is the full numeric contract; `IAdditionOperators` constrains only the `+` operator.
- Worked result: `Sum` of `[1, 2, 3, 4]` is `10` for `int` and `10.0` for `double` (1 + 2 + 3 + 4 = 10).
- The feature requires C# 11 and .NET 7 or later; `INumber<T>` lives in `System.Numerics`.
- Constrain to the narrowest operator interface your method actually needs, and use `INumber<T>` when you need the whole numeric toolbox.
