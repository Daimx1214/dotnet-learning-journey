# The 0/1 Knapsack Problem (DSA)

You have a knapsack that can carry at most `W` weight, and `n` items, each with a weight `w[i]` and a value `v[i]`. Each item can be taken at most once — that is the "0/1" in the name. The goal is to pick a subset of items whose total weight is at most `W` and whose total value is as large as possible. It is the classic introduction to dynamic programming: the greedy choice (take the best value-per-weight first) fails, because a single heavy high-value item can block two lighter items that together are worth more.

## The Recurrence

Define `dp[i, w]` as the best value achievable using only the first `i` items with capacity `w`. For item `i` there are two choices: skip it, or take it (only possible when it fits). The recurrence is:

```
dp[i, w] = dp[i - 1, w]                          if w[i] > w        (does not fit)
dp[i, w] = max(dp[i - 1, w],                     (skip item i)
               dp[i - 1, w - w[i]] + v[i])       (take item i)
```

Base case: `dp[0, w] = 0` for every `w` — with zero items the best value is zero. Fill the table row by row; the answer is `dp[n, W]`. The table has `(n + 1) × (W + 1)` cells, so the running time is O(n × W). This is called pseudo-polynomial: it is polynomial in the numeric value of `W`, not in the number of bits needed to write `W` down.

## Worked Example

Items (weight, value): A(2, 3), B(3, 4), C(4, 5), D(5, 6). Capacity `W = 5`.

| dp[i, w] | w=0 | w=1 | w=2 | w=3 | w=4 | w=5 |
|----------|-----|-----|-----|-----|-----|-----|
| i=0 (none) | 0 | 0 | 0 | 0 | 0 | 0 |
| i=1 (A) | 0 | 0 | 3 | 3 | 3 | 3 |
| i=2 (B) | 0 | 0 | 3 | 4 | 4 | 7 |
| i=3 (C) | 0 | 0 | 3 | 4 | 5 | 7 |
| i=4 (D) | 0 | 0 | 3 | 4 | 5 | 7 |

Reading the table: `dp[2, 5] = 7` comes from taking A + B (2 + 3 = 5 weight, 3 + 4 = 7 value). Adding C or D afterwards never improves on 7 at capacity 5, so the answer is 7 with items A and B. To recover the chosen items, walk back from `dp[n, W]`: if `dp[i, w] != dp[i - 1, w]`, item `i` was taken, so record it and continue from `dp[i - 1, w - w[i]]`.

## C# Implementation

```csharp
public static int Knapsack(int[] weights, int[] values, int capacity)
{
    int n = weights.Length;
    int[,] dp = new int[n + 1, capacity + 1];

    for (int i = 1; i <= n; i++)
    {
        for (int w = 0; w <= capacity; w++)
        {
            if (weights[i - 1] > w)
            {
                dp[i, w] = dp[i - 1, w];
            }
            else
            {
                dp[i, w] = Math.Max(
                    dp[i - 1, w],
                    dp[i - 1, w - weights[i - 1]] + values[i - 1]);
            }
        }
    }

    return dp[n, capacity];
}

public static void Main()
{
    int[] weights = { 2, 3, 4, 5 };
    int[] values = { 3, 4, 5, 6 };
    Console.WriteLine(Knapsack(weights, values, 5)); // 7
}
```

## Space Optimization

Each row only depends on the previous row, so the table can be collapsed to a single array of size `W + 1`. The catch is the iteration order: the inner loop must run from high capacity down to low. If it ran upward, `dp[w - weight]` would already hold this item's updated value and the item could be "taken" twice — which turns the solution into the unbounded knapsack by accident.

```csharp
public static int KnapsackOptimized(int[] weights, int[] values, int capacity)
{
    int[] dp = new int[capacity + 1];

    for (int i = 0; i < weights.Length; i++)
    {
        // Iterate backwards so each item is used at most once.
        for (int w = capacity; w >= weights[i]; w--)
        {
            dp[w] = Math.Max(dp[w], dp[w - weights[i]] + values[i]);
        }
    }

    return dp[capacity];
}
```

The trade-off: the one-dimensional version uses O(W) space instead of O(n × W), but it can no longer reconstruct which items were taken — keep the full table when you need the item list.

## Key Takeaways

- 0/1 knapsack: each item is taken at most once; greedy by value-per-weight fails.
- `dp[i, w] = max(skip, take)` over the first `i` items with capacity `w`; answer is `dp[n, W]`.
- Time is O(n × W) — pseudo-polynomial in the capacity.
- A 1-D array with a backwards inner loop cuts space to O(W), but loses the ability to reconstruct the chosen items.
