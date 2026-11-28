# ASP.NET Core Content Negotiation

Content negotiation is how a server and a client agree on the format of a response. The client states what it accepts with the `Accept` header, and the server picks a formatter that can produce one of those media types.

## How It Works

1. The client sends `Accept: application/json` (or `application/xml`, `text/csv`, and so on).
2. ASP.NET Core checks the return type against its output formatters in order.
3. The first formatter that can write the type in an accepted media type wins.
4. If no formatter matches, the server returns `406 Not Acceptable`.

```http
GET /api/products/1 HTTP/1.1
Accept: application/json
```

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"id":1,"name":"Keyboard","price":49.99}
```

## The Produces Attribute

`[Produces]` declares which media types an action can return. It also drives the OpenAPI/Swagger documentation for that endpoint.

```csharp
[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{id:int}")]
    [Produces("application/json", "application/xml")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    public ActionResult<Product> Get(int id) =>
        Ok(new Product(id, "Keyboard", 49.99m));
}
```

If the client asks for `text/html` and the action only produces JSON and XML, the response is 406. `[Produces]` does not add formatters; it only restricts which of the registered formatters may be used.

## Formatters

ASP.NET Core ships with a JSON formatter (`SystemTextJsonOutputFormatter`) registered by default. XML is **not** registered by default; add it explicitly:

```csharp
builder.Services.AddControllers()
    .AddXmlSerializerFormatters();   // or .AddXmlDataContractSerializerFormatters()
```

With both registered, the same action serves either format depending on the `Accept` header:

```http
GET /api/products/1 HTTP/1.1
Accept: application/xml
```

```http
HTTP/1.1 200 OK
Content-Type: application/xml; charset=utf-8

<Product xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" ...>
  <Id>1</Id>
  <Name>Keyboard</Name>
  <Price>49.99</Price>
</Product>
```

Formatter order matters: the first formatter that supports the requested media type wins. JSON is checked before XML by default.

## 406 Not Acceptable

When no formatter can satisfy the `Accept` header, ASP.NET Core returns 406 with an empty body. This only happens when the client asks for something unsupported; a missing `Accept` header or `Accept: */*` falls back to the default formatter (JSON).

```csharp
// Force the check in a test or client:
// Accept: application/pdf  ->  406 Not Acceptable
```

To return a custom error body instead of an empty 406, configure `ReturnHttpNotAcceptable`:

```csharp
builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
});
```

This makes ASP.NET Core return 406 even when the client sends no `Accept` header and the action is restricted with `[Produces]` to a type the default formatter cannot write.

## Custom Output Formatter Sketch

A formatter that writes plain-text CSV for `IEnumerable<Product>`:

```csharp
public sealed class CsvOutputFormatter : TextOutputFormatter
{
    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add("text/csv");
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type) =>
        typeof(IEnumerable<Product>).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var products = (IEnumerable<Product>)context.Object!;
        await using var writer = new StreamWriter(
            context.HttpContext.Response.Body, selectedEncoding);
        await writer.WriteLineAsync("Id,Name,Price");
        foreach (var p in products)
            await writer.WriteLineAsync($"{p.Id},{p.Name},{p.Price}");
        await writer.FlushAsync();
    }
}

builder.Services.AddControllers(options =>
{
    options.OutputFormatters.Add(new CsvOutputFormatter());
});
```

Registering it makes `Accept: text/csv` work on any action returning `IEnumerable<Product>`.

## Key Takeaways

- The `Accept` header drives format selection; the first matching registered formatter wins.
- JSON is registered by default; XML must be added with `AddXmlSerializerFormatters`.
- `[Produces]` declares supported media types and feeds OpenAPI docs; it does not register formatters.
- An unmatched `Accept` header produces `406 Not Acceptable`.
- Custom formatters derive from `TextOutputFormatter` or `OutputFormatter` and declare their media types.
