# Suffix Automaton

A suffix automaton (SAM) is a minimal deterministic automaton that accepts
all substrings of a given string. For a string of length `n` it has at most
`2n - 1` states and `3n - 4` transitions, which makes it a compact way to
answer substring questions in linear time.

## States and Suffix Links

Each state represents a set of substrings: all substrings whose occurrences
end at the same set of positions. A state stores:

- `len` — the length of the longest string in the state.
- `link` — the suffix link, pointing to the state for the longest proper
  suffix of the state's strings that belongs to a different state.
- `next` — transitions keyed by character.

The suffix links form a tree rooted at the initial state, and every state's
strings have lengths in the range `(link.len + 1) .. len`.

## Building Incrementally

Build the automaton one character at a time, keeping a `last` pointer to
the state for the whole string built so far. To extend by a character `c`:

1. Create a new state `cur` with `len = last.len + 1`.
2. Walk suffix links from `last`, adding a transition on `c` to `cur`
   wherever it is missing.
3. If a state `p` already has a transition on `c` to `q`:
   - If `q.len == p.len + 1`, set `cur.link = q`.
   - Otherwise clone `q` into a new state with the same transitions and
     link, `len = p.len + 1`, and redirect the transitions from `p` and
     its suffix ancestors to the clone. Set both `q.link` and `cur.link`
     to the clone.
4. Set `last = cur`.

Each extension is amortised O(1), so the whole construction is O(n).

```csharp
class State
{
    public int Len;
    public int Link = -1;
    public Dictionary<char, int> Next = new();
}

var states = new List<State> { new() { Len = 0, Link = -1 } };
int last = 0;

void Extend(char c)
{
    int cur = states.Count;
    states.Add(new State { Len = states[last].Len + 1 });
    int p = last;
    while (p != -1 && !states[p].Next.ContainsKey(c))
    {
        states[p].Next[c] = cur;
        p = states[p].Link;
    }
    if (p == -1)
    {
        states[cur].Link = 0;
    }
    else
    {
        int q = states[p].Next[c];
        if (states[p].Len + 1 == states[q].Len)
        {
            states[cur].Link = q;
        }
        else
        {
            int clone = states.Count;
            states.Add(new State
            {
                Len = states[p].Len + 1,
                Link = states[q].Link,
                Next = new Dictionary<char, int>(states[q].Next)
            });
            while (p != -1 && states[p].Next[c] == q)
            {
                states[p].Next[c] = clone;
                p = states[p].Link;
            }
            states[q].Link = states[cur].Link = clone;
        }
    }
    last = cur;
}
```

## Counting Distinct Substrings

Every distinct substring corresponds to a unique path from the initial
state, and each state contributes `len - link.len` distinct substrings
(the lengths it adds beyond its suffix link). Summing over all states
except the initial one gives the total number of distinct substrings.

```csharp
long distinct = 0;
for (int i = 1; i < states.Count; i++)
    distinct += states[i].Len - states[states[i].Link].Len;
```

## Worked Example: "ababa"

Building the SAM for `"ababa"` yields 6 states. Summing `len - link.len`
over the non-initial states gives:

| State | len | link.len | Contribution |
| ----- | --- | -------- | ------------ |
| 1 | 1 | 0 | 1 |
| 2 | 2 | 0 | 2 |
| 3 | 3 | 1 | 2 |
| 4 | 4 | 2 | 2 |
| 5 | 5 | 3 | 2 |

Total = 1 + 2 + 2 + 2 + 2 = **9 distinct substrings**: `a`, `b`, `ab`, `ba`,
`aba`, `bab`, `abab`, `baba`, `ababa`. A string of length 5 has 15
substrings with repetition, so 6 are duplicates — exactly what the SAM
collapses.

## What Else a SAM Can Do

- **Substring search:** walk the transitions for the pattern; reaching a
  state means the pattern occurs, in O(pattern length) time.
- **Longest common substring** of two strings: build the SAM for one,
  then walk the other through it, tracking the longest match.
- **Occurrence counts:** count how many end positions reach each state
  (propagate counts from longer states to their suffix links) to get the
  number of occurrences of every substring.
- **k-th lexicographic substring:** walk transitions in order, using the
  per-state substring counts.

## Key Takeaways

- A suffix automaton recognises all substrings of a string in at most
  `2n - 1` states.
- States group substrings by their end positions; suffix links connect a
  state to the state of its longest proper suffix.
- Building is incremental and amortised linear, using cloning when a
  transition target is "too long".
- The number of distinct substrings is the sum of `len - link.len` over
  all states — 9 for `"ababa"`.
- The same structure also powers substring search, longest common
  substring, and occurrence counting.
