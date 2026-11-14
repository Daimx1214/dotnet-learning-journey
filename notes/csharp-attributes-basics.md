# Attributes in C# (C#)

Attributes are metadata attached to code — classes, methods, properties, assemblies — that the compiler, the runtime, or your own code can read. They do not change what the code does by themselves; they describe it. The compiler uses attributes to warn you (`[Obsolete]`), the serializer uses them to control JSON names (`[JsonPropertyName]`), and test frameworks use them to discover tests (`[Fact]`). You can also define your own attributes and read them with reflection.

## Built-in Attributes You Will Meet Often

| Attribute | What it does |
|-----------|--------------|
| `[Obsolete("Use X instead")]` | Compiler warning (or error) when the member is used |
| `[Serializable]` | Marks a type as serializable (legacy binary serialization) |
| `[Flags]` | A enum whose values can be combined with bitwise OR |
| `[DllImport("user32.dll")]` | Declares a P/Invoke call into native code |
| `[Conditional("DEBUG")]` | Method is only called when the symbol is defined |
| `[CallerMemberName]` / `[CallerFilePath]` | Compiler fills in the caller's name/path automatically |
| `[Required]`, `[Range]`, `[StringLength]` | Data-annotation validation rules for models |

```csharp
public class LegacyService
{
    [Obsolete("Use GetCustomerById instead. This method will be removed in v3.")]
    public string GetCustomer(int id) => $"Customer {id}";

    [Conditional("DEBUG")]
    public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
}

[Flags]
public enum FileAccess
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
```

`[Conditional("DEBUG")]` is subtly different from `#if DEBUG`: the method still exists and is compiled, but call sites are removed when the symbol is not defined. That keeps the API surface stable while stripping debug-only behaviour from release builds.

## Defining a Custom Attribute

A custom attribute is a class that derives from `System.Attribute` and by convention ends in `Attribute` — the suffix can be omitted when applying it (`[HelpText]` instead of `[HelpTextAttribute]`). Positional arguments map to constructor parameters; named arguments map to settable properties. `AttributeUsage` controls where the attribute may be applied, whether it can be applied more than once, and whether subclasses inherit it.

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class HelpTextAttribute : Attribute
{
    public string Text { get; }
    public string Category { get; set; } = "General";

    public HelpTextAttribute(string text) => Text = text;
}

public class Commands
{
    [HelpText("Prints all customers.", Category = "Reporting")]
    public void ListCustomers() => Console.WriteLine("Listing...");

    [HelpText("Deletes a customer by id.")]
    public void DeleteCustomer(int id) => Console.WriteLine($"Deleting {id}...");
}
```

`AttributeTargets.Method` restricts the attribute to methods — applying it to a class would be a compile error. `AllowMultiple = false` means one instance per target, and `Inherited = true` means a derived class's override still carries the attribute from the base method.

## Reading Attributes with Reflection

Attributes are inert until something reads them. Reflection is the usual reader: `GetCustomAttribute<T>()` on a `MemberInfo` (or `GetCustomAttributes<T>()` for multiples) returns the attribute instances, and you can then use their properties to drive behaviour — a help-text printer, a mini command router, or a validation pass.

```csharp
using System.Reflection;

public static void PrintHelp(Type type)
{
    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
    {
        HelpTextAttribute? help = method.GetCustomAttribute<HelpTextAttribute>();
        if (help is not null)
        {
            Console.WriteLine($"{method.Name} [{help.Category}]: {help.Text}");
        }
    }
}

public static void Main()
{
    PrintHelp(typeof(Commands));
    // ListCustomers [Reporting]: Prints all customers.
    // DeleteCustomer [General]: Deletes a customer by id.
}
```

Reflection is powerful but slow and stringly-typed, so read attributes once at startup and cache the results — do not call `GetCustomAttribute` inside a hot loop. If the metadata drives compile-time behaviour instead, consider a source generator, which reads the same attributes without any runtime reflection cost.

## Pitfalls

- Attribute arguments must be compile-time constants (or `typeof(...)` expressions) — you cannot pass a variable, a `DateTime`, or a list.
- An attribute on a method does nothing unless something reads it; `[Obsolete]` works only because the compiler reads it.
- Attribute instances are created fresh on every reflection read, so do not store mutable state in them or compare instances by reference.

## Key Takeaways

- Attributes are metadata: they describe code for the compiler, the runtime, or your own reflection code.
- Define custom attributes by deriving from `System.Attribute` and control their targets with `[AttributeUsage]`.
- Read them with `GetCustomAttribute<T>()`, cache the results, and prefer source generators when the behaviour is compile-time.
- Arguments must be compile-time constants, and an attribute only matters if something reads it.
