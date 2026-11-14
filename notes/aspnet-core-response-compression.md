# Response Compression in ASP.NET Core (ASP.NET Core)

Response compression shrinks HTTP responses before they leave the server, usually with gzip or Brotli. Text compresses extremely well — a 100 KB JSON payload often drops to 15–20 KB — which means faster page loads and less bandwidth, especially on slow mobile connections. The middleware is cheap to add and should be on by default for most APIs and websites.

## Adding the Middleware

```csharp
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/plain" });
});

// Optional: tune the compression level (defaults are usually fine).
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

var app = builder.Build();

// Register early, before static files and endpoints.
app.UseResponseCompression();

app.Run();
```

The middleware inspects the client's `Accept-Encoding` header and picks the best provider the client supports — Brotli first if listed, then gzip. If the client sends no `Accept-Encoding`, the response goes out uncompressed.

## What to Compress — and What Not To

Compress text: JSON, HTML, CSS, JavaScript, XML, SVG, and plain text. These are large and highly compressible. Do **not** compress already-compressed formats: JPEG, PNG, WebP, MP4, ZIP, and already-gzipped files gain nothing and waste CPU. The default MIME type list already excludes them, so only extend it with text-like types.

Also skip tiny responses. Compressing a 200-byte JSON body costs more CPU than it saves in bytes. The middleware handles this sensibly by default, but if you serve many small API responses, measure before forcing compression on everything.

## HTTPS and the BREACH Warning

`EnableForHttps` is `false` by default for a real reason: compressing responses over HTTPS can leak secrets through the BREACH attack, where an attacker who can inject chosen plaintext into a response (for example a reflected search term) and observe the compressed size can gradually guess a secret token in the same response. The risk only matters when a response contains both a secret (like an anti-CSRF token) and attacker-controlled input. If your API returns tokens alongside reflected input, keep compression off for those endpoints, or separate secrets from compressible bodies. For most APIs, enabling it for HTTPS is the pragmatic default.

## Measuring the Gain

Verify compression is actually happening with a quick `curl` check — the `Content-Encoding` header tells you what the client received:

```bash
curl -H "Accept-Encoding: gzip, br" -I https://localhost:5001/api/products
# Look for: Content-Encoding: br
```

If the header is missing, the response was either too small, an excluded MIME type, or the middleware is registered too late in the pipeline. In production, also confirm your reverse proxy or CDN is not compressing the same response a second time — double compression wastes CPU and can confuse clients.

## Ordering and Static Files

`UseResponseCompression` must run before the middleware that produces the response — before `UseStaticFiles` and before endpoint routing results are written. A common bug is registering it after `UseStaticFiles`, which leaves static assets uncompressed. Also note that the middleware only compresses responses the server generates; responses already compressed by a reverse proxy or CDN should not be compressed twice, so disable it if a proxy in front of you already handles compression.

## Key takeaways

- `AddResponseCompression` + `UseResponseCompression` enables gzip/Brotli; the client picks via `Accept-Encoding`.
- Compress text (JSON, HTML, CSS, JS); never compress images, video, or archives.
- `EnableForHttps` is off by default because of BREACH — enable it unless responses mix secrets with attacker-controlled input.
- Register the middleware early, before static files, and do not double-compress behind a proxy that already compresses.
