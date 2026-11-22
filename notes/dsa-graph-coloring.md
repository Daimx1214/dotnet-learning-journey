# Graph Coloring

Graph coloring assigns a color to each vertex so that no edge connects two
vertices of the same color. The **chromatic number** χ(G) is the smallest
number of colors that makes this possible. Coloring shows up everywhere:
scheduling exams so no student has two at once, assigning radio frequencies,
and allocating CPU registers to variables.

## Chromatic Number

Two bounds frame the answer before any algorithm runs:

- **Lower bound:** a clique of size `k` (every pair connected) needs `k`
  colors, so χ(G) ≥ size of the largest clique.
- **Upper bound:** Δ + 1 colors always suffice, where Δ is the maximum degree
  (greedy argument: a vertex has at most Δ neighbors, so one of Δ + 1 colors
  is always free).

A bipartite graph has χ = 2. An odd cycle needs 3. Finding χ exactly is
NP-hard in general, so in practice we use heuristics and settle for a good
coloring, not necessarily the optimal one.

## Greedy Coloring and Welsh–Powell

Greedy coloring processes vertices in some order and gives each vertex the
smallest color none of its already-colored neighbors uses. The order matters:
coloring high-degree vertices first (the **Welsh–Powell** heuristic) usually
uses fewer colors.

```csharp
// vertices sorted by descending degree, adjacency as sets
Dictionary<int, int> GreedyColor(Dictionary<int, HashSet<int>> graph)
{
    var order = graph.Keys.OrderByDescending(v => graph[v].Count);
    var color = new Dictionary<int, int>();

    foreach (int v in order)
    {
        var used = new HashSet<int>();
        foreach (int neighbor in graph[v])
            if (color.TryGetValue(neighbor, out int c))
                used.Add(c);

        int c = 1;
        while (used.Contains(c)) c++;
        color[v] = c;
    }
    return color;
}
```

### Worked Example

Graph with vertices A–E and edges A–B, A–C, B–C, C–D, D–E, C–E.
Degrees: C = 4, A = B = D = E = 2. Welsh–Powell order: C, A, B, D, E.

| Vertex | Neighbors already colored | Color |
|--------|---------------------------|-------|
| C | — | 1 |
| A | C = 1 | 2 |
| B | A = 2, C = 1 | 3 |
| D | C = 1 | 2 |
| E | C = 1, D = 2 | 3 |

Check every edge: A–B (2≠3), A–C (2≠1), B–C (3≠1), C–D (1≠2),
D–E (2≠3), C–E (1≠3). All valid, using 3 colors. Since A, B, C form a
triangle (a 3-clique), 3 is a lower bound too — so χ = 3 and this greedy
coloring is optimal.

## Bipartite Check (2-Coloring)

A graph is 2-colorable exactly when it is bipartite — equivalently, when it
contains no odd cycle. BFS does the check in O(V + E): color the start vertex
1, its neighbors 2, their neighbors 1, and so on. If you ever reach an
already-colored vertex with the same color you are trying to assign, the
graph is not bipartite.

```csharp
bool IsBipartite(Dictionary<int, List<int>> graph)
{
    var color = new Dictionary<int, int>();
    foreach (int start in graph.Keys)
    {
        if (color.ContainsKey(start)) continue;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        color[start] = 1;
        while (queue.Count > 0)
        {
            int v = queue.Dequeue();
            foreach (int n in graph[v])
            {
                if (!color.TryGetValue(n, out int c))
                {
                    color[n] = 3 - color[v]; // flip 1 <-> 2
                    queue.Enqueue(n);
                }
                else if (c == color[v])
                {
                    return false; // odd cycle found
                }
            }
        }
    }
    return true;
}
```

An even cycle like a square (C4) passes; an odd cycle like a pentagon (C5)
fails — try it by hand and the conflict appears on the last edge.

## Why Exact Coloring Is Hard

Deciding whether a graph is 3-colorable is NP-complete, so no known
polynomial algorithm finds the chromatic number for every graph. Real
solvers use backtracking with pruning, DSATUR (pick the most constrained
vertex next), or SAT/ILP encodings. For scheduling and register allocation,
a greedy coloring that uses one or two extra colors is usually fine — the
cost of an extra color is small compared to the cost of an exact search.

## Applications

- **Exam timetabling:** vertices are exams, edges join exams sharing a
  student, colors are time slots.
- **Register allocation:** vertices are variables, edges join variables live
  at the same time, colors are CPU registers; spilling happens when colors
  run out.
- **Frequency assignment:** vertices are transmitters, edges join ones close
  enough to interfere, colors are frequencies.
- **Sudoku:** cells are vertices, edges join cells in the same row, column or
  box, colors are digits 1–9.

## Key Takeaways

- The chromatic number χ(G) is the minimum colors for a proper coloring;
  the largest clique lower-bounds it and Δ + 1 upper-bounds it.
- Greedy coloring with Welsh–Powell ordering (highest degree first) is fast
  and often near-optimal; the example graph colors optimally in 3 colors.
- A graph is 2-colorable exactly when it is bipartite, which BFS checks in
  O(V + E) by detecting odd cycles.
- Exact coloring is NP-hard, so practical work uses greedy, DSATUR or
  SAT/ILP solvers.
