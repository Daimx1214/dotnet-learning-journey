# A* Search (DSA)

A* (pronounced "A-star") is a best-first search algorithm that finds the shortest path between two nodes in a weighted graph. It is Dijkstra's algorithm with a sense of direction: where Dijkstra explores outward in all directions equally, A* uses a *heuristic* — an estimate of the remaining distance to the goal — to prefer nodes that look promising. That is why A* powers pathfinding in games, robot navigation, and GPS routing.

## The Core Formula: f(n) = g(n) + h(n)

For every node `n`, A* tracks two numbers:

- **g(n)** — the exact cost of the cheapest path found so far from the start to `n`.
- **h(n)** — the heuristic: an estimate of the cost from `n` to the goal.
- **f(n) = g(n) + h(n)** — the estimated total cost of the best path through `n`.

The algorithm always expands the node with the lowest `f(n)`, using a priority queue. On a grid, a common heuristic is Manhattan distance (`|dx| + |dy|`) for 4-directional movement, or Euclidean distance when diagonal movement is allowed.

## Admissibility: When A* Is Guaranteed Optimal

A heuristic is **admissible** if it never overestimates the true remaining cost. Manhattan distance on a grid with no diagonal movement is admissible, because the shortest possible path can never be shorter than the Manhattan distance. If the heuristic is admissible, A* is guaranteed to return the optimal path — the first time the goal is removed from the priority queue, its `g` value is the true shortest distance.

A heuristic is **consistent** (or monotone) if the estimated cost never drops by more than the cost of moving to a neighbour: `h(n) <= cost(n, m) + h(m)` for every edge. Consistency implies admissibility and guarantees that a node, once expanded, never needs to be revisited. Manhattan and Euclidean distance are both consistent on grids.

Overestimating heuristics break the guarantee. If `h(n)` overestimates, A* may settle for a suboptimal path — but it will usually find *a* path faster, which is a deliberate trade-off in some game AI (weighted A*, where `f = g + w * h` with `w > 1`).

## Implementation on a Grid

```csharp
public static List<(int Row, int Col)>? AStar(
    bool[,] walkable, (int Row, int Col) start, (int Row, int Col) goal)
{
    int rows = walkable.GetLength(0), cols = walkable.GetLength(1);

    // Manhattan distance is admissible for 4-directional movement.
    static int Heuristic((int Row, int Col) a, (int Row, int Col) b) =>
        Math.Abs(a.Row - b.Row) + Math.Abs(a.Col - b.Col);

    var open = new PriorityQueue<(int Row, int Col), int>();
    var gScore = new Dictionary<(int, int), int> { [start] = 0 };
    var cameFrom = new Dictionary<(int, int), (int, int)>();

    open.Enqueue(start, Heuristic(start, goal));

    while (open.Count > 0)
    {
        var current = open.Dequeue();
        if (current == goal) return ReconstructPath(cameFrom, current);

        foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var neighbour = (current.Row + dr, current.Col + dc);
            if (neighbour.Row < 0 || neighbour.Row >= rows ||
                neighbour.Col < 0 || neighbour.Col >= cols ||
                !walkable[neighbour.Row, neighbour.Col])
                continue;

            int tentativeG = gScore[current] + 1; // Uniform cost of 1 per step.
            if (tentativeG < gScore.GetValueOrDefault(neighbour, int.MaxValue))
            {
                cameFrom[neighbour] = current;
                gScore[neighbour] = tentativeG;
                open.Enqueue(neighbour, tentativeG + Heuristic(neighbour, goal));
            }
        }
    }
    return null; // No path exists.
}

private static List<(int Row, int Col)> ReconstructPath(
    Dictionary<(int, int), (int, int)> cameFrom, (int Row, int Col) current)
{
    var path = new List<(int, int)> { current };
    while (cameFrom.TryGetValue(current, out var previous))
    {
        current = previous;
        path.Add(current);
    }
    path.Reverse();
    return path;
}
```

## A* vs Dijkstra

| Aspect | Dijkstra | A* |
|--------|----------|-----|
| Priority | Lowest `g(n)` (cost so far) | Lowest `f(n) = g(n) + h(n)` |
| Goal knowledge | None — explores in all directions | Heuristic steers the search toward the goal |
| Optimality | Always optimal | Optimal if the heuristic is admissible |
| Nodes expanded | Many — often the whole graph | Fewer, when the heuristic is informative |
| When to use | No goal, or all-pairs distances | Single start and goal with a good distance estimate |

Dijkstra is A* with `h(n) = 0`. A good heuristic prunes the search dramatically: on an open grid, A* heads almost straight for the goal while Dijkstra expands a growing circle. The worst case is the same for both — O(E log V) with a binary heap — because a useless heuristic (one that returns 0 everywhere) degrades A* to Dijkstra.

## Key Takeaways

- A* expands the node with the lowest `f(n) = g(n) + h(n)`: exact cost so far plus an estimated cost to the goal.
- An admissible heuristic never overestimates the remaining cost; with one, A* is guaranteed to return the shortest path.
- Manhattan distance is the standard admissible heuristic for 4-directional grids; Euclidean distance fits diagonal movement.
- A* is Dijkstra plus a heuristic — it explores far fewer nodes when the heuristic is informative, and degrades to Dijkstra when it is not.
- Deliberately overestimating (weighted A*) trades optimality for speed, which is acceptable in some game pathfinding.
