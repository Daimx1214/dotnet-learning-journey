# ASP.NET Core Localization (ASP.NET Core)

Localization makes an app serve the same UI in several languages. ASP.NET Core's model has three cooperating pieces: `IStringLocalizer<T>` looks up translated strings from resource files, `RequestLocalizationOptions` decides which culture applies to the current request, and data annotations can localize validation messages. The culture is per request, not per app — two users can browse the same site in English and Urdu at the same time.

## IStringLocalizer and Resource Files

Strings are looked up by key from `.resx` resource files. The localizer is typed by a class — conventionally a marker class or the controller/page — and the resource file name must match that type's full name. `Resources/Controllers/HomeController.en.resx` holds English values for `HomeController`; `HomeController.ur.resx` holds Urdu values for the same keys. A missing key returns the key itself, so the default language usually doubles as the key.

```csharp
// Controllers/HomeController.cs
using Microsoft.Extensions.Localization;

public class HomeController : Controller
{
    private readonly IStringLocalizer<HomeController> _localizer;
    public HomeController(IStringLocalizer<HomeController> localizer)
        => _localizer = localizer;

    public IActionResult Index()
    {
        ViewData["Welcome"] = _localizer["WelcomeMessage"];
        return View();
    }
}
```

```razor
@* Views/Home/Index.cshtml — inject the localizer directly in a view *@
@using Microsoft.AspNetCore.Mvc.Localization
@inject IViewLocalizer Localizer

<h1>@Localizer["WelcomeMessage"]</h1>
```

`IViewLocalizer` is the view equivalent: it resolves resources relative to the view's path instead of a class name. For shared strings used across many pages, a single `SharedResource` marker class with one resource file per culture keeps everything in one place.

## RequestLocalizationOptions and Culture Providers

The localization middleware picks the request culture from providers, in order. The three built-in providers are: query string (`?culture=ur&ui-culture=ur`), cookie (`.AspNetCore.Culture`), and the `Accept-Language` header. `RequestLocalizationOptions` configures the supported cultures and the default.

```csharp
// Program.cs
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("ur"),
    new CultureInfo("ar")
};

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Query string provider wins, then cookie, then Accept-Language header.
    options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
});

var app = builder.Build();
app.UseRequestLocalization(); // must run before routing/middleware that reads culture
app.MapControllers();
app.Run();
```

Culture (number/date formatting) and UI culture (resource lookup) are separate: `CultureInfo.CurrentCulture` formats dates and currency, `CultureInfo.CurrentUICulture` picks the resource file. Setting both through `RequestCulture` keeps them in sync. A culture cookie set from a language switcher is the usual persistence mechanism — the query string provider is handy for testing, but a link with `?culture=ur` in every URL is not a real UX.

## Localizing Data Annotations

Validation messages can be localized too, by pointing the annotation attributes at a resource type. This makes `[Required]` and friends render in the user's language on both server and client validation.

```csharp
using System.ComponentModel.DataAnnotations;

public class RegisterViewModel
{
    [Required(ErrorMessageResourceType = typeof(SharedResource),
              ErrorMessageResourceName = "NameRequired")]
    [StringLength(100, ErrorMessageResourceType = typeof(SharedResource),
                 ErrorMessageResourceName = "NameTooLong")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "EmailAddress", ResourceType = typeof(SharedResource))]
    [EmailAddress(ErrorMessageResourceType = typeof(SharedResource),
                  ErrorMessageResourceName = "InvalidEmail")]
    public string Email { get; set; } = string.Empty;
}
```

## Key Takeaways

- `IStringLocalizer<T>` (and `IViewLocalizer` in views) resolves strings from `.resx` files named after the type or view path; a missing key returns the key itself.
- `RequestLocalizationOptions` sets supported cultures and the default; providers check query string, then cookie, then `Accept-Language`.
- Call `app.UseRequestLocalization()` early in the pipeline, before anything that reads the current culture.
- Culture formats numbers/dates; UI culture picks the resource file — set both together via `RequestCulture`.
- Data annotations localize with `ErrorMessageResourceType` / `ErrorMessageResourceName` against a shared resource type.
