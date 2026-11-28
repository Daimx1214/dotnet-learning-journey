# Null-Conditional and Null-Coalescing Operators in C#

The null-conditional operator (`?.` and `?[]`) and the null-coalescing operators (`??` and `??=`) remove most explicit null checks from C# code. They are small, but their short-circuiting and type rules have sharp edges worth knowing.

## Null-Conditional: ?. and ?[]

`?.` evaluates the left side once; if it is null, the whole chain short-circuits to null without evaluating the rest. If it is not null, the member access proceeds:

```csharp
string? name = customer?.Address?.City;
int? length = name?.Length;

// Indexer form
string? firstTag = order?.Tags?[0];
```

A chain short-circuits at the first null. In `customer?.Address?.City`, if `customer` is null, `Address` is never read, and if `Address` is null, `City` is never read. The result type is always nullable: `name?.Length` is `int?` even though `Length` is `int`.

`?.` also works for method calls and delegates:

```csharp
customer?.Notify("shipped");          // skipped entirely if customer is null
Updated?.Invoke(this, EventArgs.Empty); // the classic null-safe event raise
```

## Null-Coalescing: ?? and ??=

`??` returns the left operand when it is not null, otherwise the right operand:

```csharp
string displayName = user.Nickname ?? user.FullName ?? "Guest";
int count = items?.Count ?? 0;
```

`??=` assigns the right side only when the left side is null. It is the lazy-initialization shorthand:

```csharp
private List<string>? _tags;
public List<string> Tags => _tags ??= new List<string>();

// Equivalent longhand:
// if (_tags is null) _tags = new List<string>();
// return _tags;
```

The right side of `??` is evaluated only when needed, so `GetDefault()` in `value ?? GetDefault()` runs only when `value` is null.

## Combining ?. Chains with ??

The common pattern is a `?.` chain that may produce null, with `??` supplying the fallback:

```csharp
string city = customer?.Address?.City ?? "Unknown";
int length = text?.Length ?? 0;
```

Read this left to right: the chain yields a `string?` (or `int?`), and `??` replaces null with the fallback. This is the idiomatic way to dig into a nullable object graph safely.

## Short-Circuiting Rules

- `?.` short-circuits the rest of the chain, including method arguments: in `a?.M(B())`, `B()` is not called when `a` is null.
- `??` short-circuits the right side when the left side is not null.
- `??=` short-circuits the assignment and the right side when the left side is not null.

These rules matter when the skipped expression has side effects, such as logging or incrementing a counter.

## Common Pitfalls

### ?. on the left of ?? with a non-nullable fallback type

`?.` produces a nullable result, so the `??` fallback must match that nullable type or be implicitly convertible. This compiles:

```csharp
int length = text?.Length ?? 0; // int? ?? int -> int
```

But mixing incompatible types does not:

```csharp
// string? ?? int  ->  compile error
object result = text?.Length ?? "none"; // OK: int? boxes to object
```

### Lifted operators

When `?.` is applied to a value type member, the operator is "lifted": the result becomes `Nullable<T>` and the operation is skipped on null:

```csharp
int? doubled = number?.ToString().Length; // null when number is null
bool? isLong = text?.Length > 5;          // null, not false, when text is null
```

`text?.Length > 5` yields `bool?`, so `if (text?.Length > 5)` does not compile: an `if` needs a plain `bool`. Use `text?.Length > 5 == true` or `text is { Length: > 5 }` instead.

### ?. does not protect against null inside the chain

`?.` only guards the value immediately to its left. Once a member access succeeds without `?.`, a null there throws:

```csharp
string city = customer.Address?.City; // throws if customer is null
string city = customer?.Address.City; // throws if Address is null
string city = customer?.Address?.City; // safe
```

### ??= on properties with only a getter

`??=` needs a setter on the left side. `_tags ??= new()` works because `_tags` is a field; `Tags ??= new()` fails when `Tags` has no setter.

## Key Takeaways

- `?.` and `?[]` short-circuit a chain at the first null and always produce a nullable result.
- `??` supplies a fallback for null; `??=` assigns lazily, only when the left side is null.
- Combine them as `a?.B?.C ?? fallback` for safe deep access with a default.
- Watch lifted operators: `?.` on value types yields `Nullable<T>`, so comparisons produce `bool?`, not `bool`.
- `?.` guards only the value directly to its left; every link in the chain needs its own `?.`.
