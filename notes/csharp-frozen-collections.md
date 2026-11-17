# Frozen Collections (C#)

`FrozenDictionary<TKey, TValue>` and `FrozenSet<T>` (added in .NET 8) are immutable collections optimised for one pattern: build once, read many times. A normal `Dictionary` is designed to be mutated, so it keeps spare bucket capacity and a layout that supports fast inserts. A frozen collection is built once and then sealed, which lets the runtime choose a denser layout — often a perfect hash or a sorted array — that makes lookups faster and uses less memory. You pay for it once, at construction time, and then every read is cheaper.

## When Frozen Collections Win

Reach for them when the data is effectively static: lookup tables, configuration maps, enum-to-string mappings, allowed-value sets, HTTP status code descriptions, routing tables. If the collection changes after creation — even rarely — they are the wrong tool, because "changing" one means building a whole new frozen copy.

```csharp
using System.Collections.Frozen;

// A static lookup table: built once at startup, read on every request.
public static class HttpStatusText
{
    private static readonly FrozenDictionary<int, string> _texts =
        new Dictionary<int, string>
        {
            [200] = "OK",
            [201] = "Created",
            [400] = "Bad Request",
            [401] = "Unauthorized",
            [403] = "Forbidden",
            [404] = "Not Found",
            [500] = "Internal Server Error",
        }.ToFrozenDictionary();

    public static string Get(int code) =>
        _texts.TryGetValue(code, out string? text) ? text : "Unknown Status";
}

Console.WriteLine(HttpStatusText.Get(404)); // Output: Not Found
Console.WriteLine(HttpStatusText.Get(418)); // Output: Unknown Status
```

`ToFrozenDictionary()` and `ToFrozenSet()` are the usual construction routes; there are also constructors taking an existing dictionary or set. Duplicate keys throw `ArgumentException` at construction time, exactly like `Dictionary`'s collection-initializer behaviour.

## Construction Cost vs Lookup Speed

Freezing is not free. Building a frozen collection is slower than building a `Dictionary` of the same size, because the runtime spends extra work analysing the keys — choosing hash seeds, checking whether a perfect hash is possible, and laying the data out for cache-friendly reads. The break-even point depends on size and read count, but the shape of the trade-off is consistent:

| Collection | Build cost | Lookup speed | Memory | Mutability |
|---|---|---|---|---|
| `Dictionary` | Low | Fast | Higher (spare capacity) | Mutable |
| `FrozenDictionary` | Higher | Faster (often measurably) | Lower (dense layout) | Immutable |
| `HashSet` | Low | Fast | Higher | Mutable |
| `FrozenSet` | Higher | Faster | Lower | Immutable |

For a small table read a handful of times, the difference is noise and either choice is fine. For a large table consulted on a hot path — say a per-request lookup in a web API — the frozen version wins, because the one-time build cost is amortised over millions of reads. Microsoft's own benchmarks show frozen lookups beating `Dictionary` by a clear margin on larger key sets, while on tiny sets (a dozen keys) the two are close enough that readability should decide.

## FrozenSet Example

`FrozenSet` is the read-only counterpart of `HashSet`, useful for membership checks against a fixed allow-list.

```csharp
// Allowed file extensions for uploads — checked on every upload request.
private static readonly FrozenSet<string> _allowedExtensions =
    new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

public static bool IsAllowedExtension(string extension) =>
    _allowedExtensions.Contains(extension);

Console.WriteLine(IsAllowedExtension(".PNG"));  // Output: True (case-insensitive comparer)
Console.WriteLine(IsAllowedExtension(".exe"));  // Output: False
```

Note that the comparer is fixed at construction: `ToFrozenSet(StringComparer.OrdinalIgnoreCase)` makes `.PNG` match, whereas the default ordinal comparer would not. Pick the comparer deliberately, because it cannot be changed afterwards — the collection is frozen.

## Key Takeaways

- `FrozenDictionary` and `FrozenSet` (.NET 8) are immutable collections optimised for build-once, read-many workloads.
- Freezing trades a higher one-time construction cost for faster lookups and lower memory, thanks to a denser layout (sometimes a perfect hash).
- Use them for static lookup tables, configuration maps and allow-lists; never for collections that change after creation.
- Build with `ToFrozenDictionary()` / `ToFrozenSet()` or the constructors; duplicate keys throw at construction time.
- The comparer is fixed at construction — pass `StringComparer.OrdinalIgnoreCase` explicitly when lookups should ignore case.
- For small tables read rarely, the difference from `Dictionary`/`HashSet` is negligible; the win shows on hot paths and large key sets.
