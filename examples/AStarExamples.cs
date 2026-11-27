// A* pathfinding — complete runnable console program.
//
// Demo (a): open 3x3 grid, (0,0) -> (2,2), 4-directional moves,
//           Manhattan heuristic -> shortest path is 4 moves.
// Demo (b): 5x5 grid with a diagonal wall at (1,1), (2,2), (3,3),
//           (0,0) -> (4,4) -> shortest path is 8 moves.
// Demo (c): goal fully enclosed by obstacles -> no path exists.

using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// A* implementation
// ---------------------------------------------------------------------------

// Returns the shortest path (list of cells, start first, goal last),
// or null when the goal cannot be reached.
static List<(int Row, int Col)>? FindPath(
    int rows,
    int cols,
    (int Row, int Col) start,
    (int Row, int Col) goal,
    HashSet<(int Row, int Col)> obstacles)
{
    // Manhattan distance — admissible for 4-directional movement,
    // because every move changes the distance by exactly 1.
    static int Heuristic((int Row, int Col) cell, (int Row, int Col) goal) =>
        Math.Abs(cell.Row - goal.Row) + Math.Abs(cell.Col - goal.Col);

    // The open set, ordered by f = g + h. The priority is the f-score.
    var openSet = new PriorityQueue<(int Row, int Col), int>();
    openSet.Enqueue(start, Heuristic(start, goal));

    var cameFrom = new Dictionary<(int Row, int Col), (int Row, int Col)>();
    var gScore = new Dictionary<(int Row, int Col), int> { [start] = 0 };
    var closed = new HashSet<(int Row, int Col)>();

    // 4-directional moves: up, down, left, right.
    (int dRow, int dCol)[] directions = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    while (openSet.Count > 0)
    {
        (int Row, int Col) current = openSet.Dequeue();

        if (current == goal)
        {
            // Reconstruct the path by walking cameFrom backwards.
            var path = new List<(int Row, int Col)> { current };
            while (cameFrom.TryGetValue(path[^1], out var previous))
            {
                path.Add(previous);
            }
            path.Reverse();
            return path;
        }

        if (!closed.Add(current))
        {
            continue; // Already expanded with an equal or better g-score.
        }

        foreach ((int dRow, int dCol) in directions)
        {
            (int Row, int Col) neighbour = (current.Row + dRow, current.Col + dCol);

            if (neighbour.Row < 0 || neighbour.Row >= rows ||
                neighbour.Col < 0 || neighbour.Col >= cols)
            {
                continue; // Outside the grid.
            }

            if (obstacles.Contains(neighbour))
            {
                continue; // Blocked cell.
            }

            int tentativeG = gScore[current] + 1; // Every move costs 1.
            if (!gScore.TryGetValue(neighbour, out int existingG) || tentativeG < existingG)
            {
                cameFrom[neighbour] = current;
                gScore[neighbour] = tentativeG;
                int fScore = tentativeG + Heuristic(neighbour, goal);
                openSet.Enqueue(neighbour, fScore);
            }
        }
    }

    return null; // Open set exhausted: the goal is unreachable.
}

static void PrintResult(string title, List<(int Row, int Col)>? path)
{
    Console.WriteLine(title);
    if (path is null)
    {
        Console.WriteLine("No path");
        return;
    }

    Console.WriteLine($"Path length: {path.Count - 1} moves");
    Console.WriteLine("Path: " + string.Join(" -> ", path));
    Console.WriteLine();
}

// ---------------------------------------------------------------------------
// Demo (a): open 3x3 grid, (0,0) -> (2,2)
// Manhattan distance = |2 - 0| + |2 - 0| = 4, so the shortest path is 4 moves.
// ---------------------------------------------------------------------------
var pathA = FindPath(
    rows: 3,
    cols: 3,
    start: (0, 0),
    goal: (2, 2),
    obstacles: new HashSet<(int Row, int Col)>());
PrintResult("Demo (a): open 3x3 grid, (0,0) -> (2,2)", pathA);

// ---------------------------------------------------------------------------
// Demo (b): 5x5 grid, diagonal wall at (1,1), (2,2), (3,3)
// (0,0) -> (4,4). Manhattan distance = 4 + 4 = 8, and the wall blocks the
// direct diagonal, but the top row / right column route avoids it:
// (0,0)->(0,1)->(0,2)->(0,3)->(0,4)->(1,4)->(2,4)->(3,4)->(4,4) = 8 moves.
// ---------------------------------------------------------------------------
var diagonalWall = new HashSet<(int Row, int Col)> { (1, 1), (2, 2), (3, 3) };
var pathB = FindPath(
    rows: 5,
    cols: 5,
    start: (0, 0),
    goal: (4, 4),
    obstacles: diagonalWall);
PrintResult("Demo (b): 5x5 grid, diagonal wall, (0,0) -> (4,4)", pathB);

// ---------------------------------------------------------------------------
// Demo (c): goal fully enclosed — (2,2) surrounded by walls at (1,2), (2,1).
// No 4-directional route can reach the goal.
// ---------------------------------------------------------------------------
var enclosed = new HashSet<(int Row, int Col)> { (1, 2), (2, 1) };
var pathC = FindPath(
    rows: 3,
    cols: 3,
    start: (0, 0),
    goal: (2, 2),
    obstacles: enclosed);
PrintResult("Demo (c): 3x3 grid, goal enclosed, (0,0) -> (2,2)", pathC);
