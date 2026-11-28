# Partial Types and Methods in C#

`partial` splits one type or method across multiple files. The compiler merges the pieces into a single type, so at runtime there is no difference from writing it all in one file. Its main uses are generated code and organizing large types.

## Partial Classes, Structs, and Records

Every part must be declared `partial`, in the same assembly and namespace, with the same name. The parts share all members as if they were written together:

```csharp
// File: Order.cs
public partial class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}

// File: Order.Validation.cs
public partial class Order
{
    public bool IsValid() => Id > 0 && Total >= 0;
}
```

```csharp
var order = new Order { Id = 1, Total = 49.99m };
Console.WriteLine(order.IsValid()); // True
```

This also works for `struct`, `record`, and `record struct`. It does not work across assemblies: a partial type must be fully declared within one assembly.

Common legitimate uses:

- **Generated code:** a tool (Entity Framework scaffolding, WinForms designer, source generator) owns one file, and you own the other. Regenerating never overwrites your hand-written part.
- **Large types:** splitting a big class by responsibility (e.g. `Order.cs`, `Order.Validation.cs`, `Order.Reporting.cs`) keeps files manageable.

Do not use partial types merely to hide a large class from yourself. If a class needs five files, it usually needs to be split into five classes.

## Partial Methods

A partial method is declared in one part and optionally implemented in another. The classic use is a hook: generated code calls the method, and you implement it only if you need custom behaviour.

```csharp
// File: Entity.cs (generated)
public partial class Entity
{
    public void Save()
    {
        OnBeforeSave(); // hook: does nothing if unimplemented
        Console.WriteLine("Saving...");
    }

    partial void OnBeforeSave();
}

// File: Entity.Custom.cs (hand-written)
public partial class Entity
{
    partial void OnBeforeSave() => Console.WriteLine("Validating before save.");
}
```

```csharp
new Entity().Save();
// Validating before save.
// Saving...
```

If the implementation part is missing, the compiler removes the declaration **and every call to it**, so there is zero runtime cost for an unused hook.

### Declaration vs Implementation Rules

- The declaration has no body and no access modifier in the classic form: `partial void OnBeforeSave();`
- The implementation supplies the body: `partial void OnBeforeSave() { ... }`
- Signatures must match exactly, including parameter names in the sense that types and order match.
- Historically, partial methods had to return `void`, could not have `out` parameters, and could not be `public`. Those restrictions existed because an unimplemented method had to be erasable.

### .NET 7+ Partial Methods with Return Values

Since C# 11 (.NET 7), partial methods can return values and have accessibility modifiers, as long as the method is implemented. The trade-off: if the method is **not** implemented, the compiler errors instead of silently erasing the call, because a return value cannot be erased:

```csharp
public partial class Calculator
{
    // Declaration: public, returns a value
    public partial int Add(int a, int b);
}

public partial class Calculator
{
    // Implementation is now required; omitting it is a compile error
    public partial int Add(int a, int b) => a + b;
}
```

This form is designed for source generators: the generator declares a method it expects the user to implement, or declares one the user calls and the generator implements.

## Source-Generator Use Case

Source generators are the main reason partial methods exist today. A generator adds a file to your compilation and implements the partial methods you declared, or vice versa:

```csharp
// Your code
public partial class UserService
{
    public partial string FormatName(string first, string last);
}

// Generated code (added at compile time by a source generator)
public partial class UserService
{
    public partial string FormatName(string first, string last) =>
        $"{last}, {first}";
}
```

The same pattern powers framework features: ASP.NET Core's minimal APIs and `System.Text.Json` source generation both rely on partial types to merge generated code with your code.

## Rules Summary

- All parts must use the `partial` keyword, share the namespace, and live in one assembly.
- Members (fields, properties, methods) are shared across parts; a field declared in one part is visible in all.
- Partial methods: declaration without a body in one part, implementation in another; classic form must be `private void` and is erased when unimplemented.
- C# 11 form allows return values and accessibility, but then the implementation is mandatory.

## Key Takeaways

- `partial` merges multiple files into one type at compile time; it is a compile-time organization tool, not a runtime feature.
- Its main use is separating generated code from hand-written code so regeneration never overwrites your work.
- Partial methods are hooks: unimplemented classic partial methods are erased along with their calls, costing nothing.
- Since .NET 7, partial methods can return values and be public, but then the implementation is required.
- Source generators are the modern driver of partial types and methods.
