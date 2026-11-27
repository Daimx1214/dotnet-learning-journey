# View Components in ASP.NET Core

A View Component is a reusable, self-contained chunk of UI with its own logic — a mini-controller that renders a partial view. It is the right tool when a piece of the page needs its own data: a shopping cart summary, a navigation menu, a "recently viewed" panel.

## View Components vs Partial Views vs Tag Helpers

| Feature | Partial view | View Component | Tag Helper |
|---|---|---|---|
| Has its own logic | No — receives a model from the parent | Yes — fetches its own data | No — transforms an HTML element |
| Good for | Static fragments | Data-driven widgets | Server-side attributes on HTML |
| Invoked from | `@await Html.PartialAsync(...)` | `@await Component.InvokeAsync(...)` | Directly in markup, e.g. `<a asp-action="...">` |

A partial view is just markup with a model handed to it; it cannot query a database or a service. A View Component can. A Tag Helper is not a UI fragment at all — it is a server-side attribute that changes how one HTML element renders.

## The View Component Class

A View Component is a class derived from `ViewComponent` with an `InvokeAsync` (or `Invoke`) method that returns an `IViewComponentResult`:

```csharp
public sealed class ShoppingCartViewComponent : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        // In a real app this would come from a cart service or session.
        var cart = await Task.FromResult(new CartSummary(
            ItemCount: 3,
            Total: 59.97m));

        return View(cart);
    }
}

public sealed record CartSummary(int ItemCount, decimal Total);
```

The worked numbers: 3 items totalling $59.97 — for example three items at $19.99 each (3 × 19.99 = 59.97).

By convention the component looks for its view at `Views/Shared/Components/ShoppingCart/Default.cshtml`. The folder name is the component name minus the `ViewComponent` suffix.

## The Default View

`Views/Shared/Components/ShoppingCart/Default.cshtml`:

```cshtml
@model CartSummary

<div class="cart-summary">
    <span>@Model.ItemCount items</span>
    <span>@Model.Total.ToString("C")</span>
</div>
```

Rendered output for the worked cart:

```html
<div class="cart-summary">
    <span>3 items</span>
    <span>$59.97</span>
</div>
```

## Invoking It from a Page

In any Razor view or Razor Page, invoke the component by name (again, without the `ViewComponent` suffix):

```cshtml
@await Component.InvokeAsync("ShoppingCart")
```

You can also pass arguments, which are matched to parameters of `InvokeAsync` by name:

```cshtml
@await Component.InvokeAsync("ShoppingCart", new { maxItems = 5 })
```

Or use the component as a Tag Helper, which reads more naturally in markup:

```cshtml
<vc:shopping-cart />
```

## Passing Parameters

A parameterised version takes the limit and returns only that many items:

```csharp
public async Task<IViewComponentResult> InvokeAsync(int maxItems)
{
    var items = await _cartService.GetItemsAsync();
    var summary = new CartSummary(
        ItemCount: Math.Min(items.Count, maxItems),
        Total: items.Take(maxItems).Sum(i => i.Price));
    return View(summary);
}
```

## When to Use a View Component

- A header cart, a sidebar menu, or a recommendations panel that appears on many pages and fetches its own data.
- Any fragment that needs a service, a database query, or logic the parent view should not know about.
- A widget you want to unit-test in isolation — `InvokeAsync` is an ordinary method you can call in a test.

## Trade-Offs

View Components cannot be used inside a controller action result, and they do not participate in model binding or filters the way controllers do — they are a rendering feature, not an endpoint. If the fragment needs no logic of its own, a partial view is simpler; if you only need to tweak one HTML element, a Tag Helper is the tool.

## Key Takeaways

- A View Component is a self-contained UI fragment with its own logic, invoked with `@await Component.InvokeAsync("ShoppingCart")`.
- Unlike a partial view, it fetches its own data; unlike a Tag Helper, it renders a whole fragment, not one element.
- Worked example: `ShoppingCartViewComponent` returns a cart with 3 items totalling $59.97 (3 × $19.99).
- Its view lives by convention at `Views/Shared/Components/ShoppingCart/Default.cshtml`.
- Use View Components for data-driven widgets reused across pages; use partial views for static fragments and Tag Helpers for single elements.
