# Suffix Arrays and LCP Arrays

A suffix array sorts all suffixes of a string. Once sorted, suffixes that share a prefix sit next to each other — which makes substring search, repeated-substring detection, and many string problems much cheaper.

## The Suffix Array

For the string `"banana"`, the suffixes sorted lexicographically are `"a"`, `"ana"`, `"anana"`, `"banana"`, `"na"`, `"nana"`. Their starting positions give the suffix array:

```csharp
// "banana": suffix array = [5, 3, 1, 0, 4, 2]
//   SA[0] = 5 -> "a"
//   SA[1] = 3 -> "ana"
//   SA[2] = 1 -> "anana"
//   SA[3] = 0 -> "banana"
//   SA[4] = 4 -> "na"
//   SA[5] = 2 -> "nana"
```

Verification: `"a" < "ana" < "anana" < "banana" < "na" < "nana"`, since `'a' < 'b' < 'n'`, and among the `a`-suffixes the shorter prefix comes first, then `"ana"` vs `"anana"` is decided by the second character (`'n'` vs `'n'`, then end-of-string vs `'a'`).

## O(n log n) Doubling Construction

The naive approach sorts all suffixes with a string comparison — O(n² log n). The doubling method sorts by rank instead:

1. Rank each suffix by its first character.
2. Double the compared length each round (1, 2, 4, 8, ...): sort by the pair `(rank[i], rank[i + k])`.
3. Recompute ranks from the sorted order; stop when all ranks are distinct.

Each round is an O(n log n) sort (or O(n) with counting sort), and there are O(log n) rounds, giving O(n log n) overall. The doubling trick works because the rank of a suffix of length `2k` is fully determined by the ranks of its two length-`k` halves.

## The LCP Array and Kasai's Algorithm

The LCP array stores the longest common prefix between each pair of adjacent suffixes in the suffix array. For `"banana"` the LCP array is `[0, 1, 3, 0, 0, 2]`:

- LCP[0] = 0 by convention.
- LCP[1] = 1: `"a"` and `"ana"` share `"a"`.
- LCP[2] = 3: `"ana"` and `"anana"` share `"ana"`.
- LCP[3] = 0: `"anana"` and `"banana"` share nothing.
- LCP[4] = 0: `"banana"` and `"na"` share nothing.
- LCP[5] = 2: `"na"` and `"nana"` share `"na"`.

Kasai's algorithm computes the whole LCP array in O(n). The key insight: if suffixes at positions `i` and `j` share a prefix of length `h`, then the suffixes at `i + 1` and `j + 1` share at least `h - 1`. So each character is compared at most twice overall.

```csharp
// Kasai's algorithm: O(n) LCP from a suffix array SA and its inverse rank.
int[] BuildLcp(string s, int[] sa)
{
    int n = s.Length;
    int[] rank = new int[n];
    for (int i = 0; i < n; i++) rank[sa[i]] = i;

    int[] lcp = new int[n];
    int h = 0;
    for (int i = 0; i < n; i++)
    {
        int r = rank[i];
        if (r == 0) continue;
        int j = sa[r - 1];
        while (i + h < n && j + h < n && s[i + h] == s[j + h]) h++;
        lcp[r] = h;
        if (h > 0) h--;
    }
    return lcp;
}
```

## Uses

- **Longest repeated substring:** the maximum value in the LCP array. For `"banana"` the max is 3 (`"ana"`, at LCP[2]).
- **Substring search:** binary-search the suffix array in O(m log n) for a pattern of length `m`.
- **Number of distinct substrings:** total substrings `n(n+1)/2` minus the sum of the LCP array. For `"banana"`: 21 − 6 = 15 distinct substrings.
- **Longest common substring** between two strings: build a suffix array over `s1 + '#' + s2` and take the max LCP across the boundary.

## Key Takeaways

- A suffix array is the sorted list of all suffix starting positions; for `"banana"` it is `[5, 3, 1, 0, 4, 2]`.
- Doubling construction sorts by rank pairs and reaches O(n log n).
- The LCP array holds the common-prefix length of adjacent suffixes; for `"banana"` it is `[0, 1, 3, 0, 0, 2]`.
- Kasai's algorithm builds the LCP array in O(n) by reusing the previous match length.
- The max LCP value is the longest repeated substring (`"ana"`, length 3, for `"banana"`).
- Suffix arrays power substring search, distinct-substring counting, and longest-common-substring problems.
