# Bitmask Dynamic Programming

When a problem asks for the best way to visit a small set of items — every
subset matters, and `n` is at most about 20 — a **bitmask** is the right tool.
An integer's bits represent a subset: bit `i` is 1 when item `i` is included.
With `n` items there are `2^n` subsets, and DP over subsets turns exponential
brute force into `O(n^2 · 2^n)`.

## Representing Subsets as Bitmasks

```csharp
int n = 4;
int full = (1 << n) - 1;      // 0b1111 — every city visited
int mask = 0b0101;            // cities 0 and 2 visited
bool hasCity2 = (mask & (1 << 2)) != 0;   // true
int addCity3  = mask | (1 << 3);          // 0b1101
int popcount  = int.PopCount((uint)mask); // 2 — number of set bits
```

Bit operations make subset manipulation O(1): add an item with `|`, test
membership with `&`, iterate over set bits by stripping the lowest set bit.

## Travelling Salesman DP over Subsets

The classic application is the travelling salesman problem (TSP): start at
city 0, visit every city once, return home, minimize total distance.

State: `dp[mask][i]` = cheapest cost of a route that starts at city 0, visits
exactly the cities in `mask`, and ends at city `i`.

Transition: extend a route ending at `j` with one more city `i`:

```csharp
int n = dist.GetLength(0);
int full = (1 << n) - 1;
const int INF = int.MaxValue / 2;
int[,] dp = new int[1 << n, n];
// initialize all entries to INF, then dp[1 << 0, 0] = 0

for (int mask = 0; mask <= full; mask++)
for (int j = 0; j < n; j++)
{
    if ((mask & (1 << j)) == 0 || dp[mask, j] == INF) continue;
    for (int i = 0; i < n; i++)
    {
        if ((mask & (1 << i)) != 0) continue; // already visited
        int next = mask | (1 << i);
        dp[next, i] = Math.Min(dp[next, i], dp[mask, j] + dist[j, i]);
    }
}

int answer = INF;
for (int i = 1; i < n; i++)
    answer = Math.Min(answer, dp[full, i] + dist[i, 0]);
```

The answer adds the return leg to city 0. Complexity is O(n²·2ⁿ) time and
O(n·2ⁿ) memory — fine for n ≤ 20, hopeless for n = 30.

## Worked Example (4 Cities)

Distance matrix (symmetric, 0 on the diagonal):

|     | 0 | 1 | 2 | 3 |
|-----|---|---|---|---|
| **0** | 0 | 10 | 15 | 20 |
| **1** | 10 | 0 | 35 | 25 |
| **2** | 15 | 35 | 0 | 30 |
| **3** | 20 | 25 | 30 | 0 |

DP table, `dp[mask][i]` (mask shown as the set of visited cities):

| mask | i = 1 | i = 2 | i = 3 |
|------|-------|-------|-------|
| {0,1} | 10 | — | — |
| {0,2} | — | 15 | — |
| {0,3} | — | — | 20 |
| {0,1,2} | 50 | 45 | — |
| {0,1,3} | 45 | — | 35 |
| {0,2,3} | — | 50 | 45 |
| {0,1,2,3} | 70 | 65 | 75 |

Spot-check one row: `dp[{0,1,2}][2]` = `dp[{0,1}][1] + dist(1,2)` =
10 + 35 = 45. And `dp[{0,1,2,3}][2]` = min(45 + 35, 35 + 30) = 65.

Closing the tours: 70 + 10 = 80 (via city 1), 65 + 15 = 80 (via city 2),
75 + 20 = 95 (via city 3). **Minimum tour cost = 80**, achieved by
0 → 1 → 3 → 2 → 0 (10 + 25 + 30 + 15) and by 0 → 2 → 3 → 1 → 0
(15 + 30 + 25 + 10). Every other tour costs 95, verified by enumerating all
six permutations.

## When to Reach for Bitmask DP

- **TSP and Hamiltonian path** — the textbook case above.
- **Assignment problems:** `dp[mask]` = best cost of assigning the first
  `popcount(mask)` workers to the jobs in `mask`.
- **Set cover / subset selection** with n ≤ 20: brute force over all masks is
  only about a million states.
- **Bitmask as a state in BFS/DFS:** game states, tile puzzles, small
  constraint problems.

The limit is hard: `2^20` is about a million states, `2^25` is 33 million.
If n is bigger, bitmask DP is the wrong tool — look for a greedy or
approximation approach instead.

## Key Takeaways

- A bitmask packs a subset into one integer; `|`, `&` and `PopCount` make
  subset operations O(1).
- TSP DP over subsets runs in O(n²·2ⁿ): `dp[mask][i]` is the cheapest route
  visiting exactly `mask` and ending at `i`.
- The worked 4-city example gives a verified minimum tour cost of **80**
  (tours 0-1-3-2-0 and 0-2-3-1-0).
- Bitmask DP is practical up to about n = 20; beyond that the `2^n` states
  explode.
