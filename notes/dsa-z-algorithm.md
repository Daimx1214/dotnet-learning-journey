# Z-Algorithm for Linear-Time Pattern Matching

The Z-algorithm computes, for every position in a string, how long the prefix of the string matches the substring starting at that position. That single array turns pattern matching into a linear-time scan.

## The Z-Array Definition

For a string `s` of length `n`, `Z[i]` is the length of the longest prefix of `s` that is also a prefix of the substring `s[i..]`. By convention `Z[0] = 0` (or `n`, depending on the textbook; this note uses `0`).

```csharp
// Naive definition, O(n^2) — only for understanding, not for production use.
int[] ZNaive(string s)
{
    int n = s.Length;
    int[] z = new int[n];
    for (int i = 1; i < n; i++)
    {
        int k = 0;
        while (i + k < n && s[k] == s[i + k])
        {
            k++;
        }
        z[i] = k;
    }
    return z;
}
```

If `Z[i] > 0`, the prefix of length `Z[i]` reappears at position `i`.

## Z-Box Maintenance

The linear algorithm keeps a window `[l, r]` called the Z-box: the rightmost interval that matches the prefix. For each new `i`:

- If `i > r`, there is no useful box, so compare character by character from scratch.
- If `i <= r`, the value `Z[i - l]` is already known. If `Z[i - l] < r - i + 1`, the match ends inside the box and `Z[i] = Z[i - l]` with no new comparisons. Otherwise the match reaches (or passes) `r`, so extend it by comparing beyond `r` and update `l` and `r`.

```csharp
int[] ZAlgorithm(string s)
{
    int n = s.Length;
    int[] z = new int[n];
    int l = 0, r = 0;

    for (int i = 1; i < n; i++)
    {
        if (i <= r)
        {
            z[i] = Math.Min(r - i + 1, z[i - l]);
        }

        while (i + z[i] < n && s[z[i]] == s[i + z[i]])
        {
            z[i]++;
        }

        if (i + z[i] - 1 > r)
        {
            l = i;
            r = i + z[i] - 1;
        }
    }

    return z;
}
```

Each position is compared at most once as the right edge `r` only moves forward, which is why the total work is linear.

## Worked Example: Z-Array for "aabcaabxaaaz"

String `s = "aabcaabxaaaz"`, length 12. Index the characters:

| Index | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
|-------|---|---|---|---|---|---|---|---|---|---|----|----|
| Char  | a | a | b | c | a | a | b | x | a | a | a  | z  |

Hand-tracing each position against the prefix `"aabcaabxaaaz"`:

- `Z[0] = 0` by convention.
- `i = 1`: substring `"abcaabxaaaz"` — `a` matches, `b` vs `a` fails. `Z[1] = 1`.
- `i = 2`: `"bcaabxaaaz"` — `b` vs `a` fails. `Z[2] = 0`.
- `i = 3`: `"caabxaaaz"` — `c` vs `a` fails. `Z[3] = 0`.
- `i = 4`: `"aabxaaaz"` — `a`, `a`, `b` match, then `x` vs `c` fails. `Z[4] = 3`.
- `i = 5`: `"abxaaaz"` — `a` matches, `b` vs `a` fails. `Z[5] = 1`.
- `i = 6`: `"bxaaaz"` — `b` vs `a` fails. `Z[6] = 0`.
- `i = 7`: `"xaaaz"` — `x` vs `a` fails. `Z[7] = 0`.
- `i = 8`: `"aaaz"` — `a`, `a` match, `a` vs `b` fails. `Z[8] = 2`.
- `i = 9`: `"aaz"` — `a`, `a` match, `z` vs `b` fails. `Z[9] = 2`.
- `i = 10`: `"az"` — `a` matches, `z` vs `a` fails. `Z[10] = 1`.
- `i = 11`: `"z"` — `z` vs `a` fails. `Z[11] = 0`.

Verified result: `Z = [0, 1, 0, 0, 3, 1, 0, 0, 2, 2, 1, 0]`.

Check the sum as a sanity check: `0 + 1 + 0 + 0 + 3 + 1 + 0 + 0 + 2 + 2 + 1 + 0 = 10`, which matches the sum of the hand-traced values above.

## Pattern Search Usage

To find a pattern `p` in a text `t`, build the combined string `p + "$" + t`, where `$` is a separator character that appears in neither `p` nor `t`, and compute its Z-array. Every position where `Z[i] == p.Length` is a match: the pattern starts in `t` at index `i - p.Length - 1`.

```csharp
List<int> FindOccurrences(string pattern, string text)
{
    string combined = pattern + "$" + text;
    int[] z = ZAlgorithm(combined);
    var matches = new List<int>();

    for (int i = pattern.Length + 1; i < combined.Length; i++)
    {
        if (z[i] == pattern.Length)
        {
            matches.Add(i - pattern.Length - 1);
        }
    }

    return matches;
}

// Example: FindOccurrences("aab", "aabcaabxaaaz") returns [0, 4].
// "aab" occurs at text index 0 ("aab...") and at index 4 ("...aab...").
// It does not occur at index 8 ("aaa...") because the third character differs.
```

## Complexity

Building the Z-array costs `O(n)` time and `O(n)` extra space for a string of length `n`. Pattern search on a pattern of length `m` and a text of length `n` therefore costs `O(n + m)` time and `O(n + m)` space for the combined string. The separator is essential: without it, a match could span the boundary between pattern and text and produce a false positive.

## Key Takeaways

- `Z[i]` is the length of the longest prefix of `s` that also starts at `i`.
- The Z-box `[l, r]` reuses already-computed matches so no character is compared twice as a right edge.
- Pattern matching reduces to one Z-array computation on `pattern + "$" + text`, in `O(n + m)` time.
- For `"aabcaabxaaaz"`, the verified Z-array is `[0, 1, 0, 0, 3, 1, 0, 0, 2, 2, 1, 0]`.
- The separator character must not occur in the pattern or the text, or matches can be reported incorrectly.
