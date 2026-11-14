# JSON Serialization in C# (C#)

`System.Text.Json` is the built-in JSON library for .NET. It replaced `Newtonsoft.Json` as the default for ASP.NET Core, and it is fast because it works directly on UTF-8 bytes with minimal allocations. The two entry points are `JsonSerializer.Serialize` and `JsonSerializer.Deserialize<T>`, both of which accept a `JsonSerializerOptions` to control naming, casing, null handling, and more.

## Serialize and Deserialize

By default, property names are written exactly as declared (PascalCase) and deserialization is case-insensitive. A parameterless constructor (or a matching parameterized constructor) is required for deserialization, and only public properties are serialized — fields are ignored unless you opt in.

```csharp
using System.Text.Json;

public record Customer(int Id, string Name, string? Email, DateTime CreatedAt);

var customer = new Customer(1, "Daim Ali", "daim@example.com", DateTime.UtcNow);

string json = JsonSerializer.Serialize(customer);
Console.WriteLine(json);
// {"Id":1,"Name":"Daim Ali","Email":"daim@example.com","CreatedAt":"2026-11-14T14:47:00Z"}

Customer? restored = JsonSerializer.Deserialize<Customer>(json);
Console.WriteLine(restored?.Name); // Daim Ali
```

`DateTime` values serialize to ISO 8601 by default. Prefer `DateTimeOffset` for timestamps that cross time zones — a bare `DateTime` carries no offset, so a reader in another zone cannot tell whether it was UTC or local.

## JsonSerializerOptions

Creating a `JsonSerializerOptions` per call is wasteful; the serializer caches reflection metadata per options instance, so create one shared instance and reuse it. The most common settings are camel-case naming for JavaScript clients, indented output for config files, and ignoring nulls to keep payloads small. Note that options become effectively read-only after first use: mutating a shared instance while another thread serializes with it throws.

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
};

string json = JsonSerializer.Serialize(customer, options);
Console.WriteLine(json);
// {
//   "id": 1,
//   "name": "Daim Ali",
//   "email": "daim@example.com",
//   "createdAt": "2026-11-14T14:47:00Z"
// }
```

`PropertyNamingPolicy` only affects output names — the C# property names stay PascalCase. `PropertyNameCaseInsensitive = true` is the default for deserialization, so `{"name": "..."}` and `{"Name": "..."}` both bind to `Name`.

## Controlling Names and Members with Attributes

Attributes give per-property control that options cannot: rename one property, exclude a secret, or require a value to be present.

```csharp
public class Product
{
    public int Id { get; set; }

    [JsonPropertyName("product_name")]
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public string InternalSku { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Stock { get; set; }

    [JsonRequired]
    public decimal Price { get; set; }
}
```

`[JsonIgnore]` keeps `InternalSku` out of both directions — serialize and deserialize. `[JsonRequired]` makes deserialization throw a `JsonException` when `price` is missing, which catches malformed payloads early instead of silently accepting a default `0`.

## Working with JsonDocument and JsonNode

When the shape is unknown or only one field is needed, do not deserialize into a full type — parse into `JsonDocument` (read-only, pooled, allocation-efficient) or `JsonNode` (mutable DOM). `JsonDocument` must be disposed, and its elements are only valid while the document lives.

```csharp
string payload = """{"user": {"name": "Daim", "tags": ["dev", "dotnet"]}}""";

using JsonDocument doc = JsonDocument.Parse(payload);
string name = doc.RootElement.GetProperty("user").GetProperty("name").GetString()!;
Console.WriteLine(name); // Daim

JsonNode? node = JsonNode.Parse(payload);
node!["user"]!["tags"]!.AsArray().Add("csharp");
Console.WriteLine(node.ToJsonString());
```

## Source Generation

Reflection-based serialization generates metadata at runtime, which costs startup time and breaks Native AOT (no runtime reflection). Source generation moves that work to compile time: declare a `JsonSerializerContext` with `[JsonSerializable]` for each type, and the compiler emits the serialization code. ASP.NET Core minimal APIs and Native AOT apps should use it. The trade-off is that every serialized type must be listed up front — a forgotten type fails at runtime with a clear error.

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(Customer))]
[JsonSerializable(typeof(List<Customer>))]
public partial class AppJsonContext : JsonSerializerContext
{
}

// Serialize with the generated context instead of reflection:
string json = JsonSerializer.Serialize(customer, AppJsonContext.Default.Customer);
Customer? restored = JsonSerializer.Deserialize(json, AppJsonContext.Default.Customer);
```

## Common Pitfalls

- **Reference cycles.** An object graph where A references B and B references A throws by default. Set `ReferenceHandler = ReferenceHandler.Preserve` (adds `$id`/`$ref` metadata) or `IgnoreCycles` (writes null for the cycle) — and prefer DTOs that break the cycle in the first place.
- **Polymorphism.** Deserializing into a base type does not reconstruct derived types unless you add `[JsonDerivedType]` attributes or a custom converter.
- **Numbers.** `JsonSerializer` rejects `NaN` and `Infinity` by default; allow them explicitly with `NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals` if the consumer expects them.

## Key Takeaways

- `JsonSerializer.Serialize` / `Deserialize<T>` with a shared, reused `JsonSerializerOptions` is the standard path.
- Use attributes (`[JsonPropertyName]`, `[JsonIgnore]`, `[JsonRequired]`) for per-property control.
- Parse unknown or partial JSON with `JsonDocument` (read-only) or `JsonNode` (mutable), not a full model.
- For Native AOT and faster startup, use source generation via `JsonSerializerContext`.
- Watch out for reference cycles, polymorphism, and the fact that shared options must not be mutated after first use.
