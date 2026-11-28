# HTTPS and HSTS in ASP.NET Core

HTTPS protects traffic between the browser and the server. ASP.NET Core
has two separate pieces for it: HTTPS redirection, which sends HTTP
requests to HTTPS, and HSTS, which tells browsers to only ever use
HTTPS for your site. They solve different problems and are configured
separately.

## HTTPS redirection: `UseHttpsRedirection`

`UseHttpsRedirection` returns a 307 redirect from `http://` to
`https://` for the same URL. Add it early in the pipeline, before
anything that produces a response:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseHttpsRedirection(); // Redirect http:// to https:// (307).

app.MapGet("/", () => "Hello over HTTPS!");
app.Run();
```

In a new ASP.NET Core project this middleware is already in the default
template. It only redirects; it does not make the server listen on HTTPS.
That comes from the server configuration (Kestrel endpoints, or the
reverse proxy in front of the app).

## HSTS: `UseHsts`

HSTS (HTTP Strict Transport Security) is a response header,
`Strict-Transport-Security`, that instructs the browser: for the next
`max-age` seconds, only contact this site over HTTPS, even if the user
types `http://` or follows an `http://` link. It protects against
downgrade attacks where an attacker on the network tries to keep the
user on plain HTTP.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // Only in production. Never enable HSTS in development.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Run();
```

The default options send `max-age=2592000` (30 days) and
`includeSubDomains`:

```csharp
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true; // Apply to all subdomains too.
    options.Preload = true;           // Eligible for browser preload lists.
});
```

## HSTS options explained

- **MaxAge:** how long the browser remembers the rule. 30 days is the
  default; one year (365 days) is the common production value. The
  preload list requires at least 31536000 seconds (365 days).
- **IncludeSubDomains:** extends the rule to every subdomain. Only
  enable it when every subdomain actually serves HTTPS, including
  internal and staging ones. One subdomain without HTTPS becomes
  unreachable in browsers that saw the header.
- **Preload:** marks the site as eligible for the HSTS preload list
  built into browsers, so even the very first visit is forced to HTTPS.
  Preload is a commitment: removal from the list takes months, and a
  site on it cannot serve plain HTTP at all. Submit at
  hstspreload.org only when you are sure HTTPS works everywhere.

## Dev vs prod behaviour

| | Development | Production |
| --- | --- | --- |
| `UseHttpsRedirection` | On, but the dev certificate must be trusted (`dotnet dev-certs https --trust`) or browsers warn | On |
| `UseHsts` | **Off** | On |

## Why HSTS must never be enabled in development

HSTS is cached by the browser per host, including `localhost`. If a dev
server sends the header, the browser remembers it and refuses plain
HTTP to `localhost` for the whole max-age period, even after the app is
stopped or the header is removed. The usual symptom is a browser error
on every local address with no way to clear it except deleting the
browser's HSTS state. That is why the template guards `UseHsts()` with
`if (!app.Environment.IsDevelopment())`, and why you should never remove
that guard.

## Reverse proxies and forwarded headers

In production, ASP.NET Core usually sits behind a reverse proxy (nginx,
IIS, or a cloud load balancer) that terminates TLS. The app itself
receives plain HTTP from the proxy, so it cannot tell the original
request was HTTPS unless the proxy says so. Configure forwarded headers
so the app sees the real scheme and client IP:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Add the proxy's IP or network here in real deployments.
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
});

var app = builder.Build();
app.UseForwardedHeaders(); // Must run before UseHttpsRedirection.
app.UseHttpsRedirection();
```

Without `UseForwardedHeaders`, the app thinks every request is HTTP and
`UseHttpsRedirection` redirects in a loop, because the proxied request
never looks secure. `UseForwardedHeaders` must run first in the
pipeline, before any middleware that reads the scheme.

## Key Takeaways

- `UseHttpsRedirection` sends HTTP requests to HTTPS with a 307
  redirect; HSTS tells the browser to only use HTTPS for the site for
  the max-age period.
- `UseHsts` belongs in production only. Never enable it in development:
  browsers cache the rule for `localhost` and then refuse plain HTTP
  local requests for the whole max-age period.
- MaxAge of 365 days with IncludeSubDomains is the common production
  setup; Preload is a long-term commitment and should only be enabled
  when every subdomain serves HTTPS.
- Behind a reverse proxy, add `UseForwardedHeaders` before
  `UseHttpsRedirection`, or the app will redirect HTTPS requests in a
  loop.
