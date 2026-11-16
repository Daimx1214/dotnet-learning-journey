# Antiforgery Tokens in ASP.NET Core (ASP.NET Core)

A cross-site request forgery (CSRF) attack tricks a logged-in user's browser into sending a request the user never intended. The attacker hosts a malicious page that auto-submits a form (or fires a fetch) to your site; the browser helpfully attaches the user's authentication cookie, and your server cannot tell the request apart from a legitimate one. Antiforgery tokens defeat this by requiring a secret value that only your own pages can produce — the attacker's site cannot read it, so its forged request fails validation.

## How the Token Works

ASP.NET Core's antiforgery system issues two linked values: a cookie token (stored in a cookie, typically `.AspNetCore.Antiforgery.*`) and a request token (embedded in the form as a hidden field). On a state-changing request the server checks that both are present, that they belong together, and that neither has expired or been tampered with. The cookie alone is not enough — the attacker cannot read the request token because of the same-origin policy, so they cannot produce a matching pair. This is the synchronizer token pattern, and it is why GET requests are not validated: a GET should never change state, so there is nothing to forge.

## Enabling Validation in Razor Pages and MVC

In Razor Pages and MVC form posts, validation is on by default when you use the tag helpers. The form tag helper emits the hidden request-token field automatically:

```html
<!-- The <form> tag helper renders a hidden __RequestVerificationToken field. -->
<form method="post" asp-page="/Account/ChangeEmail">
    <input asp-for="NewEmail" />
    <button type="submit">Change email</button>
</form>
```

For controller actions, add the `[ValidateAntiForgeryToken]` attribute. Razor Pages validate automatically via the `AutoValidateAntiforgeryToken` filter that the framework registers; MVC controllers need the attribute on each POST action (or once, globally):

```csharp
// Per-action validation.
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult ChangeEmail(ChangeEmailModel model)
{
    // Reached only when the token pair validated.
    return RedirectToAction("Index");
}

// Or globally, for every unsafe HTTP method in the app:
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
```

`AutoValidateAntiforgeryToken` skips safe methods (GET, HEAD, OPTIONS, TRACE) and only enforces validation on POST, PUT, DELETE, and PATCH. A failed validation returns HTTP 400 with an `AntiforgeryValidationException` — log it, but do not leak the token or the reason to the client.

## Working with IAntiforgery Directly

Sometimes you need the token outside a form — a hand-written HTML page, a JavaScript fetch call, or a custom endpoint. `IAntiforgery` gives you the tokens explicitly:

```csharp
// Generate the token pair and expose the request token to the page.
app.MapGet("/api/antiforgery-token", (IAntiforgery antiforgery, HttpContext context) =>
{
    AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Json(new { token = tokens.RequestToken });
});

// Validate explicitly on a minimal API endpoint (no [ValidateAntiForgeryToken] there).
app.MapPost("/api/profile", async (IAntiforgery antiforgery, HttpContext context) =>
{
    await antiforgery.ValidateRequestAsync(context);
    // ... handle the request
});
```

`GetAndStoreTokens` writes the cookie and returns the request token; `ValidateRequestAsync` throws on failure. The request token can also be sent as the `RequestVerificationToken` header from JavaScript — the default header name — which is the standard approach for fetch-based forms.

## SPA and API Considerations

Antiforgery tokens are a browser-cookie defence, and they only make sense where cookie authentication is used. A single-page application that calls an API with a bearer token in the `Authorization` header does not need antiforgery protection: the browser does not attach bearer tokens automatically, so a malicious site cannot piggyback on the user's session the way it can with cookies. If your SPA uses cookie authentication, however, CSRF is back on the table — fetch the request token once (from a dedicated endpoint or a cookie), then send it in the `RequestVerificationToken` header on every mutating call.

For APIs consumed by non-browser clients, skip antiforgery entirely and rely on bearer-token authentication plus CORS restrictions. And remember the scope: antiforgery protects against forged requests from other origins, not against XSS — if an attacker can run JavaScript on your own page, they can read the token themselves. Keep XSS defences (output encoding, Content-Security-Policy) as the other half of the story.

## Key Takeaways

- CSRF exploits the browser's automatic cookie attachment; antiforgery tokens require a secret only your own pages can produce.
- The system pairs a cookie token with a request token; validation checks both are present, matching, and untampered.
- Form tag helpers emit the request token automatically; use `[ValidateAntiForgeryToken]` or a global `AutoValidateAntiforgeryTokenAttribute` on POST/PUT/DELETE actions.
- `IAntiforgery.GetAndStoreTokens` and `ValidateRequestAsync` cover minimal APIs and JavaScript fetch calls, with the token sent in the `RequestVerificationToken` header.
- Bearer-token APIs do not need antiforgery; cookie-authenticated SPAs do. Antiforgery stops CSRF, not XSS.
