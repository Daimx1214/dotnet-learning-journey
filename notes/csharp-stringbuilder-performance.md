# StringBuilder Performance

Strings in .NET are immutable: every concatenation creates a brand new string and copies both parts into it. In a loop that is O(n^2) copying. `StringBuilder` exists to fix exactly that, but only when it is used the right way.

## Why String Concatenation in a Loop Is Slow

```csharp
string result = "";
for (int i = 0; i < 10_000; i++)
{
    result += i.ToString(); // allocates a new string every iteration
}
```

Each `+=` allocates a new string of the combined length and copies everything built so far. The total copying is roughly 1 + 2 + 3 + ... + n characters, which grows quadratically. With 10,000 iterations this allocates tens of megabytes and spends most of its time in the garbage collector.

The `StringBuilder` version does the same job in linear time:

```csharp
var sb = new StringBuilder();
for (int i = 0; i < 10_000; i++)
{
    sb.Append(i);
}
string result = sb.ToString();
```

`Append` writes into an internal character buffer and only allocates a new buffer when the current one is full, so the total work stays proportional to the final size.

## Capacity Growth

A `StringBuilder` starts with a default capacity of 16 characters. When an append would exceed the capacity, it allocates a new buffer twice as large and copies the old contents over. Doubling keeps the amortized cost of each append constant, but the copy on growth is still real work.

If you know the approximate final size, set the capacity up front and skip the growth copies entirely:

```csharp
// Building a report of roughly 5,000 characters.
var sb = new StringBuilder(capacity: 5000);
```

You can also pre-size from a known input, for example `new StringBuilder(input.Length * 2)`. Oversizing wastes a little memory; undersizing costs a few growth copies. When the size is genuinely unknown, the default is fine.

## Append vs String Concat

For a small, fixed number of concatenations, plain `+` or interpolation is faster than `StringBuilder`, because the compiler turns it into a single `string.Concat` call with one allocation:

```csharp
// One allocation. StringBuilder would be slower here.
string greeting = "Hello, " + name + "! You have " + count + " messages.";

// Equivalent and equally good.
string greeting2 = $"Hello, {name}! You have {count} messages.";
```

The rule of thumb: fewer than about 10 concatenations, or a single expression, use `+` or interpolation. A loop, or an unknown number of appends, use `StringBuilder`. The crossover point is small, so do not reach for `StringBuilder` out of habit for a two-part string.

## AppendJoin and Chaining

`AppendJoin` joins a collection with a separator without building an intermediate array of strings:

```csharp
var words = new[] { "alpha", "beta", "gamma" };
var sb = new StringBuilder();
sb.AppendJoin(", ", words);
Console.WriteLine(sb); // alpha, beta, gamma
```

`Append` returns the builder itself, so calls chain:

```csharp
var sb = new StringBuilder();
sb.Append("User ").Append(userId).Append(" logged in at ").AppendLine(DateTime.UtcNow.ToString("O"));
```

`AppendLine` adds the value plus a newline, which is handy for building multi-line text such as logs or reports. `AppendFormat` exists too, but interpolated `Append` with `$"..."` is usually clearer.

## String.Create for Zero-Allocation Formatting

When you need a formatted string and want to avoid the intermediate `StringBuilder` entirely, `string.Create` lets you write directly into the new string's buffer:

```csharp
string code = string.Create(CultureInfo.InvariantCulture, stackalloc char[8], (span, _) =>
{
    // Fill the span directly; no StringBuilder, no extra allocation.
    "INV-".CopyTo(span);
    "1234".CopyTo(span[4..]);
});
```

This is an advanced technique for hot paths. For ordinary code, `StringBuilder` or interpolation is clearer and fast enough.

## When StringBuilder Does NOT Help

- A single concatenation or a fixed handful of parts: `string.Concat` does it in one allocation.
- Building a string from a `ReadOnlySpan<char>` or a stack-allocated buffer: use `string.Create` or `new string(span)`.
- Returning a `string` that is immediately split or discarded: you may not need the string at all.
- `ToString()` on a huge builder still allocates the final string, so a builder does not remove the last allocation, it only removes the intermediate ones.
- Reusing a single `StringBuilder` across threads is not safe; it is not thread-safe, so each thread needs its own instance.

## Benchmark-Style Reasoning

Rough mental model for building a 100 KB string from 10,000 small pieces:

- `+=` in a loop: about 10,000 allocations, roughly 500 MB copied in total, dominated by quadratic copying.
- `StringBuilder` with default capacity: about 13 buffer growths (16, 32, 64, ... up to 131,072), a handful of allocations, total copying close to the final size.
- `StringBuilder` with capacity set to 100,000: one buffer allocation, zero growth copies, one final `ToString` allocation.

The difference between the first and second rows is orders of magnitude; the difference between the second and third is a small constant factor. That is why the first rule is "use a builder in a loop" and the second rule is only "set the capacity when you know it".

## Key Takeaways

- Strings are immutable, so `+=` in a loop copies everything built so far on every iteration and runs in quadratic time.
- `StringBuilder.Append` in a loop is linear, because it writes into a buffer that only doubles when full.
- Set the initial capacity when you know the approximate final size to skip growth copies.
- For a fixed small number of parts, plain `+` or interpolation is faster than `StringBuilder`, since the compiler makes one `string.Concat` call.
- `AppendJoin` joins a collection with a separator without intermediate strings, and `string.Create` is the zero-allocation option for hot paths.
