# Readonly Structs and Members in C#

`readonly` on a struct or a member is a promise: this value will not change after construction. The compiler enforces the promise, and the runtime rewards it by skipping defensive copies that it would otherwise make every time you call a method on a readonly value.

## Readonly Struct

Marking a struct `readonly` makes every field readonly and every instance member implicitly readonly:

```csharp
public readonly struct Point
{
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y) => (X, Y) = (x, y);

    public double DistanceFromOrigin() => Math.Sqrt(X * X + Y * Y);
}
```

Any attempt to assign to `X` or `Y` outside the constructor is a compile error. A readonly struct is a good default for small value types that represent a single value, such as points, money amounts, or ranges.

## Readonly Members

You can mark individual members readonly without marking the whole struct. A readonly method or property promises not to mutate the struct:

```csharp
public struct Counter
{
    private int _count;

    public void Increment() => _count++;          // mutating, not readonly

    public readonly int GetCount() => _count;     // readonly: cannot mutate

    public readonly int Count => _count;          // readonly property
}
```

Readonly members are only useful on structs. On a class they compile but change nothing, because class instances are always passed by reference and never defensively copied.

## The Defensive Copy Problem

When a struct is stored in a `readonly` field, or passed as an `in` parameter, the compiler cannot prove that a method call will not mutate it, so it copies the whole struct before the call. For a large struct called in a hot loop, that copy is real work:

```csharp
public struct BigValue
{
    public decimal A; public decimal B; public decimal C; public decimal D;

    public decimal Sum() => A + B + C + D; // not readonly
}

public sealed class Holder
{
    private readonly BigValue _value = new();

    public decimal GetSum() => _value.Sum(); // compiler copies _value (64 bytes) first
}
```

Marking `Sum()` readonly (or the whole struct readonly) removes the copy: the compiler trusts the promise and calls the method on the original storage.

## In Parameters

`in` passes a struct by reference but forbids the callee from modifying it. Combined with readonly members, it avoids both a copy and a mutation:

```csharp
public static decimal Total(in BigValue value) => value.Sum();
// No copy at the call site, and Total cannot change value.
```

Use `in` for large structs passed to hot methods. Do not use it for small structs (16 bytes or less): passing a reference is no cheaper than copying a few bytes, and it can even inhibit optimizations.

## Ref Readonly Returns

`ref readonly` returns a reference to data owned by someone else, without allowing the caller to modify it:

```csharp
public sealed class PriceBook
{
    private readonly decimal[] _prices = { 9.99m, 14.50m, 3.25m };

    public ref readonly decimal GetPrice(int index) => ref _prices[index];
}

var book = new PriceBook();
ref readonly decimal price = ref book.GetPrice(1);
Console.WriteLine(price); // 14.50
// price = 0m; // compile error: cannot assign to a readonly reference
```

This is useful for returning large structs or array elements without copying, while keeping the data safe from mutation. The caller must store the result with `ref readonly` as well, or the value is copied on assignment.

## When Readonly Helps, and When It Does Not

- It helps for large structs (more than 16 bytes) used in hot paths, especially when stored in readonly fields or passed with `in`.
- It helps for value types that model immutable data, where the guarantee itself prevents bugs.
- It does not help classes, small structs, or structs that are mutated often: making a frequently mutated struct readonly just forces you to replace the whole value instead.
- A readonly struct that contains a mutable reference type (such as a `List<T>` field) is only shallowly immutable: the reference cannot change, but the list contents can.

## Key Takeaways

- `readonly struct` makes all fields and members readonly and removes defensive copies.
- Mark individual members readonly when only some methods are non-mutating.
- The compiler copies a struct before calling a non-readonly method on a readonly field or an `in` parameter; readonly members avoid that copy.
- Use `in` parameters for large structs in hot paths, and `ref readonly` returns to hand out data without copying or allowing mutation.
- Readonly is a shallow guarantee: reference-type fields inside a readonly struct can still be mutated.
