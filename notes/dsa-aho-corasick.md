# Aho-Corasick Algorithm

The Aho-Corasick algorithm finds all occurrences of many patterns in a
text in a single pass. It builds a trie of the patterns, adds failure
links (like the KMP prefix function, but for a trie), and then scans the
text once — every time the scan reaches a node, every pattern that ends
at that node (or at one of its failure ancestors) is a match.

## The Trie

Insert every pattern into a trie. Each node stores its children and a
`fail` link, plus the pattern that ends at the node, if any. For the
patterns `he`, `she`, `his`, `hers` the trie has a root with children
`h` and `s`, and the `h` branch splits into `he` (which continues to
`hers`) and `his`.

## Failure Links

The failure link of a node points to the node for the longest proper
suffix of the node's string that is also a trie prefix. Failure links
are built with a breadth-first walk from the root:

1. Depth-1 nodes fail to the root.
2. For a node `v` reached from parent `u` on character `c`, follow
   `u.fail` until a node with a `c` child (or the root) is found; `v.fail`
   is that child, or the root if there is none.
3. Merge outputs: if `v.fail` ends a pattern, `v` also reports it
   (usually via an output link or a copied output list).

```csharp
class Node
{
    public Dictionary<char, Node> Next = new();
    public Node? Fail;
    public string? Pattern; // non-null when a pattern ends here
}

Node Build(IEnumerable<string> patterns)
{
    var root = new Node();
    foreach (var p in patterns)
    {
        var node = root;
        foreach (var c in p)
        {
            if (!node.Next.TryGetValue(c, out var child))
                node.Next[c] = child = new Node();
            node = child;
        }
        node.Pattern = p;
    }

    var queue = new Queue<Node>();
    foreach (var child in root.Next.Values)
    {
        child.Fail = root;
        queue.Enqueue(child);
    }
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        foreach (var (c, child) in current.Next)
        {
            var fail = current.Fail;
            while (fail is not null && !fail.Next.ContainsKey(c))
                fail = fail.Fail;
            child.Fail = fail is null ? root : fail.Next[c];
            queue.Enqueue(child);
        }
    }
    return root;
}
```

## Searching

Walk the text character by character, following trie edges and falling
back along failure links when an edge is missing. After each step, walk
the output chain (the node and its failure ancestors) and report every
pattern found.

```csharp
IEnumerable<(string Pattern, int EndIndex)> Search(Node root, string text)
{
    var node = root;
    for (int i = 0; i < text.Length; i++)
    {
        while (node != root && !node.Next.ContainsKey(text[i]))
            node = node.Fail!;
        if (node.Next.TryGetValue(text[i], out var next))
            node = next;

        for (var output = node; output != root; output = output.Fail!)
            if (output.Pattern is not null)
                yield return (output.Pattern, i);
    }
}
```

## Worked Example: "ushers"

Patterns `he`, `she`, `his`, `hers` in the text `"ushers"`
(`u` 0, `s` 1, `h` 2, `e` 3, `r` 4, `s` 5):

| Index | Char | Node reached | Matches ending here |
| ----- | ---- | ------------ | ------------------- |
| 0 | u | root | — |
| 1 | s | `s` | — |
| 2 | h | `sh` | — |
| 3 | e | `she` | `she`, and via its failure link to `he`: `he` |
| 4 | r | `her` | — |
| 5 | s | `hers` | `hers` |

Three matches in one six-character scan: `she` (1–3), `he` (2–3) and
`hers` (2–5). `his` never matches because the text has no `i`. The
failure link from `she` to `he` is what finds the overlapping match
without rescanning.

## Complexity

Building the automaton costs O(m), where `m` is the total length of all
patterns. Searching costs O(n + z), where `n` is the text length and
`z` is the number of matches reported — the scan itself is linear
because each character moves forward through the trie or a failure link,
and failure links only shorten the current match. This is why
Aho-Corasick beats running KMP once per pattern: `k` patterns of total
length `m` would cost O(n · k) with separate scans, but one automaton
does it in O(n + m + z).

## Where It Is Used

- **Content filtering and profanity detection:** thousands of banned
  words checked against each message in one pass.
- **Intrusion detection:** Snort-style rule sets match many signatures
  against each packet payload.
- **Bioinformatics:** searching a genome for a large set of known
  motifs at once.
- **Syntax highlighting and tokenising:** many keywords matched against
  source text in a single scan.

## Key Takeaways

- Aho-Corasick matches many patterns in one linear scan of the text.
- The trie holds the patterns; failure links point each node to the
  longest proper suffix that is also a trie prefix.
- Failure links are built with a breadth-first walk, KMP-style, and
  output links report every pattern ending at a node.
- In `"ushers"` the patterns `he`, `she`, `his`, `hers` yield exactly
  three matches: `she`, `he` and `hers`.
- Total cost is O(n + m + z) — linear in the text, the patterns and the
  matches — which beats one KMP scan per pattern.
