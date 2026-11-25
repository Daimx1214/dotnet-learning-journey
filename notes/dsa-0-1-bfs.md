# 0-1 BFS with a Deque

Breadth-first search finds shortest paths in an unweighted graph in O(V + E). Dijkstra handles weighted graphs in O((V + E) log V). 0-1 BFS covers the middle ground: a graph where every edge weight is 0 or 1. It runs in O(V + E) using a double-ended queue (deque) instead of a priority queue.

## The Idea

Dijkstra's priority queue exists to always expand the smallest distance first. When weights are only 0 or 1, distances can only grow by 0 or 1, so a deque is enough:

- Relaxing an edge of weight 0 cannot increase the distance, so the neighbour belongs at the **front** of the deque, to be processed before anything with a larger distance.
- Relaxing an edge of weight 1 increases the distance by 1, so the neighbour goes at the **back**, behind everything at the current distance.

This keeps the deque sorted by distance at all times, which is exactly the invariant a priority queue provides, but with O(1) push and pop instead of O(log V).

## Pseudocode

```text
ZeroOneBFS(graph, source):
    dist = [infinity] * V
    dist[source] = 0
    deque = [source]
    while deque is not empty:
        u = deque.pop_front()
        for (v, w) in graph[u]:        // w is 0 or 1
            if dist[u] + w < dist[v]:
                dist[v] = dist[u] + w
                if w == 0:
                    deque.push_front(v)
                else:
                    deque.push_back(v)
    return dist
```

Each vertex is relaxed at most a few times, and every edge is examined once per relaxation, so the total time is O(V + E) and the space is O(V).

## C# Sketch

```csharp
int[] ZeroOneBfs(List<(int To, int Weight)>[] graph, int source)
{
    int n = graph.Length;
    int[] dist = Enumerable.Repeat(int.MaxValue, n).ToArray();
    dist[source] = 0;

    var deque = new LinkedList<int>();
    deque.AddFirst(source);

    while (deque.Count > 0)
    {
        int u = deque.First!.Value;
        deque.RemoveFirst();

        foreach (var (v, w) in graph[u])
        {
            if (dist[u] + w < dist[v])
            {
                dist[v] = dist[u] + w;
                if (w == 0)
                    deque.AddFirst(v);  // no extra cost: process next
                else
                    deque.AddLast(v);   // costs 1: process after current level
            }
        }
    }
    return dist;
}
```

C# has no built-in deque in the standard library; `LinkedList<T>` with `AddFirst` and `AddLast` is the usual stand-in, or a `Deque<T>` from a library.

## Worked Example

Graph with nodes 0 to 3 and directed edges:

| Edge | Weight |
|------|--------|
| 0 -> 1 | 0 |
| 1 -> 2 | 1 |
| 0 -> 2 | 1 |
| 2 -> 3 | 0 |
| 1 -> 3 | 1 |

Find the shortest distances from node 0.

**Start:** dist = [0, inf, inf, inf], deque = [0].

**Pop 0 (dist 0):**
- Edge 0 -> 1, weight 0: dist[1] = 0 + 0 = 0. Weight 0, so push 1 to the front. Deque = [1].
- Edge 0 -> 2, weight 1: dist[2] = 0 + 1 = 1. Weight 1, so push 2 to the back. Deque = [1, 2].

**Pop 1 (dist 0):**
- Edge 1 -> 2, weight 1: candidate 0 + 1 = 1, not better than dist[2] = 1. Skip.
- Edge 1 -> 3, weight 1: dist[3] = 0 + 1 = 1. Weight 1, so push 3 to the back. Deque = [2, 3].

**Pop 2 (dist 1):**
- Edge 2 -> 3, weight 0: candidate 1 + 0 = 1, not better than dist[3] = 1. Skip.

**Pop 3 (dist 1):** no outgoing edges. Deque is empty, so we stop.

Final distances from 0: **[0, 0, 1, 1]**.

Notice that node 1 is reached at distance 0 through the free edge 0 -> 1, and node 3 is reached at distance 1 either via 1 -> 3 or via 2 -> 3. A plain BFS would have treated every edge as cost 1 and wrongly reported dist[1] = 1 and dist[3] = 2.

## When to Use It

- Grid problems where moving is free in some directions and costs 1 in others, such as turning a laser or crossing a wall.
- Graphs with two kinds of edges: free links and paid links.
- Anywhere Dijkstra would work but the weights are only 0 and 1 and the log factor matters.

For weights 0 to k with small k, the same idea generalizes to a Dial's algorithm bucket queue.

## Key Takeaways

- 0-1 BFS handles graphs with edge weights 0 or 1 in O(V + E), faster than Dijkstra's O((V + E) log V).
- Weight 0 edges push to the front of the deque, weight 1 edges push to the back, which keeps the deque sorted by distance.
- The worked example gives distances [0, 0, 1, 1] from node 0: node 1 is free via 0 -> 1, and node 3 costs 1.
- Plain BFS would give wrong answers here because it treats every edge as cost 1.
- C# has no built-in deque, so `LinkedList<T>` with `AddFirst` and `AddLast` is the usual stand-in.
