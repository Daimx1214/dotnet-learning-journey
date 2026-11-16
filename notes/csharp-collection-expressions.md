# Collection Expressions (C# 12)

Collection expressions, introduced in C# 12, give every collection type a single concise literal syntax: square brackets. Before them, arrays used `{ ... }`, lists needed `new List<int> { ... }`, and spans had no literal at all. Now `[1, 2, 3]` works for all of them, and the compiler figures out the target type from context. The feature is purely syntactic — the generated code is the same as the older forms — but it removes a surprising amount of ceremony.

## Basic Syntax and Target Typing

A collection expression has no type of its own; it takes its type from where it is used. That is *target typing*: the same literal can become an array, a `List<T>`, an immutable array, or a span, depending on the declared type.

```csharp
int[] numbers = [1, 2, 3, 4, 5];
List<string> names = ["Ada", "Grace", "Linus"];
HashSet<int> unique = [1, 2, 2, 3];          // Duplicates are removed by the set.
ImmutableArray<int> frozen = [10, 20, 30];
Span<int> slice = stackalloc int[3];          // Collection expressions work with spans too:
Span<int> values = [7, 8, 9];                 // (on the stack when assigned to a Span)

int[] Empty() => [];                          // An empty collection expression.
List<int> GetScores() => [95, 82, 77];        // Return type supplies the target type.

// Method arguments are target-typed as well:
void PrintAll(IEnumerable<int> items) => Console.WriteLine(string.Join(", ", items));
PrintAll([1, 2, 3]);
```

Collection expressions also work as arguments to `params` parameters and in `foreach` initializers, anywhere the compiler can see the expected type. They do *not* work with `var`, because there is no target type to infer from — `var xs = [1, 2, 3];` is a compile error.

## The Spread Element

The spread element `..` splices another collection into the literal, replacing the old `Concat` / `AddRange` dance. It works with any `IEnumerable<T>`-compatible source, and multiple spreads can be mixed with individual elements.

```csharp
int[] first = [1, 2, 3];
int[] second = [4, 5];
int[] combined = [.. first, 0, .. second];    // [1, 2, 3, 0, 4, 5]

List<string> baseTags = ["csharp", "dotnet"];
List<string> allTags = [.. baseTags, "aspnet", "efcore"];

string greeting = "Hello";
char[] chars = [.. greeting];                 // Spreading a string yields its chars.
```

## How the Compiler Builds Them

The compiler picks the most efficient construction it can for the target type. For arrays it emits the array directly; for `List<T>` it creates the list and adds the elements; for types with a `Create` method or a suitable constructor it may call that instead. Any type with a collection builder — the `[CollectionBuilder]` attribute — can participate, which is how `ImmutableArray<T>` and `Span<T>` are supported.

```csharp
// For List<T>, the compiler generates roughly:
//   var list = new List<int>(3);
//   list.Add(1); list.Add(2); list.Add(3);
List<int> scores = [95, 82, 77];

// For a span over a stack-allocated buffer, no heap allocation happens at all:
Span<int> triple = [1, 2, 3];
```

## Performance Notes

- **Arrays and spans:** collection expressions for `T[]` and `Span<T>` allocate exactly once, the same as the older initializers. A `Span<T>` literal is stack-allocated when the span stays local, so it is allocation-free.
- **Lists:** a `List<T>` literal pre-sizes the list to the element count, avoiding the growth reallocations of repeated `Add` calls on a default-constructed list.
- **No runtime cost:** the feature is compile-time sugar. There is no reflection, no dynamic dispatch, and no hidden allocation beyond what the target type itself requires.
- **Prefer `ReadOnlySpan<T>` for hot paths:** when a method only reads a small fixed set of values, `ReadOnlySpan<int> values = [1, 2, 3];` avoids a heap allocation entirely.

## Key Takeaways

- Collection expressions use `[...]` syntax for arrays, lists, sets, immutable arrays, and spans — one literal, many target types.
- The literal is target-typed: the declared type, return type, or parameter type decides what is built; `var` cannot be used with it.
- The spread element `..` splices an existing collection (or string) into a new one, replacing `Concat` and `AddRange` in literals.
- The compiler generates the same efficient code as the older initializers — arrays allocate once, lists are pre-sized, spans can be stack-allocated.
- Use them for readability, not performance: the win is less ceremony, and the runtime cost is identical to what you would have written by hand.
