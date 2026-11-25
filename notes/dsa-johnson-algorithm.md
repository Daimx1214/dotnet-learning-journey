# Johnson's Algorithm

Johnson's algorithm finds the shortest path between every pair of vertices in a sparse graph that may contain negative edge weights, as long as there is no negative-weight cycle. It is the standard choice when Dijkstra alone cannot run (negative edges) and Floyd-Warshall would be too slow on a sparse graph.

## The Idea

Dijkstra is fast but requires non-negative edge weights. Bellman-Ford handles negative edges but is slow when run from every vertex. Johnson's algorithm combines the two: run Bellman-Ford once to compute a potential for each vertex, use the potentials to reweight every edge to a non-negative value, then run Dijkstra once from each vertex on the reweighted graph.

## Step 1: Add a Super Source and Run Bellman-Ford

Add a new vertex `s` with a zero-weight edge to every other vertex. Run Bellman-Ford from `s` and let `h(v)` be the shortest distance from `s` to `v`. If Bellman-Ford reports a negative cycle, Johnson's algorithm stops: shortest paths are undefined.

The potentials `h(v)` have a key property: for every edge `(u, v)` with weight `w`, the triangle inequality gives `h(v) <= h(u) + w(u, v)`. Rearranged, `w(u, v) + h(u) - h(v) >= 0`.

## Step 2: Reweight the Edges

Define the new weight of every edge as:

```text
w'(u, v) = w(u, v) + h(u) - h(v)
```

By the property above, every reweighted edge is non-negative, so Dijkstra can now run. Reweighting preserves shortest paths: for any path `p` from `u` to `v`, the reweighted length telescopes to `w'(p) = w(p) + h(u) - h(v)`. The `h` terms are the same for every `u`-to-`v` path, so the shortest path in the reweighted graph is the shortest path in the original graph.

## Step 3: Run Dijkstra from Every Vertex

Run Dijkstra from each vertex on the reweighted graph to get distances `d'(u, v)`, then convert back to true distances:

```text
d(u, v) = d'(u, v) - h(u) + h(v)
```

## Worked Example

Four vertices with a negative edge and no negative cycle:

| Edge | Weight |
|------|--------|
| 0 -> 1 | 3 |
| 0 -> 2 | 8 |
| 1 -> 2 | -4 |
| 1 -> 3 | 2 |
| 2 -> 3 | 1 |
| 3 -> 0 | 2 |

Check for a negative cycle first. The cycle 0 -> 1 -> 2 -> 3 -> 0 has weight 3 - 4 + 1 + 2 = 2, and every other cycle is also non-negative, so shortest paths are well defined.

Bellman-Ford from the super source `s` (zero edges to all vertices) gives:

| Vertex | h(v) | How it is reached |
|--------|------|-------------------|
| 0 | -1 | s -> 3 -> 0 gives 0 + (-3) + 2 = -1 |
| 1 | 0 | direct edge s -> 1 |
| 2 | -4 | s -> 1 -> 2 gives 0 + 0 - 4 = -4 |
| 3 | -3 | s -> 1 -> 2 -> 3 gives 0 - 4 + 1 = -3 |

Note that `h(0)` is -1, not 0: once `h(3)` drops to -3, the edge 3 -> 0 relaxes `h(0)` to -3 + 2 = -1. Bellman-Ford must run to completion; stopping early gives wrong potentials.

Reweighted edges, `w' = w + h(u) - h(v)`:

| Edge | Calculation | w' |
|------|-------------|----|
| 0 -> 1 | 3 + (-1) - 0 | 2 |
| 0 -> 2 | 8 + (-1) - (-4) | 11 |
| 1 -> 2 | -4 + 0 - (-4) | 0 |
| 1 -> 3 | 2 + 0 - (-3) | 5 |
| 2 -> 3 | 1 + (-4) - (-3) | 0 |
| 3 -> 0 | 2 + (-3) - (-1) | 0 |

Every reweighted edge is non-negative, so Dijkstra works. As a spot check, Dijkstra from 0 on the reweighted graph gives `d'(0, 2) = 2` via 0 -> 1 -> 2 (2 + 0). Converting back: `d(0, 2) = 2 - h(0) + h(2) = 2 - (-1) + (-4) = -1`, which matches the direct path 0 -> 1 -> 2 with weight 3 - 4 = -1.

Final all-pairs shortest distances (original weights):

| From / To | 0 | 1 | 2 | 3 |
|-----------|---|---|---|---|
| 0 | 0 | 3 | -1 | 0 |
| 1 | -1 | 0 | -4 | -3 |
| 2 | 3 | 6 | 0 | 1 |
| 3 | 2 | 5 | 1 | 0 |

Spot checks: `d(1, 0) = -1` via 1 -> 2 -> 3 -> 0 (-4 + 1 + 2). `d(2, 1) = 6` via 2 -> 3 -> 0 -> 1 (1 + 2 + 3). `d(3, 2) = 1` via 3 -> 0 -> 1 -> 2 (2 + 3 - 4).

## Pseudocode

```text
Johnson(V, edges):
    add super source s with 0-weight edges to all vertices
    h = BellmanFord(s)
    if h reports a negative cycle: return error
    for each edge (u, v, w):
        w'(u, v) = w + h[u] - h[v]
    for each vertex u:
        d'[u] = Dijkstra(u) on the reweighted graph
        for each vertex v:
            d[u][v] = d'[u][v] - h[u] + h[v]
    return d
```

## Complexity

Bellman-Ford costs `O(VE)`. Dijkstra from each vertex with a binary heap costs `O(V^2 log V + VE)`, so the total is `O(V^2 log V + VE)`. On sparse graphs this beats Floyd-Warshall's `O(V^3)`. On dense graphs Floyd-Warshall is simpler and often faster.

## When to Use It

- All-pairs shortest paths on a sparse graph with negative edges but no negative cycle.
- Road networks or currency graphs where a few edges are negative.
- If all weights are non-negative, skip the reweighting and just run Dijkstra from each vertex.
- If the graph is dense, prefer Floyd-Warshall.

## Key Takeaways

- Johnson's algorithm is Bellman-Ford once for potentials, then Dijkstra from every vertex.
- Reweighting with `w'(u, v) = w(u, v) + h(u) - h(v)` makes every edge non-negative while keeping the same shortest paths.
- The potentials come from a super source with zero edges to all vertices, and Bellman-Ford must run to completion.
- Convert reweighted distances back with `d(u, v) = d'(u, v) - h(u) + h(v)`.
- Total time is `O(V^2 log V + VE)`, which beats Floyd-Warshall on sparse graphs.
