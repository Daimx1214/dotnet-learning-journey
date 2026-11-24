// Kruskal's algorithm with Union-Find.
// Runnable console program: dotnet run
// Expected output: MST edges (1-2, 1), (1-3, 2), (3-4, 2), (0-2, 3), total weight 8.

using System;
using System.Collections.Generic;

// Union-Find (disjoint-set union) with path compression and union by rank.
public sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public UnionFind(int size)
    {
        _parent = new int[size];
        _rank = new int[size];
        for (int i = 0; i < size; i++)
        {
            _parent[i] = i;
            _rank[i] = 0;
        }
    }

    public int Find(int x)
    {
        if (_parent[x] != x)
        {
            _parent[x] = Find(_parent[x]);
        }
        return _parent[x];
    }

    // Returns true if a merge happened, false if already in the same set.
    public bool Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA == rootB)
        {
            return false;
        }

        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootB] = rootA;
            _rank[rootA]++;
        }
        return true;
    }
}

public static class KruskalExamples
{
    public static void Main()
    {
        // Graph: 5 vertices (0-4), 7 edges.
        var edges = new List<(int U, int V, int W)>
        {
            (0, 1, 4),
            (0, 2, 3),
            (1, 2, 1),
            (1, 3, 2),
            (2, 3, 4),
            (3, 4, 2),
            (2, 4, 5)
        };

        var mst = Kruskal(5, edges, out int totalWeight);

        Console.WriteLine("Minimum Spanning Tree edges:");
        foreach (var (u, v, w) in mst)
        {
            Console.WriteLine($"  {u}-{v} (weight {w})");
        }
        Console.WriteLine($"Total MST weight: {totalWeight}");

        // Expected: edges (1-2, 1), (1-3, 2), (3-4, 2), (0-2, 3); total 8.
        bool correct = totalWeight == 8 && mst.Count == 4;
        Console.WriteLine(correct
            ? "Check passed: MST total is 8 as expected."
            : "Check FAILED: unexpected MST result.");

        // Demo: an edge that forms a cycle is rejected.
        // After the MST is built, all vertices are in one set,
        // so Union(0, 1) must return false — edge 0-1 (weight 4)
        // would close the cycle 0-2-1-0.
        var uf = new UnionFind(5);
        foreach (var (u, v, _) in mst)
        {
            uf.Union(u, v);
        }
        bool merged = uf.Union(0, 1);
        Console.WriteLine($"Trying rejected edge 0-1 (weight 4): " +
            (merged ? "accepted (unexpected)" : "rejected - would form a cycle"));
    }

    // Returns the MST edges and outputs the total weight via out parameter.
    public static List<(int U, int V, int W)> Kruskal(
        int vertexCount, List<(int U, int V, int W)> edges, out int totalWeight)
    {
        // Sort edges by weight, lightest first.
        edges.Sort((a, b) => a.W.CompareTo(b.W));

        var uf = new UnionFind(vertexCount);
        var mst = new List<(int U, int V, int W)>();
        totalWeight = 0;

        foreach (var (u, v, w) in edges)
        {
            if (uf.Union(u, v))
            {
                mst.Add((u, v, w));
                totalWeight += w;
                if (mst.Count == vertexCount - 1)
                {
                    break;
                }
            }
        }

        return mst;
    }
}
