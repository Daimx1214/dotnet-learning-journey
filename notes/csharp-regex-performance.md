# Regex Performance in C#

Regular expressions are convenient, but a careless pattern can be
orders of magnitude slower than the code around it — and in the worst
case, a single hostile input can hang the process. Most of the cost
comes from how the pattern is compiled and from backtracking.

## Compile Once, Reuse

A `Regex` parses its pattern on construction. Creating a new `Regex`
inside a loop pays that cost every iteration. Create it once as a static
field, or use `RegexOptions.Compiled` to generate IL for the matcher:

```csharp
// Parsed once, reused forever.
private static readonly Regex EmailRegex = new(
    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase,
    TimeSpan.FromSeconds(2));
```

`RegexOptions.Compiled` trades slower startup for faster matching — a
win for patterns used often, a loss for one-off matches. The static
helpers `Regex.IsMatch(input, pattern)` cache the last 15 patterns, so
they are fine for occasional use but not for hot loops.

## Source Generators: [GeneratedRegex]

In .NET 7+, `[GeneratedRegex]` generates the matching code at compile
time, giving compiled-level speed with zero startup cost:

```csharp
public partial class Validators
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    public static partial Regex EmailRegex();
}

// Usage:
bool valid = Validators.EmailRegex().IsMatch(email);
```

The method must be `static partial` inside a `partial` class. This is
the best option for hot paths in modern .NET.

## Timeouts

Always set a timeout on a regex that touches user input. Without one, a
pathological input can run effectively forever:

```csharp
var regex = new Regex(@"^(a+)+$", RegexOptions.None,
    TimeSpan.FromSeconds(1));

try
{
    bool match = regex.IsMatch(userInput);
}
catch (RegexMatchTimeoutException)
{
    // Treat as no match; log the input for review.
}
```

The timeout is checked periodically during matching, so it bounds the
damage even when the pattern itself is slow.

## Catastrophic Backtracking

Nested quantifiers are the classic trap. The pattern `^(a+)+$` against
the input `"aaaaaaaaaaaaaaaaaaaaaaaa!"` (many `a`s, no match) explodes:
the engine tries every way to split the `a`s between the inner and
outer `+`, and the number of combinations grows exponentially with the
input length.

The fix is to remove the ambiguity. Most cases need only one quantifier:

```csharp
// Bad: nested quantifiers, exponential on non-matching input.
var bad = new Regex(@"^(a+)+$");

// Good: a single quantifier says the same thing here.
var good = new Regex(@"^a+$");
```

When the structure genuinely needs nesting, use atomic groups
`(?>...)` or possessive quantifiers (`a++` in .NET 7+) to stop the
engine from backtracking into a group once it has matched, or rewrite
the pattern so each character can only be consumed one way.

## Span-Based Alternatives

For simple checks, a hand-written span loop is faster than any regex
and allocates nothing:

```csharp
// Faster than Regex.IsMatch(input, @"^\d+$") and allocation-free.
public static bool IsAllDigits(ReadOnlySpan<char> input)
{
    if (input.IsEmpty) return false;
    foreach (char c in input)
        if (!char.IsDigit(c)) return false;
    return true;
}
```

Reach for `string.Contains`, `StartsWith`, `IndexOf`, or a span loop
before a regex when the task is a fixed string, a prefix, or a simple
character class. Regex earns its place for real patterns — emails,
structured formats, tokenising — not for checks a loop does in a line.

## Key Takeaways

- Create a `Regex` once (static field) and reuse it; never construct one
  in a hot loop.
- `RegexOptions.Compiled` speeds repeated matching; `[GeneratedRegex]`
  is faster still, with no startup cost.
- Always set a match timeout on patterns that see user input.
- Nested quantifiers cause catastrophic backtracking — remove the
  ambiguity with one quantifier, atomic groups, or possessive
  quantifiers.
- For simple checks, a span loop or `string.Contains` beats a regex on
  speed and allocations.
