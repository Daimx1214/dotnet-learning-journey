# Word Ladder (BFS)

Given a begin word, an end word, and a word list, find the length of the shortest transformation sequence where each step changes exactly one letter and every intermediate word is in the list. This is a shortest-path problem on an implicit graph, so breadth-first search (BFS) is the natural tool.

## Worked example

- Begin word: `"hit"`
- End word: `"cog"`
- Word list: `["hot", "dot", "dog", "lot", "log", "cog"]`

The shortest path is:

```text
hit -> hot -> dot -> dog -> cog
```

Count the words in the sequence, including both ends: hit (1), hot (2), dot (3), dog (4), cog (5). The shortest transformation length is **5**.

Check each step changes exactly one letter: `hit`→`hot` (i→o), `hot`→`dot` (h→d), `dot`→`dog` (t→g), `dog`→`cog` (d→c). Every intermediate word (`hot`, `dot`, `dog`) is in the list, and `cog` is the target. No 4-step path exists — `hit` has only one neighbour in the list (`hot`), and from `hot` the only route onwards is through `dot`/`lot`, both of which still need two more steps to reach `cog`.

## Pattern buckets: finding neighbours fast

Comparing every word against every other word is O(N^2 * M). The standard trick is pattern buckets: for each word, generate all patterns with one position replaced by `*`, and group words by pattern. Words sharing a pattern differ in exactly one position.

```text
hit -> *it, h*t, hi*
hot -> *ot, h*t, ho*
dot -> *ot, d*t, do*
dog -> *og, d*g, do*
```

`hit` and `hot` share the bucket `h*t`, so they are neighbours. `hot` and `dot` share `*ot`. Building the buckets costs O(M * N) for N words of length M, and each word has at most M * (bucket size) neighbours to explore.

## C# solution

```csharp
public static int LadderLength(string beginWord, string endWord, IList<string> wordList)
{
    var wordSet = new HashSet<string>(wordList);
    if (!wordSet.Contains(endWord)) return 0;   // end word must be reachable

    // Build pattern -> words buckets: "h*t" -> ["hit", "hot"]
    var buckets = new Dictionary<string, List<string>>();
    foreach (var word in wordSet)
    {
        for (int i = 0; i < word.Length; i++)
        {
            string pattern = word[..i] + "*" + word[(i + 1)..];
            if (!buckets.TryGetValue(pattern, out var list))
                buckets[pattern] = list = new List<string>();
            list.Add(word);
        }
    }

    var queue = new Queue<(string Word, int Level)>();
    queue.Enqueue((beginWord, 1));
    var visited = new HashSet<string> { beginWord };

    while (queue.Count > 0)
    {
        var (word, level) = queue.Dequeue();
        for (int i = 0; i < word.Length; i++)
        {
            string pattern = word[..i] + "*" + word[(i + 1)..];
            if (!buckets.TryGetValue(pattern, out var neighbours)) continue;
            foreach (var next in neighbours)
            {
                if (next == endWord) return level + 1;
                if (visited.Add(next)) queue.Enqueue((next, level + 1));
            }
            buckets[pattern].Clear();   // never re-scan this bucket
        }
    }
    return 0;   // no path exists
}
```

Running it on the worked example: level 1 is `hit`, level 2 is `hot`, level 3 is `dot` (and `lot`), level 4 is `dog` (and `log`), and level 5 reaches `cog` — returning **5**, as worked out by hand.

## Complexity

Generating the buckets costs O(M * N) and each of the N words is dequeued once, expanding M patterns: total time is **O(M^2 * N)** in the usual statement of the problem (each word contributes M patterns of length M), with O(M * N) space for the buckets. Clearing a bucket after its first use keeps the neighbour scan from repeating work.

## The "no path" case

If the end word is not in the list, or BFS exhausts the queue without reaching it, return **0** — not -1, not the level reached. That is the LeetCode convention for this problem, and it distinguishes "no transformation exists" from any valid length (valid lengths are always ≥ 1).

## Key Takeaways

- Word ladder is BFS on an implicit graph: one-letter changes are edges, shortest path is the answer.
- Worked example: hit → hot → dot → dog → cog, length 5.
- Pattern buckets (`h*t`) find all one-letter neighbours in O(M) per word instead of comparing all pairs.
- Complexity is O(M^2 * N) time and O(M * N) space; clearing used buckets avoids repeat scans.
- Return 0 when no transformation sequence exists.
