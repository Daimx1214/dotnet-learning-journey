# Edmonds-Karp Max Flow

Edmonds-Karp is the Ford-Fulkerson max-flow method with one rule: every augmenting path is found by breadth-first search, so it is always a shortest path (fewest edges) in the residual graph. That single rule turns Ford-Fulkerson's possibly endless augmenting into a polynomial algorithm.

## BFS Augmenting Paths

An augmenting path is a path from source `S` to sink `T` in the residual graph, where every edge still has spare capacity. The flow pushed along it is the bottleneck: the smallest residual capacity on the path. BFS guarantees the fewest edges, which bounds how many times any edge can become critical.

```csharp
// BFS that returns, for each reached node, its parent, the residual
// capacity of the edge used to reach it, and whether that edge is a
// forward edge (true) or a backward edge that cancels flow (false).
static Dictionary<int, (int From, int Residual, bool Forward)> Bfs(
    Dictionary<(int, int), int> capacity,
    Dictionary<(int, int), int> flow,
    int source, int sink)
{
    var parent = new Dictionary<int, (int, int, bool)>();
    var queue = new Queue<int>();
    queue.Enqueue(source);
    parent[source] = (-1, int.MaxValue, true);

    void Visit(int from, int to, int residual, bool forward)
    {
        if (residual > 0 && !parent.ContainsKey(to))
        {
            parent[to] = (from, residual, forward);
            queue.Enqueue(to);
        }
    }

    while (queue.Count > 0)
    {
        int u = queue.Dequeue();
        foreach (var ((a, b), cap) in capacity)
        {
            if (a == u) Visit(u, b, cap - flow[(a, b)], forward: true);  // forward edge
            if (b == u) Visit(u, a, flow[(a, b)], forward: false);       // backward edge
        }
        if (parent.ContainsKey(sink)) return parent;
    }
    return parent; // sink absent => no augmenting path left
}
```

## The Residual Graph

For every original edge `u -> v` with capacity `c` and current flow `f`, the residual graph has a forward edge with residual capacity `c - f` and a backward edge with residual capacity `f`. Pushing flow along the backward edge cancels flow on the original edge, which is how the algorithm corrects an earlier greedy choice. The algorithm stops exactly when `T` is unreachable from `S` in the residual graph; at that point the set of reachable nodes defines a minimum cut whose capacity equals the flow, proving the flow is maximum (max-flow min-cut theorem).

## Worked Example: A 4-Node Graph

Nodes: `S = 0`, `A = 1`, `B = 2`, `T = 3`. Capacities:

```text
S -> A : 10     S -> B : 5
A -> B : 4      A -> T : 6
B -> T : 9
```

Total capacity out of `S` is 10 + 5 = 15, and total capacity into `T` is 6 + 9 = 15, so the max flow cannot exceed 15. BFS explores neighbours in the order `A` before `B`:

**Path 1 — S -> A -> T** (2 edges, the shortest available). Bottleneck = min(10, 6) = **6**. Flow added: 6. Running total: 6. Residuals left: `S -> A` = 4, `A -> T` = 0.

**Path 2 — S -> B -> T** (2 edges). Bottleneck = min(5, 9) = **5**. Flow added: 5. Running total: 6 + 5 = 11. Residuals left: `S -> B` = 0, `B -> T` = 4.

**Path 3 — S -> A -> B -> T** (3 edges; the 2-edge paths are now saturated). Bottleneck = min(4, 4, 4) = **4**. Flow added: 4. Running total: 11 + 4 = 15. Residuals left: `S -> A` = 0, `A -> B` = 0, `B -> T` = 0.

No augmenting path remains: `S` has no residual outgoing edge (`S -> A` = 0, `S -> B` = 0). The algorithm stops.

Verification of the total: flow conservation holds at `A` (in 6 + 4 = 10, out 6 + 4 = 10) and at `B` (in 5 + 4 = 9, out 9). The cut `{S, A, B} | {T}` has capacity 6 + 9 = **15**, which equals the flow — so by max-flow min-cut, **15 is the maximum flow**, not just a flow that happens to be stuck. The `A -> B` edge is the interesting one: without it, `A` could only send 6 to `T` and 4 units of `S -> A` capacity would be wasted.

## Complexity: O(V · E²)

Each augmentation saturates at least one edge, and BFS guarantees an edge can become the bottleneck (critical) at most O(V) times, because the shortest-path distance to its head strictly increases between saturations. There are E edges, so there are O(V · E) augmentations, each costing one O(E) BFS: O(V · E²) in total. For the example graph, V = 4 and E = 5, giving a bound of 4 × 25 = 100 BFS-edge operations — the actual run used 3 augmentations.

```csharp
static int EdmondsKarp(
    Dictionary<(int, int), int> capacity, int source, int sink)
{
    var flow = capacity.Keys.ToDictionary(e => e, _ => 0);
    int totalFlow = 0;

    while (true)
    {
        var parent = Bfs(capacity, flow, source, sink);
        if (!parent.ContainsKey(sink)) break;

        int bottleneck = int.MaxValue;
        for (int v = sink; v != source; v = parent[v].From)
            bottleneck = Math.Min(bottleneck, parent[v].Residual);

        for (int v = sink; v != source; v = parent[v].From)
        {
            var (from, _, forward) = parent[v];
            // Forward edge: add flow. Backward edge: cancel flow on (v, from).
            if (forward) flow[(from, v)] += bottleneck;
            else flow[(v, from)] -= bottleneck;
        }

        totalFlow += bottleneck;
    }
    return totalFlow;
}
```

## Key Takeaways

- Edmonds-Karp is Ford-Fulkerson plus BFS: always augment along a shortest (fewest-edge) path in the residual graph.
- The residual graph's backward edges let the algorithm undo earlier flow; that is what makes the greedy augmentation correct.
- The worked 4-node example reaches max flow **15** in 3 augmentations (6 + 5 + 4), verified by flow conservation and by the min cut of capacity 15.
- Complexity is O(V · E²); for dense graphs Dinic's algorithm is the usual faster choice.
