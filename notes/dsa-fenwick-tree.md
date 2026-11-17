# Fenwick Tree (Binary Indexed Tree) (DSA)

A Fenwick tree (also called a Binary Indexed Tree, or BIT) is a compact data structure for prefix sums over an array that changes: it supports adding a value to one element and asking for the sum of a prefix, both in O(log n) time, using a single array of size n + 1. It is the go-to structure when you need dynamic range sums and do not need anything fancier — it is far simpler to implement than a segment tree.

## The Lowbit Trick

The whole structure rests on one bit operation: `lowbit(i) = i & -i` isolates the lowest set bit of `i`. In the tree, index `i` stores the sum of `lowbit(i)` array elements ending at `i`. So `tree[6]` (110₂, lowbit 2) stores `a[5] + a[6]`, while `tree[8]` (1000₂, lowbit 8) stores `a[1] + ... + a[8]`. The array is 1-indexed, and index 0 is unused — that is what makes the bit arithmetic work.

```csharp
public class FenwickTree
{
    private readonly long[] _tree;

    public FenwickTree(int size) => _tree = new long[size + 1];

    // Add 'delta' to element at 1-based index 'i'.
    public void Add(int i, long delta)
    {
        for (; i < _tree.Length; i += i & -i) // Climb to every range covering i.
            _tree[i] += delta;
    }

    // Sum of elements 1..i.
    public long PrefixSum(int i)
    {
        long sum = 0;
        for (; i > 0; i -= i & -i) // Walk down, collecting stored ranges.
            sum += _tree[i];
        return sum;
    }

    // Sum of elements l..r.
    public long RangeSum(int l, int r) => PrefixSum(r) - PrefixSum(l - 1);
}
```

`Add` moves *up* by adding the lowbit, touching each stored range that contains the element; `PrefixSum` moves *down* by subtracting the lowbit, collecting the stored ranges that tile the prefix. Both loops run at most log₂(n) steps, because each step clears or sets one bit.

## Worked Example

Build a tree over `a = [1, 2, 3, 4, 5]` (1-indexed). After adding each value, the internal array is:

```text
tree[1] = 1            (a[1])
tree[2] = 3            (a[1] + a[2])
tree[3] = 3            (a[3])
tree[4] = 10           (a[1] + a[2] + a[3] + a[4])
tree[5] = 5            (a[5])
```

Verify the queries: `PrefixSum(5) = tree[5] + tree[4] = 5 + 10 = 15`, which matches 1 + 2 + 3 + 4 + 5. `PrefixSum(3) = tree[3] + tree[2] = 3 + 3 = 6`, matching 1 + 2 + 3. `RangeSum(2, 4) = PrefixSum(4) − PrefixSum(1) = 10 − 1 = 9`, matching 2 + 3 + 4. Now update: `Add(3, 2)` changes `a[3]` from 3 to 5, touching `tree[3]` (3 → 5) and `tree[4]` (10 → 12); `PrefixSum(3)` becomes 5 + 3 = 8, matching 1 + 2 + 5.

## Fenwick Tree vs Segment Tree

The Fenwick tree is smaller, faster in practice, and roughly twenty lines of code — but it only does prefix sums (and, by subtraction, range sums) of an invertible operation: sums, products, XOR. A segment tree is more general: it handles range minimum/maximum, range assignment, and lazy propagation, none of which a Fenwick tree can express. Prefer the Fenwick tree when the operation is a prefix sum or prefix XOR and updates are point updates; reach for a segment tree when you need range queries over a non-invertible operation (min/max) or range updates. A Fenwick tree can also find the smallest index with a given prefix sum (binary lifting on the tree) in O(log n), which is handy for order statistics and frequency tables.

## Common Uses

Fenwick trees appear anywhere a running total changes over time: counting inversions in an array (process values and query how many larger values were already seen), dynamic frequency tables, and range-sum queries in coding problems with point updates. In application code they are rarer — a database usually owns that kind of aggregation — but the pattern (an array of partial sums indexed by lowbit) is worth knowing, because it is the simplest example of a data structure that trades a little extra space for logarithmic updates.

## Key Takeaways

- A Fenwick tree supports point updates and prefix-sum queries in O(log n) time with a single array of size n + 1.
- Index `i` stores the sum of `lowbit(i) = i & -i` elements ending at `i`; the array is 1-indexed and index 0 is unused.
- `Add` climbs by adding the lowbit; `PrefixSum` descends by subtracting it; a range sum is `PrefixSum(r) − PrefixSum(l − 1)`.
- Verified on `[1, 2, 3, 4, 5]`: prefix sums 15 and 6, range sum 9, and a point update that moves the prefix sum to 8.
- Prefer it over a segment tree for prefix sums/XOR with point updates — it is simpler and faster; prefer a segment tree for min/max or range updates.
- It also supports order statistics (find-by-prefix-sum) in O(log n) via binary lifting.
