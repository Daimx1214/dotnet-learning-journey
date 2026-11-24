# Bipartite Graph Check

A graph is bipartite if its vertices can be split into two groups so that every edge connects a vertex from one group to a vertex from the other. Equivalently, the graph is 2-colorable: no edge joins two vertices of the same color.

## The Odd-Cycle Rule

A graph is bipartite if and only if it contains no odd-length cycle. Proof sketch: in a 2-coloring, colors must alternate along any cycle, so a cycle can only close consistently if it has an even number of edges. An odd cycle forces the first and last vertices to share a color, which contradicts the coloring. Conversely, if no odd cycle exists, a BFS 2-coloring never hits a conflict.

## Checking with BFS 2-Coloring

Color the start vertex 0, its neighbors 1, their neighbors 0, and so on. If an edge ever connects two vertices of the same color, the graph is not bipartite.

```csharp
public static bool IsBipartite(List<int>[] adj)
{
    int n = adj.Length;
    int[] color = new int[n];
    Array.Fill(color, -1);

    for (int start = 0; start < n; start++)
    {
        if (color[start] != -1) continue;

        var queue = new Queue<int>();
        queue.Enqueue(start);
        color[start] = 0;

        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            foreach (int to in adj[v])
            {
                if (color[to] == -1)
                {
                    color[to] = 1 - color[v];
                    queue.Enqueue(to);
                }
                else if (color[to] == color[v])
                {
                    return false;
                }
            }
        }
    }

    return true;
}
```

The outer loop matters: a disconnected graph has several components, and each one needs its own BFS start. A single BFS from vertex 0 would leave later components uncolored.

## Hand-Verified Examples

**Bipartite — a square (4-cycle).** Edges: 0-1, 1-2, 2-3, 3-0.

| Vertex | Color |
|--------|-------|
| 0 | 0 |
| 1 | 1 |
| 2 | 0 |
| 3 | 1 |

Every edge joins color 0 to color 1. The graph is bipartite. Partition: {0, 2} and {1, 3}.

**Not bipartite — a triangle (3-cycle).** Edges: 0-1, 1-2, 2-0.

Color 0 as 0, 1 as 1, then 2 must be 0 (neighbor of 1). But edge 2-0 joins two vertices of color 0 — conflict. The odd cycle of length 3 makes 2-coloring impossible.

**Not bipartite — a 5-cycle.** Edges: 0-1, 1-2, 2-3, 3-4, 4-0. Colors alternate 0, 1, 0, 1, 0 around the ring, and the closing edge 4-0 joins color 0 to color 0. Any odd cycle fails the same way.

**Bipartite — a tree.** Every tree is bipartite: a tree has no cycles at all, so no odd cycle exists. BFS 2-coloring always succeeds.

## Where Bipartiteness Shows Up

- Matching problems: workers to jobs, students to projects — the two sides are the bipartition.
- Checking whether a graph's vertices split into two conflict-free groups (e.g. two teams with no rivals on the same team).
- Hopcroft-Karp maximum matching only applies to bipartite graphs, so the check is the gatekeeper for that algorithm.

## Complexity

BFS visits every vertex and edge once: `O(V + E)` time, `O(V)` extra space for the color array and queue. A DFS coloring works identically; BFS is shown here because the level-by-level coloring mirrors the definition most directly.

## Key Takeaways

- A graph is bipartite exactly when it has no odd cycle — that is the theorem behind the algorithm.
- BFS 2-coloring detects a conflict the moment an edge joins two equal colors.
- Handle disconnected graphs: restart BFS from every uncolored vertex.
- Verified: a square (4-cycle) is bipartite with partitions {0, 2} and {1, 3}; a triangle (3-cycle) is not.
- Time `O(V + E)`, space `O(V)`.
