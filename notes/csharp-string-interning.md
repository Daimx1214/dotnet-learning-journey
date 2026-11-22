# String Interning in C#

Every string literal in a .NET program is **interned** automatically: the
runtime keeps one shared copy of each distinct literal in the *intern pool*,
so two variables holding the literal `"hello"` point to the same object in
memory. Interning trades memory for reference comparisons — but only when
used deliberately.

## How Literals Are Interned

```csharp
string a = "hello";
string b = "hello";
Console.WriteLine(ReferenceEquals(a, b)); // True — same pooled object

string c = new string(new[] { 'h', 'e', 'l', 'l', 'o' });
Console.WriteLine(ReferenceEquals(a, c)); // False — c is a fresh object
Console.WriteLine(a == c);                // True — == compares by value
```

Computed strings are **not** interned automatically. Concatenation,
`ToString()`, parsing and `Substring` all create fresh objects, even if an
equal string already exists in the pool.

## string.Intern and string.IsInterned

You can intern a string yourself and check whether one is already pooled:

```csharp
string dynamic = ReadTagFromFile();        // a fresh object
string? pooled = string.IsInterned(dynamic);
if (pooled is null)
{
    pooled = string.Intern(dynamic);       // add to the pool, return the pooled instance
}
Console.WriteLine(ReferenceEquals(dynamic, pooled)); // False — dynamic is still the old object
Console.WriteLine(ReferenceEquals(string.Intern(dynamic), pooled)); // True
```

`string.Intern` returns the pooled instance, which may be a *different
object* from the one you passed in. Always use the return value afterwards.
`IsInterned` returns the pooled instance if present, or `null`.

## The Memory Trade-Off

Interning helps when the same strings repeat thousands of times — parsed
tokens, column names, tags, enum-like values read from files. Instead of
10,000 copies of `"Pending"`, the pool holds one and every variable points
at it.

The cost is real, though:

- The intern pool lives for the **lifetime of the AppDomain** (effectively the
  process). Interned strings are never garbage collected, so interning
  unbounded user input is a memory leak by design.
- `string.Intern` takes a lock and does a hash-table lookup; interning in a
  hot loop can cost more than it saves.
- After interning, comparisons with `ReferenceEquals` are fast, but `==` on
  strings is already optimized (it checks reference equality first), so the
  speed gain is usually small.

## When (Not) to Intern

**Consider interning** when a bounded set of strings repeats heavily: a
fixed vocabulary of status values, keys read from a config file thousands of
times, or tokens in a parser.

**Do not intern** unbounded or user-supplied data: free-text input, IDs from a
large dataset, or anything where the number of distinct values grows with the
input. The pool will grow forever and never shrink.

## The Better Alternative: Dictionaries and string.Create

Most of the time, a `Dictionary<string, T>` or a `HashSet<string>` gives the
same deduplication without touching the global pool — and the entries *can*
be collected when the dictionary goes out of scope:

```csharp
var seen = new HashSet<string>(StringComparer.Ordinal);
foreach (string tag in ReadTags())
    if (seen.Add(tag)) Process(tag); // only first occurrence processed
```

And when the goal is avoiding allocations rather than deduplication, use
`string.Create` to build a string directly into its final buffer:

```csharp
// Builds "ID-0042" with a single allocation, no intermediate strings
string id = string.Create(null, stackalloc char[16], (span, _) =>
{
    "ID-".CopyTo(span);
    42.TryFormat(span[3..], out _);
});
```

## Key Takeaways

- String literals are interned automatically; computed strings are not.
- `string.Intern` adds a string to the pool and returns the pooled instance;
  `string.IsInterned` returns it or `null`. Always use the return value.
- Interning saves memory only for a bounded, heavily repeated vocabulary.
- Interned strings live for the life of the process — interning unbounded
  input is a leak.
- Prefer a `Dictionary`/`HashSet` for deduplication and `string.Create` for
  allocation-free construction; intern only when the pool semantics are
  genuinely what you need.
