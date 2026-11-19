# dynamic and ExpandoObject

The `dynamic` keyword tells the compiler to skip static type checking and resolve member access at runtime. That is useful at boundaries where the shape of the data is not known at compile time, but it gives up the safety net that makes C# code reliable.

## When dynamic Is Legitimate

Two cases genuinely need it: COM interop, where Office APIs return loosely typed objects, and data whose shape is only known at runtime, such as a JSON document you do not want to model with a class. In those cases `dynamic` removes pages of casting and reflection code.

```csharp
using System.Dynamic;
using System.Text.Json;

dynamic person = JsonSerializer.Deserialize<ExpandoObject>("""{"Name":"Daim","Age":22}""")!;
Console.WriteLine(person.Name); // Daim
Console.WriteLine(person.Age);  // 22

// ExpandoObject lets you build a dynamic object member by member.
dynamic settings = new ExpandoObject();
settings.Theme = "dark";
settings.Retries = 3;
Console.WriteLine(settings.Theme);   // dark
Console.WriteLine(settings.Retries); // 3
```

`ExpandoObject` is a dictionary in disguise: you can cast it to `IDictionary<string, object?>` and enumerate or add members programmatically, which is handy for building JSON-like payloads on the fly.

## When to Avoid It

Everywhere else, prefer a real type. A typo in a member name (`person.Nmae`) compiles fine with `dynamic` and only fails at runtime with a `RuntimeBinderException`. The compiler cannot help with refactoring, IntelliSense, or overload resolution, and readers of the code cannot tell what members exist without running it. Deserialising JSON into a declared record or class is almost always better than `dynamic` when the shape is known.

## Performance Note

`dynamic` member access goes through the DLR (Dynamic Language Runtime), which caches call sites, so repeated calls to the same shape are much faster than the first — but still slower than a direct typed call, and each new shape pays a binding cost. In a tight loop over thousands of items, that overhead is real. Keep `dynamic` at the edges of your program (parsing input, calling COM) and convert to strong types as soon as possible.

## Key Takeaways

- `dynamic` defers member resolution to runtime — useful for COM interop and schema-less JSON.
- `ExpandoObject` builds dynamic objects member by member and doubles as a string-keyed dictionary.
- Typos and missing members only fail at runtime with `dynamic`; the compiler cannot catch them.
- Prefer declared records or classes whenever the data shape is known at compile time.
- `dynamic` dispatch is slower than typed calls, so convert to strong types early and keep it out of hot loops.
