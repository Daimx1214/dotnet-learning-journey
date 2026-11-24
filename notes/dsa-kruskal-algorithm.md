# Kruskal's Algorithm

Kruskal's algorithm finds a Minimum Spanning Tree (MST) of a weighted, undirected, connected graph. It builds the MST by adding edges in increasing order of weight, skipping any edge that would form a cycle.

## The Idea

Sort all edges by weight, lightest first. Walk through them one by one: take an edge if its endpoints are in different components, skip it if they are already connected. Stop when the MST has `V - 1` edges.

The "already connected" test is exactly what a disjoint-set union (Union-Find) data structure answers in near-constant time.

## Union-Find in Brief

Union-Find keeps a partition of vertices into components. Two operations matter:

- `Find(x)` returns the representative of x's component (with path compression).
- `Union(a, b)` merges two components (by rank or size).

Both run in amortized `O(alpha(V))`, effectively constant.

## Pseudocode

```text
Kruskal(V, edges):
    sort edges by weight ascending
    uf = UnionFind(V)
    mst = []
    for (u, v, w) in edges:
        if uf.Find(u) != uf.Find(v):
            uf.Union(u, v)
            mst.append((u, v, w))
        if len(mst) == V - 1:
            break
    return mst
```

## Worked Example

Graph with 5 vertices and 7 edges:

| Edge | Weight |
|------|--------|
| 1-2  | 1 |
| 1-3  | 2 |
| 3-4  | 2 |
| 0-2  | 3 |
| 0-1  | 4 |
| 2-3  | 4 |
| 2-4  | 5 |

Walk through the sorted edges:

1. **1-2 (1)**: different components. Take it. Components: {1, 2}, {0}, {3}, {4}.
2. **1-3 (2)**: 1 is in {1, 2}, 3 is alone. Take it. Components: {1, 2, 3}, {0}, {4}.
3. **3-4 (2)**: 3 is in {1, 2, 3}, 4 is alone. Take it. Components: {1, 2, 3, 4}, {0}.
4. **0-2 (3)**: 0 is alone, 2 is in the big component. Take it. Components: {0, 1, 2, 3, 4}.
5. **0-1 (4)**: both endpoints already in the same component. Skip it — it would form the cycle 0-2-1-0.
6. **2-3 (4)**: same component. Skip it — cycle 2-1-3-2.
7. **2-4 (5)**: same component. Skip it — cycle 2-3-4-2.

The MST has `V - 1 = 4` edges, so we stop after step 4.

MST edges: (1-2, 1), (1-3, 2), (3-4, 2), (0-2, 3).
Total weight: 1 + 2 + 2 + 3 = **8**.

## C# Sketch

```csharp
var edges = new List<(int U, int V, int W)>
{
    (0, 1, 4), (0, 2, 3), (1, 2, 1),
    (1, 3, 2), (2, 3, 4), (3, 4, 2), (2, 4, 5)
};

edges.Sort((a, b) => a.W.CompareTo(b.W));
var uf = new UnionFind(5);
int total = 0;
foreach (var (u, v, w) in edges)
{
    if (uf.Find(u) != uf.Find(v))
    {
        uf.Union(u, v);
        total += w;
        Console.WriteLine($"Take {u}-{v} (weight {w})");
    }
    else
    {
        Console.WriteLine($"Skip {u}-{v} (weight {w}) - would form a cycle");
    }
}
Console.WriteLine($"MST total weight: {total}"); // 8
```

## Complexity

Sorting dominates: `O(E log E)`. The Union-Find passes cost `O(E alpha(V))`. Total time is `O(E log E)`, space is `O(V + E)`. For dense graphs, Prim's algorithm with a Fibonacci heap can be faster, but Kruskal's is simpler and works well on sparse graphs.

## When to Use It

- Network design: connect all sites with minimum cable cost.
- Clustering: stop early to get k clusters instead of one tree.
- Anytime edges arrive pre-sorted, Kruskal's is almost linear.

## Key Takeaways

- Kruskal's is greedy: always take the lightest edge that does not form a cycle.
- Union-Find is the engine: it answers "same component?" in effectively constant time.
- A connected graph with V vertices has an MST of exactly V - 1 edges and total weight 8 in the worked example.
- Skipped edges are not wasted work — each one proves a cycle exists among already-chosen edges.
- Time complexity is dominated by the sort: `O(E log E)`.
