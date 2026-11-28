# Equals and GetHashCode in C#

`Equals` and `GetHashCode` form a contract: two objects that are equal must produce the same hash code. Break the contract and hash-based collections (`Dictionary`, `HashSet`) silently lose your objects.

## The Contract

1. If `a.Equals(b)` is true, then `a.GetHashCode() == b.GetHashCode()`.
2. `GetHashCode` must return the same value for the lifetime of an object while it is used as a key. Mutating a key after inserting it breaks lookups.
3. `Equals` must be reflexive, symmetric, transitive, and consistent, and `x.Equals(null)` must be false.

The reverse does not hold: two unequal objects may share a hash code (a collision). Collisions only cost performance, not correctness.

## Overriding Both Together

Override `Equals` without `GetHashCode` and the compiler warns you (CS0659), because the default `GetHashCode` is identity-based: two equal-by-value objects get different hashes, and a `Dictionary` lookup fails.

```csharp
public sealed class Product : IEquatable<Product>
{
    public string Sku { get; }
    public string Name { get; }

    public Product(string sku, string name) => (Sku, Name) = (sku, name);

    public bool Equals(Product? other) =>
        other is not null && Sku == other.Sku && Name == other.Name;

    public override bool Equals(object? obj) => Equals(obj as Product);

    public override int GetHashCode() => HashCode.Combine(Sku, Name);

    public static bool operator ==(Product? left, Product? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Product? left, Product? right) => !(left == right);
}
```

Key points: implement `IEquatable<T>` so generic collections call the typed overload without boxing; compare strings with `==` (ordinal by default for `string`); and override `==`/`!=` to match, or `a == b` will still mean reference equality.

## GetHashCode.Combine

`HashCode.Combine` (available since .NET Core 3.0) is the standard way to build a hash from fields. It handles nulls and mixes the values well:

```csharp
public override int GetHashCode() => HashCode.Combine(Sku, Name);
// Up to 8 values: HashCode.Combine(a, b, c, d, e, f, g, h)
```

For more than 8 fields, or for a struct, use the `HashCode` struct directly:

```csharp
public override int GetHashCode()
{
    var hash = new HashCode();
    hash.Add(Sku);
    hash.Add(Name);
    hash.Add(Price);
    return hash.ToHashCode();
}
```

Do not write your own mixing formula (such as `x * 31 + y`) unless you have measured a problem with `Combine`; hand-rolled hashes are usually worse.

Only include fields that participate in `Equals`. Including a field in the hash but not in equality is harmless but wasteful; including a field in equality but not in the hash is a contract violation only if that field can differ between equal objects, which it cannot by definition. The real rule: hash the same fields you compare.

## Records and Value Equality

Records implement value equality for you. Two records with the same field values are equal, and their hash codes match:

```csharp
public record ProductRecord(string Sku, string Name);

var a = new ProductRecord("KB-1", "Keyboard");
var b = new ProductRecord("KB-1", "Keyboard");
Console.WriteLine(a == b);                       // True
Console.WriteLine(a.GetHashCode() == b.GetHashCode()); // True
```

For a class that should behave like a value, a `record` (or `record class`) removes the entire boilerplate above. Use a `record struct` for small immutable values. You only need to hand-write `Equals`/`GetHashCode` when the type is a class with custom equality rules, such as case-insensitive comparison of one field.

## Dictionary and HashSet Consequences

A bad hash breaks lookups in ways that look like data loss:

```csharp
var stock = new Dictionary<Product, int>();
var key = new Product("KB-1", "Keyboard");
stock[key] = 42;

var lookup = new Product("KB-1", "Keyboard");
Console.WriteLine(stock.TryGetValue(lookup, out int qty)); // True only if GetHashCode is overridden
Console.WriteLine(qty); // 42
```

With the default identity hash, `TryGetValue` returns false even though an equal key exists, because the dictionary looks in the wrong bucket. Worse, mutating a key after insertion strands the entry: the stored hash no longer matches the key's current hash, so the entry becomes unreachable even by the original reference.

Rules for keys: make them immutable, or at least never mutate the fields that participate in `Equals`/`GetHashCode` while the object is in the collection.

## Key Takeaways

- Equal objects must have equal hash codes; always override `GetHashCode` when you override `Equals`.
- Implement `IEquatable<T>` and use `HashCode.Combine` for the hash.
- Hash exactly the fields you compare in `Equals`.
- Records give you correct value equality and hashing for free; hand-write it only for custom rules.
- Never mutate a dictionary or hash-set key's equality fields while it is in the collection.
