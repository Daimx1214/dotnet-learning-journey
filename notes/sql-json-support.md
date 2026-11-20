# JSON Support in SQL Server

SQL Server has no native JSON type (until SQL Server 2025's `json` type), but it has a full set of functions for storing JSON in `nvarchar` columns and querying it like structured data.

## Producing JSON with FOR JSON

Turn query results into JSON for APIs without a serialization layer in your app:

```sql
SELECT TOP 5
    ProductId,
    Name,
    Price
FROM Products
ORDER BY Price DESC
FOR JSON PATH, ROOT('products');
```

`FOR JSON PATH` gives you control over nesting with column aliases (`'category.name'`), while `FOR JSON AUTO` nests automatically based on the joins. Add `INCLUDE_NULL_VALUES` when clients need explicit nulls.

## Reading JSON with OPENJSON and JSON_VALUE

`OPENJSON` shreds a JSON document into rows; `JSON_VALUE` and `JSON_QUERY` pull out single values:

```sql
-- Shred a JSON array into rows
SELECT *
FROM OPENJSON('[{"id":1,"name":"Keyboard"},{"id":2,"name":"Mouse"}]')
WITH (
    id   INT '$.id',
    name NVARCHAR(100) '$.name'
);

-- Extract values from a JSON column
SELECT
    OrderId,
    JSON_VALUE(Payload, '$.customer.name') AS customer_name,
    JSON_QUERY(Payload, '$.items') AS items_json
FROM Orders
WHERE ISJSON(Payload) = 1;
```

`JSON_VALUE` returns a scalar (string, number, bool); `JSON_QUERY` returns an object or array. `ISJSON` validates before parsing so bad rows do not break the query.

## Indexing JSON with Computed Columns

JSON columns cannot be indexed directly, but a computed column over `JSON_VALUE` can:

```sql
ALTER TABLE Orders
ADD CustomerName AS JSON_VALUE(Payload, '$.customer.name');

CREATE INDEX IX_Orders_CustomerName ON Orders (CustomerName);
```

Now a filter on `CustomerName` uses the index instead of scanning and parsing every row. Persist the computed column (`PERSISTED`) if the JSON is large or the column is read often.

## When JSON Columns Beat Separate Tables

JSON columns are the right call when the shape varies per row and you rarely filter or join on the inner fields: audit payloads, flexible product attributes, webhook bodies, user preferences. They are the wrong call when you filter, join, or aggregate on inner fields constantly, or when relationships need foreign keys — then a normalized table is faster and safer. A hybrid works well: normalized columns for the fields you query, one JSON column for the flexible remainder.

## Key Takeaways

- `FOR JSON PATH` / `FOR JSON AUTO` serialize query results to JSON for APIs.
- `OPENJSON` shreds JSON into rows; `JSON_VALUE` extracts scalars, `JSON_QUERY` extracts objects/arrays.
- Always guard parsing with `ISJSON` so malformed rows do not fail the query.
- Index JSON by creating a computed column over `JSON_VALUE` and indexing that column.
- Use JSON columns for variable-shape, rarely-filtered data like audit payloads and preferences.
- Normalize into tables when you filter, join, or aggregate on the inner fields regularly.
