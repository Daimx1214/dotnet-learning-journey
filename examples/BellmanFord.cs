// Bellman-Ford shortest paths — works with negative edge weights.
// Unlike Dijkstra, Bellman-Ford relaxes every edge V-1 times, so it can
// handle negative weights and can detect a negative-weight cycle that is
// reachable from the source. Time complexity is O(V * E).

using System;

public static class BellmanFord
{
    // A large "infinity" that is safe to add a weight to without overflowing.
    // int.MaxValue itself would overflow when we compute dist[u] + weight,
    // so we use half of it.
    private const int INF = int.MaxValue / 2;

    // Runs Bellman-Ford from `source` on a graph with `vertexCount` vertices
    // (numbered 0..vertexCount-1) and the given edges.
    // Returns the shortest distances from the source, or null when a
    // negative-weight cycle reachable from the source exists.
    public static int[]? ShortestPaths(
        int vertexCount,
        (int From, int To, int Weight)[] edges,
        int source)
    {
        int[] dist = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            dist[i] = INF;
        }
        dist[source] = 0;

        // Relax every edge V-1 times. After k passes, dist holds the
        // shortest paths that use at most k edges, so V-1 passes are enough
        // for any shortest path (which never needs more than V-1 edges).
        for (int pass = 0; pass < vertexCount - 1; pass++)
        {
            bool changed = false;
            foreach (var (from, to, weight) in edges)
            {
                // Skip unreachable vertices: INF + weight is meaningless,
                // and the guard below would never be true for them anyway.
                if (dist[from] == INF)
                {
                    continue;
                }

                if (dist[from] + weight < dist[to])
                {
                    dist[to] = dist[from] + weight;
                    changed = true;
                }
            }

            // Early exit: if a whole pass changed nothing, the distances
            // are already final.
            if (!changed)
            {
                break;
            }
        }

        // One more pass: if any edge can still be relaxed, a negative-weight
        // cycle is reachable from the source (the cycle keeps lowering the
        // distance forever, so no shortest path exists).
        foreach (var (from, to, weight) in edges)
        {
            if (dist[from] != INF && dist[from] + weight < dist[to])
            {
                return null;
            }
        }

        return dist;
    }

    public static void Main()
    {
        // Demo 1: the classic CLRS example graph (5 vertices).
        // Expected shortest distances from source 0: [0, 2, 4, 7, -2].
        // Note the negative edges (1 -> 4 is -4, 2 -> 1 is -2, 3 -> 2 is -3),
        // which Dijkstra would get wrong — Bellman-Ford handles them.
        var clrsEdges = new (int From, int To, int Weight)[]
        {
            (0, 1, 6),
            (0, 3, 7),
            (1, 2, 5),
            (1, 3, 8),
            (1, 4, -4),
            (2, 1, -2),
            (3, 2, -3),
            (3, 4, 9),
            (4, 0, 2),
            (4, 2, 7),
        };

        Console.WriteLine("CLRS graph (5 vertices), source = 0:");
        int[]? clrs = ShortestPaths(5, clrsEdges, 0);
        if (clrs is null)
        {
            Console.WriteLine("Negative-weight cycle reachable from source.");
        }
        else
        {
            for (int v = 0; v < clrs.Length; v++)
            {
                Console.WriteLine($"  dist[{v}] = {clrs[v]}");
            }
            Console.WriteLine("No negative-weight cycle reachable from source.");
        }

        // Demo 2: a tiny graph that DOES contain a negative cycle.
        // 0 -> 1 (1), 1 -> 2 (-1), 2 -> 0 (-1): the cycle 0 -> 1 -> 2 -> 0
        // has total weight 1 + (-1) + (-1) = -1, so going around it again
        // and again keeps making distances smaller forever.
        var cycleEdges = new (int From, int To, int Weight)[]
        {
            (0, 1, 1),
            (1, 2, -1),
            (2, 0, -1),
        };

        Console.WriteLine();
        Console.WriteLine("Negative-cycle graph (3 vertices), source = 0:");
        int[]? cycle = ShortestPaths(3, cycleEdges, 0);
        if (cycle is null)
        {
            Console.WriteLine("Negative-weight cycle reachable from source.");
        }
        else
        {
            for (int v = 0; v < cycle.Length; v++)
            {
                Console.WriteLine($"  dist[{v}] = {cycle[v]}");
            }
            Console.WriteLine("No negative-weight cycle reachable from source.");
        }
    }
}
