# Manacher's Algorithm

Manacher's algorithm finds the longest palindromic substring of a string in linear time, `O(n)`. A naive expand-around-center check is `O(n^2)`; Manacher's avoids re-checking characters it has already compared.

## The Key Insight

A palindrome mirrors around its center. If a large palindrome is already known, the palindrome centered inside it mirrors a palindrome on the other side — so its radius is known without re-expanding, except possibly past the known boundary.

Manacher's tracks two values as it scans left to right:

- `C` — the center of the rightmost palindrome found so far.
- `R` — the right boundary of that palindrome (exclusive).

For each new center `i`, its mirror is `mirror = 2 * C - i`. If `i` is inside the known palindrome (`i < R`), the radius at `i` is at least `min(R - i, radius[mirror])`. Only the part beyond `R` needs fresh expansion.

## Handling Even and Odd Lengths

Odd-length palindromes have a character center; even-length ones have a gap center. The standard trick unifies both by inserting a separator between every character:

```text
"ababa"  ->  "^#a#b#a#b#a#$"
"babad"  ->  "^#b#a#b#a#d#$"
```

The `^` and `$` are sentinels that prevent bounds checks. In the transformed string every palindrome is odd-length, and its radius in the transformed string equals the length of the real palindrome.

## Algorithm

```csharp
public static string LongestPalindromicSubstring(string s)
{
    // Transform: insert '#' between characters, add sentinels.
    var t = new System.Text.StringBuilder("^");
    foreach (char c in s) { t.Append('#'); t.Append(c); }
    t.Append("#$");
    string transformed = t.ToString();

    int n = transformed.Length;
    int[] radius = new int[n];
    int center = 0, right = 0;

    for (int i = 1; i < n - 1; i++)
    {
        int mirror = 2 * center - i;
        if (i < right)
            radius[i] = Math.Min(right - i, radius[mirror]);

        // Expand beyond the known boundary.
        while (transformed[i + radius[i] + 1] == transformed[i - radius[i] - 1])
            radius[i]++;

        if (i + radius[i] > right)
        {
            center = i;
            right = i + radius[i];
        }
    }

    int maxRadius = 0, maxCenter = 0;
    for (int i = 1; i < n - 1; i++)
    {
        if (radius[i] > maxRadius) { maxRadius = radius[i]; maxCenter = i; }
    }

    int start = (maxCenter - maxRadius) / 2;
    return s.Substring(start, maxRadius);
}
```

## Hand-Verified Examples

**"ababa"** — the whole string is a palindrome, length 5. The algorithm returns `"ababa"`.

**"babad"** — two palindromes of length 3 exist: `"bab"` (positions 0–2) and `"aba"` (positions 1–3). The algorithm returns one of them, length 3. Either answer is correct.

**"cbbd"** — the longest palindromic substring is `"bb"`, length 2.

**"a"** — length 1. **""** — length 0 (handle the empty string before the transform).

## Why It Is Linear

Each successful character comparison moves `R` one step to the right, and `R` never moves left. There are at most `n` successful expansions. Failed comparisons happen at most once per center. Total work is `O(n)`, with `O(n)` extra space for the radius array.

## When to Use It

- Longest palindromic substring / longest palindromic subsequence variants.
- Counting all palindromic substrings in linear time (sum the radii).
- Any problem where expand-around-center is the naive solution and the input is large.

## Key Takeaways

- Manacher's reuses mirror information inside a known palindrome, so no character pair is compared twice.
- The `#` separator trick unifies even and odd palindromes into one odd-length case.
- `C` and `R` (center and right boundary of the rightmost palindrome) are the only state the algorithm needs.
- Time is `O(n)` and space is `O(n)`; the naive approach is `O(n^2)`.
- Verified: "ababa" gives length 5, "babad" gives length 3, "cbbd" gives length 2.
