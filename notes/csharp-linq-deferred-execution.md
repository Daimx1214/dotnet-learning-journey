# LINQ Deferred Execution

Most LINQ operators do not run when they are written. They build a query object, and the query only executes when something enumerates it. That laziness is the single most misunderstood behaviour in LINQ, and it explains a whole class of bugs: queries that run twice, exceptions that surface far from the query, and filters that see data changed after the query was defined.

## IEnumerable Laziness

A query over `IEnumerable<T>` is a recipe, not a result. Nothing is read, filtered, or projected until enumeration starts, and enumeration re-runs the whole pipeline from the start every time.

```csharp
var numbers = new[] { 1, 2, 3, 4, 5 };

// Nothing has run yet: no element has been touched.
var evens = numbers.Where(n => n % 2 == 0);

// Enumeration starts here, and only here.
foreach (var n in evens)
    Console.WriteLine(n);   // prints 2, then 4
```

If `numbers` is mutated between defining `evens` and enumerating it, the query sees the mutated data. A query captures the source, not a snapshot of it.

## When Queries Actually Run

| Operator | Execution | Notes |
|---|---|---|
| `Where`, `Select`, `OrderBy`, `Take`, `Skip` | Deferred | Build the pipeline; run on enumeration |
| `ToList`, `ToArray`, `ToDictionary` | Immediate | Materialize: run the whole pipeline now |
| `Count`, `Sum`, `Average`, `First`, `Any` | Immediate | Run the pipeline (or as much as needed) now |
| `First`, `Single`, `Any` | Immediate, short-circuiting | Stop as soon as the answer is known |
| `OrderBy` | Deferred, but buffering | Runs on enumeration, then sorts the whole sequence before yielding the first element |

`OrderBy` is the subtle one: it is deferred, but once enumeration starts it must consume the entire source before yielding anything, because the first output element depends on every input element.

## Side-Effect Demo with an Exact Counter Trace

A counter inside the predicate makes the laziness countable. Trace every value by hand:

```csharp
var numbers = new[] { 1, 2, 3, 4, 5 };
int evaluations = 0;

var query = numbers.Where(n =>
{
    evaluations++;
    return n % 2 == 0;
});

Console.WriteLine(evaluations);            // 0: the query has not run

var first = query.First();                 // enumerates 1 (odd), then 2 (even) and stops
Console.WriteLine(first);                  // 2
Console.WriteLine(evaluations);            // 2

var list = query.ToList();                 // re-enumerates from the start: all 5 elements
Console.WriteLine(evaluations);            // 2 + 5 = 7
Console.WriteLine(list.Count);             // 2  (elements 2 and 4)

foreach (var _ in query) { }               // a third full enumeration: 5 more
Console.WriteLine(evaluations);            // 7 + 5 = 12
```

Verify the arithmetic: `First()` stops at the first match, so it evaluates exactly 2 elements (1 and 2). `ToList()` restarts from the beginning and evaluates all 5, taking the counter from 2 to 7. The `foreach` evaluates all 5 again, taking it from 7 to 12. The same query object produced three separate executions — 2 + 5 + 5 = **12** predicate evaluations in total — and the counter proves it.

## Multiple Enumeration Cost

Every enumeration re-runs the pipeline, including any expensive work inside it: database calls, file reads, or per-element computation. Enumerating the same query three times costs three times the work, and if the source is a database query, three round trips.

```csharp
// BAD: the filter and the database call run once per use.
var expensive = db.Products.Where(p => p.Price > 100);
var count = expensive.Count();       // round trip 1
var top = expensive.Take(10).ToList(); // round trip 2

// GOOD: materialize once, then reuse the in-memory list.
var products = db.Products.Where(p => p.Price > 100).ToList();
var count = products.Count;          // no query
var top = products.Take(10).ToList(); // no query
```

`ToList()` and `ToArray()` are the materialization operators: they run the query once, store the results, and every later use reads the stored list. The trade-off is memory — the whole result set is held at once — so materialize when the result is reused, and stream when it is consumed once and is large.

## Deferred vs Immediate Operators

The rule of thumb: operators that return `IEnumerable<T>` (or `IOrderedEnumerable<T>`) are usually deferred; operators that return a single value, a scalar, or a concrete collection are immediate. The exceptions to remember are `OrderBy` (deferred but buffering) and `First`/`Any` (immediate but short-circuiting — they do not necessarily consume the whole sequence).

```csharp
var source = Enumerable.Range(1, 1_000_000);

// Deferred: returns instantly, no work done.
var query = source.Where(n => n % 2 == 0).Select(n => n * 2);

// Immediate: forces the whole pipeline to run now.
var materialized = query.ToList();
```

## Key Takeaways

- A LINQ query is a recipe: defining it runs nothing; enumeration runs everything, from the start, every time.
- The counter trace proves it: `First()` evaluated 2 elements, `ToList()` 5 more, a `foreach` 5 more — **12** evaluations from one query object.
- Materialize with `ToList()`/`ToArray()` when a result is reused; stream when it is consumed once.
- `OrderBy` is deferred but buffers the whole sequence; `First`/`Any` are immediate but stop early.
- Side effects inside a query predicate run once per enumeration, so keep predicates pure.
