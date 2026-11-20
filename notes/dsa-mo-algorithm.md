# Mo's Algorithm

Mo's algorithm answers many range queries offline by reordering them so the current interval moves as little as possible. It is the standard trick when each query needs an aggregate over a range and you can add or remove one element in O(1).

## The Idea

Divide the array into blocks of size ~√n. Sort the queries by (block of left endpoint, then right endpoint — ascending in even blocks, descending in odd blocks, to reduce movement). Then sweep a single `[L, R]` window across the array in that order, adding or removing elements at the edges:

```csharp
// Core window movement: each step is O(1).
void Add(int value)    { freq[value]++; if (freq[value] == 1) distinct++; }
void Remove(int value) { freq[value]--; if (freq[value] == 0) distinct--; }

while (currentL > query.L) { currentL--; Add(a[currentL]); }
while (currentR < query.R) { currentR++; Add(a[currentR]); }
while (currentL < query.L) { Remove(a[currentL]); currentL++; }
while (currentR > query.R) { Remove(a[currentR]); currentR--; }
```

The odd-block reversal (the "Hilbert order" lite) keeps the right pointer from jumping back to the start of the array on every block change.

## Sorting the Queries

Sorting is where the algorithm gets its speed. With block size `B = (int)Math.Sqrt(n)`, each query is ordered by:

```csharp
// Block size ~sqrt(n); sort queries before sweeping.
int blockSize = (int)Math.Sqrt(a.Length);
Array.Sort(queries, (x, y) =>
{
    int blockX = x.L / blockSize;
    int blockY = y.L / blockSize;
    if (blockX != blockY) return blockX.CompareTo(blockY);
    // Even blocks: R ascending. Odd blocks: R descending.
    return blockX % 2 == 0
        ? x.R.CompareTo(y.R)
        : y.R.CompareTo(x.R);
});
```

Without the odd-block reversal, the right pointer would travel back across the whole array each time the left block changes — O(n) per block change instead of O(n) per block overall. A further refinement is Hilbert-curve ordering, which reduces pointer movement in practice but keeps the same asymptotic bound.

## Worked Example: Distinct Values

Array `[1, 1, 2, 1, 3, 2, 2, 3]`, counting distinct values per query:

| Query | Range | Distinct values |
|-------|-------|-----------------|
| Q1 | [0, 3] | **2** — {1, 2} |
| Q2 | [2, 5] | **3** — {1, 2, 3} |
| Q3 | [4, 7] | **2** — {2, 3} |

Check Q2: elements at indices 2–5 are `[2, 1, 3, 2]`, distinct set {1, 2, 3}. Check Q3: indices 4–7 are `[3, 2, 2, 3]`, distinct set {2, 3}. Adding or removing one element updates the frequency map and the distinct count in O(1), so the whole cost is the pointer movement.

## Complexity

Sorting costs O(q log q). The pointers move O((n + q) · √n) in total: the left pointer moves at most O(√n) per query within its block sweep, and the right pointer moves O(n) per block, over √n blocks. Overall complexity is **O((n + q) · √n)**.

That beats answering each query independently in O(n) when q is large, but it is slower than a segment tree or Fenwick tree (O((n + q) log n)) for aggregates those structures support. Mo's wins when the aggregate is awkward for a tree — distinct counts, mode queries, or any operation that is easy to update incrementally but hard to merge from two halves.

## Key Takeaways

- Mo's algorithm reorders range queries by √n-sized blocks to minimize window movement.
- Maintain a sliding `[L, R]` window; each add/remove at the edges costs O(1).
- Sort by block of L, then R ascending in even blocks and descending in odd blocks.
- Total complexity is O((n + q) · √n), including the O(q log q) sort.
- For `[1, 1, 2, 1, 3, 2, 2, 3]`, distinct counts are 2, 3, and 2 for queries [0,3], [2,5], [4,7].
- Choose Mo's when the aggregate is easy to update incrementally but hard to merge, like distinct counts.
