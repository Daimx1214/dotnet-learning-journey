# Advanced LINQ Operators (C#)

Beyond `Where`, `Select` and `OrderBy`, LINQ has a second tier of operators that replace hand-written loops with a single expressive call. Most of these arrived in .NET 6 and 7, and they are easy to misuse if you guess at their behaviour — so every example below includes its exact output, hand-traced.

## Chunk and Index

`Chunk(n)` splits a sequence into fixed-size arrays; the last chunk is shorter when the count does not divide evenly. `Index()` (from .NET 9) pairs each element with its zero-based position, which removes the need for a manual counter variable in a `foreach`.

```csharp
int[] numbers = Enumerable.Range(1, 10).ToArray();

// Chunk(3): [1,2,3] [4,5,6] [7,8,9] [10]  — 10 items, so the last chunk has 1 item.
foreach (int[] chunk in numbers.Chunk(3))
    Console.WriteLine(string.Join(",", chunk));
// Output:
// 1,2,3
// 4,5,6
// 7,8,9
// 10

// Index(): (0, "a") (1, "b") (2, "c")
foreach ((int i, string s) in new[] { "a", "b", "c" }.Index())
    Console.WriteLine($"{i}: {s}");
```

## DistinctBy, MaxBy and MinBy

`DistinctBy` keeps the first element for each distinct key, which is what you actually want when deduplicating objects by one property — plain `Distinct()` compares whole objects. `MaxBy` and `MinBy` return the *element* with the greatest or least key, unlike `Max()` which returns only the key value.

```csharp
record Person(string Name, int Age);
var people = new[]
{
    new Person("Alice", 30),
    new Person("Bob", 25),
    new Person("Alice", 28), // Same name, different age.
};

// DistinctBy(Name): Alice 30, Bob 25 — the first Alice wins, the second is dropped.
var unique = people.DistinctBy(p => p.Name).ToList(); // Count: 2.

record Product(string Name, decimal Price);
var products = new[]
{
    new Product("Laptop", 1200m),
    new Product("Mouse", 25m),
    new Product("Monitor", 300m),
};

// MaxBy(Price): Laptop, 1200. MinBy(Price): Mouse, 25.
Product priciest = products.MaxBy(p => p.Price)!;
Product cheapest = products.MinBy(p => p.Price)!;
```

Both `MaxBy` and `MinBy` throw `InvalidOperationException` on an empty sequence, and on ties they return the first element with the extreme key. If you need the last one, reverse first or sort explicitly.

## Aggregate and Zip

`Aggregate` folds a sequence into a single value by repeatedly applying a function — it is the general form of `Sum`, `Count` and friends. `Zip` pairs elements from two sequences position by position and stops at the shorter one.

```csharp
// Aggregate: 1 -> 12 -> 123 -> 1234. (Each step: acc * 10 + next.)
int folded = new[] { 1, 2, 3, 4 }.Aggregate((acc, next) => acc * 10 + next);
Console.WriteLine(folded); // Output: 1234

// Zip stops at the shorter sequence: (1, "a") (2, "b") — "c" has no partner.
var pairs = new[] { 1, 2, 3 }.Zip(new[] { "a", "b" });
foreach ((int n, string s) in pairs)
    Console.WriteLine($"{n}-{s}");
// Output:
// 1-a
// 2-b
```

`Aggregate` has an overload taking a seed value, which also makes it work on empty sequences (the seed is returned). Without a seed, an empty sequence throws.

## Set Operators with Keys: ExceptBy, IntersectBy, UnionBy

The classic set operators compare whole elements; the `*By` variants compare by a key selector, which is what you need when working with objects. All three preserve the order of the first sequence and keep the first occurrence of each key.

```csharp
int[] all = { 1, 2, 3 };
int[] banned = { 2 };
int[] shared = { 2, 3 };
int[] extra = { 3, 4 };

Console.WriteLine(string.Join(",", all.ExceptBy(banned, x => x)));    // 1,3
Console.WriteLine(string.Join(",", all.IntersectBy(shared, x => x))); // 2,3
Console.WriteLine(string.Join(",", all.UnionBy(extra, x => x)));      // 1,2,3,4
```

Hand-trace `UnionBy`: start with `1, 2, 3`, then append elements of `3, 4` whose keys are not already present — `3` is already there, `4` is new — giving `1, 2, 3, 4`. The real-world use is joining on IDs: `orders.ExceptBy(shippedOrders, o => o.Id)` gives the unshipped orders without a manual `Where(!ids.Contains(...))`.

## Key Takeaways

- `Chunk(n)` splits a sequence into fixed-size arrays; the last chunk is shorter when the count does not divide evenly.
- `DistinctBy`, `MaxBy` and `MinBy` work on a key selector — deduplicate by one property, or get the extreme *element* rather than just its key value.
- `Index()` pairs each element with its zero-based position, removing manual counters from loops.
- `Aggregate` folds a sequence into one value (seeded overload handles empty sequences); `Zip` pairs sequences positionally and stops at the shorter one.
- `ExceptBy`, `IntersectBy` and `UnionBy` are the key-based set operators — ideal for filtering or merging by ID.
- `MaxBy`/`MinBy` throw on empty sequences and return the first element on a tie; plain `Max()` returns only the key, not the element.
