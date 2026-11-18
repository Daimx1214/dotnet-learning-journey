# Enums and Flags Enums in C#

An `enum` is a value type that gives names to a set of related integer constants. Instead of scattering magic numbers through your code — `if (status == 2)` — you write `if (status == OrderStatus.Shipped)`, and the compiler checks that you are comparing the right kind of value. A `[Flags]` enum extends the idea: its values are bit flags that can be combined with bitwise OR, so one variable can hold several options at once.

It matters because enums are everywhere in .NET — HTTP status codes, days of the week, log levels, file access modes — and the flags variant is the standard way to express "any combination of these options" in an API.

## Basic Enums

```csharp
public enum OrderStatus
{
    Pending,    // 0
    Paid,       // 1
    Shipped,    // 2
    Delivered,  // 3
    Cancelled   // 4
}
```

By default the underlying type is `int` and values start at 0, incrementing by 1. You can assign explicit values — useful when the numbers are part of a contract (a database column, a wire protocol, an external API):

```csharp
public enum HttpStatus
{
    Ok = 200,
    Created = 201,
    BadRequest = 400,
    Unauthorized = 401,
    NotFound = 404,
    InternalServerError = 500
}
```

A few rules worth remembering:

- The **first value should be 0** and should mean "none / unknown / default". The default value of any enum is 0, so if 0 is not a named member, a default-initialised variable holds a value with no name.
- Duplicate values are allowed, but two names for one number is usually a design smell.
- Enums are **not** type-safe against arbitrary integers: `(OrderStatus)99` compiles and produces a value with no name. Validate with `Enum.IsDefined` when the value comes from outside (user input, a database, deserialization).

```csharp
public static bool IsValidStatus(int value) => Enum.IsDefined(typeof(OrderStatus), value);
```

## Switching Over Enums

A `switch` over an enum is clear and the compiler can warn you about missing cases:

```csharp
public static string Describe(OrderStatus status) => status switch
{
    OrderStatus.Pending   => "Waiting for payment",
    OrderStatus.Paid      => "Payment received",
    OrderStatus.Shipped   => "On its way",
    OrderStatus.Delivered => "Delivered",
    OrderStatus.Cancelled => "Cancelled",
    _                     => "Unknown status"
};
```

The `_` arm matters: because any integer can be cast to the enum, a value with no name can arrive at runtime. Handle it explicitly instead of letting it fall through silently.

## Flags Enums

Mark an enum with `[Flags]` when values can be combined. Each member must be a distinct power of two so the bits never overlap:

```csharp
[Flags]
public enum FileAccess
{
    None    = 0,
    Read    = 1 << 0, // 1
    Write   = 1 << 1, // 2
    Execute = 1 << 2, // 4
    Delete  = 1 << 3  // 8
}
```

Using `1 << n` instead of literal numbers makes the pattern obvious and prevents mistakes when you add a member later. Combinations are built with bitwise OR (`|`) and tested with bitwise AND (`&`) or `HasFlag`:

```csharp
var permissions = FileAccess.Read | FileAccess.Write;

// Test with HasFlag — readable, slightly slower.
bool canRead = permissions.HasFlag(FileAccess.Read);       // true
bool canDelete = permissions.HasFlag(FileAccess.Delete);   // false

// Test with bitwise AND — faster, equally correct.
bool canWrite = (permissions & FileAccess.Write) != 0;     // true

// Add a flag.
permissions |= FileAccess.Execute;

// Remove a flag.
permissions &= ~FileAccess.Write;

// Toggle a flag.
permissions ^= FileAccess.Read;
```

The .NET `FileAccess`, `RegexOptions`, and `BindingFlags` enums are all flags enums — this is the pattern the framework itself uses for option combinations.

### Why the [Flags] Attribute Matters

The attribute does not change the bitwise behaviour — OR and AND work without it. What it changes is the *string representation*: `ToString()` on a flags enum with the attribute prints the combined names (`"Read, Write"`), while without it prints the raw number (`"3"`). Always add the attribute when the enum is meant to be combined; it is also a signal to other developers that combination is intended.

```csharp
Console.WriteLine(permissions.ToString()); // "Read, Write" (with [Flags])
```

### Common Flags Members

Two conventional members make flags enums easier to use:

```csharp
[Flags]
public enum NotificationChannel
{
    None  = 0,
    Email = 1 << 0,
    Sms   = 1 << 1,
    Push  = 1 << 2,
    All   = Email | Sms | Push
}
```

`None = 0` means "no flags set" — the natural default. `All` is a convenience combination, but define it as an OR of the members (not a literal) so it stays correct when a member is added.

## Parsing and Converting

Enums convert to and from strings and integers constantly — configuration values, API payloads, database columns.

```csharp
// String -> enum (case-insensitive).
var status = Enum.Parse<OrderStatus>("shipped", ignoreCase: true);

// Safe version: returns false instead of throwing.
if (Enum.TryParse<OrderStatus>(input, ignoreCase: true, out var parsed))
    Console.WriteLine($"Parsed: {parsed}");

// Enum -> string: the name.
string name = OrderStatus.Shipped.ToString(); // "Shipped"

// Enum -> number.
int number = (int)OrderStatus.Shipped;        // 2

// Number -> enum (unchecked: any int is allowed).
var fromNumber = (OrderStatus)2;              // Shipped
```

For flags enums, `Enum.Parse` also accepts comma-separated combinations: `Enum.Parse<FileAccess>("Read, Write")` gives `Read | Write`. That makes flags enums easy to store as strings in configuration.

## Underlying Types

The default underlying type is `int`, but you can choose a smaller (or unsigned) type when the values are stored or transmitted in bulk:

```csharp
public enum SmallStatus : byte
{
    Pending, Paid, Shipped
}

public enum LargeFlags : long
{
    Option1 = 1L << 0,
    Option2 = 1L << 1,
    // ... up to 64 flags with long
}
```

A `byte` enum uses 1 byte per value instead of 4 — worth it in large arrays or compact storage. A `long` flags enum supports up to 64 flags instead of 32. The choice is a storage decision, not a behaviour one: the API surface is identical.

## Best Practices

- **Start at 0 with a "None" / "Unknown" member.** The default value of the type must be meaningful.
- **Use powers of two (via `1 << n`) for flags enums**, and always apply the `[Flags]` attribute.
- **Never reorder or reuse numeric values** once an enum is persisted (database, serialized payload, public API). Add new members at the end.
- **Do not rely on the numeric values being contiguous** — use `Enum.GetValues<T>()` to enumerate members instead of looping over integers.
- **Validate external input** with `Enum.IsDefined` or `Enum.TryParse`; a cast from an arbitrary integer always succeeds.
- **Prefer enums over string constants** for fixed sets of values: the compiler catches typos, IntelliSense lists the options, and refactoring is safe.

```csharp
// Enumerating all members (safe even with gaps in the numbers):
foreach (var status in Enum.GetValues<OrderStatus>())
    Console.WriteLine($"{(int)status} = {status}");
```

## Key Takeaways

- An enum gives names to related constants; the default underlying type is `int`, values start at 0, and the 0 member should mean "none / unknown".
- Use a `switch` expression over enums for clear dispatch, and keep a fallback arm because any integer can be cast to the enum.
- `[Flags]` enums use powers of two (`1 << n`) so values combine with `|` and are tested with `HasFlag` or `&`; the attribute makes `ToString()` print the combined names.
- Define `None = 0` and, where useful, `All` as an OR of the members — never as a literal number.
- Parse with `Enum.TryParse` (safe) rather than `Enum.Parse` (throws), and validate external integers with `Enum.IsDefined`.
- Never reorder or reuse numeric values after an enum is persisted or public; append new members at the end.
