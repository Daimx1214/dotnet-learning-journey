# Required Members and Init-Only Setters in C#

C# 11 introduced `required` members and C# 9 introduced `init`-only setters. Together they let a type guarantee that certain properties are set during object creation — and never changed afterwards.

## Init-Only Setters

An `init` accessor works like `set`, but it can only run during object initialization (or in a constructor). After that, the property is read-only.

```csharp
public class User
{
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; }
}

var user = new User { Name = "Daim", Age = 21 };
// user.Name = "Ali"; // Compile error: init-only property can only be assigned in an object initializer
```

This gives you immutable-after-construction objects without writing a long constructor, and it works naturally with records and `with` expressions.

## The `required` Keyword

`init` alone does not force the caller to set the property — `new User()` compiles fine and leaves `Name` as `string.Empty`. The `required` modifier (C# 11) makes the compiler enforce it:

```csharp
public class Order
{
    public required string OrderId { get; init; }
    public required decimal Total { get; init; }
    public string? Notes { get; init; }
}

var order = new Order { OrderId = "ORD-1", Total = 49.99m }; // OK
// var bad = new Order { OrderId = "ORD-2" }; // Compile error: Total must be set
```

`required` works on properties and fields, and on both `class` and `struct` types. The compiler checks every construction site, including deserialization paths that use a parameterless constructor — which is why the next section matters.

## SetsRequiredMembers

Sometimes a constructor sets the required members itself, so callers should not have to. Mark such a constructor with `[SetsRequiredMembers]`:

```csharp
public class Invoice
{
    public required string Number { get; init; }
    public required decimal Amount { get; init; }

    public Invoice() { } // Callers must set Number and Amount.

    [SetsRequiredMembers]
    public Invoice(string number, decimal amount)
    {
        Number = number;
        Amount = amount;
    }
}

var invoice = new Invoice("INV-100", 120.00m); // OK — no initializer needed
```

Without the attribute, the compiler would wrongly demand an object initializer even though the constructor already sets everything. Note that `[SetsRequiredMembers]` is a promise: if the constructor forgets a member, the compiler will not catch it.

## Key Takeaways

- `init` accessors allow a property to be set only during object initialization, making objects immutable afterwards.
- `required` (C# 11) makes the compiler enforce that a member is set at construction time — missing members are compile errors, not runtime nulls.
- `required` pairs naturally with `init`, but it also works with `set` and with fields.
- `[SetsRequiredMembers]` tells the compiler a constructor already assigns the required members, so callers need no initializer.
- Use `required` + `init` for DTOs, configuration objects, and domain models where a missing value would be a bug — it moves that bug from runtime to compile time.
