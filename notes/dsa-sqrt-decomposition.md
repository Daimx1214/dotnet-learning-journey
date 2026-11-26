# Square Root Decomposition

Square root decomposition answers range queries with point updates. Split
the array into blocks of about `sqrt(n)` elements, precompute one aggregate
value per block, and answer a query by combining whole blocks with a partial
scan of at most two edge blocks.

## The Idea

Take an array of `n` elements and divide it into blocks of size `B`. A
range query touches three parts: a partial block on the left edge, a run of
whole blocks in the middle, and a partial block on the right edge. The whole
blocks are answered from their precomputed aggregates in O(1) each, and the
two edge blocks are scanned element by element, at most `2B` steps.

Total cost per query is roughly `n / B + B`, minimized when `B = sqrt(n)`,
giving O(sqrt n) per query. A point update changes one element and its block
aggregate: O(1) for a sum, O(B) if the aggregate must be rescanned.

## Worked Example: Range Sum on [1..10]

Use the array `[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]` with block size 3 (`sqrt(10)` is about 3.16):

```
Index:  0  1  2 | 3  4  5 | 6  7  8 | 9
Value:  1  2  3 | 4  5  6 | 7  8  9 | 10
Block:    0     |   1     |   2     |  3
Sum:     6     |   15    |   24    |  10
```

Query the sum of indices 2 through 8, that is, values `3` to `9`:

- Left partial block (block 0): index 2 only, value `3`.
- Whole blocks: block 1 (sum `15`) and block 2 (sum `24`).
- Right partial block: none, because index 8 ends exactly at the end of
  block 2, so the sum is `3 + 15 + 24 = 42`.

Check: `3 + 4 + 5 + 6 + 7 + 8 + 9 = 42`.

Now update index 4 from `5` to `50`: only block 1 changes, its sum becomes
`4 + 50 + 6 = 60`, so the update is O(1).

## Implementation Sketch

```csharp
public sealed class RangeSum
{
    private readonly int[] _data;
    private readonly long[] _blockSums;
    private readonly int _blockSize;

    public RangeSum(int[] data)
    {
        _data = data;
        _blockSize = (int)Math.Sqrt(data.Length) + 1;
        int blocks = (_data.Length + _blockSize - 1) / _blockSize;
        _blockSums = new long[blocks];
        for (int i = 0; i < _data.Length; i++)
            _blockSums[i / _blockSize] += _data[i];
    }

    public void Update(int index, int value)
    {
        _blockSums[index / _blockSize] += value - _data[index];
        _data[index] = value;
    }

    public long Query(int left, int right)
    {
        long sum = 0;
        int startBlock = left / _blockSize, endBlock = right / _blockSize;

        if (startBlock == endBlock)
        {
            for (int i = left; i <= right; i++) sum += _data[i];
            return sum;
        }
        int endOfStart = (startBlock + 1) * _blockSize - 1;
        for (int i = left; i <= endOfStart; i++) sum += _data[i];

        for (int b = startBlock + 1; b <= endBlock - 1; b++)
            sum += _blockSums[b];

        for (int i = endBlock * _blockSize; i <= right; i++)
            sum += _data[i];

        return sum;
    }
}
```

The `startBlock == endBlock` case handles a query inside a single block
with a plain scan. The three loops handle the left edge, the whole blocks,
and the right edge.

## Mo's Algorithm Connection

Mo's algorithm answers many offline range queries by sorting them so the
query window moves as little as possible, using the same block idea: sort
by the block of the left endpoint, then by the right endpoint. The total
window movement is O((n + q) * sqrt(n)) for `q` queries. Square root
decomposition is the natural first step toward Mo's algorithm.

## When to Prefer It Over a Segment Tree

A segment tree answers the same queries in O(log n) with O(n) space, which
beats O(sqrt n) asymptotically. Square root decomposition wins on
simplicity: the code above is short, there is no recursion, and it adapts
easily to aggregates a segment tree handles awkwardly, such as range mode queries.

Choose a segment tree (or a Fenwick tree for prefix sums) when the query
count is large and O(log n) matters. Choose square root decomposition when
the code must be written quickly, understood at a glance, or adapted to an unusual aggregate.

## Key Takeaways

- Split the array into blocks of about `sqrt(n)` and precompute one aggregate per block.
- A range query combines at most two partial edge scans with whole-block aggregates, costing O(sqrt n).
- A point update only changes its own block aggregate, costing O(1) for a sum.
- The same block idea powers Mo's algorithm for offline range queries.
- Prefer square root decomposition for simplicity and unusual aggregates; prefer a segment tree when O(log n) per query matters.
