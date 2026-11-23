# Prim's Algorithm for the Minimum Spanning Tree

Given a connected, weighted, undirected graph, a minimum spanning tree (MST) connects all vertices with the smallest possible total edge weight. Prim's algorithm grows one tree, one cheapest edge at a time, starting from any vertex.

## The Greedy Cut Property

The algorithm rests on the cut property: for any cut (a split of the vertices into two non-empty sets), the cheapest edge crossing that cut belongs to some MST. Prim's maintains the cut between the vertices already in the tree and the vertices outside it, and repeatedly adds the cheapest crossing edge. Because that edge is always safe, the greedy choice never needs to be undone. This is why the algorithm works only when edge weights are fixed and the graph is connected — a disconnected graph has no spanning tree at all.

## Min-Heap Implementation Sketch

A priority queue (min-heap) holds the frontier edges, keyed by weight, so the cheapest crossing edge is always found in logarithmic time.

```csharp
// Sketch: edges are (weight, from, to). The heap orders by weight.
var visited = new HashSet<int>();
var heap = new PriorityQueue<(int From, int To), int>();

visited.Add(start);
foreach (var (to, weight) in graph[start])
{
    heap.Enqueue((start, to), weight);
}

int totalWeight = 0;
while (heap.Count > 0 && visited.Count < vertexCount)
{
    var (from, to) = heap.Dequeue();
    if (visited.Contains(to))
    {
        continue; // Stale entry: 'to' joined the tree through a cheaper edge.
    }

    visited.Add(to);
    // Add the weight of this edge to totalWeight here; the full version
    // below shows that step by carrying the weight in the dequeued item.
}
```

The full version below stores the weight in the dequeued item so the weight of the edge just taken is the one added to the total:

```csharp
int PrimsMst(List<(int To, int Weight)>[] graph, int start)
{
    int n = graph.Length;
    var visited = new bool[n];
    var heap = new PriorityQueue<(int From, int To, int Weight), int>();
    visited[start] = true;

    foreach (var (to, weight) in graph[start])
    {
        heap.Enqueue((start, to, weight), weight);
    }

    int total = 0;
    int added = 0;

    while (heap.Count > 0 && added < n - 1)
    {
        var edge = heap.Dequeue();
        if (visited[edge.To])
        {
            continue;
        }

        visited[edge.To] = true;
        total += edge.Weight;
        added++;

        foreach (var (to, weight) in graph[edge.To])
        {
            if (!visited[to])
            {
                heap.Enqueue((edge.To, to, weight), weight);
            }
        }
    }

    return total;
}
```

Stale heap entries are expected: when a vertex is reached by a cheaper edge, the older, heavier entries for it stay in the heap and are skipped by the `visited` check when they surface.

## Worked Example: 5-Node Graph

Vertices `0, 1, 2, 3, 4` with these undirected edges and weights:

| Edge | Weight |
|------|--------|
| 0 - 1 | 2 |
| 0 - 3 | 6 |
| 1 - 2 | 3 |
| 1 - 3 | 8 |
| 1 - 4 | 5 |
| 2 - 3 | 4 |
| 2 - 4 | 7 |
| 3 - 4 | 9 |

Start Prim's at vertex `0`. Trace the growing tree:

1. Tree `{0}`. Frontier edges: `0-1 (2)`, `0-3 (6)`. Cheapest is `0-1 (2)`. Tree becomes `{0, 1}`, running total `2`.
2. Tree `{0, 1}`. Frontier edges: `0-3 (6)`, `1-2 (3)`, `1-3 (8)`, `1-4 (5)`. Cheapest is `1-2 (3)`. Tree becomes `{0, 1, 2}`, running total `2 + 3 = 5`.
3. Tree `{0, 1, 2}`. Frontier edges: `0-3 (6)`, `1-3 (8)`, `1-4 (5)`, `2-3 (4)`, `2-4 (7)`. Cheapest is `2-3 (4)`. Tree becomes `{0, 1, 2, 3}`, running total `5 + 4 = 9`.
4. Tree `{0, 1, 2, 3}`. Frontier edges: `1-4 (5)`, `2-4 (7)`, `3-4 (9)`. Cheapest is `1-4 (5)`. Tree becomes `{0, 1, 2, 3, 4}`, running total `9 + 5 = 14`.

Verified result: MST edges are `0-1 (2)`, `1-2 (3)`, `2-3 (4)`, `1-4 (5)`. Total weight `2 + 3 + 4 + 5 = 14`. Sanity checks: the tree has `5 - 1 = 4` edges, it connects all 5 vertices, and no cheaper alternative exists — the only edge not taken that could replace one, `0-3 (6)`, is heavier than the `2-3 (4)` edge that connects vertex 3, and `2-4 (7)` and `3-4 (9)` are both heavier than `1-4 (5)`.

## Prim vs Kruskal Comparison

| Aspect | Prim's | Kruskal's |
|--------|--------|-----------|
| Growth | One tree growing from a start vertex | A forest of small trees merged together |
| Edge choice | Cheapest edge crossing the tree cut | Cheapest edge overall that does not form a cycle |
| Data structure | Min-heap of frontier edges | Sorting edges once + union-find |
| Best for | Dense graphs (many edges) | Sparse graphs (few edges) |
| Complexity | `O(E log V)` with a binary heap | `O(E log E)` dominated by sorting |

Both produce an MST of the same total weight (the tree itself may differ when equal-weight edges exist). Choose Prim's when the graph is dense and you already have an adjacency list; choose Kruskal's when the edges are already sorted or the graph is sparse.

## Key Takeaways

- Prim's adds the cheapest edge crossing the cut between the tree and the rest, which the cut property guarantees is safe.
- A min-heap makes each cheapest-edge choice `O(log V)`; stale entries are skipped with a visited check.
- On the worked 5-node graph, the verified MST weight is `14` from edges `0-1`, `1-2`, `2-3`, and `1-4`.
- Prim's grows one tree and suits dense graphs; Kruskal's sorts edges and suits sparse graphs.
- Both algorithms return the same minimum total weight, though the chosen edges can differ on ties.
