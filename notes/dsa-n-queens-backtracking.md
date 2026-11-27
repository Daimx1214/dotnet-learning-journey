# N-Queens Backtracking

Place N queens on an N x N chessboard so no two queens attack each other — no shared row, column, or diagonal. It is the canonical backtracking problem: try a placement, recurse, and undo it when it leads nowhere.

## The backtracking idea

Place queens row by row. In each row, try every column that is not already attacked. Three sets track the attacks in O(1) time:

- `columns` — columns already occupied.
- `diag1` — the "down-right" diagonals, identified by `row - col` (constant along a diagonal).
- `diag2` — the "down-left" diagonals, identified by `row + col`.

When a column is safe in all three sets, place the queen, recurse to the next row, then remove it (backtrack) and try the next column.

## Worked example: 4-queens

The 4-queens problem has exactly **2** solutions. Here is one of them, with rows 0–3 top to bottom and columns 0–3 left to right:

```text
. Q . .
. . . Q
Q . . .
. . Q .
```

As a row → column array: **`[1, 3, 0, 2]`** — row 0 has its queen in column 1, row 1 in column 3, row 2 in column 0, row 3 in column 2.

Verify no two queens share a diagonal. Two queens at (r1, c1) and (r2, c2) share a diagonal exactly when `|r1 - r2| == |c1 - c2|`. Check all six pairs:

| Pair | \|Δrow\| | \|Δcol\| | Diagonal? |
|------|--------:|--------:|-----------|
| (0,1) – (1,3) | 1 | 2 | No |
| (0,1) – (2,0) | 2 | 1 | No |
| (0,1) – (3,2) | 3 | 1 | No |
| (1,3) – (2,0) | 1 | 3 | No |
| (1,3) – (3,2) | 2 | 1 | No |
| (2,0) – (3,2) | 1 | 2 | No |

Columns are all distinct (1, 3, 0, 2) and no pair shares a diagonal, so the placement is valid. The only other solution is its mirror image, `[2, 0, 3, 1]`.

## How many solutions are there?

| N | Solutions |
|---|----------:|
| 1 | 1 |
| 2 | 0 |
| 3 | 0 |
| 4 | 2 |
| 5 | 10 |
| 6 | 4 |
| 7 | 40 |
| 8 | **92** |

The famous number is **8-queens = 92 solutions** (12 if you count rotations and reflections as the same board).

## C# solver

```csharp
public static IList<IList<string>> SolveNQueens(int n)
{
    var results = new List<IList<string>>();
    var queens = new int[n];                 // queens[row] = column
    var columns = new HashSet<int>();
    var diag1 = new HashSet<int>();          // row - col
    var diag2 = new HashSet<int>();          // row + col

    Backtrack(0);
    return results;

    void Backtrack(int row)
    {
        if (row == n)
        {
            results.Add(BuildBoard());
            return;
        }
        for (int col = 0; col < n; col++)
        {
            if (columns.Contains(col) ||
                diag1.Contains(row - col) ||
                diag2.Contains(row + col))
                continue;                    // under attack — skip

            queens[row] = col;
            columns.Add(col);
            diag1.Add(row - col);
            diag2.Add(row + col);

            Backtrack(row + 1);              // recurse to the next row

            columns.Remove(col);             // backtrack: undo the placement
            diag1.Remove(row - col);
            diag2.Remove(row + col);
        }
    }

    IList<string> BuildBoard()
    {
        var board = new List<string>();
        for (int r = 0; r < n; r++)
        {
            var line = new char[n];
            Array.Fill(line, '.');
            line[queens[r]] = 'Q';
            board.Add(new string(line));
        }
        return board;
    }
}
```

For n = 4 this returns exactly 2 boards, the first of which is the `[1, 3, 0, 2]` board worked out above.

## Complexity

The worst case is O(N!) — at row `r` there are at most `N - r` safe columns. In practice the diagonal sets prune most branches early, and the classic 8-queens search finishes in microseconds. Bitmasking the three sets into integers is the standard speed-up for large N.

## Key Takeaways

- Place queens row by row; three sets (columns, `row - col`, `row + col`) test safety in O(1).
- 4-queens has exactly 2 solutions; one is row → col `[1, 3, 0, 2]`, verified diagonal-free by hand.
- 8-queens has 92 solutions (12 up to symmetry).
- Backtracking is try, recurse, undo — the undo step is what makes the search exhaustive.
- The worst case is O(N!), but diagonal pruning keeps small boards fast.
