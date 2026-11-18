# SQL Server Full-Text Search

`LIKE '%word%'` cannot use an index — it scans the table, and it cannot do word stemming, ranking, or "any of these words" queries. SQL Server's full-text search is the built-in alternative: it builds a specialized index over words in text columns and lets you search by words and phrases, with relevance ranking, in milliseconds on large tables.

It matters for .NET backends whenever users search free text: product descriptions, article bodies, support tickets, document titles. A search box backed by `LIKE` gets slower as the table grows and returns results in arbitrary order; full-text search stays fast and returns the best matches first.

## How it works: catalogs and indexes

Full-text search has two objects. A **full-text catalog** is a container (a logical grouping, stored in the database) that holds one or more full-text indexes. A **full-text index** is built on a table: it tokenizes the text in the chosen columns into individual words (using a word breaker for the column's language), removes stopwords ("the", "and", "is"), and stores an inverted index of word → rows.

```sql
-- 1. Create a catalog to hold the indexes.
CREATE FULLTEXT CATALOG ProductCatalog AS DEFAULT;
GO

-- 2. Create a full-text index on the Products table.
--    It needs a unique, single-column, non-nullable key (usually the PK).
CREATE FULLTEXT INDEX ON dbo.Products
(
    Name LANGUAGE 1033,        -- 1033 = English word breaker
    Description LANGUAGE 1033
)
KEY INDEX PK_Products          -- the unique index on the key column
ON ProductCatalog
WITH CHANGE_TRACKING AUTO;     -- keep the index updated as rows change
GO
```

`CHANGE_TRACKING AUTO` keeps the full-text index updated as rows are inserted, updated, and deleted. `MANUAL` requires you to run `ALTER FULLTEXT INDEX ... START FULL POPULATION` yourself — useful for bulk loads where you populate once at the end.

## CONTAINS vs FREETEXT

There are two main predicates. `CONTAINS` is precise: you control exactly what matches. `FREETEXT` is fuzzy: it takes a phrase, breaks it into words, and finds rows matching any of them, including inflected forms.

```sql
-- CONTAINS: exact word or phrase match.
SELECT ProductId, Name
FROM dbo.Products
WHERE CONTAINS(Description, 'wireless');

-- CONTAINS with a phrase and a prefix term.
SELECT ProductId, Name
FROM dbo.Products
WHERE CONTAINS(Description, '"noise cancelling" OR "bluetooth*"');

-- CONTAINS with NEAR: words close together.
SELECT ProductId, Name
FROM dbo.Products
WHERE CONTAINS(Description, 'NEAR((wireless, headphones), 5)');

-- FREETEXT: meaning-based match — any of the words, inflections included.
-- 'running shoes' also matches 'run' and 'shoe'.
SELECT ProductId, Name
FROM dbo.Products
WHERE FREETEXT(Description, 'running shoes');
```

`CONTAINS` is the right default for a search box where the user typed specific terms: it is predictable and fast. `FREETEXT` is better for "find me things like this description" — pasting a sentence and getting similar documents back. `CONTAINS` also supports `FORMSOF(INFLECTIONAL, run)` (matches run, running, ran) and `FORMSOF(THESAURUS, ...)` (matches synonyms from a thesaurus file).

## Ranking with CONTAINSTABLE

`CONTAINS` returns a yes/no match. `CONTAINSTABLE` returns the same matches plus a `RANK` value (0–1000) so you can order results by relevance — the most relevant first, which is what a search page needs.

```sql
-- Ranked search: best matches first.
SELECT p.ProductId, p.Name, ft.RANK
FROM dbo.Products AS p
INNER JOIN CONTAINSTABLE(dbo.Products, Description, 'wireless headphones') AS ft
    ON p.ProductId = ft.[KEY]
ORDER BY ft.RANK DESC;
```

The rank reflects term frequency (how often the word appears in the row), document length (a short row matching the term ranks higher than a long one), and how many of the query terms matched. It is a relative score, not a percentage — use it for ordering, not for absolute thresholds.

There is also `FREETEXTTABLE`, the ranked version of `FREETEXT`. Both table-valued functions return `[KEY]` (the key column value) and `[RANK]`, and you join them back to the table.

## Languages and stoplists

The word breaker and stemmer are language-specific: English stemming knows "running" relates to "run"; other languages have their own breakers. Set the language per column (as in the `CREATE FULLTEXT INDEX` example) or per query with `LANGUAGE 1033`.

A **stoplist** is the list of words ignored during indexing and searching — the default system stoplist contains words like "the", "and", "a". Searching for a stopword returns nothing, which surprises people the first time it happens.

```sql
-- See what the system stoplist ignores.
SELECT stopword FROM sys.fulltext_system_stopwords WHERE language_id = 1033;

-- Create an empty stoplist (index every word) and attach it to the index.
CREATE FULLTEXT STOPLIST NoStopwords FROM SYSTEM STOPLIST;
GO
ALTER FULLTEXT INDEX ON dbo.Products SET STOPLIST NoStopwords;
```

Use the system stoplist for normal text search. Use an empty or custom stoplist when your domain words overlap with stopwords — for example, if "IT" or "A" are meaningful terms in your data, the default stoplist would make them unsearchable.

## Searching multiple columns and weighting terms

A full-text index can cover several columns, and a query can search across all of them at once. You can also give one column more influence than another by running separate `CONTAINSTABLE` queries and combining the ranks — SQL Server has no built-in per-column weighting, so the common pattern is a weighted sum.

```sql
-- Search both Name and Description in one predicate.
SELECT ProductId, Name
FROM dbo.Products
WHERE CONTAINS((Name, Description), 'wireless');

-- Weight the Name column higher than Description: rank from the Name
-- match counts double, so a match in the name outranks a match in the body.
SELECT p.ProductId, p.Name,
       (ISNULL(n.RANK, 0) * 2 + ISNULL(d.RANK, 0)) AS WeightedRank
FROM dbo.Products AS p
LEFT JOIN CONTAINSTABLE(dbo.Products, Name, 'wireless') AS n
    ON p.ProductId = n.[KEY]
LEFT JOIN CONTAINSTABLE(dbo.Products, Description, 'wireless') AS d
    ON p.ProductId = d.[KEY]
WHERE n.[KEY] IS NOT NULL OR d.[KEY] IS NOT NULL
ORDER BY WeightedRank DESC;
```

## Common gotchas

- **Searching for a stopword returns nothing.** `CONTAINS(Description, 'the')` finds no rows because "the" was never indexed. If your users can search for such words, use a custom or empty stoplist.
- **Special characters need quoting.** Terms containing characters like `-` or `&` must be wrapped in double quotes inside the search condition, or the parser treats them as operators.
- **Short words may be ignored.** The word breaker drops single characters in some languages, so searching for "A" or "C" as a product grade needs a different approach (a normal indexed column, not full-text).
- **The index lags behind bulk changes with CHANGE_TRACKING MANUAL.** If you load data in bulk and forget the population step, searches silently return stale results. Prefer `CHANGE_TRACKING AUTO` unless you have a specific reason to populate manually.
- **Full-text search is word-based, not substring-based.** `CONTAINS(Description, 'phone')` does not match "headphones" — use a prefix term (`"phone*"`) only when you genuinely want prefix matching, and remember it still matches whole words starting with the prefix, not words containing it.

## When to use full-text search vs LIKE

- **Use full-text search** when users search words inside large text columns, when you need ranking, stemming, or phrase/NEAR queries, and when the table is large enough that a scan hurts.
- **Use `LIKE 'prefix%'`** when you only need a prefix match on a short column (a name, a code) — a normal index handles that well, and full-text search adds nothing.
- **Use `LIKE '%word%'`** only on small tables or as a fallback for exact substring matching, because it always scans and cannot rank.
- **Consider a dedicated search engine** (Elasticsearch, Azure AI Search, Meilisearch) when you need faceting, typo tolerance, autocomplete, or multi-language search at scale. Full-text search is the right built-in choice for a single SQL Server database with straightforward text search needs.

One more practical note: full-text search is not enabled on every edition in older versions, and it must be installed as a feature — on Azure SQL Database it is available by default, on a self-hosted SQL Server it is a setup option. Check `SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')` if a `CREATE FULLTEXT` statement fails.

## Key Takeaways

- Full-text search builds a word-level inverted index (catalog + full-text index) over text columns, so word searches stay fast as the table grows — unlike `LIKE '%word%'`, which always scans.
- `CONTAINS` is precise (exact words, phrases, prefix terms, NEAR); `FREETEXT` is fuzzy (any word, inflections included) for "find things like this" queries.
- `CONTAINSTABLE` and `FREETEXTTABLE` add a `RANK` value so results can be ordered by relevance, best match first.
- Word breaking and stemming are language-specific — set the language per column or per query — and the stoplist controls which common words are ignored.
- Use `CHANGE_TRACKING AUTO` to keep the index updated as rows change; use `MANUAL` with an explicit population for bulk loads.
- Choose full-text search for word search in large text columns, `LIKE 'prefix%'` with a normal index for prefix matches, and a dedicated search engine when you need faceting, typo tolerance, or autocomplete at scale.
