# Generic Math in .NET 7+

Generic math lets you write one algorithm that works for any numeric type — `int`, `double`, `decimal`, `BigInteger` — without boxing, without `dynamic`, and without writing a separate overload for each type. It is built on static abstract interface members, a C# 11 feature.

## How It Works: INumber<T> and Static Abstract Members

Before .NET 7, an interface could not declare a static operator, so a generic method could not write `a + b` for an unknown `T`. Now interfaces such as `INumber<T>` declare operators as `static abstract`, and numeric types implement them. Constraining `T` to `INumber<T>` makes `+`, `-`, `*`, `/`, and `Zero` available inside the generic method, resolved at compile time for each concrete `T`.

```csharp
using System.Numerics;

T Sum<T>(IEnumerable<T> values) where T : INumber<T>
{
    T total = T.Zero;
    foreach (T value in values)
    {
        total += value;
    }
    return total;
}

Console.WriteLine(Sum(new[] { 1, 2, 3, 4 }));       // 10
Console.WriteLine(Sum(new[] { 1.5, 2.5 }));         // 4
Console.WriteLine(Sum(new[] { 1m, 2m, 3m }));       // 6
```

## A Generic Average Example

Division also comes from the interface, so an average needs no per-type code. Note that integer division truncates: the average of `1, 2, 3, 4` as `int` is `2`, not `2.5`, because `T` is `int` and `10 / 4` is integer division.

```csharp
using System.Numerics;

T Average<T>(IEnumerable<T> values) where T : INumber<T>
{
    T total = T.Zero;
    int count = 0;
    foreach (T value in values)
    {
        total += value;
        count++;
    }
    return total / T.CreateChecked(count);
}

Console.WriteLine(Average(new[] { 1.0, 2.0, 3.0, 4.0 })); // 2.5
Console.WriteLine(Average(new[] { 1, 2, 3, 4 }));         // 2
```

## When to Use It vs dynamic

Use generic math when the types are known at compile time and you want full type safety and speed — the compiler generates a specialised version per `T`, with no runtime dispatch. Use `dynamic` only when the type is genuinely unknown until runtime, such as values coming from JSON or COM, and you are willing to pay for runtime binding and lose compile-time checking. Generic math cannot help there, because there is no `T` to constrain. Conversely, reaching for `dynamic` to add two numbers in a library is almost always the wrong call now that `INumber<T>` exists.

## Key Takeaways

- `INumber<T>` and static abstract interface members let generic code use `+`, `-`, `*`, `/` on any numeric type.
- `T.Zero`, `T.One`, and `T.CreateChecked(n)` give you numeric constants without knowing the concrete type.
- One generic method replaces a set of per-type overloads, with no boxing and no runtime dispatch.
- Integer division still truncates inside generic math — the result type decides the semantics, not the algorithm.
- Prefer generic math over `dynamic` whenever the types are known at compile time; keep `dynamic` for genuinely runtime-only values.
