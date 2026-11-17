# Columnstore Indexes in SQL Server (SQL)

A normal (rowstore) index stores whole rows together, which is ideal when a query needs a few complete rows — an order lookup by ID, for example. A columnstore index stores each column separately, compressed, which is ideal when a query scans millions of rows but only touches a few columns — a `SUM(SalesAmount)` grouped by year, for instance. The difference is not incremental: analytical queries on columnstore tables routinely run 10–100× faster than on rowstore, because the engine reads only the columns the query needs and processes them in batches.

## Rowstore vs Columnstore

Consider a `Sales` table with 10 million rows and 20 columns. A query asking for `SUM(Amount) GROUP BY Year` touches 2 of the 20 columns. On a rowstore table, SQL Server must read all 20 columns of every row (or at least the full index) to reach the two it needs. On a columnstore table, it reads only the `Amount` and `Year` column segments, skips the other 18 entirely, and benefits from two further effects: compression and batch mode.

```sql
-- A clustered columnstore index replaces the rowstore clustered index entirely.
CREATE CLUSTERED COLUMNSTORE INDEX CCI_Sales ON dbo.Sales;
GO

-- Or keep the rowstore table and add a nonclustered columnstore index
-- on just the columns your analytics queries use.
CREATE NONCLUSTERED COLUMNSTORE INDEX NCCI_Sales_Analytics
ON dbo.Sales (SaleDate, Region, ProductId, Amount, Quantity);
GO

-- The query this index is built for: scan 10M rows, touch 2 columns.
SELECT YEAR(SaleDate) AS SaleYear, SUM(Amount) AS TotalSales
FROM dbo.Sales
GROUP BY YEAR(SaleDate)
ORDER BY SaleYear;
```

## Compression and Batch Mode

Columnstore compresses each column segment independently, and columns compress far better than rows: a column of repeated region names or dates compresses to a fraction of its rowstore size, typically 5–10× smaller on real data. Smaller data means fewer pages read from disk and more data fitting in memory. The second accelerator is *batch mode* execution: instead of processing one row at a time, the engine processes vectors of up to ~900 rows per batch, which is far more CPU-efficient for aggregations, joins and sorts. Together, compression plus batch mode is what produces the 10–100× speedups on analytical workloads — the often-quoted figure is not one trick but the product of reading less data and processing it faster.

## Clustered vs Nonclustered Columnstore

- **Clustered columnstore** — the table itself *is* the columnstore index; there is no separate rowstore copy. Best when the table is primarily analytical (a fact table in a warehouse). Inserts go first to a *deltastore* (a small rowstore staging area) and are compressed into column segments in the background once the deltastore reaches about 1,048,576 rows. Small trickle inserts therefore stay in rowstore form for a while, which is fine for bulk loads but adds overhead to single-row inserts.
- **Nonclustered columnstore** — the rowstore table stays as it is, and the columnstore index is an extra copy of selected columns. Best for operational tables that also need analytics: OLTP queries keep using the rowstore indexes, analytical queries use the columnstore index. The cost is storage for the extra copy and maintenance on writes.

## When to Avoid Columnstore

Columnstore is a poor fit for heavy OLTP workloads: tables with frequent single-row inserts, updates and deletes. Every update or delete marks rows in a delete bitmap and forces segment maintenance, so write-heavy tables can end up slower than plain rowstore. Other situations to avoid it: tables with very few rows (the overhead is not worth it below roughly a million rows), queries that mostly fetch a handful of complete rows by key, and tables with many `varchar(max)` / `varbinary(max)` columns, which cannot be stored efficiently in column segments. As a rule of thumb: if the workload is "read a few rows, write a lot", stay rowstore; if it is "scan millions of rows, aggregate a few columns", use columnstore.

## Key Takeaways

- Columnstore indexes store each column separately and compressed, so analytical queries read only the columns they need instead of whole rows.
- Compression (typically 5–10× smaller) plus batch mode execution (up to ~900 rows per batch) is what delivers 10–100× speedups on aggregations over millions of rows.
- A clustered columnstore index makes the whole table columnstore — best for warehouse fact tables; a nonclustered columnstore index is an extra copy of selected columns on a rowstore table — best for operational tables that also need analytics.
- Small inserts land in a rowstore deltastore and are compressed into segments in the background once it reaches about 1,048,576 rows.
- Avoid columnstore for heavy single-row OLTP writes, small tables (below roughly a million rows), and key-lookup workloads — updates and deletes pay segment-maintenance costs.
- Rule of thumb: "read a few rows, write a lot" stays rowstore; "scan millions, aggregate a few columns" goes columnstore.
