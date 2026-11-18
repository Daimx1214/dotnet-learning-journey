# Bellman-Ford Algorithm

The Bellman-Ford algorithm finds the shortest paths from a single source vertex to every other vertex in a weighted graph — including graphs with **negative edge weights**. Dijkstra's algorithm is faster but breaks on negative edges, because it assumes that once a vertex is finalised its distance can never improve. Bellman-Ford makes no such assumption: it simply relaxes every edge over and over until no distance can improve any further.

It matters whenever edge weights can be negative: currency exchange rates (arbitrage detection), network routing with costs and penalties, and any shortest-path problem where "cheaper" can mean a negative number.

## The Relaxation Idea

The heart of the algorithm is *relaxation*. For an edge `u -> v` with weight `w`, if reaching `v` through `u` is cheaper than the best distance known so far, update it:

```text
if dist[u] + w < dist[v]:
    dist[v] = dist[u] + w
    predecessor[v] = u
```

A shortest path in a graph with `V` vertices can contain at most `V - 1` edges (any longer path repeats a vertex and therefore contains a cycle, which cannot help if there are no negative cycles). So if we relax **every** edge `V - 1` times, every shortest path has had the chance to propagate, edge by edge, from the source to every vertex.

## The Algorithm

```csharp
// Returns true if no negative cycle is reachable from the source.
public static bool BellmanFord(
    int vertexCount,
    List<(int From, int To, int Weight)> edges,
    int source,
    out int[] dist,
    out int[] predecessor)
{
    dist = Enumerable.Repeat(int.MaxValue, vertexCount).ToArray();
    predecessor = Enumerable.Repeat(-1, vertexCount).ToArray();
    dist[source] = 0;

    // Relax every edge V - 1 times.
    for (int i = 0; i < vertexCount - 1; i++)
    {
        foreach (var (u, v, w) in edges)
        {
            if (dist[u] != int.MaxValue && dist[u] + w < dist[v])
            {
                dist[v] = dist[u] + w;
                predecessor[v] = u;
            }
        }
    }

    // One more pass: if anything still relaxes, a negative cycle exists.
    foreach (var (u, v, w) in edges)
    {
        if (dist[u] != int.MaxValue && dist[u] + w < dist[v])
            return false; // negative cycle reachable from source
    }

    return true;
}
```

The `dist[u] != int.MaxValue` guard matters: without it, adding a weight to "infinity" would overflow and produce a bogus smaller distance.

## Worked Example (CLRS)

Consider the classic example from *Introduction to Algorithms* (CLRS), with source vertex `0` and these edges:

```text
0 -> 1 (weight 6)     0 -> 3 (weight 7)
1 -> 2 (weight 5)     1 -> 3 (weight 8)     1 -> 4 (weight -4)
2 -> 1 (weight -2)
3 -> 2 (weight -3)    3 -> 4 (weight 9)
4 -> 0 (weight 2)     4 -> 2 (weight 7)
```

Running Bellman-Ford from source `0` gives the final distances:

```text
dist = [0, 2, 4, 7, -2]
```

Walk through why vertex `4` ends up at `-2`: the path `0 -> 3 -> 2 -> 1 -> 4` costs `7 + (-3) + (-2) + (-4) = -2`, which beats the direct-looking route `0 -> 1 -> 4` costing `6 + (-4) = 2`. Dijkstra would have finalised vertex `1` at distance `6` after the first extraction and never discovered the cheaper `0 -> 3 -> 2 -> 1` route — this is exactly the case where Bellman-Ford wins.

After `V - 1 = 4` relaxation passes, no edge can improve any distance further, so the algorithm stops. A fifth pass that still finds an improvement would prove a negative cycle is reachable from the source.

## Negative-Cycle Detection

A negative cycle is a cycle whose total weight is negative. Going around it forever keeps decreasing the distance, so no shortest path exists. Bellman-Ford detects this for free: after the `V - 1` passes, run one more pass over all edges. If any edge still relaxes, a negative cycle is reachable from the source.

This is not a theoretical curiosity. In currency exchange, vertices are currencies, edges are exchange rates (log-transformed to weights), and a negative cycle is an arbitrage opportunity — a sequence of trades that ends with more money than you started with.

## Complexity

| | Bellman-Ford | Dijkstra (binary heap) |
|---|---|---|
| Time | O(V × E) | O((V + E) log V) |
| Space | O(V) | O(V) |
| Negative edges | Yes | No |
| Negative cycles | Detects them | Undefined behaviour |
| Implementation | Very simple | Needs a priority queue |

`O(VE)` looks expensive, and it is — on a dense graph with `V` vertices and roughly `V²` edges it becomes `O(V³)`. Dijkstra is the right default when all weights are non-negative. Bellman-Ford earns its place when weights can be negative, or when you specifically need negative-cycle detection.

An easy optimisation: stop early if a full pass makes no changes. In practice many graphs converge well before `V - 1` passes, and the early exit costs nothing.

```csharp
for (int i = 0; i < vertexCount - 1; i++)
{
    bool changed = false;
    foreach (var (u, v, w) in edges)
    {
        if (dist[u] != int.MaxValue && dist[u] + w < dist[v])
        {
            dist[v] = dist[u] + w;
            predecessor[v] = u;
            changed = true;
        }
    }
    if (!changed) break; // already converged
}
```

## Reconstructing a Path

The `predecessor` array records, for each vertex, which vertex gave it its best distance. Walk backwards from the target to the source to recover the actual shortest path:

```csharp
public static List<int> ReconstructPath(int[] predecessor, int source, int target)
{
    var path = new List<int>();
    for (int v = target; v != -1; v = predecessor[v])
        path.Add(v);
    path.Reverse();
    return path[0] == source ? path : []; // empty if unreachable
}
```

## Key Takeaways

- Bellman-Ford finds single-source shortest paths in graphs with negative edge weights, where Dijkstra fails.
- It relaxes every edge `V - 1` times, because a shortest path without cycles has at most `V - 1` edges.
- One extra relaxation pass that still improves a distance proves a negative cycle is reachable from the source.
- In the CLRS example from source `0`, final distances are `[0, 2, 4, 7, -2]` — vertex 4 is reached via the negative edges `0 -> 3 -> 2 -> 1 -> 4`.
- Time complexity is O(VE), slower than Dijkstra's O((V + E) log V), so use Dijkstra when all weights are non-negative.
- The `predecessor` array lets you reconstruct the actual shortest path, and an early exit when a pass makes no changes speeds up typical graphs.
