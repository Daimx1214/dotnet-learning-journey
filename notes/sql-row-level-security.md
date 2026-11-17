# Row-Level Security in SQL Server (SQL)

Row-Level Security (RLS) filters rows inside the database engine, so every query — no matter which application, report, or ad-hoc tool runs it — only ever sees the rows it is allowed to see. It is the database-level answer to multi-tenancy: instead of trusting every query to remember `WHERE TenantId = @tenant`, you define the rule once as a security policy, and SQL Server applies it silently to every `SELECT`, `UPDATE` and `DELETE`. A forgotten `WHERE` clause then returns too few rows rather than another tenant's data.

## How It Works: Predicate Functions and Policies

RLS has two parts. First, an inline table-valued function — the *security predicate* — that returns `1` for rows the caller may see and `0` for rows it may not. Second, a *security policy* that binds that function to a table. The predicate receives the row's columns as arguments, so it can compare them against the current user or session context.

```sql
-- The tenant id for this session is stored in SESSION_CONTEXT by the app.
CREATE FUNCTION dbo.fn_TenantPredicate(@TenantId int)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS AccessGranted
WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS int);
GO

CREATE SECURITY POLICY dbo.TenantPolicy
ADD FILTER PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.Orders
WITH (STATE = ON);
GO
```

From now on, `SELECT * FROM dbo.Orders` in a session where `SESSION_CONTEXT(N'TenantId')` is `7` returns only orders with `TenantId = 7` — even if the query has no `WHERE` clause at all. The application sets the context once per connection (or per request, when using a pooled connection, it must be set on every checkout):

```sql
EXEC sp_set_session_context @key = N'TenantId', @value = 7;
```

## Filter vs Block Predicates

A policy can carry two kinds of predicate, and they do different jobs:

- **Filter predicate** — controls which rows a query can *read*. It applies to `SELECT`, and also to the rows an `UPDATE` or `DELETE` can touch. Rows that fail the filter are invisible.
- **Block predicate** — controls which rows a query can *write*. It applies to `INSERT`, `UPDATE` and `DELETE`, and comes in three flavours: `AFTER INSERT` (the inserted row must satisfy the predicate), `AFTER UPDATE` (the updated row must still satisfy it), and `BEFORE UPDATE` / `BEFORE DELETE` (the row being changed must satisfy it before the change).

A typical multi-tenant policy uses both: a filter predicate so tenants only read their own rows, and a block predicate so a tenant cannot insert a row stamped with somebody else's `TenantId`, or reassign one of their rows to another tenant.

```sql
CREATE SECURITY POLICY dbo.TenantPolicy
ADD FILTER PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.Orders,
ADD BLOCK PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.Orders AFTER INSERT,
ADD BLOCK PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.Orders AFTER UPDATE
WITH (STATE = ON);
```

With this in place, `INSERT INTO dbo.Orders (TenantId, ...) VALUES (9, ...)` from tenant 7's session fails with an error instead of silently creating a cross-tenant row.

## Gotchas

- **`dbo` (the table owner) bypasses RLS.** Members of the `db_owner` role and the table owner are not filtered. This is by design, but it means your application must never connect as `dbo` or `db_owner` — use a dedicated application login with only the permissions it needs, and test RLS under that login, not as an admin.
- **Performance.** The predicate runs per row, so a scalar function or a predicate that joins other tables can turn a fast scan into a slow one. Keep predicates simple (a comparison against `SESSION_CONTEXT` or `USER_NAME()`), and make sure the filtered column — usually `TenantId` — is indexed, so the optimizer can seek instead of scanning.
- **Context must be set.** If `SESSION_CONTEXT(N'TenantId')` is `NULL`, the cast to `int` yields `NULL`, the comparison is `NULL` rather than true, and the predicate returns no rows — the session sees an empty table. That fail-closed behaviour is good, but it also means a forgotten `sp_set_session_context` looks like "the table is empty", which is confusing to debug.
- **It is not encryption.** RLS hides rows from queries; it does not protect the data at rest, and a user with `SELECT` on the underlying table through another path (or with the policy disabled) can still read everything. Combine it with proper permissions and, where needed, column-level security.

## Key Takeaways

- Row-Level Security enforces row filtering inside the database engine, so every query and tool is subject to the same rule — a forgotten `WHERE` clause cannot leak another tenant's data.
- It has two parts: an inline table-valued predicate function returning 1/0, and a security policy binding it to a table.
- Filter predicates control reads; block predicates (`AFTER INSERT`, `AFTER UPDATE`, `BEFORE UPDATE/DELETE`) control writes.
- In multi-tenant apps, store the tenant id in `SESSION_CONTEXT` and compare it against the row's `TenantId` in the predicate; set the context on every connection checkout.
- The table owner and `db_owner` bypass RLS, so the app must connect as a least-privilege login and RLS must be tested under that login.
- Keep predicates simple and index the filtered column, and remember that a missing session context fails closed to an empty result set.
