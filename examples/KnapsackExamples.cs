using System;

/// <summary>
/// Three knapsack variants, each solved with the classic approach:
/// 0/1 knapsack with DP, unbounded knapsack with DP, and
/// fractional knapsack with a greedy value-density sort.
/// </summary>
class Program
{
    // 0/1 knapsack: each item can be taken at most once.
    // DP table: dp[i, w] = best value using the first i items with capacity w.
    static int Knapsack01(int[] weights, int[] values, int capacity)
    {
        int n = weights.Length;
        int[,] dp = new int[n + 1, capacity + 1];
        for (int i = 1; i <= n; i++)
        {
            for (int w = 0; w <= capacity; w++)
            {
                dp[i, w] = dp[i - 1, w]; // skip item i - 1
                if (weights[i - 1] <= w)
                {
                    dp[i, w] = Math.Max(dp[i, w],
                        dp[i - 1, w - weights[i - 1]] + values[i - 1]);
                }
            }
        }
        return dp[n, capacity];
    }

    // Unbounded knapsack: each item can be taken any number of times.
    // 1-D DP: dp[w] = best value for capacity w, items reused freely.
    static int KnapsackUnbounded(int[] weights, int[] values, int capacity)
    {
        int[] dp = new int[capacity + 1];
        for (int w = 0; w <= capacity; w++)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= w)
                    dp[w] = Math.Max(dp[w], dp[w - weights[i]] + values[i]);
            }
        }
        return dp[capacity];
    }

    // Fractional knapsack: items can be split, so take them in order of
    // value per unit weight until the capacity is full.
    static double KnapsackFractional(int[] weights, int[] values, int capacity)
    {
        int n = weights.Length;
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        // Sort by value density, highest first.
        Array.Sort(order, (a, b) =>
            ((double)values[b] / weights[b]).CompareTo((double)values[a] / weights[a]));

        double total = 0;
        int remaining = capacity;
        foreach (int i in order)
        {
            if (remaining == 0) break;
            int take = Math.Min(weights[i], remaining);
            total += (double)values[i] * take / weights[i];
            remaining -= take;
        }
        return total;
    }

    static void Main()
    {
        // 0/1 knapsack: weights [2,3,4,5], values [3,4,5,6], capacity 5.
        // Best: items of weight 2 and 3 -> value 3 + 4 = 7.
        int value01 = Knapsack01(
            new[] { 2, 3, 4, 5 }, new[] { 3, 4, 5, 6 }, 5);
        Console.WriteLine($"0/1 knapsack max value: {value01}"); // Expected: 7

        // Unbounded knapsack: weights [1,3,4], values [1,4,5], capacity 6.
        // Best: two weight-3 items -> value 4 + 4 = 8.
        int valueUnbounded = KnapsackUnbounded(
            new[] { 1, 3, 4 }, new[] { 1, 4, 5 }, 6);
        Console.WriteLine($"Unbounded knapsack max value: {valueUnbounded}"); // Expected: 8

        // Fractional knapsack: weights [10,20,30], values [60,100,120], capacity 50.
        // Take all of items 1 and 2, plus 20/30 of item 3 -> 60 + 100 + 80 = 240.
        double valueFractional = KnapsackFractional(
            new[] { 10, 20, 30 }, new[] { 60, 100, 120 }, 50);
        Console.WriteLine($"Fractional knapsack max value: {valueFractional}"); // Expected: 240
    }
}
