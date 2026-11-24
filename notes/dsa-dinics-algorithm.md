# Dinic's Algorithm for Maximum Flow

Dinic's algorithm computes the maximum flow in a flow network faster than the basic Ford-Fulkerson method by pushing flow in phases. Each phase builds a level graph with a breadth-first search from the source, then pushes a blocking flow through that level graph with depth-first search. A phase ends when no augmenting path remains in the level graph, and the algorithm terminates when the sink is unreachable in the level graph.

## How It Works

The level graph assigns every vertex a level equal to its shortest distance (in edges) from the source, considering only edges with remaining capacity. Flow is only pushed along edges that go from level `d` to level `d + 1`. This restriction is what makes Dinic's fast: within one phase, every augmenting path has the same length, and the shortest path length strictly increases between phases.

A blocking flow saturates at least one edge on every source-to-sink path in the level graph. Once a blocking flow is pushed, the next BFS produces a strictly longer shortest path (or finds the sink unreachable). Dinic's runs in `O(V^2 * E)` time in general, and `O(min(V^(2/3), E^(1/2)) * E)` on unit-capacity networks.

## Worked Example

Consider this network with source `0` and sink `5`:

```text
0 -> 1 (10)   0 -> 2 (10)
1 -> 2 (2)    1 -> 3 (4)    1 -> 4 (8)
2 -> 4 (9)
3 -> 5 (10)
4 -> 3 (6)    4 -> 5 (10)
```

Phase 1: BFS levels are `0: {0}`, `1: {1, 2}`, `2: {3, 4}`, `3: {5}`. A blocking flow pushes 4 units on `0-1-3-5`, 6 units on `0-1-4-5` (limited by the 6 units of `0->1` capacity left), and 4 units on `0-2-4-5` (limited by the 4 units of `4->5` capacity left). That is 14 units so far.

Phase 2: the residual graph still allows `0-2-4-3-5`. BFS levels are `0: {0}`, `1: {2}`, `2: {4}`, `3: {3}`, `4: {5}` (`1` is unreachable now because `0->1` is saturated). A blocking flow pushes 5 more units on `0-2-4-3-5`, limited by the 5 units of `2->4` capacity left.

The maximum flow is **19**. It cannot be 20: `0->2` (10) can deliver at most 9 onward because `2->4` caps at 9, and `0->1` (10) caps the other side, so the achievable total is 19. One optimal flow: `0->1` = 10, `0->2` = 9, `1->3` = 4, `1->4` = 6, `2->4` = 9, `4->3` = 5, `4->5` = 10, `3->5` = 9. Check the sink: 10 + 9 = 19.

## Implementation Sketch in C#

```csharp
sealed class Dinic(int n)
{
    private readonly List<Edge>[] _g = Enumerable.Range(0, n)
        .Select(_ => new List<Edge>()).ToArray();
    private int[] _level = new int[n];
    private int[] _it = new int[n];

    private sealed record Edge(int To, int Rev, int Cap);

    public void AddEdge(int from, int to, int cap)
    {
        _g[from].Add(new Edge(to, _g[to].Count, cap));
        _g[to].Add(new Edge(from, _g[from].Count - 1, 0));
    }

    private bool Bfs(int s, int t)
    {
        Array.Fill(_level, -1);
        var q = new Queue<int>();
        _level[s] = 0;
        q.Enqueue(s);
        while (q.Count > 0)
        {
            int v = q.Dequeue();
            foreach (var e in _g[v])
            {
                if (e.Cap > 0 && _level[e.To] < 0)
                {
                    _level[e.To] = _level[v] + 1;
                    q.Enqueue(e.To);
                }
            }
        }
        return _level[t] >= 0;
    }

    private int Dfs(int v, int t, int f)
    {
        if (v == t) return f;
        for (; _it[v] < _g[v].Count; _it[v]++)
        {
            var e = _g[v][_it[v]];
            if (e.Cap > 0 && _level[v] < _level[e.To])
            {
                int d = Dfs(e.To, t, Math.Min(f, e.Cap));
                if (d > 0)
                {
                    _g[v][_it[v]] = e with { Cap = e.Cap - d };
                    var rev = _g[e.To][e.Rev];
                    _g[e.To][e.Rev] = rev with { Cap = rev.Cap + d };
                    return d;
                }
            }
        }
        return 0;
    }

    public int MaxFlow(int s, int t)
    {
        int flow = 0;
        while (Bfs(s, t))
        {
            Array.Fill(_it, 0);
            int f;
            while ((f = Dfs(s, t, int.MaxValue)) > 0)
                flow += f;
        }
        return flow;
    }
}
```

## Dinic's vs Edmonds-Karp

Edmonds-Karp augments along one shortest path at a time and runs in `O(V * E^2)`. Dinic's pushes a whole blocking flow per phase, so it needs at most `V - 1` phases. On dense graphs the difference is large: for `V = 100, E = 1000`, Edmonds-Karp is bounded by roughly `10^8` operations, while Dinic's is bounded by roughly `10^7` — an order of magnitude better. On unit networks (every capacity 1), Dinic's is effectively `O(min(V^(2/3), E^(1/2)) * E)`, which is why it is the standard choice for bipartite matching.

## Common Pitfalls

- Forgetting the reverse edge with capacity 0 means flow can never be rerouted, and the result will be wrong on graphs where an early greedy choice blocks a better path (like the `4->3` reroute in the example).
- Not resetting the current-edge pointer (`_it`) between phases causes the DFS to skip edges that regained capacity.
- Dinic's assumes integer capacities for the complexity bounds; with floating-point capacities it still terminates but the bounds do not apply.

## Key Takeaways

- Dinic's algorithm alternates BFS level-graph construction with DFS blocking-flow pushes, and the shortest path length grows every phase.
- On the classic example graph (source 0, sink 5), the maximum flow is 19, verified by an explicit flow assignment and the min-cut bound.
- Time complexity is `O(V^2 * E)` in general and much better on unit networks, beating Edmonds-Karp on dense graphs.
- Always add reverse edges with zero capacity; without them the algorithm cannot correct earlier routing decisions.
