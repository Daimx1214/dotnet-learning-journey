# Keyset Pagination in EF Core

Keyset pagination (also called seek pagination) pages by remembering the last row seen instead of counting rows to skip. It is the correct way to page large tables in EF Core.

## The Problem with OFFSET

The usual pattern looks harmless:

```csharp
var page = await context.Products
    .OrderBy(p => p.Id)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

SQL Server translates `Skip` to `OFFSET ... ROWS`, which still reads and discards every skipped row. Page 1 costs 20 rows; page 10,000 costs 200,000 rows scanned to return 20. Cost grows linearly with page depth.

OFFSET also has a correctness bug: if a row is inserted or deleted between page requests, rows shift and a row can appear twice or disappear entirely.

## How Keyset Pagination Works

Instead of a page number, the client sends the key of the last row it saw. The next page is "rows after that key":

```csharp
public async Task<List<Product>> GetPageAsync(int? lastSeenId, int pageSize = 20)
{
    var query = context.Products.OrderBy(p => p.Id);

    if (lastSeenId.HasValue)
    {
        query = (IOrderedQueryable<Product>)query.Where(p => p.Id > lastSeenId.Value);
    }

    return await query.Take(pageSize).ToListAsync();
}
```

This becomes `WHERE Id > @lastSeenId ORDER BY Id`, which seeks directly into the index. Page 10,000 costs the same as page 1: an index seek plus 20 rows.

## Composite Keys and Stable Ordering

A single-column key is easy, but most real orderings are composite — for example, newest first by `CreatedAt`, with ties broken by `Id`. The seek condition must mirror the ordering exactly:

```csharp
var query = context.Products
    .OrderByDescending(p => p.CreatedAt)
    .ThenByDescending(p => p.Id);

if (lastCreatedAt.HasValue && lastId.HasValue)
{
    query = (IOrderedQueryable<Product>)query.Where(p =>
        p.CreatedAt < lastCreatedAt.Value ||
        (p.CreatedAt == lastCreatedAt.Value && p.Id < lastId.Value));
}

return await query.Take(pageSize).ToListAsync();
```

The rule: the `WHERE` clause must express "comes after the last row in this exact ordering". With `n` sort columns, that is an `n`-part condition. If the ordering has ties that no column breaks, paging is unstable and rows can repeat or vanish — always end the ordering with a unique column.

## A Worked Example

Products ordered by `CreatedAt DESC, Id DESC`:

| Id | CreatedAt |
|----|-----------|
| 7  | 2026-11-24 10:00 |
| 6  | 2026-11-24 09:30 |
| 5  | 2026-11-24 09:30 |
| 4  | 2026-11-23 18:00 |
| 3  | 2026-11-23 12:00 |

Page 1 (size 2) returns Ids 7 and 6. The client sends back `lastCreatedAt = 09:30, lastId = 6`. Page 2 filters to `CreatedAt < 09:30 OR (CreatedAt = 09:30 AND Id < 6)`, returning Ids 5 and 4. Id 5 is not skipped even though it shares a timestamp with Id 6 — the tie-breaker column handles it.

## Trade-offs

Keyset pagination cannot jump to page 47 directly, and it cannot report "page 3 of 120" without a separate count query. That is usually fine: infinite scroll and "next page" UIs are the common cases, and a total count is a separate `COUNT(*)` query when the UI genuinely needs it.

Use OFFSET when the table is small, when the UI needs random page jumps, or when exact page numbers matter more than performance. Use keyset pagination for feeds, audit logs, and any table that will grow past a few thousand rows.

## Key Takeaways

- OFFSET scans and discards every skipped row, so deep pages get slower and slower; keyset pagination seeks directly to the last seen row.
- The seek condition must mirror the sort order exactly, including a unique tie-breaker column as the final sort key.
- Keyset pagination is stable under concurrent inserts and deletes; OFFSET is not.
- The cost is losing random page jumps and cheap page counts — a good trade for large tables and infinite scroll.
- In EF Core, keyset pagination is just a `Where` on the last seen key plus `Take`; no raw SQL is needed.
