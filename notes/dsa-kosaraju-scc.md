# Kosaraju's Algorithm: Strongly Connected Components

A strongly connected component (SCC) is a maximal set of vertices where every vertex can reach every other vertex. Kosaraju's algorithm finds all SCCs with two depth-first searches and a reversed graph.

## The Two-Pass Idea

1. **First DFS on the original graph.** Run DFS from every unvisited vertex and record vertices in order of *finishing* time (when DFS backtracks out of a vertex).
2. **Reverse every edge** to build the transpose graph.
3. **Second DFS on the reversed graph**, processing vertices in *decreasing* finishing order. Each DFS tree in this pass is exactly one SCC.

The intuition: the vertex that finishes last in the first pass belongs to a "source" SCC of the condensation graph — one with no incoming edges from other SCCs. In the reversed graph that SCC becomes a sink, so a DFS from it cannot escape into other components.

## Worked Example

Graph with edges: 0→1, 1→2, 2→0, 2→3, 3→4.

- First DFS (say starting at 0): visits 0, 1, 2, then 3, then 4. Finishing order: 4, 3, 2, 1, 0.
- Reversed edges: 1→0, 2→1, 0→2, 3→2, 4→3.
- Second pass in decreasing finish order (0, 1, 2, 3, 4): DFS from 0 on the reversed graph reaches 0, 2, 1 — that is one SCC. DFS from 3 reaches only 3. DFS from 4 reaches only 4.

**SCCs: {0, 1, 2}, {3}, {4}.** Vertices 0, 1, 2 form a cycle so each reaches the others; 3 and 4 are alone because the edges 2→3 and 3→4 only go one way.

## C# Implementation

```csharp
using System;
using System.Collections.Generic;

class Kosaraju
{
    static void Dfs(int v, List<int>[] graph, bool[] visited, Stack<int> finishOrder)
    {
        visited[v] = true;
        foreach (int next in graph[v])
            if (!visited[next]) Dfs(next, graph, visited, finishOrder);
        finishOrder.Push(v); // record when the vertex finishes
    }

    static void DfsCollect(int v, List<int>[] graph, bool[] visited, List<int> component)
    {
        visited[v] = true;
        component.Add(v);
        foreach (int next in graph[v])
            if (!visited[next]) DfsCollect(next, graph, visited, component);
    }

    static List<List<int>> FindSccs(int n, List<(int From, int To)> edges)
    {
        var graph = new List<int>[n];
        var reversed = new List<int>[n];
        for (int i = 0; i < n; i++) { graph[i] = new(); reversed[i] = new(); }
        foreach (var (from, to) in edges)
        {
            graph[from].Add(to);
            reversed[to].Add(from);
        }

        var visited = new bool[n];
        var finishOrder = new Stack<int>();
        for (int i = 0; i < n; i++)
            if (!visited[i]) Dfs(i, graph, visited, finishOrder);

        Array.Fill(visited, false);
        var sccs = new List<List<int>>();
        while (finishOrder.Count > 0)
        {
            int v = finishOrder.Pop();
            if (visited[v]) continue;
            var component = new List<int>();
            DfsCollect(v, reversed, visited, component);
            sccs.Add(component);
        }
        return sccs;
    }

    static void Main()
    {
        var edges = new List<(int, int)> { (0, 1), (1, 2), (2, 0), (2, 3), (3, 4) };
        var sccs = FindSccs(5, edges);
        foreach (var scc in sccs)
            Console.WriteLine("SCC: {" + string.Join(", ", scc) + "}");
        // Output: SCC: {0, 2, 1}, SCC: {3}, SCC: {4}
    }
}
```

## Key Takeaways

- An SCC is a maximal set of vertices that all reach each other.
- Kosaraju's algorithm runs two DFS passes: finishing order first, then DFS on the reversed graph.
- Process the second pass in decreasing finishing order; each DFS tree is one SCC.
- For edges 0→1, 1→2, 2→0, 2→3, 3→4, the SCCs are {0, 1, 2}, {3}, {4}.
- Time complexity is O(V + E) and space is O(V + E) for the reversed graph.
- Tarjan's algorithm finds SCCs in a single DFS pass, but Kosaraju's is easier to implement correctly.
