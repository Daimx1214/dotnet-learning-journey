# Suffix Arrays and LCP Arrays with Kasai's Algorithm

A suffix array (SA) lists all suffixes of a string in sorted order. The LCP array stores, for each adjacent pair of suffixes in that order, the length of their longest common prefix. Together they power substring search, longest repeated substring, and longest common substring problems in linear or near-linear time.

## The worked example: "banana"

The six suffixes of `banana` (index : suffix):

| Index | Suffix |
|------:|--------|
| 0 | banana |
| 1 | anana |
| 2 | nana |
| 3 | ana |
| 4 | na |
| 5 | a |

Sorted lexicographically they are: `a` (5), `ana` (3), `anana` (1), `banana` (0), `na` (4), `nana` (2).

So the suffix array is:

```text
SA = [5, 3, 1, 0, 4, 2]
```

## The LCP array

LCP[i] is the longest common prefix of the suffixes at SA[i] and SA[i-1]. The first entry is defined as 0. Working pair by pair:

- SA[1] = 3 ("ana") vs SA[0] = 5 ("a"): common prefix "a" → **1**
- SA[2] = 1 ("anana") vs SA[1] = 3 ("ana"): common prefix "ana" → **3**
- SA[3] = 0 ("banana") vs SA[2] = 1 ("anana"): first letters differ → **0**
- SA[4] = 4 ("na") vs SA[3] = 0 ("banana"): first letters differ → **0**
- SA[5] = 2 ("nana") vs SA[4] = 4 ("na"): common prefix "na" → **2**

```text
LCP = [0, 1, 3, 0, 0, 2]
```

## Why those two values are what they are

- `lcp("ana", "anana") = 3`: compare character by character — `a`=`a`, `n`=`n`, `a`=`a`, then "ana" ends. The shorter string is exhausted after 3 matches, so the LCP is 3.
- `lcp("na", "nana") = 2`: `n`=`n`, `a`=`a`, then "na" ends. LCP is 2.

## Longest repeated substring

The longest repeated substring of a string is simply the maximum value in the LCP array, because any repeated substring appears as a common prefix of two adjacent suffixes in sorted order. For "banana" the maximum is 3, and the substring is **"ana"** (it occurs at positions 1 and 3).

## Kasai's algorithm — O(n)

Naively comparing every adjacent pair costs O(n^2). Kasai's trick: process suffixes in order of their starting position (not SA order), and keep a running match length `k`. When moving from suffix `i` to suffix `i+1`, the LCP with its SA-neighbour can drop by at most 1 — so the next comparison starts at `k - 1` instead of 0. Each character is compared at most twice overall, giving O(n) time.

```csharp
// Builds the LCP array (first entry 0) from s and its suffix array.
public static int[] Kasai(string s, int[] sa)
{
    int n = s.Length;
    int[] rank = new int[n];   // rank[pos] = position of suffix pos in SA
    for (int i = 0; i < n; i++) rank[sa[i]] = i;

    int[] lcp = new int[n];
    int k = 0;
    for (int i = 0; i < n; i++)
    {
        int r = rank[i];
        if (r == 0) { lcp[r] = 0; continue; }

        int j = sa[r - 1];     // previous suffix in SA order
        // The k-1 trick: k can only drop by 1 when i advances by 1.
        while (i + k < n && j + k < n && s[i + k] == s[j + k]) k++;
        lcp[r] = k;
        if (k > 0) k--;        // <-- the k-1 trick
    }
    return lcp;
}
```

Trace the trick on "banana": when `i = 1` ("anana") the algorithm finds `k = 3` against "ana". Advancing to `i = 2` ("nana"), it starts from `k = 2` rather than 0 — and indeed `lcp("nana", "na") = 2` is found without re-checking the first two characters from scratch.

## Where this is used

- Longest repeated substring: `max(LCP)`.
- Longest common substring of two strings: build SA/LCP over `s1 + '#' + s2` and take the max LCP between suffixes from different strings.
- Counting distinct substrings: `n*(n+1)/2 - sum(LCP)`. For "banana" that is `21 - 6 = 15` distinct substrings.

## Key Takeaways

- The suffix array of "banana" is `[5, 3, 1, 0, 4, 2]` and its LCP array is `[0, 1, 3, 0, 0, 2]`.
- LCP[i] compares the suffixes at SA[i] and SA[i-1]; the first entry is always 0.
- The longest repeated substring is the maximum LCP value — "ana" (length 3) for "banana".
- Kasai's algorithm runs in O(n) because of the `k - 1` trick: the running match length drops by at most 1 per step.
- SA + LCP turn substring problems (search, repeats, common substrings) into simple array scans.
