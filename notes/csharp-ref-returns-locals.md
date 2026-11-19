# Ref Returns and Ref Locals

Normally a method returns a copy of a value. A `ref return` returns a reference to the storage itself, so the caller can read — and write — the original location. Combined with `ref` locals, this lets hot-path code work directly on an element inside an array or span without copying it.

## Finding the Max Element by Ref

The example below returns a reference to the largest element of a span. The caller stores it in a `ref` local and can then modify the array through that local. Because a `Span<int>` here wraps an array, the reference stays valid as long as the array lives.

```csharp
static ref int FindMax(Span<int> values)
{
    ref int max = ref values[0];
    for (int i = 1; i < values.Length; i++)
    {
        if (values[i] > max)
        {
            max = ref values[i];
        }
    }
    return ref max;
}

int[] numbers = { 3, 1, 4, 1, 5, 9, 2 };
ref int max = ref FindMax(numbers);
Console.WriteLine(max); // 9
max = 0;                // writes through the reference into the array
Console.WriteLine(numbers[5]); // 0 — the array element itself changed
```

## ref readonly

`ref readonly` returns a reference the caller may read but not write through. Use it when you want to expose an element without copying a large struct and without letting the caller mutate it. A `ref readonly` local is declared with `ref readonly int x = ref ...;`.

```csharp
static ref readonly int FirstReadOnly(Span<int> values) => ref values[0];

int[] data = { 10, 20, 30 };
ref readonly int first = ref FirstReadOnly(data);
Console.WriteLine(first); // 10
// first = 99; // compile error: cannot assign to a readonly reference
```

## Safety Rules

A `ref` return must point at storage that outlives the call — a field, an array element, or a `ref` parameter. Returning a reference to a local variable is a compile error, because the local dies when the method returns. The same rule applies to `Span<T>`: you cannot store a span in a field or return one from an async method, since spans are stack-only (`ref struct`). Keep `ref` returns in small, well-tested helpers; they make code harder to reason about, so only use them where the copy they avoid is measurably expensive, such as large structs in tight loops.

## Key Takeaways

- A `ref` return hands back a reference to the original storage, not a copy — the caller can mutate through it.
- `ref` locals (`ref int x = ref ...`) hold that reference; plain assignment would copy the value instead.
- `ref readonly` exposes an element for reading without copying and without allowing mutation.
- Never return a reference to a local variable; the storage must outlive the method call.
- Use ref returns sparingly — only where avoiding a copy of a large struct in a hot path is worth the complexity.
