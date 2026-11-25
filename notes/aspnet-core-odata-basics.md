# OData in ASP.NET Core

OData (Open Data Protocol) is a standard for building queryable REST APIs. Instead of writing a separate endpoint for every filter, sort, and page combination, the client sends query options in the URL (`$filter`, `$select`, `$orderby`, `$top`), and the server applies them to an `IQueryable`. It is a good fit when clients need flexible, ad-hoc querying over a dataset.

## Setup

Install `Microsoft.AspNetCore.OData`, then register OData with an EDM (Entity Data Model) that describes which entities and operations are exposed:

```csharp
var modelBuilder = new ODataConventionModelBuilder();
modelBuilder.EntitySet<Product>("Products");

builder.Services.AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", modelBuilder.GetEdmModel()));
```

`SetMaxTop(100)` caps page size so a client cannot request a million rows. The route prefix `odata` means endpoints live under `/odata/Products`.

## The EDM Model

The EDM model is the contract: it lists entity sets, keys, navigation properties, and functions. The convention model builder infers most of it from the CLR types, including navigation properties such as `Product.Category`. Explicit configuration is used for things the conventions cannot infer, like a bound function or a property that should be hidden.

## EnableQuery

The heart of OData in a controller is the `[EnableQuery]` attribute:

```csharp
public class ProductsController : ODataController
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [EnableQuery]
    public IQueryable<Product> Get()
    {
        return _db.Products;
    }

    [EnableQuery]
    public SingleResult<Product> Get(int key)
    {
        return SingleResult.Create(_db.Products.Where(p => p.Id == key));
    }
}
```

The action returns an `IQueryable` without executing it. OData translates the query options into LINQ, applies them to the query, and EF Core runs the final SQL. Filtering and paging therefore happen in the database, not in memory. Returning `IEnumerable` or a materialized list defeats this and should be avoided.

## Query Options in Practice

Against `/odata/Products`, a client can write:

```text
GET /odata/Products?$filter=Price gt 50 and Category/Name eq 'Books'
GET /odata/Products?$select=Name,Price&$orderby=Price desc&$top=10&$skip=20
GET /odata/Products?$expand=Category&$filter=contains(Name, 'net')
GET /odata/Products/$count?$filter=InStock eq true
```

- `$filter`: `eq`, `ne`, `gt`, `lt`, `and`, `or`, `not`, `contains`, `startswith`.
- `$select`: return only the named properties, which shrinks payloads.
- `$orderby`: sort by one or more properties, with `asc` or `desc`.
- `$top` / `$skip`: paging. Combine with `$count=true` for total counts.
- `$expand`: include a navigation property such as `Category` in the response.

## ODataController vs a Plain Controller

`ODataController` adds routing conventions for OData paths (entity sets, keys, navigation properties) and helper methods like `SingleResult.Create`. A plain `ControllerBase` with `[EnableQuery]` also works for simple GET endpoints, but the OData routing conventions only apply to controllers derived from `ODataController`.

## When to Use OData vs Plain REST

Use OData when clients need ad-hoc querying: reporting tools, admin grids, or a data API where the consumer decides the filter and the columns. Use plain REST when the API surface is fixed, the responses are shaped per use case, or strict control over what can be queried matters. OData exposes the entity model directly, so it fits internal or partner APIs more than public APIs where the schema must stay stable and minimal.

## Security Notes

- Always set `SetMaxTop` and consider `MaxExpansionDepth` to bound expensive queries.
- Do not expose an entity set for a sensitive table, or restrict it with authorization and a filtered `IQueryable`.
- `[EnableQuery]` on an unfiltered `DbSet` lets a client read every row, so combine it with per-user filtering in the action.

## Key Takeaways

- OData lets clients filter, sort, page, and shape results with `$filter`, `$select`, `$orderby`, `$top`, and `$expand`.
- The EDM model declares the exposed entities; `[EnableQuery]` applies query options to an `IQueryable` so SQL does the work.
- Cap page size with `SetMaxTop` and never return a materialized list from an OData action.
- OData fits flexible data APIs and admin grids; plain REST fits fixed, shaped endpoints.
- Exposing an entity set is exposing the model, so authorize and filter the queryable before returning it.
