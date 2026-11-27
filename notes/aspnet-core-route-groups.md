# Minimal API Route Groups in ASP.NET Core

As a Minimal API grows, repeating the same prefix, authorization rule, and tags on every endpoint gets noisy. `MapGroup` lets you define a group of endpoints that share a URL prefix and metadata — authorization, tags, filters, rate-limiting — in one place.

## The example: an orders API

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization();
var app = builder.Build();

// Shared state for the demo (in a real app this is a service + database).
var orders = new Dictionary<int, Order>
{
    [1] = new Order(1, "Alice", 49.99m),
    [2] = new Order(2, "Bob", 120.00m)
};

var ordersGroup = app.MapGroup("/orders")
    .RequireAuthorization()          // applies to every route in the group
    .WithTags("Orders");            // one OpenAPI tag for the whole group

ordersGroup.MapGet("/", () => Results.Ok(orders.Values));

ordersGroup.MapGet("/{id:int}", (int id) =>
    orders.TryGetValue(id, out var order)
        ? Results.Ok(order)
        : Results.NotFound());

ordersGroup.MapPost("/", (Order order) =>
{
    orders[order.Id] = order;
    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

public record Order(int Id, string Customer, decimal Total);
```

## What the group gives you

### Shared prefix

Every route inside the group is prefixed with `/orders`, so `MapGet("/{id:int}")` becomes `GET /orders/{id:int}`. Change the prefix once and every route moves with it.

### Shared metadata

`.RequireAuthorization()` on the group protects all three endpoints at once — there is no way to add a new route to the group and forget the auth rule. `.WithTags("Orders")` groups all three under one heading in Swagger/OpenAPI instead of repeating the tag per route.

### Nested groups

Groups can nest for deeper structure. A nested group inherits the parent's prefix and metadata and adds its own:

```csharp
var orderItems = ordersGroup.MapGroup("/{id:int}/items")
    .RequireAuthorization("CanEditOrders");   // extra policy, on top of the group's

orderItems.MapGet("/", (int id) => Results.Ok(GetItems(id)));
orderItems.MapPost("/", (int id, OrderItem item) => AddItem(id, item));
```

Here `GET /orders/{id:int}/items` requires *both* the group's authorization and the `CanEditOrders` policy. Metadata composes; it never replaces the parent's.

## One GET and one POST, traced

**GET /orders/1** — the handler looks up id 1, finds Alice's order, and returns it:

```text
HTTP 200 OK
{ "id": 1, "customer": "Alice", "total": 49.99 }
```

**POST /orders** with body `{ "id": 3, "customer": "Carol", "total": 25.50 }` — the handler stores the order and returns `Created` with the location of the new resource:

```text
HTTP 201 Created
Location: /orders/3
{ "id": 3, "customer": "Carol", "total": 25.50 }
```

Returning `Results.Created` (201) instead of `Ok` (200) tells the client a new resource exists and where to find it — the standard REST convention for POST.

## Shared vs per-route

Put on the group what is true for *every* route in it: the prefix, the base authorization, the tag, common filters. Put on the individual route what is specific to it: the response type (`.Produces<Order>()`), a tighter authorization policy, or a per-route filter. If a single route needs to opt out of a group rule — say a public health check inside an authorized group — use `.AllowAnonymous()` on that route; it overrides the group's `RequireAuthorization` for that route only.

## Key Takeaways

- `MapGroup("/orders")` defines one shared prefix and shared metadata for all its routes.
- Group-level `.RequireAuthorization()` and `.WithTags()` apply to every route; nested groups inherit and extend them.
- A GET handler returns `Results.Ok` (200); a POST creating a resource returns `Results.Created` (201) with its location.
- Keep group-level settings for what is common to all routes, per-route settings for what differs.
- Nested groups compose prefixes and policies — the child route must satisfy both parent and child rules.
