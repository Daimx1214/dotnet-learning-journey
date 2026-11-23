# Levenshtein Edit Distance

The Levenshtein distance between two strings is the minimum number of single-character edits — insert, delete, or substitute — needed to turn one string into the other. It is the classic dynamic-programming string problem and the basis of spell checking, fuzzy search, and diff tools.

## DP Recurrence

Let `dp[i][j]` be the distance between the first `i` characters of `s` and the first `j` characters of `t`. The first row and column are the cost of building a string from nothing (all inserts or all deletes):

```text
dp[0][j] = j          // insert j characters
dp[i][0] = i          // delete i characters
dp[i][j] = min(
    dp[i - 1][j] + 1,                        // delete s[i-1]
    dp[i][j - 1] + 1,                        // insert t[j-1]
    dp[i - 1][j - 1] + cost)                 // substitute, or match
// cost = 0 if s[i-1] == t[j-1], else 1
```

The recurrence is correct because the last edit of an optimal sequence must be one of those three operations, and everything before it is optimal for the smaller prefix pair — the optimal-substructure property that makes the table fill work.

## Full Table: "kitten" to "sitting"

Filling the table row by row (`s = "kitten"` down the rows, `t = "sitting"` across the columns) gives:

```text
        ""  s   i   t   t   i   n   g
""       0   1   2   3   4   5   6   7
k        1   1   2   3   4   5   6   7
i        2   2   1   2   3   4   5   6
t        3   3   2   1   2   3   4   5
t        4   4   3   2   1   2   3   4
e        5   5   4   3   2   2   3   4
n        6   6   5   4   3   3   2   3
```

Spot-check three cells by hand from the recurrence. Cell (row `i`, column `s`): `s[1] = 'i'` vs `t[0] = 's'` differ, so `dp[2][1] = min(dp[1][1] + 1, dp[2][0] + 1, dp[1][0] + 1) = min(2, 3, 2) = 2`. Cell (row `t`, column `t`): the characters match, so `dp[3][3] = min(dp[2][3] + 1, dp[3][2] + 1, dp[2][2] + 0) = min(3, 3, 1) = 1`. Final cell (row `n`, column `g`): `dp[6][7] = min(dp[5][7] + 1, dp[6][6] + 1, dp[5][6] + 1) = min(5, 3, 4) = 3`.

The final cell is **3**, so the Levenshtein distance from "kitten" to "sitting" is **3**. One optimal alignment proves it:

```text
kitten
sitten    (substitute k -> s)
sittin    (substitute e -> i)
sitting   (insert g)
```

Three edits, and no two-edit sequence exists — the table's final cell is the proof, since it is the minimum over all edit sequences.

## C# Implementation

```csharp
public static int Levenshtein(string s, string t)
{
    int m = s.Length, n = t.Length;
    int[,] dp = new int[m + 1, n + 1];

    for (int i = 0; i <= m; i++) dp[i, 0] = i;
    for (int j = 0; j <= n; j++) dp[0, j] = j;

    for (int i = 1; i <= m; i++)
    for (int j = 1; j <= n; j++)
    {
        int cost = s[i - 1] == t[j - 1] ? 0 : 1;
        dp[i, j] = Math.Min(
            Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
            dp[i - 1, j - 1] + cost);
    }

    return dp[m, n];
}
```

Time complexity is O(m × n) and space is O(m × n) for the full table — 6 × 7 = 42 cells plus the borders, 7 × 8 = 56 cells in total for "kitten" and "sitting".

## Space-Optimized Two-Row Version

Each cell only needs the current row and the previous row, so two arrays of length `n + 1` are enough. The values produced are identical to the full table's rows, which can be checked cell by cell against the table above.

```csharp
public static int LevenshteinTwoRows(string s, string t)
{
    int[] previous = new int[t.Length + 1];
    int[] current = new int[t.Length + 1];

    for (int j = 0; j <= t.Length; j++) previous[j] = j;

    for (int i = 1; i <= s.Length; i++)
    {
        current[0] = i;
        for (int j = 1; j <= t.Length; j++)
        {
            int cost = s[i - 1] == t[j - 1] ? 0 : 1;
            current[j] = Math.Min(
                Math.Min(previous[j] + 1, current[j - 1] + 1),
                previous[j - 1] + cost);
        }
        (previous, current) = (current, previous); // swap rows
    }

    return previous[t.Length];
}
```

Tracing the last row: after processing `i = 6` (`'n'`), the `previous` array holds `[6, 6, 5, 4, 3, 3, 2, 3]` — exactly the last row of the full table — and `previous[7] = 3`. Space drops from O(m × n) to O(n), at the cost of losing the ability to reconstruct the alignment (reconstruction needs the full table or a Hirschberg-style pass).

## Damerau-Levenshtein Note

Damerau-Levenshtein adds a fourth operation, transposition of two adjacent characters, as a single edit. Under plain Levenshtein, "ca" to "ac" costs 2 (two substitutions); under Damerau-Levenshtein it costs 1 (one transposition). Real spell checkers use the Damerau variant because typos are usually transpositions, but the recurrence is more complex: the transposition case needs `dp[i - 2][j - 2] + 1` when `s[i - 1] == t[j - 2]` and `s[i - 2] == t[j - 1]`, and the common "optimal string alignment" variant restricts each substring to one edit, so it is not the true Damerau distance in every case.

## Key Takeaways

- The recurrence has three cases (delete, insert, substitute-or-match); the table is filled row by row from the empty-string borders.
- "kitten" to "sitting" is distance **3**, verified by the full table's final cell and by the explicit 3-edit alignment.
- The full table costs O(m × n) time and space; the two-row version keeps the same answer in O(n) space but cannot reconstruct the alignment.
- Damerau-Levenshtein counts an adjacent transposition as one edit, which matches how typos actually happen.
