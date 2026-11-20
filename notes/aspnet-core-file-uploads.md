# ASP.NET Core File Uploads

File uploads look trivial — an `IFormFile` parameter and a `CopyToAsync` — until a large file, a malicious filename, or a wrong content type shows up. The safe path is: limit size, validate the content, stream instead of buffering, and never trust the client-supplied name.

## Basic Uploads with IFormFile

For small files, model binding does the work. `IFormFile` gives you the filename, content type, length, and a stream to the uploaded bytes.

```csharp
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    var safeName = Path.GetRandomFileName() + Path.GetExtension(file.FileName);
    var path = Path.Combine("uploads", safeName);

    await using var stream = File.Create(path);
    await file.CopyToAsync(stream);
    return Results.Ok(new { storedAs = safeName, size = file.Length });
});
```

Never use `file.FileName` directly as a path — it can contain `../` traversal sequences. Generate a random name and keep the original name only as metadata in a database.

## Size Limits

ASP.NET Core has several limits, and the smallest one wins: Kestrel's `MaxRequestBodySize` (default ~30 MB), the form options `MultipartBodyLengthLimit` (default ~128 MB), and MVC's `RequestSizeLimit` attribute. Set them explicitly for the endpoints that need them.

```csharp
// Global form limit in Program.cs:
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});

// Kestrel limit:
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});
```

For a single large-file endpoint, prefer `[RequestSizeLimit(500_000_000)]` (MVC) or `DisableRequestSizeLimit` on that endpoint rather than raising the global limit for the whole app.

## Streaming Large Uploads Without Buffering

The default model binding buffers the entire file in memory (or to disk) before your handler runs. For large files, disable form value model binding and read the multipart stream yourself with `MultipartReader`, copying straight to disk in chunks.

```csharp
[HttpPost("upload-large")]
[DisableFormValueModelBinding]          // custom attribute: skips form binding
[RequestSizeLimit(2L * 1024 * 1024 * 1024)]
public async Task<IActionResult> UploadLarge()
{
    var boundary = MultipartRequestHelper.GetBoundary(Request.ContentType);
    var reader = new MultipartReader(boundary, Request.Body);

    MultipartSection? section;
    while ((section = await reader.ReadNextSectionAsync()) is not null)
    {
        if (!ContentDispositionHeaderValue.TryParse(
                section.ContentDisposition, out var disposition)
            || !disposition.FileName.HasValue)
            continue;

        var safeName = Path.GetRandomFileName();
        await using var target = File.Create(Path.Combine("uploads", safeName));
        await section.Body.CopyToAsync(target);   // streamed, never fully buffered
    }
    return Ok();
}
```

`DisableFormValueModelBinding` is a small custom action filter (see the official docs sample) that removes the form-value providers so binding never buffers the body. The memory footprint stays flat regardless of file size.

## Validating Content Type and Extension

`ContentType` and the file extension both come from the client and can be lied about. Validate the extension against an allow-list, and verify the actual bytes — check the file signature (magic bytes) for the formats you accept.

```csharp
private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".pdf"];

private static bool HasAllowedExtension(string fileName) =>
    AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

// Check magic bytes: PNG starts with 89 50 4E 47, JPEG with FF D8 FF
private static async Task<bool> IsPngAsync(Stream stream)
{
    Span<byte> header = stackalloc byte[4];
    stream.Position = 0;
    await stream.ReadExactlyAsync(header);
    return header is [0x89, 0x50, 0x4E, 0x47];
}
```

Store uploads outside the web root (or serve them through a controller that checks authorisation) so an uploaded file can never be executed as code by requesting its URL directly.

## Key Takeaways

- Use `IFormFile` for small uploads; never trust `file.FileName` as a path — generate a random stored name.
- Set size limits explicitly (`MultipartBodyLengthLimit`, Kestrel `MaxRequestBodySize`, per-endpoint `RequestSizeLimit`).
- Stream large files with `MultipartReader` and `DisableFormValueModelBinding` so memory stays flat.
- Validate the extension against an allow-list and verify magic bytes — `ContentType` from the client is not proof.
- Store uploads outside the web root and serve them through authorised endpoints, never by direct URL.
- Return a clear error when the file is missing, empty, too large, or of a disallowed type.
