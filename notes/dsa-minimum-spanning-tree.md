# Minimum Spanning Tree (DSA)

A minimum spanning tree (MST) of a weighted, connected, undirected graph is a subset of its edges that connects every vertex, contains no cycles, and has the smallest possible total edge weight. Spanning trees exist only for connected graphs, and the MST is unique when all edge weights are distinct. MSTs show up wherever you need to connect a set of points as cheaply as possible: laying network cables, designing road or pipeline networks, and clustering points in machine learning.

## The Cut Property

Both classic MST algorithms rest on one idea: for any cut of the graph — a partition of the vertices into two sets — the lightest edge crossing the cut belongs to *some* MST. Kruskal's algorithm applies this greedily by always picking the lightest remaining edge that does not form a cycle; Prim's algorithm grows a single tree by always adding the lightest edge that connects the tree to a new vertex. Both produce an MST, and both run in polynomial time.

## Kruskal's Algorithm (Union-Find)

Kruskal sorts all edges by weight, then adds edges one at a time, skipping any edge whose endpoints are already connected. A disjoint-set union (union-find) structure tracks connectivity in near-constant time per operation, which keeps the whole algorithm dominated by the sort.

```csharp
public record Edge(int From, int To, int Weight);

public class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public UnionFind(int n)
    {
        _parent = Enumerable.Range(0, n).ToArray();
        _rank = new int[n];
    }

    public int Find(int x)
    {
        if (_parent[x] != x)
            _parent[x] = Find(_parent[x]); // Path compression.
        return _parent[x];
    }

    public bool Union(int a, int b)
    {
        int rootA = Find(a), rootB = Find(b);
        if (rootA == rootB) return false; // Already connected — edge would form a cycle.
        if (_rank[rootA] < _rank[rootB]) (rootA, rootB) = (rootB, rootA);
        _parent[rootB] = rootA;
        if (_rank[rootA] == _rank[rootB]) _rank[rootA]++;
        return true;
    }
}

public static List<Edge> Kruskal(int vertexCount, List<Edge> edges)
{
    var mst = new List<Edge>();
    var uf = new UnionFind(vertexCount);

    foreach (Edge edge in edges.OrderBy(e => e.Weight))
    {
        if (uf.Union(edge.From, edge.To))
        {
            mst.Add(edge);
            if (mst.Count == vertexCount - 1) break; // A tree on n vertices has n - 1 edges.
        }
    }
    return mst;
}
```

## Prim's Algorithm

Prim grows one tree from a starting vertex. At each step it picks the cheapest edge from the tree to a vertex outside it. A priority queue makes that choice efficient: push each candidate edge as it is discovered, and ignore edges whose far end is already in the tree.

```csharp
public static List<Edge> Prim(int vertexCount, List<(int To, int Weight)>[] adjacency)
{
    var mst = new List<Edge>();
    var inTree = new bool[vertexCount];
    var pq = new PriorityQueue<(int From, int To, int Weight), int>();

    inTree[0] = true;
    foreach (var (to, weight) in adjacency[0])
        pq.Enqueue((0, to, weight), weight);

    while (pq.Count > 0 && mst.Count < vertexCount - 1)
    {
        var (from, to, weight) = pq.Dequeue();
        if (inTree[to]) continue; // The other end is already in the tree.

        inTree[to] = true;
        mst.Add(new Edge(from, to, weight));

        foreach (var (next, w) in adjacency[to])
            if (!inTree[next])
                pq.Enqueue((to, next, w), w);
    }
    return mst;
}
```

## Kruskal vs Prim

| Aspect | Kruskal | Prim |
|--------|---------|------|
| Strategy | Sorts all edges, adds the lightest that avoids a cycle | Grows one tree, always adds the cheapest connecting edge |
| Data structure | Union-find | Priority queue |
| Time complexity | O(E log E) — dominated by sorting | O(E log V) with a binary heap |
| Best for | Sparse graphs (few edges) | Dense graphs (many edges) |
| Works on | Any connected graph; also finds a minimum spanning forest on disconnected graphs | Needs a connected graph (or one run per component) |
| Implementation | Simpler — sort, then union-find | Slightly more involved — adjacency list and a heap |

The rule of thumb: reach for Kruskal when the graph is sparse, because sorting a short edge list is cheap; reach for Prim when the graph is dense, because its cost depends on vertices rather than edges. In .NET, `PriorityQueue<TElement, TPriority>` (available since .NET 6) makes Prim straightforward to write.

## Key Takeaways

- An MST connects all vertices with no cycles and the smallest total edge weight; it is unique when all weights are distinct.
- Kruskal sorts edges and uses union-find to skip cycle-forming edges — O(E log E), best on sparse graphs.
- Prim grows one tree with a priority queue, always adding the cheapest edge to a new vertex — O(E log V), best on dense graphs.
- Both algorithms are correct because of the cut property: the lightest edge across any cut belongs to some MST.
- Use Kruskal for a minimum spanning *forest* on disconnected graphs; use Prim when the graph is dense or you only need one component.
