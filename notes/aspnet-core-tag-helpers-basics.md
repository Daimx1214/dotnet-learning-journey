# Razor Tag Helpers Basics

Tag Helpers are server-side attributes that let C# code participate in writing HTML. Instead of calling a C# method that returns an HTML string, you write ordinary-looking HTML and add `asp-` attributes; at runtime the Tag Helper rewrites the element — generating URLs, binding form fields, and conditionally including content.

## Anchor Tag Helper

The anchor Tag Helper builds a URL from routing data instead of a hard-coded string:

```cshtml
<a asp-controller="Products" asp-action="Details" asp-route-id="5">
    View product
</a>
```

With the default route `{controller=Home}/{action=Index}/{id?}`, this renders as:

```html
<a href="/Products/Details/5">View product</a>
```

If the route template changes later, every anchor Tag Helper updates automatically — no search-and-replace through hard-coded URLs. Available attributes include `asp-controller`, `asp-action`, `asp-route-{value}` (any route value, such as `asp-route-id`), `asp-area`, and `asp-fragment` for a `#anchor`.

## Form Tag Helper and asp-for

The form Tag Helper wires a form to a controller action and generates anti-forgery protection automatically:

```cshtml
@model ProductViewModel

<form asp-controller="Products" asp-action="Create" method="post">
    <label asp-for="Name"></label>
    <input asp-for="Name" />
    <span asp-validation-for="Name"></span>

    <button type="submit">Save</button>
</form>
```

`asp-for="Name"` on the `<input>` does three things from the model property:

- Sets `id="Name"` and `name="Name"` so model binding works on post-back.
- Sets the `value` to the model's current `Name`.
- Picks the input `type` from the property type (`string` → `text`, `DateTime` → `datetime-local`, `bool` → `checkbox`).

Rendered HTML for a model with `Name = "Desk Lamp"`:

```html
<form action="/Products/Create" method="post">
    <label for="Name">Name</label>
    <input type="text" id="Name" name="Name" value="Desk Lamp" />
    <span class="field-validation-valid" data-valmsg-for="Name"></span>
    <input name="__RequestVerificationToken" type="hidden" value="..." />
    <button type="submit">Save</button>
</form>
```

The hidden `__RequestVerificationToken` input is added automatically — one less thing to forget. The `<label asp-for="Name">` also picks up the property's `[Display(Name = "...")]` text when one is set.

## Environment Tag Helper

The Environment Tag Helper includes content only in the named environments:

```cshtml
<environment include="Development">
    <script src="~/js/debug-tools.js"></script>
</environment>

<environment exclude="Development">
    <script src="~/js/app.min.js"></script>
</environment>
```

In Development, only `debug-tools.js` is rendered; in Production, only the minified `app.min.js` is. The environment comes from `ASPNETCORE_ENVIRONMENT`, and the Tag Helper supports comma-separated lists: `include="Staging,Production"`.

## Tag Helpers vs HTML Helpers

HTML Helpers are the older approach — C# methods that return HTML strings:

```cshtml
@Html.ActionLink("View product", "Details", "Products", new { id = 5 }, null)
@Html.TextBoxFor(m => m.Name)
@Html.LabelFor(m => m.Name)
```

| Aspect | HTML Helpers | Tag Helpers |
|---|---|---|
| Syntax | C# method calls inside `@...` | HTML-like attributes on real elements |
| Readability | The markup is hidden inside C# | A designer can read the HTML directly |
| Tooling | No HTML IntelliSense in the string | Full HTML IntelliSense and validation |
| Behaviour | Same features underneath | Same features underneath |

Both produce the same output. Tag Helpers win on readability: the file still looks like HTML, which matters when a page mixes lots of markup with a little server logic. HTML Helpers remain available and are fine in existing code, but new Razor views should prefer Tag Helpers.

## Enabling Tag Helpers

Tag Helpers are opt-in per project. A `_ViewImports.cshtml` file at the root of `Views` (or the project root for Razor Pages) enables them:

```cshtml
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

That single line activates all built-in Tag Helpers (anchor, form, input, environment, cache, and more). Custom Tag Helpers are enabled the same way with their own assembly name.

## Common Built-In Tag Helpers

- **Anchor** (`asp-controller`, `asp-action`, `asp-route-id`) — route-based links.
- **Form** (`asp-action`, `asp-controller`) — posts to an action with an anti-forgery token.
- **Input / Label / Select / Textarea** (`asp-for`) — model-bound form fields.
- **Validation Message / Validation Summary** (`asp-validation-for`) — displays model errors.
- **Environment** (`include`, `exclude`) — environment-conditional content.
- **Cache** (`expires-after`, `vary-by`) — caches a fragment of rendered output.
- **Image** (`asp-append-version`) — appends a cache-busting hash to an image URL.

## Key Takeaways

- Tag Helpers are `asp-` attributes on ordinary HTML elements; the server rewrites the element at render time.
- The anchor Tag Helper turns `asp-controller="Products" asp-action="Details" asp-route-id="5"` into `/Products/Details/5`.
- The form Tag Helper adds an anti-forgery token automatically, and `asp-for` generates `id`, `name`, and `value` from the model property.
- The Environment Tag Helper renders content only in the listed environments, e.g. dev-only scripts.
- Tag Helpers and HTML Helpers produce the same output, but Tag Helpers keep views readable as HTML — prefer them in new code.
