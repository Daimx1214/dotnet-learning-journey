# System.Text.Json Source Generation

System.Text.Json source generation moves serialization metadata from runtime reflection to compile-time generated code. Instead of discovering properties with reflection on first use, the compiler generates a `JsonSerializerContext` with strongly typed `JsonTypeInfo<T>` for each type you declare. The result is faster startup, lower memory use, trimming compatibility, and Native AOT support — reflection-based serialization is not available in Native AOT at all.

## How It Works

You declare a partial class deriving from `JsonSerializerContext` and annotate it with `[JsonSerializable(typeof(T))]` for each type. The source generator emits the serialization code at compile time. The context is then passed to `JsonSerializer.Serialize` / `Deserialize` instead of relying on the default reflection-based path.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

// The source generator fills in this partial class at compile time.
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
[JsonSerializable(typeof(OrderStatus))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonContext : JsonSerializerContext
{
}

public enum OrderStatus { Pending, Shipped, Delivered }

public sealed record Order(
    int Id,
    string Customer,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset PlacedAt);

// Serialize with the generated context — no reflection at runtime.
string json = JsonSerializer.Serialize(
    new Order(1, "Daim", 49.99m, OrderStatus.Pending, DateTimeOffset.UtcNow),
    AppJsonContext.Default.Order);

// Deserialize the same way.
Order? order = JsonSerializer.Deserialize(json, AppJsonContext.Default.Order);
```

## What JsonSourceGenerationOptions Controls

`JsonSourceGenerationOptions` on the context sets defaults for every type in it: `PropertyNamingPolicy` (e.g. camelCase), `WriteIndented`, `DefaultIgnoreCondition` (skip nulls, skip default values), `PropertyNameCaseInsensitive`, and `NumberHandling`. Options set per call still override context defaults. A useful pattern is one context per bounded area (e.g. `OrdersJsonContext`, `CatalogJsonContext`) so trimming only keeps the metadata each area needs.

```csharp
// Per-type customization still works with source generation.
public sealed record Product(
    int Id,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    decimal Discount,
    [property: JsonPropertyName("sku")]
    string Sku);
```

## Performance and AOT

The gains are real but specific. On first serialization of a type, reflection-based `JsonSerializer` pays a metadata-build cost of roughly 10–100 microseconds per type (and more for large object graphs), plus it keeps `PropertyInfo` metadata rooted in memory. Source generation pays that cost at compile time: first-call latency drops to near zero, and steady-state throughput is typically 10–30% faster because property access goes through generated code instead of reflection emits. The bigger win is deployment: with source generation plus trimming, a Native AOT console app serializing the `Order` type above trims to roughly 5–10 MB instead of 30+ MB with reflection, and it actually runs — reflection-based serialization throws at runtime under Native AOT.

## What Source Generation Cannot Do

- Polymorphic serialization needs explicit `[JsonDerivedType]` declarations; the generator cannot discover subtypes at runtime.
- Types without a public parameterless constructor or a matching parameterized constructor need `[JsonConstructor]`.
- `object`, `dynamic`, and undeclared generic instantiations cannot be serialized — every concrete type must be listed with `[JsonSerializable]`.
- `JsonSerializerOptions` features that depend on runtime type discovery (like `TypeInfoResolver` chains) must be composed explicitly via `JsonTypeInfoResolver.Combine`.
- Private members are not serialized; use `JsonInclude` on the property or field if it must be included.

## Common Pitfalls

- Forgetting to pass the context to `Serialize`/`Deserialize` silently falls back to reflection — the code compiles fine and is slow (or crashes under AOT). Always pass `AppJsonContext.Default.Order`, not just the value.
- Declaring `[JsonSerializable(typeof(List<Order>))]` is required separately; `typeof(Order)` alone does not cover collections of it.
- Two contexts with conflicting `PropertyNamingPolicy` for the same type cause confusing output — keep one context per type.
- Source generation runs at compile time, so adding a property to a record without rebuilding does nothing until the next build; stale binaries are a real debugging trap.

## Key Takeaways

- Source generation replaces runtime reflection with compile-time generated `JsonTypeInfo<T>` via a `JsonSerializerContext` subclass.
- Every serializable type (including `List<T>` wrappers) needs its own `[JsonSerializable]` attribute.
- `JsonSourceGenerationOptions` sets naming policy, null handling, and indentation for the whole context.
- The main wins are near-zero first-call latency, 10–30% faster steady-state throughput, trimming, and Native AOT support.
- Always pass the context explicitly; forgetting it silently reverts to reflection.
