# Lowest Common Ancestor (LCA)

Given a rooted tree, the lowest common ancestor of two nodes `u` and `v`
is the deepest node that is an ancestor of both. LCA is the core of many
tree queries: distance between nodes, path queries, and subtree checks
all reduce to finding it.

The tree used in this note:

```text
        0
       / \
      1   2
     / \ / \
    3  4 5  6
```

Depths: depth(0) = 0, depth(1) = depth(2) = 1, depth(3) = depth(4) =
depth(5) = depth(6) = 2.

## Naive approach: walk up the parents

Lift the deeper node up until both nodes are at the same depth, then
lift both together until they meet:

```csharp
int LcaNaive(int u, int v, int[] parent, int[] depth)
{
    while (depth[u] > depth[v]) u = parent[u];
    while (depth[v] > depth[u]) v = parent[v];
    while (u != v)
    {
        u = parent[u];
        v = parent[v];
    }
    return u;
}
```

This is simple and correct, but each query costs O(n) in the worst case:
on a chain-shaped tree, lifting one node can walk the whole height.
For one query that is fine; for thousands of queries on a large tree it
is too slow.

## Binary lifting: O(log n) per query

Binary lifting precomputes jump tables so a node can be lifted by any
number of steps in O(log n) jumps. Define:

- `up[0][v]` = the parent of `v` (the 2^0 ancestor).
- `up[k][v]` = the 2^k-th ancestor of `v`, computed as
  `up[k][v] = up[k - 1][ up[k - 1][v] ]`.

For n nodes, the table needs LOG = ceil(log2(n)) + 1 levels, so
preprocessing costs O(n log n) time and memory. With n = 7, LOG = 3
is enough, because 2^3 = 8 covers every possible jump.

Query in two steps:

1. Lift the deeper node up to the depth of the shallower node, using
   the binary representation of the depth difference.
2. If they are now equal, that node is the LCA. Otherwise, lift both
   nodes together from the largest jump down, skipping any jump that
   would make them equal, then return the parent of either node.

```csharp
// up[k][v] = 2^k-th ancestor of v; up[0][v] = parent, up[0][root] = root.
// depth[v] = distance from the root. LOG = 3 covers n = 7 nodes.
int Lca(int u, int v, int[][] up, int[] depth, int log)
{
    if (depth[u] < depth[v])
    {
        (u, v) = (v, u); // u is now the deeper node.
    }

    // Step 1: lift u up to the depth of v.
    int diff = depth[u] - depth[v];
    for (int k = 0; k < log; k++)
    {
        if ((diff & (1 << k)) != 0)
        {
            u = up[k][u];
        }
    }

    if (u == v) return u;

    // Step 2: lift both together, largest jump first.
    for (int k = log - 1; k >= 0; k--)
    {
        if (up[k][u] != up[k][v])
        {
            u = up[k][u];
            v = up[k][v];
        }
    }

    return up[0][u]; // The parent of both is the LCA.
}
```

## Distance between two nodes

Once the LCA is known, the distance (number of edges) is:

```text
distance(u, v) = depth[u] + depth[v] - 2 * depth[LCA(u, v)]
```

The path from `u` up to the LCA plus the path from the LCA down to `v`
is exactly the shortest path between them.

## Worked example on the tree above

Parents: parent(0) = 0 (the root points to itself), parent(1) = 0,
parent(2) = 0, parent(3) = 1, parent(4) = 1, parent(5) = 2,
parent(6) = 2.

- LCA(3, 4) = 1. Both are children of 1, so 1 is their deepest common
  ancestor.
- LCA(3, 5) = 0. Node 3 is in the left subtree, node 5 in the right
  subtree, so only the root is common.
- LCA(5, 6) = 2. Both are children of 2.
- LCA(3, 6) = 0. Node 3 is under 1, node 6 is under 2; the root is the
  only common ancestor.
- distance(3, 6) = depth(3) + depth(6) - 2 * depth(0) = 2 + 2 - 0 = 4
  edges. Path: 3 - 1 - 0 - 2 - 6.
- distance(3, 4) = depth(3) + depth(4) - 2 * depth(1) = 2 + 2 - 2 = 2
  edges. Path: 3 - 1 - 4.
- distance(4, 5) = depth(4) + depth(5) - 2 * depth(0) = 2 + 2 - 0 = 4
  edges. Path: 4 - 1 - 0 - 2 - 5.

## Complexity summary

| Approach | Preprocess | Query |
| --- | --- | --- |
| Naive parent walk | O(n) | O(n) worst case |
| Binary lifting | O(n log n) | O(log n) |

Binary lifting is the right choice when many queries run on the same
tree. The naive walk wins only when there are very few queries and the
table setup is not worth it.

## Key Takeaways

- LCA(u, v) is the deepest node that is an ancestor of both u and v.
- The naive parent walk is O(n) per query; binary lifting answers each
  query in O(log n) after O(n log n) preprocessing of the up[k][v] table.
- distance(u, v) = depth[u] + depth[v] - 2 * depth[LCA(u, v)].
- On the example tree, LCA(3, 4) = 1, LCA(3, 5) = 0, LCA(5, 6) = 2,
  distance(3, 6) = 4 edges, and distance(3, 4) = 2 edges.
