# Sparse Table for Range Minimum Queries

A sparse table answers range minimum queries (RMQ) in `O(1)` time after `O(n log n)` preprocessing. It is the right tool when the array is static and queries are frequent.

## The Idea

Precompute the minimum of every interval whose length is a power of two. `st[k][i]` stores the minimum of the `2^k` elements starting at index `i`.

Any query interval `[l, r]` can be covered by two overlapping power-of-two blocks: one starting at `l`, one ending at `r`. Because minimum is idempotent — `min(x, x) = x` — the overlap does not change the answer.

## Building the Table

```csharp
public sealed class SparseTable
{
    private readonly int[,] _st;
    private readonly int[] _log;
    private readonly int _n;

    public SparseTable(int[] arr)
    {
        _n = arr.Length;
        int k = (int)Math.Log2(_n) + 1;
        _st = new int[k, _n];
        _log = new int[_n + 1];
        for (int i = 2; i <= _n; i++)
            _log[i] = _log[i / 2] + 1;

        for (int i = 0; i < _n; i++)
            _st[0, i] = arr[i];

        for (int j = 1; j < k; j++)
        {
            for (int i = 0; i + (1 << j) <= _n; i++)
            {
                _st[j, i] = Math.Min(_st[j - 1, i], _st[j - 1, i + (1 << (j - 1))]);
            }
        }
    }

    public int QueryMin(int l, int r)
    {
        int len = r - l + 1;
        int j = _log[len];
        return Math.Min(_st[j, l], _st[j, r - (1 << j) + 1]);
    }
}
```

The recurrence: a block of length `2^j` is the minimum of its two halves of length `2^(j-1)`.

## Hand-Verified Example

Array: `[7, 2, 3, 0, 5, 10, 3, 12, 18]` (indices 0–8).

**Query min(0, 4):** length 5, largest power of two that fits is 4 (`j = 2`). Cover with block `[0, 3]` and block `[1, 4]`:
- `min(7, 2, 3, 0) = 0`
- `min(2, 3, 0, 5) = 0`
- Answer: `min(0, 0) = 0`. Verified: the true minimum of `[7, 2, 3, 0, 5]` is 0.

**Query min(5, 8):** length 4, `j = 2`, a single block `[5, 8]`:
- `min(10, 3, 12, 18) = 3`. Verified: the true minimum of `[10, 3, 12, 18]` is 3.

**Query min(2, 7):** length 6, `j = 2` (block size 4). Cover with `[2, 5]` and `[4, 7]`:
- `min(3, 0, 5, 10) = 0`
- `min(5, 10, 3, 12) = 3`
- Answer: `min(0, 3) = 0`. The true minimum of `[3, 0, 5, 10, 3, 12]` is 0.

## The Idempotence Requirement

The two-block trick only works for idempotent operations, where overlapping data does not double-count: `min`, `max`, `gcd`, bitwise AND/OR. It does **not** work for sum: the overlap would be counted twice. For range sums, use a prefix-sum array instead; for range sums with updates, use a Fenwick tree or segment tree.

## Complexity and Trade-offs

| | Build | Query | Update |
|---|---|---|---|
| Sparse table | O(n log n) | O(1) | Not supported (rebuild) |
| Segment tree | O(n) | O(log n) | O(log n) |
| Prefix sums | O(n) | O(1) | Not supported |

Memory is `O(n log n)` — for `n = 100,000` that is about 17 layers, roughly 1.7 million integers. Choose a sparse table when the array never changes and queries are frequent; choose a segment tree when updates are needed.

## Key Takeaways

- Precompute minima for all power-of-two intervals; answer any query with two overlapping blocks.
- The trick depends on idempotence: it works for min, max, gcd — never for sum.
- Build `O(n log n)`, query `O(1)`, memory `O(n log n)`, and no updates without a rebuild.
- Verified on `[7, 2, 3, 0, 5, 10, 3, 12, 18]`: min(0, 4) = 0, min(5, 8) = 3, min(2, 7) = 0.
- For static arrays with many queries, it beats a segment tree; for changing arrays, use a segment tree instead.
