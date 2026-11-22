# Binary Lifting

Many tree problems ask for the *k-th ancestor* of a node, or the *lowest
common ancestor* (LCA) of two nodes. Walking up one parent at a time costs
O(n) per query. **Binary lifting** precomputes jumps of size 1, 2, 4, 8, …
so any jump is assembled from O(log n) precomputed steps.

## The 2^k Ancestors Table

`up[k][v]` is the ancestor of `v` that is `2^k` steps above it. The table is
built with one recurrence:

```text
up[0][v] = parent(v)
up[k][v] = up[k-1][ up[k-1][v] ]     // two jumps of 2^(k-1) = one jump of 2^k
```

With `LOG = ceil(log2(n)) + 1` levels, the table costs O(n log n) time and
memory — trivial for most trees. A missing ancestor (above the root) is
stored as 0 / null.

## Answering a k-th Ancestor Query

Write `k` in binary and take the jumps whose bits are set, from the largest
bit down:

```csharp
int KthAncestor(int v, int k)
{
    for (int bit = 0; bit < LOG; bit++)
        if ((k & (1 << bit)) != 0)
        {
            v = up[bit][v];
            if (v == 0) return 0; // walked past the root
        }
    return v;
}
```

Jumping 13 = 8 + 4 + 1 costs three table lookups instead of thirteen steps.

## LCA in O(log n)

1. If the nodes are at different depths, lift the deeper one up to the
   shallower one's depth (using the binary representation of the difference).
2. If they are now the same node, that node is the LCA.
3. Otherwise, lift **both** nodes together from the largest jump down,
   skipping any jump that would land both on the same node. After the loop,
   both sit just below the LCA — so the answer is the parent of either.

```csharp
int Lca(int a, int b)
{
    if (depth[a] < depth[b]) (a, b) = (b, a);
    a = KthAncestor(a, depth[a] - depth[b]);
    if (a == b) return a;
    for (int k = LOG - 1; k >= 0; k--)
        if (up[k][a] != up[k][b])
        {
            a = up[k][a];
            b = up[k][b];
        }
    return up[0][a];
}
```

The key subtlety is step 3: lift only when the landing spots *differ*,
so the loop stops with both nodes as children of the LCA.

## Worked Example

Tree: 1 is the root, children 2 and 3; 2 has children 4 and 5; 3 has child 6;
5 has child 7; 6 has children 8 and 9.

Depths: 1→0, 2→1, 3→1, 4→2, 5→2, 6→2, 7→3, 8→3, 9→3.

`up` table (columns are 2^0 = 1, 2^1 = 2, 2^2 = 4 steps up; 0 = none):

| Node | up[0] | up[1] | up[2] |
|------|-------|-------|-------|
| 1 | 0 | 0 | 0 |
| 2 | 1 | 0 | 0 |
| 3 | 1 | 0 | 0 |
| 4 | 2 | 1 | 0 |
| 5 | 2 | 1 | 0 |
| 6 | 3 | 1 | 0 |
| 7 | 5 | 2 | 0 |
| 8 | 6 | 3 | 0 |
| 9 | 6 | 3 | 0 |

Check node 7: 1 step up is 5, 2 steps up is 2 (7→5→2), 4 steps up would
leave the tree (7→5→2→1→none), so up[2][7] = 0. Check node 8: 2 steps up is
3 (8→6→3). Both match the table.

Queries, verified by walking the tree by hand:

- **3rd ancestor of 7:** 3 = 2 + 1, so jump 2 then 1: 7 → 2 → 1. Answer **1**.
  (By hand: 7→5→2→1.)
- **2nd ancestor of 8:** 8→6→3. Answer **3**.
- **LCA(7, 9):** both at depth 3. Jump 2 together: 7→2, 9→3 (differ, so
  lift). Jump 1 together: 2→1, 3→1 (same, so skip). Answer is the parent
  of 2: **1**.
- **LCA(4, 7):** lift 7 up by 1 to depth 2: 7→5. Jump 2 together: 4→1, 5→1
  (same, skip). Jump 1: 4→2, 5→2 (same, skip). Answer is the parent of 4:
  **2**. (By hand: 4's ancestors are 2, 1; 7's are 5, 2, 1; lowest common
  is 2.)
- **LCA(8, 9):** both depth 3, jumps land together at every level, so the
  answer is the parent of 8: **6**.

## Preprocessing Cost and Alternatives

Binary lifting needs one DFS/BFS to fill depths and parents, then the table
build. For a static tree with many queries it is the standard choice. For a
single LCA query, a simple walk up is cheaper; for a dynamic tree where
parents change, a Link-Cut Tree or Euler tour + RMQ may fit better (Euler
tour + sparse table also answers LCA in O(1) after O(n log n) preprocessing,
but cannot answer k-th ancestor queries).

## Key Takeaways

- `up[k][v]` is the `2^k`-th ancestor of `v`; the table is built by
  `up[k][v] = up[k-1][up[k-1][v]]` in O(n log n).
- A k-th ancestor query decomposes `k` into powers of two: O(log n).
- LCA lifts the deeper node to equal depth, then lifts both together from
  the largest jump down, skipping jumps that would collide: O(log n).
- The worked tree verifies the table and queries by hand, including
  LCA(7, 9) = 1 and LCA(4, 7) = 2.
