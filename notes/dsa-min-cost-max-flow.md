# Minimum Cost Maximum Flow

Minimum cost maximum flow finds the largest possible flow through a network, and among all maximum flows, the cheapest one. It is the algorithm behind assignment problems, transportation planning, and any system where moving units has both a capacity and a price.

## Problem Setup

A flow network has a source `s`, a sink `t`, and directed edges with a capacity (how much flow the edge can carry) and a cost per unit of flow. The goal is to push as much flow as possible from `s` to `t`, minimizing the total cost, where total cost is the sum over edges of `flow x cost`.

The standard algorithm is the **successive shortest augmenting path** method:

1. Start with zero flow.
2. Find the cheapest path from `s` to `t` in the residual graph.
3. Push as much flow as the path's bottleneck capacity allows.
4. Repeat until no `s-t` path remains.

## The Residual Graph and Negative Reverse Costs

After pushing flow along an edge, the residual graph gains a reverse edge with the **negative** of the original cost. The reverse edge represents undoing flow: sending flow back along it refunds the cost. This is what lets the algorithm correct an earlier greedy choice. A later shortest path may use a reverse edge to reroute flow onto a cheaper route, which is why shortest-path searches here must handle negative edge costs.

With non-negative original costs, the first shortest path can be found with Dijkstra. After reverse edges with negative costs appear, use SPFA (Bellman-Ford style) or Dijkstra with potentials (Johnson's trick): maintain a potential per node so reduced costs stay non-negative, and re-run Dijkstra each round. Potentials keep the algorithm fast, O(F x E log V) for F units of flow in the worst case, while SPFA is simpler to write and fast enough for small graphs.

## Worked Example

Network with source 0 and sink 3:

| Edge | Capacity | Cost |
|---|---|---|
| 0 -> 1 | 3 | 1 |
| 0 -> 2 | 2 | 2 |
| 1 -> 3 | 2 | 2 |
| 2 -> 3 | 3 | 1 |
| 1 -> 2 | 1 | 1 |

**Round 1:** the cheapest source-to-sink path is 0 -> 1 -> 3, cost 1 + 2 = 3 per unit. Bottleneck is min(3, 2) = 2, so push 2 units. Cost so far: 2 x 3 = 6.

**Round 2:** the next cheapest path is 0 -> 2 -> 3, cost 2 + 1 = 3 per unit. Bottleneck is min(2, 3) = 2, so push 2 units. Cost so far: 6 + 2 x 3 = 12.

**Round 3:** remaining capacities are 0 -> 1 = 1, 1 -> 2 = 1, 2 -> 3 = 1. The only remaining path is 0 -> 1 -> 2 -> 3, cost 1 + 1 + 1 = 3 per unit. Bottleneck is 1, so push 1 unit. Cost so far: 12 + 1 x 3 = 15.

No source-to-sink path remains: edge 0 -> 1 is saturated (3/3), edge 0 -> 2 is saturated (2/2), and the sink edges are full (1 -> 3 at 2/2, 2 -> 3 at 3/3).

**Result: maximum flow 5, minimum cost 15.**

The flow is forced in this network: total capacity out of the source is 3 + 2 = 5, and total capacity into the sink is 2 + 3 = 5, so any maximum flow pushes 5 units. Every source-to-sink path costs exactly 3 per unit (0-1-3 = 3, 0-2-3 = 3, 0-1-2-3 = 3), so every maximum flow costs 5 x 3 = 15. The three augmentations above are one valid decomposition: 2 units on 0-1-3 (cost 6), 2 units on 0-2-3 (cost 6), 1 unit on 0-1-2-3 (cost 3).

## C# Sketch

A compact successive-shortest-path implementation using SPFA for the shortest path, so negative reverse costs are handled:

```csharp
public sealed class MinCostMaxFlow
{
    private sealed record Edge(int To, int Rev, int Cap, int Cost);

    private readonly List<Edge>[] _graph;

    public MinCostMaxFlow(int nodeCount)
    {
        _graph = Enumerable.Range(0, nodeCount)
            .Select(_ => new List<Edge>()).ToArray();
    }

    public void AddEdge(int from, int to, int cap, int cost)
    {
        _graph[from].Add(new Edge(to, _graph[to].Count, cap, cost));
        _graph[to].Add(new Edge(from, _graph[from].Count - 1, 0, -cost)); // reverse edge
    }

    // Returns (maxFlow, minCost). SPFA finds the cheapest augmenting path each round.
    public (int Flow, int Cost) Solve(int source, int sink)
    {
        int flow = 0, cost = 0;
        int n = _graph.Length;
        var dist = new int[n];
        var prevNode = new int[n];
        var prevEdge = new int[n];

        while (true)
        {
            Array.Fill(dist, int.MaxValue);
            dist[source] = 0;
            var inQueue = new bool[n];
            var queue = new Queue<int>();
            queue.Enqueue(source);
            inQueue[source] = true;

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                inQueue[v] = false;
                for (int i = 0; i < _graph[v].Count; i++)
                {
                    Edge e = _graph[v][i];
                    if (e.Cap > 0 && dist[v] != int.MaxValue
                        && dist[e.To] > dist[v] + e.Cost)
                    {
                        dist[e.To] = dist[v] + e.Cost;
                        prevNode[e.To] = v;
                        prevEdge[e.To] = i;
                        if (!inQueue[e.To])
                        {
                            queue.Enqueue(e.To);
                            inQueue[e.To] = true;
                        }
                    }
                }
            }

            if (dist[sink] == int.MaxValue)
                break; // no augmenting path left

            int add = int.MaxValue;
            for (int v = sink; v != source; v = prevNode[v])
                add = Math.Min(add, _graph[prevNode[v]][prevEdge[v]].Cap);

            for (int v = sink; v != source; v = prevNode[v])
            {
                Edge e = _graph[prevNode[v]][prevEdge[v]];
                _graph[prevNode[v]][prevEdge[v]] =
                    e with { Cap = e.Cap - add };
                Edge rev = _graph[v][e.Rev];
                _graph[v][e.Rev] = rev with { Cap = rev.Cap + add };
            }

            flow += add;
            cost += add * dist[sink];
        }

        return (flow, cost);
    }
}
```

```csharp
var mcmf = new MinCostMaxFlow(4);
mcmf.AddEdge(0, 1, 3, 1);
mcmf.AddEdge(0, 2, 2, 2);
mcmf.AddEdge(1, 3, 2, 2);
mcmf.AddEdge(2, 3, 3, 1);
mcmf.AddEdge(1, 2, 1, 1);

var (flow, cost) = mcmf.Solve(0, 3);
Console.WriteLine($"flow={flow}, cost={cost}"); // flow=5, cost=15
```

Note that this sketch uses `record ... with` to update edge capacities, which creates a new record per update. For large graphs, use a mutable edge class instead; the record form is clearer for learning.

## Key Takeaways

- Minimum cost maximum flow maximizes flow first, then minimizes cost among maximum flows.
- Successive shortest augmenting path repeats: find the cheapest residual path, push its bottleneck, repeat.
- Reverse edges carry negative costs so later paths can undo earlier routing decisions.
- Use SPFA for simplicity or Dijkstra with potentials for speed on larger graphs.
- On the worked network (source 0, sink 3), the maximum flow is 5 and the minimum cost is 15.
