# Lazy Propagation on Segment Trees

A segment tree answers range-sum queries in O(log n), but a naive range update — adding a value to every element in a range — still touches each element, costing O(n). Lazy propagation fixes that by deferring work until it is actually needed.

## Why Naive Range Updates Are O(n)

A segment tree over an array of size n has about 4n nodes, each storing the sum of its segment. A point update touches one node per level, so it costs O(log n). But a range update that visits every leaf in the range touches O(n) leaves in the worst case — for example, updating half the array visits roughly n/2 leaves. The query side is fast, but the update side is not.

## The Lazy Array Idea

Lazy propagation adds a second array, `lazy`, alongside the tree. When an update fully covers a node's segment, instead of pushing the update down to every leaf, we:

1. Update that node's stored sum immediately (add `value × segment length`).
2. Record the pending addition in `lazy[node]`.
3. Stop — do not visit the children.

Later, when a query or a partial update reaches that node, we **push** the pending value down to its children first, then continue. Work is only done on the paths that are actually visited, so both range updates and range queries cost O(log n).

```csharp
void Push(int node, int l, int r)
{
    if (lazy[node] == 0 || l == r) return;
    int mid = (l + r) / 2;
    Apply(node * 2, l, mid, lazy[node]);       // left child covers mid - l + 1 elements
    Apply(node * 2 + 1, mid + 1, r, lazy[node]); // right child covers r - mid elements
    lazy[node] = 0;
}

void Apply(int node, int l, int r, long value)
{
    tree[node] += value * (r - l + 1);
    lazy[node] += value;
}
```

## Worked Example: Range Add + Range Sum

Array (0-based indices): `[1, 3, 5, 7, 9, 11]`

- Total sum = 1 + 3 + 5 + 7 + 9 + 11 = **36**.
- Sum of indices [1..4] = 3 + 5 + 7 + 9 = **24**. ✓
- Now add 10 to indices [1..3]: the values become `[1, 13, 15, 17, 9, 11]`.
- Sum of indices [1..4] after the update = 13 + 15 + 17 + 9 = **54**. ✓
- New total sum = 1 + 13 + 15 + 17 + 9 + 11 = **66**, which is 36 + (10 × 3 updated elements) = 36 + 30 = **66**. ✓

With lazy propagation, the update to [1..3] touches only O(log n) nodes: the fully covered segments get their sums adjusted and a lazy tag recorded, and the leaves are never visited. The later query for [1..4] pushes the tags down only along the paths it needs.

## Complexity

| Operation        | Without lazy propagation | With lazy propagation |
|------------------|--------------------------|-----------------------|
| Build            | O(n)                     | O(n)                  |
| Point update     | O(log n)                 | O(log n)              |
| Range update     | O(n)                     | O(log n)              |
| Range query      | O(log n)                 | O(log n)              |
| Extra memory     | O(n)                     | O(n) (one lazy array) |

## Key Takeaways

- Naive range updates on a segment tree cost O(n) because they visit every leaf in the range.
- Lazy propagation stores a pending update in a `lazy` array at a node instead of pushing it to all descendants.
- A pending value is pushed down only when a later operation actually needs to visit that node's children.
- Range add + range sum on `[1, 3, 5, 7, 9, 11]`: sum [1..4] is 24; after adding 10 to [1..3] it is 54, and the total is 66.
- With lazy propagation, range updates and range queries both cost O(log n), at the cost of one extra array and a push step.
