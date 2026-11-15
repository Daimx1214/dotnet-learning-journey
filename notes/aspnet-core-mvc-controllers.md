# MVC Controllers in ASP.NET Core (ASP.NET Core)

In ASP.NET Core MVC, a controller is the class that receives an HTTP request, runs your logic, and decides what to send back — usually a view (HTML) or data (JSON). Controllers sit between routing and your application code, and getting their shape right keeps the rest of the app clean.

## Controller vs ControllerBase

`Controller` inherits from `ControllerBase` and adds view support (`View()`, `PartialView()`, `ViewBag`). `ControllerBase` has everything else: `Ok()`, `NotFound()`, `StatusCode()`, `ModelState`, and `User`.

```csharp
// MVC controller: returns views (HTML pages).
public class BooksController : Controller
{
    public IActionResult Details(int id) => View(GetBook(id));
}

// API controller: returns data, no view support needed.
[ApiController]
[Route("api/[controller]")]
public class BooksApiController : ControllerBase
{
    [HttpGet("{id}")]
    public ActionResult<Book> Get(int id) => Ok(GetBook(id));
}
```

Rule of thumb: use `Controller` for server-rendered pages, `ControllerBase` (with `[ApiController]`) for APIs. `[ApiController]` also turns on automatic model validation — a request with an invalid model returns 400 before your action runs — and requires attribute routing.

## Action Results

An action returns `IActionResult` (any result) or `ActionResult<T>` (a result or a typed value). Common results:

| Result | Meaning |
|---|---|
| `View(model)` | Renders a Razor view with the model |
| `Ok(value)` / `Ok()` | 200 with JSON, or empty 200 |
| `NotFound()` | 404 |
| `BadRequest()` | 400 |
| `CreatedAtAction(...)` | 201 with a Location header pointing at the new resource |
| `RedirectToAction(...)` | 302 redirect to another action |
| `NoContent()` | 204, typical for a successful PUT/DELETE |

```csharp
[HttpPost]
public IActionResult Create(Book book)
{
    if (!ModelState.IsValid) return View(book); // re-show the form with errors
    var saved = _books.Add(book);
    return RedirectToAction(nameof(Details), new { id = saved.Id }); // PRG pattern
}
```

Returning `RedirectToAction` after a successful POST is the Post-Redirect-Get pattern: it stops the browser re-submitting the form on refresh.

## Routing Attributes

Attribute routing puts the URL on the action itself. `[Route("api/[controller]")]` on the class plus `[HttpGet("{id}")]` on the method gives `GET /api/books/5`. The `[controller]` token is replaced by the class name minus the `Controller` suffix. Constraints narrow a route: `[HttpGet("{id:int}")]` only matches integers, and `[HttpGet("search/{term:minlength(3)}")]` requires at least 3 characters.

## Model Binding Sources

Model binding fills action parameters from the request. The source is chosen by attribute (or inferred under `[ApiController]`):

| Attribute | Source | Example |
|---|---|---|
| `[FromRoute]` | URL segment | `/books/5` → `id` |
| `[FromQuery]` | Query string | `/books?page=2` |
| `[FromBody]` | JSON request body | POST with a JSON book |
| `[FromForm]` | Form fields | HTML form POST |
| `[FromHeader]` | HTTP header | `X-Api-Key` |

```csharp
[HttpGet("search")]
public IActionResult Search([FromQuery] string term, [FromQuery] int page = 1) { ... }

[HttpPost]
public IActionResult Create([FromBody] Book book) { ... }
```

Only one parameter may come `[FromBody]` — a request has a single body. Complex types are bound from the body by default in API controllers; simple types come from the route or query string.

## Filters

Filters run code around an action, in this order: authorization → resource → action → exception → result. Use them for cross-cutting concerns instead of repeating code in every action:

```csharp
[Authorize]                    // authorization filter: rejects anonymous users
[ResponseCache(Duration = 60)] // result filter: caches the response for 60s
public IActionResult Index() => View(_books.All());
```

Custom filters implement `IActionFilter` (or inherit `ActionFilterAttribute`) for logic like logging or timing. Keep filters thin; business logic belongs in services, not filters.

## Key Takeaways

- `Controller` is for views, `ControllerBase` with `[ApiController]` is for APIs; the latter adds automatic 400s and requires attribute routing.
- Return the right result type: `View` for pages, `Ok`/`CreatedAtAction`/`NoContent` for APIs, and redirect after a successful POST.
- Model binding sources are explicit: route, query, body, form, or header — and only one `[FromBody]` per action.
- Filters handle cross-cutting concerns (auth, caching, logging) so actions stay focused on one job.
