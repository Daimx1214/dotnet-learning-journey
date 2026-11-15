# Security Headers in ASP.NET Core (ASP.NET Core)

Security headers are HTTP response headers that tell the browser how to behave: don't run this page inside a frame, don't guess the content type, only load scripts from these origins. They cost almost nothing to add, they are set in one place in `Program.cs`, and they close off whole classes of attacks — clickjacking, MIME sniffing, and script injection among them.

## HSTS: Force HTTPS

HTTP Strict Transport Security tells the browser to only ever use HTTPS for this site, for the next year, including subdomains:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // Sends: Strict-Transport-Security: max-age=31536000; includeSubDomains
}
```

Only enable it in production — a development HSTS header can lock your browser into HTTPS for `localhost` long after you stop testing. Once a browser sees the header it remembers it, so a misconfigured HSTS header is hard to undo; test with a short `max-age` first if you are unsure. Note that `UseHsts()` must run before any redirect or response is written, and it does nothing on the first HTTP request — the browser has to see it over HTTPS once before it takes effect.

## X-Content-Type-Options and X-Frame-Options

`X-Content-Type-Options: nosniff` stops the browser from guessing a response's content type. Without it, a browser might execute an uploaded file as JavaScript even though the server sent it as `text/plain` — a classic stored-XSS vector for file uploads. `X-Frame-Options: DENY` (or `SAMEORIGIN`) stops other sites embedding your pages in an `<iframe>`, which blocks clickjacking attacks that trick users into clicking something they cannot see.

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    await next();
});
```

There is no built-in middleware for these two headers in ASP.NET Core, so a small inline middleware like the one above is the standard approach. `X-Frame-Options` is being superseded by CSP's `frame-ancestors` directive, but sending both is harmless and covers older browsers.

## Content Security Policy (CSP)

CSP is the strongest header on this list: it whitelists exactly where scripts, styles, images, and other resources may load from. A page with `script-src 'self'` simply refuses to run an injected `<script>` tag, which is why CSP is the main defence against cross-site scripting.

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;";
    await next();
});
```

Start with `Content-Security-Policy-Report-Only` in production and watch the violation reports before enforcing: a policy that is too strict will break your own scripts and styles. Common directives are `default-src` (fallback for everything), `script-src`, `style-src`, `img-src`, and `frame-ancestors` (replaces `X-Frame-Options`). `'unsafe-inline'` weakens the policy considerably, so prefer nonces or hashes for inline scripts when you can.

## Referrer-Policy and the Rest

`Referrer-Policy` controls how much of the URL the browser sends in the `Referer` header when navigating away. `strict-origin-when-cross-origin` is the sensible default: same-origin navigation sends the full URL, cross-origin sends only the origin, and HTTPS-to-HTTP sends nothing — so internal paths and query strings never leak to other sites.

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.ReferrerPolicy = "strict-origin-when-cross-origin";
    context.Response.Headers.Remove("X-Powered-By"); // don't advertise the stack
    await next();
});
```

Other headers worth knowing: `Permissions-Policy` (disables browser features like camera or geolocation for your pages), `Cross-Origin-Opener-Policy` and `Cross-Origin-Embedder-Policy` (isolation for `SharedArrayBuffer` and similar features), and `Cache-Control: no-store` on sensitive responses so back-button history does not leak data.

## Putting It Together in Program.cs

Security headers are middleware, so they must run early — before static files, routing, and endpoints — so every response carries them:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.ReferrerPolicy = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.Run();
```

Verify the result with `curl -I https://localhost:5001` or a scanner such as securityheaders.com, which grades the full set. Headers are defence in depth — they do not fix an XSS bug in your code, but they limit what that bug can do.

## Key Takeaways

- Add security headers once, in `Program.cs`, as early middleware so every response carries them.
- HSTS forces HTTPS (production only); `nosniff` stops MIME guessing; `X-Frame-Options: DENY` blocks clickjacking.
- CSP whitelists resource origins and is the main defence against XSS — test in report-only mode first.
- `Referrer-Policy: strict-origin-when-cross-origin` stops internal URLs leaking to other sites.
