# KMP String Matching (DSA)

The Knuth-Morris-Pratt (KMP) algorithm finds all occurrences of a pattern in a text in O(n + m) time, where n is the text length and m is the pattern length. It is a classic improvement over the naive approach, which can take O(n × m) in the worst case.

## Why Naive Matching Fails

Naive matching slides the pattern along the text and compares character by character. When a mismatch happens, it shifts the pattern by one and starts comparing from scratch — throwing away all the characters that already matched. Consider searching for `ABABC` in `ABABABC`: after matching `ABAB` and failing on the fifth character, naive restarts at text position 1, even though the text already tells us the next possible match starts at position 2. KMP never re-examines a text character: it uses the information in the matched prefix to decide how far the pattern can safely shift.

## The Prefix Function (LPS Array)

The key idea is the prefix function, usually called the LPS (longest proper prefix which is also a suffix) array. For each position `i` in the pattern, `lps[i]` is the length of the longest proper prefix of `pattern[0..i]` that is also a suffix of it. "Proper" means it cannot be the whole substring.

For pattern `ABABCABAB`:

| i | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|---|
| pattern[i] | A | B | A | B | C | A | B | A | B |
| lps[i] | 0 | 0 | 1 | 2 | 0 | 1 | 2 | 3 | 4 |

For example, `lps[3] = 2` because in `ABAB` the longest proper prefix that is also a suffix is `AB`. `lps[4] = 0` because nothing in `ABABC` repeats at the end. The array is built in O(m) time by walking the pattern once, using previously computed values to fall back instead of restarting.

## How the Search Uses LPS

During the search, `j` tracks how many pattern characters have matched so far. When `text[i] != pattern[j]`, instead of resetting `j` to 0, KMP sets `j = lps[j - 1]`: the matched prefix's longest border is still a valid partial match, so the pattern shifts forward by `j - lps[j - 1]` positions and the text pointer `i` never moves backwards. When `j` reaches `m`, a full match is found at `i - m`, and the search continues from `j = lps[j - 1]` to find overlapping matches.

```csharp
public static List<int> KmpSearch(string text, string pattern)
{
    var matches = new List<int>();
    if (pattern.Length == 0) return matches;

    int[] lps = BuildLps(pattern);
    int j = 0; // length of current match in the pattern

    for (int i = 0; i < text.Length; i++)
    {
        while (j > 0 && text[i] != pattern[j])
            j = lps[j - 1];

        if (text[i] == pattern[j]) j++;

        if (j == pattern.Length)
        {
            matches.Add(i - pattern.Length + 1);
            j = lps[j - 1];
        }
    }

    return matches;
}

private static int[] BuildLps(string pattern)
{
    int[] lps = new int[pattern.Length];
    int len = 0; // length of the previous longest prefix-suffix

    for (int i = 1; i < pattern.Length; i++)
    {
        while (len > 0 && pattern[i] != pattern[len])
            len = lps[len - 1];

        if (pattern[i] == pattern[len]) len++;

        lps[i] = len;
    }

    return lps;
}

public static void Main()
{
    var matches = KmpSearch("ABABABCABAB", "ABAB");
    Console.WriteLine(string.Join(", ", matches)); // 0, 2, 7
}
```

## Complexity

| | Naive | KMP |
|---|---|---|
| Worst-case time | O(n × m) | O(n + m) |
| Extra space | O(1) | O(m) for the LPS array |
| Re-examines text characters | Yes | No |

The O(n + m) bound holds because the text pointer only moves forward and the pattern pointer `j` only moves backwards via LPS lookups, each of which strictly decreases `j` — so the total number of fallback steps across the whole run is O(n + m).

## When to Use It

Use KMP when the pattern has repeating structure and the text is long — searching log files, DNA sequences, or streaming input where you cannot afford to re-scan. For a one-off search in a short string, `string.IndexOf` is simpler and fast enough (it uses a tuned Boyer-Moore-Horspool variant internally). KMP also extends naturally to streaming: because it never backtracks in the text, it can process characters as they arrive, which is why the same prefix-function idea appears in network intrusion detection and text editors.

## Key Takeaways

- KMP's LPS array records, for every prefix, the longest proper prefix that is also a suffix.
- On a mismatch, KMP falls back to `lps[j - 1]` instead of restarting, so no text character is ever re-examined.
- Total time is O(n + m) with O(m) extra space.
- The same idea works on streams, since the text pointer only moves forward.
