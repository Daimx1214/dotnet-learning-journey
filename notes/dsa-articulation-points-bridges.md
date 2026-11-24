# Articulation Points and Bridges (Tarjan's Low-Link)

An articulation point (cut vertex) is a vertex whose removal disconnects the graph. A bridge (cut edge) is an edge whose removal disconnects the graph. Both are found in one DFS pass with Tarjan's low-link values.

## Discovery Time and Low-Link

During DFS, record two numbers per vertex:

- `disc[v]` — the time `v` is first visited (a counter that increments per vertex).
- `low[v]` — the earliest discovery time reachable from `v` using zero or more tree edges followed by at most one back edge.

```text
low[v] = min(
    disc[v],
    disc[w] for each back edge (v, w),
    low[to] for each DFS tree child `to` of v
)
```

A low value below a vertex's own discovery time proves there is a back edge escaping its subtree.

## The Two Rules

After DFS returns from a child `to` of `v`:

- **Bridge:** edge `v-to` is a bridge if `low[to] > disc[v]`. The child subtree has no back edge reaching `v` or above, so removing the edge isolates it.
- **Articulation point:** `v` is an articulation point if `low[to] >= disc[v]` for any child `to`. The child subtree cannot escape past `v`, so removing `v` disconnects that subtree. The root is a special case: it is an articulation point only if it has two or more DFS children.

The difference is strict (`>`) versus non-strict (`>=`): a back edge to `v` itself saves the edge but not the vertex.## C# Implementation

```csharp
public sealed class ArticulationAndBridges
{
    private readonly List<int>[] _adj;
    private readonly int[] _disc;
    private readonly int[] _low;
    private int _time;
    public List<int> ArticulationPoints { get; } = new();
    public List<(int, int)> Bridges { get; } = new();

    public ArticulationAndBridges(int n, List<(int, int)> edges)
    {
        _adj = new List<int>[n];
        for (int i = 0; i < n; i++) _adj[i] = new List<int>();
        foreach (var (u, v) in edges)
        {
            _adj[u].Add(v);
            _adj[v].Add(u);
        }
        _disc = new int[n];
        _low = new int[n];
        Array.Fill(_disc, -1);
    }

    public void Run()
    {
        for (int i = 0; i < _adj.Length; i++)
            if (_disc[i] == -1)
                Dfs(i, parent: -1);
    }

    private void Dfs(int v, int parent)
    {
        _disc[v] = _low[v] = ++_time;
        int children = 0;

        foreach (int to in _adj[v])
        {
            if (to == parent) continue;
            if (_disc[to] == -1)
            {
                children++;
                Dfs(to, v);
                _low[v] = Math.Min(_low[v], _low[to]);

                if (_low[to] > _disc[v])
                    Bridges.Add((v, to));
                if (parent != -1 && _low[to] >= _disc[v])
                    ArticulationPoints.Add(v);
            }
            else
            {
                // Back edge to an ancestor already visited.
                _low[v] = Math.Min(_low[v], _disc[to]);
            }
        }

        if (parent == -1 && children >= 2)
            ArticulationPoints.Add(v);
    }
}
```

## Hand-Verified Example

Graph: vertices 0–4, edges 0-1, 1-2, 2-0 (a triangle) plus 1-3, 3-4 (a chain).

DFS from 0 visits 0, 1, 2, then 3, 4:

| Vertex | disc | low | Reason |
|--------|------|-----|--------|
| 0 | 1 | 1 | root |
| 1 | 2 | 1 | back edge 2-0 reaches disc 1 |
| 2 | 3 | 1 | back edge to 0 |
| 3 | 4 | 4 | no back edge |
| 4 | 5 | 5 | no back edge |

- Edge 1-3: `low[3] = 4 > disc[1] = 2`, so it is a **bridge**. Edge 3-4: `low[4] = 5 > disc[3] = 4`, so it is a **bridge**. Triangle edges are not bridges because `low[2] = 1` is not greater than `disc[1] = 2`.
- Vertex 1: child 3 has `low[3] = 4 >= disc[1] = 2`, so 1 is an **articulation point** (removing it isolates 3 and 4). Vertex 3: child 4 has `low[4] = 5 >= disc[3] = 4`, so 3 is also an **articulation point**. Vertex 0 is the root with one DFS child, so it is not one.

Result: articulation points {1, 3}, bridges {1-3, 3-4}.

## Complexity

One DFS pass: `O(V + E)` time, `O(V)` extra space for the arrays and recursion stack.

## Key Takeaways

- `low[v]` is the earliest discovery time reachable from `v` via tree edges plus at most one back edge.
- Bridge: `low[child] > disc[v]`. Articulation point: `low[child] >= disc[v]` for some child (root needs 2+ children).
- The `>` versus `>=` distinction is the whole difference between the two rules.
- Verified on the triangle-plus-chain graph: articulation points {1, 3}, bridges {1-3, 3-4}.
- One DFS pass gives both answers in `O(V + E)` time.
