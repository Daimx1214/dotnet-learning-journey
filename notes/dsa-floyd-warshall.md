# Floyd–Warshall Algorithm (DSA)

The Floyd–Warshall algorithm finds the shortest path between *every* pair of vertices in a weighted graph, including graphs with negative edge weights — as long as there is no negative-weight cycle. It is a dynamic-programming algorithm: instead of running Dijkstra once per vertex, it builds up shortest paths by gradually allowing more and more vertices to be used as intermediate stops.

## The Recurrence

Let `dist[k][i][j]` be the shortest distance from `i` to `j` using only vertices `0..k` as intermediates. Either the best path does not use vertex `k`, or it goes from `i` to `k` and then from `k` to `j`:

```text
dist[k][i][j] = min(dist[k-1][i][j], dist[k-1][i][k] + dist[k-1][k][j])
```

The algorithm keeps only one two-dimensional table and updates it in place, which is safe because the `k`th iteration only reads values that already allow `k` as an intermediate. The loop order matters: `k` must be the **outermost** loop. Swapping the loops so `i` or `j` is outermost computes something else entirely, and is the most common implementation bug.

```csharp
public static int[,] FloydWarshall(int[,] graph, int vertexCount)
{
    const int INF = int.MaxValue / 2; // Halved so additions cannot overflow.
    var dist = new int[vertexCount, vertexCount];

    for (int i = 0; i < vertexCount; i++)
        for (int j = 0; j < vertexCount; j++)
            dist[i, j] = graph[i, j];

    for (int k = 0; k < vertexCount; k++)
        for (int i = 0; i < vertexCount; i++)
            for (int j = 0; j < vertexCount; j++)
                if (dist[i, k] + dist[k, j] < dist[i, j])
                    dist[i, j] = dist[i, k] + dist[k, j];

    return dist;
}
```

The running time is O(V³) and the space is O(V²). That cubic cost is the trade-off: for a sparse graph with few sources of interest, running Dijkstra from each source (O(V · E log V)) is usually faster. Floyd–Warshall wins on dense graphs, on tiny graphs where simplicity matters, and whenever *all* pairs are genuinely needed.

## Worked Example: A 4-Node Graph

Take this directed graph (missing edges are ∞):

```text
0 → 1 (5)     0 → 3 (10)
1 → 2 (3)
2 → 3 (1)
3 → 1 (2)
```

Initial distance matrix:

```text
       0    1    2    3
  0    0    5    ∞   10
  1    ∞    0    3    ∞
  2    ∞    ∞    0    1
  3    ∞    2    ∞    0
```

- **k = 0:** nothing improves, because no shortest path can use vertex 0 as an intermediate — nothing points into 0.
- **k = 1:** paths through 1 appear: 0 → 1 → 2 becomes 8, and 3 → 1 → 2 becomes 5.
- **k = 2:** paths through 2 appear: 0 → 1 → 2 → 3 drops from 10 to 9, and 1 → 2 → 3 becomes 4.
- **k = 3:** paths through 3 appear: 2 → 3 → 1 becomes 3.

Final distance matrix:

```text
       0    1    2    3
  0    0    5    8    9
  1    ∞    0    3    4
  2    ∞    3    0    1
  3    ∞    2    5    0
```

Check two entries by hand: 0 → 3 is 0 → 1 → 2 → 3 = 5 + 3 + 1 = 9, which beats the direct edge of 10; 2 → 1 is 2 → 3 → 1 = 1 + 2 = 3. Vertex 0 is unreachable from every other vertex, so column 0 stays ∞ below the diagonal.

## Negative-Cycle Detection

After the algorithm finishes, a negative cycle exists if and only if some `dist[i][i] < 0` — a vertex can reach itself with negative total cost. For example, add edges 0 → 1 (1), 1 → 2 (−2), 2 → 0 (−1) to a graph: the cycle 0 → 1 → 2 → 0 costs 1 − 2 − 1 = −2, so `dist[0][0]` ends up at −2. When a negative cycle is present, shortest paths are undefined (looping around the cycle keeps making the path cheaper), so always check the diagonal before trusting the table. Note that a single negative *edge* is fine — only a negative *cycle* breaks the algorithm.

## Transitive Closure and Other Uses

Setting every edge weight to 1 and treating "reachable" as the distance turns Floyd–Warshall into Warshall's algorithm for transitive closure: after the run, `dist[i][j] < ∞` means `j` is reachable from `i`. The same table also answers "what is the cheapest route between any two cities?" style questions in one shot, and it is the standard first step when a problem needs all-pairs distances as input — for example, in some vehicle-routing and network-design formulations. If you only need the distances (not the paths), the code above is complete; to reconstruct actual paths, keep a `next[i][j]` table that records the first step from `i` toward `j` and update it whenever a shorter route is found.

## Key Takeaways

- Floyd–Warshall computes all-pairs shortest paths with the recurrence `dist[k][i][j] = min(dist[k][i][j], dist[i][k] + dist[k][j])`, in O(V³) time and O(V²) space.
- The `k` loop must be outermost — it represents the set of vertices allowed as intermediates, and the order is load-bearing.
- It handles negative edge weights, unlike Dijkstra, but fails when a negative-weight cycle exists.
- A negative cycle is detected after the run: any `dist[i][i] < 0` proves one exists.
- Use it for dense graphs, small graphs, and all-pairs problems; prefer repeated Dijkstra on sparse graphs or when only a few sources matter.
- With unit weights it doubles as Warshall's transitive-closure algorithm for reachability.
