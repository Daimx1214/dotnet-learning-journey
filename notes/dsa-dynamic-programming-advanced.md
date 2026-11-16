# Advanced Dynamic Programming: LCS, Edit Distance, LIS (DSA)

Basic dynamic programming — Fibonacci, climbing stairs — teaches the pattern: define the state, write the recurrence, fill the table. The classic string and sequence problems are where DP earns its keep in real code: diff tools, spell checkers, and version control all run on the algorithms below.

## Longest Common Subsequence (LCS)

Given two strings, find the length of the longest subsequence present in both (characters need not be adjacent, only in order). Let `dp[i, j]` be the LCS length of the first `i` characters of `s1` and the first `j` of `s2`. If the current characters match, they extend the LCS of the prefixes before them; otherwise the answer is the best of dropping one character from either string.

```csharp
public static int LongestCommonSubsequence(string s1, string s2)
{
    int m = s1.Length, n = s2.Length;
    int[,] dp = new int[m + 1, n + 1];

    for (int i = 1; i <= m; i++)
    {
        for (int j = 1; j <= n; j++)
        {
            if (s1[i - 1] == s2[j - 1])
                dp[i, j] = dp[i - 1, j - 1] + 1;   // match: extend the diagonal
            else
                dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]); // skip one side
        }
    }
    return dp[m, n];
}

// LCS("abcde", "ace") = 3  ("ace")
// LCS("AGGTAB", "GXTXAYB") = 4  ("GTAB")
```

Time is O(m × n) and space is O(m × n). To reconstruct the actual subsequence, walk back from `dp[m, n]`: on a match take the character and move diagonally, otherwise move toward the larger neighbour.
## Edit Distance (Levenshtein Distance)

Edit distance counts the minimum insertions, deletions, and substitutions needed to turn one string into another. `dp[i, j]` is the distance between the first `i` characters of `s1` and the first `j` of `s2`. The base cases are the cost of building a string from empty: `dp[i, 0] = i` deletions and `dp[0, j] = j` insertions. For matching characters the cost is unchanged; otherwise take the cheapest of the three operations, each costing 1 plus the subproblem it leads to.

```csharp
public static int EditDistance(string s1, string s2)
{
    int m = s1.Length, n = s2.Length;
    int[,] dp = new int[m + 1, n + 1];

    for (int i = 0; i <= m; i++) dp[i, 0] = i;  // delete i chars
    for (int j = 0; j <= n; j++) dp[0, j] = j;  // insert j chars

    for (int i = 1; i <= m; i++)
    {
        for (int j = 1; j <= n; j++)
        {
            if (s1[i - 1] == s2[j - 1])
                dp[i, j] = dp[i - 1, j - 1];
            else
                dp[i, j] = 1 + Math.Min(
                    dp[i - 1, j],      // delete from s1
                    Math.Min(
                        dp[i, j - 1],  // insert into s1
                        dp[i - 1, j - 1])); // substitute
        }
    }
    return dp[m, n];
}

// EditDistance("kitten", "sitting") = 3  (k→s, e→i, insert g)
// EditDistance("flaw", "lawn") = 2
```

This is the algorithm behind spell-check suggestions ("did you mean…?") and fuzzy search ranking: strings with a small edit distance are near-duplicates.

## Longest Increasing Subsequence (LIS)

Given an array, find the length of the longest strictly increasing subsequence. The O(n²) DP defines `dp[i]` as the LIS ending exactly at index `i`: it is 1 plus the best `dp[j]` over all `j < i` with `nums[j] < nums[i]`, or 1 if no such `j` exists. The answer is the maximum over all `dp[i]`.

```csharp
public static int LongestIncreasingSubsequence(int[] nums)
{
    int n = nums.Length;
    int[] dp = new int[n];
    Array.Fill(dp, 1);
    for (int i = 1; i < n; i++)
        for (int j = 0; j < i; j++)
            if (nums[j] < nums[i]) dp[i] = Math.Max(dp[i], dp[j] + 1);
    return dp.Max();
}

// LIS([10, 9, 2, 5, 3, 7, 101, 18]) = 4  ([2, 3, 7, 101] or [2, 5, 7, 101])
```

The O(n²) version is the one to understand first, but LIS also has a well-known O(n log n) solution: maintain an array of the smallest possible tail value for increasing subsequences of each length, and binary-search the position where each new number belongs.

## Space Optimisation

In both LCS and edit distance, each cell only depends on the current row, the previous row, and the diagonal — so the full (m + 1) × (n + 1) table is unnecessary. Two one-dimensional arrays cut the space from O(m × n) to O(min(m, n)). The trade-off is that you lose the ability to reconstruct the solution by walking the table back.

```csharp
// Edit distance in O(n) space: only two rows are kept.
public static int EditDistanceOptimized(string s1, string s2)
{
    int m = s1.Length, n = s2.Length;
    int[] prev = new int[n + 1], curr = new int[n + 1];
    for (int j = 0; j <= n; j++) prev[j] = j;
    for (int i = 1; i <= m; i++)
    {
        curr[0] = i;
        for (int j = 1; j <= n; j++)
            curr[j] = s1[i - 1] == s2[j - 1]
                ? prev[j - 1]
                : 1 + Math.Min(prev[j], Math.Min(curr[j - 1], prev[j - 1]));
        (prev, curr) = (curr, prev); // swap rows
    }
    return prev[n];
}
```

## Key Takeaways

- LCS, edit distance, and LIS all share the DP recipe: define the state, write the recurrence, fill the table bottom-up.
- LCS recurrence: match extends the diagonal, mismatch takes the best of dropping one side; edit distance adds the cheapest of delete, insert, or substitute.
- LIS is O(n²) with a one-dimensional table, and O(n log n) with patience-sorting-style binary search on tail values.
- When only the current and previous rows are needed, two 1-D arrays reduce space to O(min(m, n)) — at the cost of losing easy solution reconstruction.
- These are not just interview problems: diffing, spell checking, and fuzzy matching are all production uses of the same recurrences.
