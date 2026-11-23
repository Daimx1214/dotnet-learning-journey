# Tarjan's Strongly Connected Components Algorithm

A strongly connected component (SCC) is a maximal set of vertices where every vertex can reach every other vertex in the set. Tarjan's algorithm finds all SCCs in a single depth-first search by tracking, for each vertex, its discovery time and the lowest discovery time reachable from it.

## Discovery Time and Low-Link

During DFS, each vertex `v` gets two numbers:

- `disc[v]`: the step at which `v` was first discovered (a running counter starting at 0).
- `low[v]`: the smallest discovery time reachable from `v` using zero or more tree edges followed by at most one back edge to a vertex still on the DFS stack.

`low[v]` starts equal to `disc[v]`. When DFS explores an edge `v -> w`:

- If `w` is unvisited, recurse into `w`, then `low[v] = min(low[v], low[w])`.
- If `w` is already visited and still on the stack, `low[v] = min(low[v], disc[w])`.
- If `w` is already visited and no longer on the stack, it belongs to a finished SCC — ignore the edge.

A vertex `v` is the root of an SCC exactly when `low[v] == disc[v]`. At that point, `v` and everything above it on the stack form one SCC and can be popped.

## The Stack Invariant

The stack holds exactly the vertices of the current DFS path plus the vertices of SCCs that are still open — that is, vertices that have been discovered but not yet assigned to a finished SCC. A vertex stays on the stack until its SCC root is found. This is why the back-edge rule only applies to vertices still on the stack: an edge to a vertex already popped points into a different, already-closed SCC and must not lower `low[v]`.

```csharp
void StrongConnect(int v, List<int>[] graph, int[] disc, int[] low,
                   bool[] onStack, Stack<int> stack, ref int time,
                   List<List<int>> sccs)
{
    disc[v] = low[v] = time++;
    stack.Push(v);
    onStack[v] = true;

    foreach (int w in graph[v])
    {
        if (disc[w] == -1)
        {
            StrongConnect(w, graph, disc, low, onStack, stack, ref time, sccs);
            low[v] = Math.Min(low[v], low[w]);
        }
        else if (onStack[w])
        {
            low[v] = Math.Min(low[v], disc[w]);
        }
    }

    if (low[v] == disc[v])
    {
        var scc = new List<int>();
        int w;
        do
        {
            w = stack.Pop();
            onStack[w] = false;
            scc.Add(w);
        } while (w != v);
        sccs.Add(scc);
    }
}
```

## Worked Example

Graph with 5 vertices and 6 edges:

- `0 -> 1`
- `1 -> 2`
- `2 -> 0`
- `2 -> 3`
- `3 -> 4`
- `4 -> 3`

Run DFS starting at vertex 0, taking neighbours in listed order:

| Vertex | disc | low | How low was set |
|--------|------|-----|-----------------|
| 0 | 0 | 0 | back edge `2 -> 0` propagates up: `low[2] = min(2, 0) = 0`, then `low[1] = min(1, 0) = 0`, then `low[0] = min(0, 0) = 0` |
| 1 | 1 | 0 | `low[1] = min(1, low[2]) = min(1, 0) = 0` |
| 2 | 2 | 0 | `low[2] = min(2, disc[0]) = 0` via the back edge `2 -> 0` |
| 3 | 3 | 3 | `low[4] = 3` propagates back, so `low[3] = min(3, 3) = 3` |
| 4 | 4 | 3 | `low[4] = min(4, disc[3]) = 3` via the back edge `4 -> 3` |

Trace the SCC extraction, in order:

1. Vertex 4 finishes with `low[4] = 3`, which is not equal to `disc[4] = 4`, so nothing pops yet.
2. Vertex 3 finishes with `low[3] = 3 == disc[3]`, so it is an SCC root. Pop `4` and `3` from the stack. First SCC: `{3, 4}`.
3. Vertex 2 finishes with `low[2] = 0`, not equal to `disc[2] = 2`; vertex 1 finishes with `low[1] = 0`, not equal to `disc[1] = 1`.
4. Vertex 0 finishes with `low[0] = 0 == disc[0]`, so it is an SCC root. Pop `2`, `1`, and `0`. Second SCC: `{0, 1, 2}`.

Verified result: the SCCs are `{0, 1, 2}` and `{3, 4}`. Check by reachability: `0 -> 1 -> 2 -> 0` is a cycle, so all three reach each other; `3 -> 4 -> 3` is a cycle, so both reach each other; there is an edge `2 -> 3` but no path back from `3` to `2`, so the two groups are separate SCCs. The vertex count checks out too: `3 + 2 = 5` vertices assigned, none left over.

## Complexity

Tarjan's algorithm visits each vertex once and each edge once, so it runs in `O(V + E)` time. It uses `O(V)` extra space for the discovery times, low-link values, the stack, and the recursion depth (which is `O(V)` in the worst case for a long chain).

## Key Takeaways

- `disc[v]` records when a vertex was found; `low[v]` records the earliest discovery time reachable from it through one back edge.
- A vertex is an SCC root exactly when `low[v] == disc[v]`; its SCC is itself plus everything above it on the stack.
- Only back edges to vertices still on the stack may lower `low[v]`; edges to finished SCCs are ignored.
- On the worked graph, the verified SCCs are `{0, 1, 2}` and `{3, 4}`.
- The whole algorithm is one DFS pass: `O(V + E)` time and `O(V)` space.
