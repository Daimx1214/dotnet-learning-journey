# Coin Change Problem

The coin change problem comes in two flavours that look similar but need different DP tables: (1) the minimum number of coins needed to make an amount, and (2) the number of distinct ways to make that amount. Both are solved with dynamic programming, and both are classic interview problems because they expose the difference between greedy and DP thinking.

The setting: you have coin denominations (for example US coins `[1, 5, 10, 25]`) and an amount to make. You have an unlimited supply of each coin. The question is either "fewest coins?" or "how many combinations?".

## Minimum coins — the DP

Let `dp[amount]` be the minimum number of coins needed to make that amount. The recurrence tries every coin as the last coin used: `dp[amount] = 1 + min(dp[amount - coin])` over all coins not larger than the amount. The base case is `dp[0] = 0` — zero coins make amount zero.

```csharp
// Minimum number of coins to make `amount`, or -1 if impossible.
public static int MinCoins(int[] coins, int amount)
{
    int[] dp = new int[amount + 1];
    Array.Fill(dp, int.MaxValue);
    dp[0] = 0;

    for (int a = 1; a <= amount; a++)
    {
        foreach (int coin in coins)
        {
            if (coin <= a && dp[a - coin] != int.MaxValue)
            {
                dp[a] = Math.Min(dp[a], dp[a - coin] + 1);
            }
        }
    }

    return dp[amount] == int.MaxValue ? -1 : dp[amount];
}
```

### Worked example: coins [1, 5, 10, 25], amount 63

Greedy (take the biggest coin first) gives 25 + 25 + 10 + 1 + 1 + 1 = 6 coins, and here DP agrees: `dp[63] = 6`. The DP table builds up from small amounts — `dp[5] = 1`, `dp[10] = 1`, `dp[25] = 1`, `dp[30] = 2` (25 + 5), `dp[50] = 2` (25 + 25), `dp[60] = 3` (25 + 25 + 10), and finally `dp[63] = dp[62] + 1 = 6` via the last coin being 1. The answer is 6 coins: two quarters, one dime, and three pennies.

```csharp
int[] usCoins = { 1, 5, 10, 25 };
Console.WriteLine(MinCoins(usCoins, 63));  // 6
```

## Counting ways — a different table

Counting the number of combinations needs a different recurrence. `dp[amount]` is now the number of ways to make that amount, and the loops must be ordered coin-first, amount-second — otherwise you count permutations (orderings) instead of combinations.

```csharp
// Number of distinct combinations (order of coins does not matter).
public static int CountWays(int[] coins, int amount)
{
    int[] dp = new int[amount + 1];
    dp[0] = 1;  // exactly one way to make amount 0: use no coins

    foreach (int coin in coins)          // coins on the OUTSIDE
    {
        for (int a = coin; a <= amount; a++)
        {
            dp[a] += dp[a - coin];
        }
    }

    return dp[amount];
}
```

The loop order is the subtle part. With coins on the outside, each coin is "introduced" once and all amounts are built with it, so the combination `{1, 5}` is counted once. If you swap the loops (amount outside, coin inside), `{1, 5}` and `{5, 1}` are counted as two different ways, which is wrong for combinations. For amount 63 with US coins, `CountWays` returns 73 combinations.

## Greedy vs DP — why greedy fails

For US coins, greedy works: always taking the largest coin first gives the minimum count. That is a special property of "canonical" coin systems, and it is easy to assume it holds generally. It does not.

Counterexample: coins `[1, 3, 4]`, amount 6. Greedy takes the largest coin first: 4, then 1, then 1 — three coins. But the optimal answer is two coins: 3 + 3. Greedy fails because taking the 4 "uses up" the amount in a way that blocks the better pairing.

```text
Coins [1, 3, 4], amount 6:
  Greedy:  4 + 1 + 1        -> 3 coins
  Optimal: 3 + 3            -> 2 coins   (DP finds this)

Coins [1, 5, 10, 25], amount 63:
  Greedy:  25 + 25 + 10 + 1 + 1 + 1 -> 6 coins (optimal here)
```

The DP never makes this mistake because it considers every coin as the last one used and takes the minimum over all of them. Whenever the coin system is arbitrary — a game currency, a fictional denomination set, an interview problem — use DP, not greedy. Greedy is only safe when you have proven (or been told) that the coin system is canonical.

A greedy solution is still worth knowing for the canonical case because it is O(n) and trivially simple, but always state the assumption: it is correct for US coins, not in general.

## Reconstructing the coins used

The min-coins DP above only returns the count. To also list which coins make up the answer, keep a `choice` array that records, for each amount, which coin was used last in the optimal solution — then walk backwards from the target amount.

```csharp
// Returns the list of coins in one optimal solution, or null if impossible.
public static List<int>? MinCoinsUsed(int[] coins, int amount)
{
    int[] dp = new int[amount + 1];
    int[] choice = new int[amount + 1];  // last coin used for each amount
    Array.Fill(dp, int.MaxValue);
    dp[0] = 0;

    for (int a = 1; a <= amount; a++)
    {
        foreach (int coin in coins)
        {
            if (coin <= a && dp[a - coin] != int.MaxValue
                && dp[a - coin] + 1 < dp[a])
            {
                dp[a] = dp[a - coin] + 1;
                choice[a] = coin;
            }
        }
    }

    if (dp[amount] == int.MaxValue)
        return null;

    var used = new List<int>();
    int remaining = amount;
    while (remaining > 0)
    {
        used.Add(choice[remaining]);
        remaining -= choice[remaining];
    }
    return used;  // e.g. [25, 25, 10, 1, 1, 1] for amount 63
}
```

This is the same idea as reconstructing the LCS from its DP table: the table stores the optimal value, and a small amount of extra bookkeeping lets you recover the decisions that produced it. The extra array costs O(amount) space — the same order as the table itself.

## Unbounded vs 0/1 coin change

The versions above assume an unlimited supply of each coin (the unbounded knapsack variant). If each coin can be used at most once — you have one 25-cent coin, one 10-cent coin, and so on — the problem changes: it becomes the 0/1 variant, and the DP must iterate amounts backwards for each coin so a coin is never reused within the same pass.

```csharp
// 0/1 variant: each coin usable at most once. Counts ways with given coins.
public static int CountWaysSingleUse(int[] coins, int amount)
{
    int[] dp = new int[amount + 1];
    dp[0] = 1;

    foreach (int coin in coins)
    {
        // Backwards: dp[a - coin] still holds the value from before
        // this coin was introduced, so the coin cannot be counted twice.
        for (int a = amount; a >= coin; a--)
        {
            dp[a] += dp[a - coin];
        }
    }

    return dp[amount];
}
```

The loop direction is the whole difference: forward iteration reuses the current coin (unbounded), backward iteration does not (0/1). Mixing the two up is one of the most common DP bugs, so always ask first: "can I reuse this item?"

## Complexity

Both DP solutions run in **O(amount × number of coins)** time and use **O(amount)** space. The min-coins version can also be written as a 2D table, but the 1D version above is enough and uses less memory.

## Key Takeaways

- Coin change has two versions: minimum coins (`dp[a] = 1 + min(dp[a - coin])`) and counting combinations (coin-outer loop, `dp[a] += dp[a - coin]`).
- With US coins `[1, 5, 10, 25]`, amount 63 needs 6 coins (25 + 25 + 10 + 1 + 1 + 1); there are 73 combinations.
- For counting ways, the loop order matters: coins on the outside counts combinations, amount on the outside counts permutations.
- Greedy fails on non-canonical systems: coins `[1, 3, 4]`, amount 6 — greedy gives 3 coins (4 + 1 + 1), optimal is 2 (3 + 3).
- DP is correct for any coin system because it considers every coin as the last one used; greedy is only safe for proven canonical systems like US coins.
- Both DP solutions are O(amount × coins) time and O(amount) space; return -1 for the min-coins version when the amount cannot be made.
