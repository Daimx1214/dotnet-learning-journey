# ASP.NET Core JSON Options Customization

System.Text.Json is the default JSON serializer in ASP.NET Core. Its behaviour is controlled through `JsonOptions`, which you configure once in `Program.cs` and which then applies to every JSON response and request body.

## Configuring in Program.cs

For controllers, use `AddControllers().AddJsonOptions(...)`. For minimal APIs, use `ConfigureHttpJsonOptions(...)`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition =
        JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.WriteIndented = true;
});

// Minimal APIs use a separate options object
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition =
        JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();
app.MapControllers();
app.Run();
```

Note that controllers and minimal APIs do **not** share the same `JsonSerializerOptions` instance. Configure both if your app uses both styles, or they will serialize differently.

## PropertyNamingPolicy

By default ASP.NET Core serializes property names in camelCase (`FirstName` becomes `firstName`). You can change or remove this:

```csharp
options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // default in ASP.NET Core
options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower; // first_name (.NET 8+)
options.JsonSerializerOptions.PropertyNamingPolicy = null; // keep PascalCase as declared
```

`PropertyNamingPolicy` also affects dictionary keys. `DictionaryKeyPolicy` controls dictionary keys independently when you need them to differ.

## DefaultIgnoreCondition

Controls which properties are skipped when serializing:

```csharp
options.JsonSerializerOptions.DefaultIgnoreCondition =
    JsonIgnoreCondition.WhenWritingNull;      // skip null properties
options.JsonSerializerOptions.DefaultIgnoreCondition =
    JsonIgnoreCondition.WhenWritingDefault;   // skip default values (0, false, null)
options.JsonSerializerOptions.DefaultIgnoreCondition =
    JsonIgnoreCondition.Never;                // always write everything (default)
```

`WhenWritingNull` keeps payloads small and is the most common choice. Apply it per property with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` when you need it only in one place.

## Converters

### Enum as string

By default enums serialize as numbers. `JsonStringEnumConverter` writes their names instead, which is far easier for clients to read:

```csharp
options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

public enum OrderStatus { Pending, Shipped, Delivered }

public record Order(int Id, OrderStatus Status);
// Serializes as {"id": 1, "status": "Shipped"} instead of {"id": 1, "status": 1}
```

### Custom JsonConverter

For types with special formatting, write a converter. This one serializes a `Money` value as a single string like `"USD 12.50"`:

```csharp
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        string[] parts = reader.GetString()!.Split(' ');
        return new Money(parts[0], decimal.Parse(parts[1],
            CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, Money value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue($"{value.Currency} {value.Amount.ToString(CultureInfo.InvariantCulture)}");
}

options.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
```

## Reference Handling

Object graphs with cycles (a parent referencing a child that references the parent) throw `JsonException` by default. `ReferenceHandler.Preserve` handles them by emitting `$id` and `$ref` metadata:

```csharp
options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
```

This changes the JSON shape, so clients must understand `$ref`. Prefer fixing the model instead: return DTOs without back-references, or mark the back-reference with `[JsonIgnore]`. `ReferenceHandler.IgnoreCycles` (available since .NET 5) simply drops the repeated reference and keeps the JSON shape normal, which is often the better trade-off:

```csharp
options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
```

## Deserialization Strictness

```csharp
options.JsonSerializerOptions.PropertyNameCaseInsensitive = true; // default in ASP.NET Core
options.JsonSerializerOptions.NumberHandling =
    JsonNumberHandling.AllowReadingFromString; // accept "42" for an int property
options.JsonSerializerOptions.AllowTrailingCommas = true;
```

Keep `PropertyNameCaseInsensitive` on: clients will send `firstName`, `FirstName`, or `FIRSTNAME`, and all three should bind.

## Key Takeaways

- Configure controllers with `AddJsonOptions` and minimal APIs with `ConfigureHttpJsonOptions`; they are separate option objects.
- CamelCase naming and case-insensitive property matching are the ASP.NET Core defaults.
- Use `JsonStringEnumConverter` for readable enums and custom `JsonConverter<T>` for special types.
- Prefer DTOs or `ReferenceHandler.IgnoreCycles` over `Preserve` for cyclic object graphs.
- Set `DefaultIgnoreCondition` to `WhenWritingNull` to keep response payloads lean.
