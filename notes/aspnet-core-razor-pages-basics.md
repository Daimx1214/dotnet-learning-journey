# Razor Pages Basics (ASP.NET Core)

Razor Pages is ASP.NET Core's page-focused model for building server-rendered web UI. Each page is a `.cshtml` file paired with a `PageModel` class in a `.cshtml.cs` file, and routing is derived from the file's location under the `Pages` folder — `Pages/Products/Edit.cshtml` maps to `/Products/Edit` automatically. It is the simplest way to build a traditional multi-page website in ASP.NET Core, and it suits forms-heavy admin panels, CRUD screens, and content sites far better than a hand-rolled MVC controller setup.

## PageModel and Handlers

The `PageModel` is the page's code-behind. It holds the data the view needs, the bound form values, and one handler method per HTTP verb. `OnGet` renders the page; `OnPost` processes a form submission. Handlers can also be named — `OnPostDeleteAsync` is invoked by a form whose submit button carries `asp-page-handler="Delete"` — so one page can serve several actions.

```csharp
// Pages/Products/Edit.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    [BindProperty]
    public ProductInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Product? product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();
        Input = new ProductInput { Name = product.Name, Price = product.Price };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid) return Page(); // re-render with validation errors

        Product? product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();
        product.Name = Input.Name;
        product.Price = Input.Price;
        await _db.SaveChangesAsync();
        return RedirectToPage("/Products/Index");
    }
}
```

## Routing

Razor Pages routing is convention-based: the URL is the file path under `Pages`, minus the extension. Route parameters are added with the `@page` directive at the top of the `.cshtml` file:

```razor
@page "{id:int}"
@model EditModel
```

This makes `/Products/Edit/42` bind `id = 42`. Optional segments use `{id:int?}`, and constraints such as `:int`, `:guid`, or `:alpha` are the same route constraints MVC uses. Custom routes can be added with `AddRazorPagesOptions` and `AddPageRoute` when a URL must not match the file layout.

## Model Binding and Validation

`[BindProperty]` binds form fields to page-model properties by name, so an `<input asp-for="Input.Name" />` posts back into `Input.Name`. Data annotations drive validation, and the `asp-validation-for` tag helper renders the error next to the field:

```csharp
public class ProductInput
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 100_000)]
    public decimal Price { get; set; }
}
```

```razor
<form method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger"></div>
    <label asp-for="Input.Name"></label>
    <input asp-for="Input.Name" class="form-control" />
    <span asp-validation-for="Input.Name" class="text-danger"></span>
    <button type="submit" class="btn btn-primary">Save</button>
</form>
```

`ModelState.IsValid` must be checked in every `OnPost` handler — Razor Pages does not stop invalid data by itself. Antiforgery tokens are automatic: the `<form>` tag helper emits one, and the framework validates it on POST.

## Razor Pages vs MVC vs API

| Approach | Best for |
|----------|----------|
| Razor Pages | Server-rendered, page-per-URL sites; forms and CRUD |
| MVC controllers + views | Complex routing, shared views across many actions |
| Minimal APIs / controllers | JSON APIs consumed by SPAs or mobile apps |

Choose Razor Pages when each URL is a distinct screen with its own form and its own small slice of logic — that is most admin tools and line-of-business apps. Choose MVC when several actions genuinely share views and routing logic, and choose an API when the client is JavaScript or a mobile app. Razor Pages and MVC can coexist in one project; nothing forces a single choice.

## Key Takeaways

- One page = one `.cshtml` view + one `PageModel`; the URL comes from the file path under `Pages`.
- `OnGet`/`OnPost` handlers (optionally named, e.g. `OnPostDeleteAsync`) handle each verb; handler names map to `asp-page-handler`.
- `[BindProperty]` plus data annotations gives model binding and validation; always check `ModelState.IsValid` on POST.
- Antiforgery protection is automatic on forms; route parameters come from the `@page` directive.
- Pick Razor Pages for page-per-URL, form-heavy server-rendered sites; pick MVC or an API when the shape of the app demands it.
