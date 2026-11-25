using System;
using System.Collections.Generic;

// Dijkstra's algorithm on a 5-node graph using PriorityQueue.
// Hand-traced before writing: expected distances from 0 are [0, 3, 1, 4, 7]
// and the shortest path to 4 is 0-2-1-3-4.
//
// Trace (smallest unvisited distance first):
//   Visit 0 (dist 0): relax 0->1 to 4, 0->2 to 1.
//   Visit 2 (dist 1): relax 2->1 to 1+2=3 (better than 4), 2->3 to 1+5=6.
//   Visit 1 (dist 3): relax 1->3 to 3+1=4 (better than 6), 1->4 to 3+7=10.
//   Visit 3 (dist 4): relax 3->4 to 4+3=7 (better than 10).
//   Visit 4 (dist 7): done. Distances [0, 3, 1, 4, 7]. Path to 4: 0-2-1-3-4.
var edges = new List<(int From, int To, int Weight)>
{
    (0, 1, 4),
    (0, 2, 1),
    (2, 1, 2),
    (1, 3, 1),
    (2, 3, 5),
    (3, 4, 3),
    (1, 4, 7)
};

const int nodeCount = 5;
var graph = new List<(int To, int Weight)>[nodeCount];
for (int i = 0; i < nodeCount; i++)
{
    graph[i] = new List<(int To, int Weight)>();
}
foreach (var (from, to, weight) in edges)
{
    graph[from].Add((to, weight));
}

int[] dist = Dijkstra(graph, 0, out int[] previous);

Console.WriteLine("Distances from 0: [" + string.Join(", ", dist) + "]");
// Expected: Distances from 0: [0, 3, 1, 4, 7]
Console.WriteLine("Path to 4: " + string.Join("-", ReconstructPath(previous, 4)));
// Expected: Path to 4: 0-2-1-3-4

static int[] Dijkstra(List<(int To, int Weight)>[] graph, int source, out int[] previous)
{
    int n = graph.Length;
    var dist = new int[n];
    previous = new int[n];
    var visited = new bool[n];
    Array.Fill(dist, int.MaxValue);
    Array.Fill(previous, -1);
    dist[source] = 0;

    var pq = new PriorityQueue<int, int>();
    pq.Enqueue(source, 0);

    while (pq.Count > 0)
    {
        int u = pq.Dequeue();
        if (visited[u])
        {
            continue;
        }
        visited[u] = true;

        foreach (var (v, w) in graph[u])
        {
            int newDist = dist[u] + w;
            if (newDist < dist[v])
            {
                dist[v] = newDist;
                previous[v] = u;
                pq.Enqueue(v, newDist);
            }
        }
    }

    return dist;
}

static List<int> ReconstructPath(int[] previous, int target)
{
    var path = new List<int>();
    for (int current = target; current != -1; current = previous[current])
    {
        path.Add(current);
    }
    path.Reverse();
    return path;
}
