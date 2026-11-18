# Longest Common Subsequence (LCS)

The longest common subsequence problem asks for the longest sequence that appears as a subsequence in both of two strings — the characters must appear in the same order in both strings, but they do not need to be next to each other. It is a classic dynamic programming problem and the idea behind it shows up everywhere: diff tools compare files by finding their LCS, DNA sequence alignment compares genomes the same way, and spell-checkers use similar edit-distance DP.

A subsequence is not a substring. For `"ABCBDAB"`, `"ABD"` is a subsequence (A, then B, then D, in order) but `"BCB"` appearing with gaps is also one; a substring must be contiguous. That distinction is the whole problem: you are allowed to skip characters in either string.

## The DP recurrence

Let `dp[i][j]` be the length of the LCS of the first `i` characters of `A` and the first `j` characters of `B`. The recurrence compares the last characters of the two prefixes:

- If `A[i] == B[j]`, that character can be part of the LCS: `dp[i][j] = dp[i-1][j-1] + 1`.
- If they differ, the character at the end of one of the prefixes must be skipped: `dp[i][j] = max(dp[i-1][j], dp[i][j-1])`.
- Base case: `dp[0][j] = dp[i][0] = 0` — the LCS with an empty string is empty.

```csharp
// Returns the length of the longest common subsequence of a and b.
public static int LcsLength(string a, string b)
{
    int m = a.Length, n = b.Length;
    int[,] dp = new int[m + 1, n + 1];

    for (int i = 1; i <= m; i++)
    {
        for (int j = 1; j <= n; j++)
        {
            if (a[i - 1] == b[j - 1])
                dp[i, j] = dp[i - 1, j - 1] + 1;
            else
                dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
        }
    }

    return dp[m, n];
}
```

## Worked example: "ABCBDAB" and "BDCABA"

The LCS length here is 4 — one possible LCS is `"BCBA"` (another is `"BDAB"`). Filling the table row by row, where rows are the characters of `A = ABCBDAB` and columns are the characters of `B = BDCABA`:

```text
        ""  B   D   C   A   B   A
    ""   0   0   0   0   0   0   0
    A    0   0   0   0   1   1   1
    B    0   1   1   1   1   2   2
    C    0   1   1   2   2   2   2
    B    0   1   1   2   2   3   3
    D    0   1   2   2   2   3   3
    A    0   1   2   2   3   3   4
    B    0   1   2   2   3   4   4
```

Reading the table: the bottom-right cell is 4, the LCS length. Notice how the value only increases on a diagonal step when the characters match — for example the `A` row against the `A` column jumps from 2 to 3 — and otherwise carries the best value from the cell above or the cell to the left.

## Reconstructing the actual subsequence

The length is often not enough; you usually want the subsequence itself. Walk backwards from `dp[m, n]` and rebuild it:

```csharp
// Reconstructs one longest common subsequence of a and b.
public static string Lcs(string a, string b)
{
    int m = a.Length, n = b.Length;
    int[,] dp = new int[m + 1, n + 1];

    for (int i = 1; i <= m; i++)
        for (int j = 1; j <= n; j++)
            dp[i, j] = a[i - 1] == b[j - 1]
                ? dp[i - 1, j - 1] + 1
                : Math.Max(dp[i - 1, j], dp[i, j - 1]);

    // Walk backwards from the bottom-right corner.
    var result = new System.Text.StringBuilder();
    int i = m, j = n;
    while (i > 0 && j > 0)
    {
        if (a[i - 1] == b[j - 1])
        {
            // Characters match: this one belongs to the LCS.
            result.Insert(0, a[i - 1]);
            i--; j--;
        }
        else if (dp[i - 1, j] >= dp[i, j - 1])
        {
            i--;   // move up: skip a[i-1]
        }
        else
        {
            j--;   // move left: skip b[j-1]
        }
    }
    return result.ToString();  // e.g. "BCBA" for the example above
}
```

When both `dp[i-1, j]` and `dp[i, j-1]` are equal, either direction is valid — that is why the LCS is not always unique. Different tie-breaking choices give different (equally long) subsequences.

## Complexity and space optimization

The table has `(m+1) x (n+1)` cells and each is filled in constant time, so the time complexity is **O(mn)** and the space complexity is **O(mn)**.

If you only need the length, you do not need the whole table: each row only depends on the previous row. Keeping two rows (previous and current) drops the space to **O(min(m, n))** — iterate over the shorter string as the columns.

```csharp
// Length only, O(min(m, n)) space: keep two rows instead of the full table.
public static int LcsLengthSpaceOptimized(string a, string b)
{
    // Make b the shorter string so the rows are as small as possible.
    if (a.Length < b.Length) (a, b) = (b, a);

    int n = b.Length;
    int[] prev = new int[n + 1];
    int[] curr = new int[n + 1];

    foreach (char ca in a)
    {
        for (int j = 1; j <= n; j++)
        {
            curr[j] = ca == b[j - 1]
                ? prev[j - 1] + 1
                : Math.Max(prev[j], curr[j - 1]);
        }
        (prev, curr) = (curr, prev);  // swap rows for the next iteration
    }

    return prev[n];
}
```

The trade-off: the two-row version gives you the length but cannot reconstruct the subsequence, because reconstruction needs the full table to walk backwards through. If you need the actual LCS string, keep the full table.

## Key Takeaways

- LCS finds the longest sequence appearing in order (not necessarily contiguously) in both strings; it is different from the longest common substring.
- The recurrence is: match → `dp[i-1][j-1] + 1`, mismatch → `max(dp[i-1][j], dp[i][j-1])`, with an empty-string base case of 0.
- For `"ABCBDAB"` and `"BDCABA"` the LCS length is 4 (e.g. `"BCBA"` or `"BDAB"`), read from the bottom-right cell of the table.
- Reconstruct the subsequence by walking backwards from `dp[m, n]`, taking matching characters and moving up or left otherwise.
- Time is O(mn) and space is O(mn) with the full table; two rows reduce space to O(min(m, n)) when only the length is needed.
- The LCS is not always unique — equal choices during reconstruction give different valid subsequences of the same length.
