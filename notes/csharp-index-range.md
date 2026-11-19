# Index (^) and Range (..) Operators

C# 8 added the `Index` (`^`) and `Range` (`..`) operators for slicing. `^1` means "the last element", `^2` the second-last, and `..` builds a range with a start (inclusive) and an end (exclusive). They make from-the-end access readable without manual length arithmetic.

## From-End Indexing and Slicing

`^0` is not the last element — it is one past the end, so `array[^0]` throws. The last element is `array[^1]`, which equals `array[array.Length - 1]`. A range `a..b` includes `a` and excludes `b`, just like the half-open intervals used by most slicing APIs.

```csharp
string[] words = { "alpha", "beta", "gamma", "delta", "epsilon" };

Console.WriteLine(words[^1]);   // epsilon — last element
Console.WriteLine(words[^2]);   // delta — second-last
Console.WriteLine(words[1]);    // beta

string[] middle = words[1..4];  // beta, gamma, delta (index 4 excluded)
Console.WriteLine(string.Join(", ", middle)); // beta, gamma, delta

string[] fromSecond = words[1..];  // beta, gamma, delta, epsilon
string[] firstThree = words[..3];  // alpha, beta, gamma
string[] lastTwo = words[^2..];    // delta, epsilon
Console.WriteLine(string.Join(", ", lastTwo)); // delta, epsilon
```

## Slicing Strings and Span

Ranges work on `string` and on `Span<T>`, where slicing is allocation-free because a span is just a window over existing memory. Slicing an array with `..` allocates a new array, so prefer `AsSpan()[..]` in hot paths.

```csharp
string text = "Hello, World!";
Console.WriteLine(text[7..]);   // World!
Console.WriteLine(text[..5]);   // Hello
Console.WriteLine(text[^6..]);  // World!

int[] numbers = { 10, 20, 30, 40, 50 };
Span<int> slice = numbers.AsSpan()[1..4]; // 20, 30, 40 — no allocation
Console.WriteLine(slice[0]); // 20
slice[0] = 99;
Console.WriteLine(numbers[1]); // 99 — the span writes through to the array
```

## Gotchas

`Range` does not work directly on `List<T>` — `list[1..3]` is a compile error, because `List<T>` has no indexer taking a `Range`. Use `list.GetRange(1, 2)` (count, not end index) or `CollectionsMarshal.AsSpan(list)[1..3]` when you need a span view. Ranges are also half-open, so `words[1..4]` has 3 elements, not 4 — an easy off-by-one if you are used to inclusive ends. Finally, `^0` means "past the end", not "last", so `array[^0]` always throws `IndexOutOfRangeException`.

## Key Takeaways

- `^n` counts from the end: `^1` is the last element, `^2` the second-last; `^0` is past the end and throws.
- Ranges are half-open: `a..b` includes `a` and excludes `b`, so `[1..4]` yields 3 elements.
- `..` slices arrays (allocating a copy), strings, and `Span<T>` (allocation-free, writes through).
- `List<T>` has no `Range` indexer — use `GetRange(start, count)` or `CollectionsMarshal.AsSpan(list)[..]`.
- Omitting a bound means "to the start" or "to the end": `[1..]`, `[..3]`, and `[..]` are all valid.
