# IEqualityComparer<T> and IEquatable<T>

`Distinct()`, `GroupBy()`, `ToDictionary()` and `Dictionary<TKey, TValue>`
all decide "are these two values the same?" using equality. By default a
class uses reference equality, which is often not what you want. C# gives
you two hooks to define equality yourself: `IEquatable<T>` on the type, and
`IEqualityComparer<T>` as a separate object.

## IEquatable<T>: Equality Built Into the Type

Implement `IEquatable<T>` when the type itself knows what equality means —
a value object, an entity compared by ID, a key type:

```csharp
public sealed class Product : IEquatable<Product>
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public decimal Price { get; init; }

    public bool Equals(Product? other)
        => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as Product);

    public override int GetHashCode() => Id.GetHashCode();
}
```

Always override `Equals(object)` and `GetHashCode` together with
`IEquatable<T>`. The generic `Equals` is used by generic collections; the
`object` override covers non-generic code paths.

## The GetHashCode Contract

The rule is absolute: **if two objects are equal, they must produce the same
hash code.** The reverse is not required — unequal objects may collide, it
only costs performance.

Break the rule and hash-based collections silently misbehave: a `Dictionary`
lookup hashes the key to find the bucket, then uses `Equals` inside it. If
an equal key hashes to a different bucket, the entry is never found, even
though it is in the dictionary. Consequences of a bad hash:

- Equal objects with different hashes: lookups fail, `Contains` returns
  false for items that are present.
- A hash based on a **mutable** field: the object moves buckets after being
  added, and the dictionary can never find it again.
- A constant hash (`return 0`): correct, but every lookup degrades to a
  linear scan — O(n) instead of O(1).

Use `HashCode.Combine` for multi-field hashes — it mixes fields well:

```csharp
public override int GetHashCode() => HashCode.Combine(Id, Name);
```

## IEqualityComparer<T>: Equality as a Separate Object

Sometimes equality depends on context, not on the type. Two products might
be "the same" by ID in one report and by name in another. That is what
`IEqualityComparer<T>` is for — pass it to the LINQ method or the
dictionary constructor:

```csharp
public sealed class ProductNameComparer : IEqualityComparer<Product>
{
    public bool Equals(Product? x, Product? y)
        => string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(Product obj)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);
}

var catalog = new[]
{
    new Product { Id = 1, Name = "Keyboard", Price = 49.99m },
    new Product { Id = 2, Name = "keyboard", Price = 59.99m },
    new Product { Id = 3, Name = "Mouse", Price = 29.99m },
};

var distinct = catalog.Distinct(new ProductNameComparer()).ToList();
// distinct has 2 items: "Keyboard" (first wins) and "Mouse"

var byName = new Dictionary<Product, int>(new ProductNameComparer());
byName[catalog[0]] = 10;
Console.WriteLine(byName[catalog[1]]); // 10 — same key by name
```

The comparer must satisfy the same contract: equal objects (by its
`Equals`) must return equal hash codes. Here the hash uses the same
case-insensitive comparer as the equality check, so the contract holds.

## Records: Equality for Free

Records implement value equality automatically — two records are equal when
their types match and all properties are equal, and `GetHashCode` is
generated to match:

```csharp
public sealed record ProductRecord(int Id, string Name, decimal Price);

var p1 = new ProductRecord(1, "Keyboard", 49.99m);
var p2 = new ProductRecord(1, "Keyboard", 49.99m);
Console.WriteLine(p1 == p2); // True
Console.WriteLine(new HashSet<ProductRecord> { p1, p2 }.Count); // 1
```

Prefer a record when the type is a value-like carrier of data. Reach for
`IEquatable<T>` on a class when you need identity-based equality (by ID, for
example), and for `IEqualityComparer<T>` when the equality rule varies by
context.

## Which One to Use

| Situation | Choice |
|-----------|--------|
| Type is a value object / DTO | `record` (built-in) |
| Class with one natural equality (e.g. by Id) | `IEquatable<T>` + overrides |
| Equality rule depends on the caller | `IEqualityComparer<T>` |
| Case-insensitive string keys | Built-in `StringComparer.OrdinalIgnoreCase` |

## Key Takeaways

- `IEquatable<T>` defines a type's own equality; `IEqualityComparer<T>`
  defines equality from the outside, per use case.
- The hash contract is non-negotiable: equal objects must hash equal, or
  dictionaries and sets silently break.
- Hash on the same fields you compare, never on mutable fields, and use
  `HashCode.Combine` for multi-field hashes.
- Records give correct value equality and hashing for free — prefer them
  for data carriers.
