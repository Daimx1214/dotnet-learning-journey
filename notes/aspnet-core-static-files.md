# Serving Static Files in ASP.NET Core

Static files — HTML, CSS, JavaScript, images, fonts — are served by dedicated middleware, not by controllers. Understanding where that middleware looks and how it sets cache headers avoids the two most common mistakes: files returning 404 because the middleware was never added, and stale files because caching was never configured.

## UseStaticFiles and wwwroot

By default, `UseStaticFiles()` serves files from the `wwwroot` folder at the web root. A file at `wwwroot/css/site.css` is requested as `/css/site.css`.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Must come before routing / authorization for files that need no auth.
app.UseStaticFiles();

app.MapGet("/", () => "Hello from the API");
app.Run();
```

Middleware order matters: `UseStaticFiles` should run before `UseRouting` and `UseAuthorization` when the files are public. If it runs after authorization, every static request also passes through authorization. Files outside `wwwroot` are never served by this default call.

## Custom FileProvider

To serve files from another folder, pass a `PhysicalFileProvider` and a `RequestPath`. The request path is a virtual prefix; it does not have to match the folder name on disk.

```csharp
using Microsoft.Extensions.FileProviders;

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Uploads")),
    RequestPath = "/files"
});

// A file at <content-root>/Uploads/report.pdf is now served as /files/report.pdf.
```

A `ManifestEmbeddedFileProvider` serves files embedded in an assembly instead, which is useful for files shipped inside a library. The same `RequestPath` mechanism applies.

## Cache Headers with StaticFileOptions

`OnPrepareResponse` runs for every static-file response and is the place to set cache headers. Without it, ASP.NET Core sends only a weak default; browsers may revalidate on every visit.

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // 7 days = 7 * 24 * 60 * 60 = 604,800 seconds.
        ctx.Context.Response.Headers.CacheControl =
            "public,max-age=604800";
    }
});
```

Verified arithmetic: `7 * 86,400 = 604,800` seconds in 7 days. For versioned assets (filenames containing a content hash), a long `max-age` such as one year (`31,536,000` seconds) is safe because a changed file gets a new name. For unversioned files, keep the lifetime short or use `must-revalidate` so updates reach users.

```csharp
// Shorter lifetime for files that change without a name change.
ctx.Context.Response.Headers.CacheControl = "public,max-age=3600"; // 1 hour
```

## Serving from Outside wwwroot

Serving from outside `wwwroot` is a deliberate choice, not a workaround: user uploads, generated reports, and tenant assets live outside the web root by design. Two rules apply:

1. Never map a `FileProvider` directly at the content root or a parent of it — that would expose source files, configuration, and secrets. Map exactly the folder that should be public.
2. Do not serve a folder that contains files with different access rules through one public provider. If some files need authentication, serve them from a controller action that checks the user first and returns a `PhysicalFileResult`.

```csharp
app.MapGet("/downloads/{fileName}", (string fileName) =>
{
    // Validate fileName here: reject "..", "/", and characters outside
    // the expected set before touching the file system.
    string path = Path.Combine(
        builder.Environment.ContentRootPath, "PrivateFiles", fileName);
    return Results.PhysicalFile(path, "application/octet-stream");
});
```

## SPA Fallback Note

A single-page application (React, Angular, Blazor WebAssembly) needs its client-side routes to fall back to `index.html` while real files are still served directly. `MapFallbackToFile` does this, and it must be registered after the static-files middleware and after API endpoints so it only catches requests nothing else handled.

```csharp
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok("ok"));
app.MapFallbackToFile("index.html");
```

Request flow: `/css/site.css` is served as a file; `/api/health` hits the endpoint; `/dashboard/users` falls through to `index.html` and the SPA router renders the page. Registering the fallback before `UseStaticFiles` would swallow real file requests and return `index.html` for CSS and JavaScript instead.

## Key Takeaways

- `UseStaticFiles()` serves `wwwroot` only, and middleware order decides whether static requests pass through routing and authorization.
- A custom `FileProvider` plus `RequestPath` serves any folder under a virtual prefix; map exactly the intended folder, never a parent of it.
- Set cache headers in `OnPrepareResponse`; 7 days is `max-age=604800`, and long lifetimes are only safe for content-hashed filenames.
- Files needing authorization belong behind a controller action, not behind a public static-file provider.
- For SPAs, register `MapFallbackToFile("index.html")` last so real files and API routes win first.
