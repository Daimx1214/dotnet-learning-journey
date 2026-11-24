# Longest Increasing Subsequence (LIS)

The Longest Increasing Subsequence problem asks for the longest subsequence (not necessarily contiguous) of an array that is strictly increasing. It is a classic dynamic programming problem with two standard solutions: an `O(n^2)` DP that is easy to reason about, and an `O(n log n)` patience-sorting method that is the one to use in practice.

## The O(n^2) Dynamic Programming Solution

Define `dp[i]` as the length of the longest increasing subsequence ending exactly at index `i`. For each `i`, look at all earlier `j < i` with `nums[j] < nums[i]` and take `dp[i] = 1 + max(dp[j])`, defaulting to 1 when no such `j` exists. The answer is the maximum over all `dp[i]`.

Worked example on `[10, 9, 2, 5, 3, 7, 101, 18]`:

```text
i:     0   1   2   3   4   5   6    7
nums:  10  9   2   5   3   7   101  18
dp:    1   1   1   2   2   3   4    4
```

`dp[3] = 2` (2, 5), `dp[5] = 3` (2, 3, 7), `dp[6] = 4` (2, 3, 7, 101), `dp[7] = 4` (2, 3, 7, 18). The LIS length is **4**. One actual subsequence is `[2, 3, 7, 18]`; another is `[2, 5, 7, 101]`.

```csharp
// O(n^2) DP: dp[i] = LIS length ending at index i.
static int LengthOfLIS_DP(int[] nums)
{
    int n = nums.Length;
    if (n == 0) return 0;
    int[] dp = new int[n];
    Array.Fill(dp, 1);
    int best = 1;
    for (int i = 1; i < n; i++)
    {
        for (int j = 0; j < i; j++)
        {
            if (nums[j] < nums[i])
                dp[i] = Math.Max(dp[i], dp[j] + 1);
        }
        best = Math.Max(best, dp[i]);
    }
    return best;
}
```

## The O(n log n) Patience Sorting Solution

Maintain a list `tails`, where `tails[k]` is the smallest possible tail value of an increasing subsequence of length `k + 1`. For each number `x`, binary-search `tails` for the first element `>= x` (lower bound) and replace it with `x`; if `x` is larger than everything, append it. The length of `tails` at the end is the LIS length.

Trace on `[10, 9, 2, 5, 3, 7, 101, 18]`:

```text
x=10  -> tails = [10]
x=9   -> tails = [9]
x=2   -> tails = [2]
x=5   -> tails = [2, 5]
x=3   -> tails = [2, 3]
x=7   -> tails = [2, 3, 7]
x=101 -> tails = [2, 3, 7, 101]
x=18  -> tails = [2, 3, 7, 18]   (101 replaced by 18)
```

Final length is 4. Note that `tails` itself is not necessarily a real subsequence (here it happens to be `[2, 3, 7, 18]`, which is valid, but in general the intermediate values may come from different positions). Only its length is guaranteed correct.

```csharp
// O(n log n): tails[k] = smallest tail of an increasing subsequence of length k+1.
static int LengthOfLIS(int[] nums)
{
    var tails = new List<int>();
    foreach (int x in nums)
    {
        int lo = 0, hi = tails.Count;
        while (lo < hi) // lower_bound: first tails[i] >= x
        {
            int mid = lo + (hi - lo) / 2;
            if (tails[mid] < x) lo = mid + 1;
            else hi = mid;
        }
        if (lo == tails.Count) tails.Add(x);
        else tails[lo] = x;
    }
    return tails.Count;
}
```

## Strictly vs Non-Decreasing

The choice of lower bound vs upper bound decides the variant. Using lower bound (first `>= x`) replaces equal values, which enforces strictly increasing subsequences: duplicates cannot extend the sequence. Using upper bound (first `> x`) lets equal values extend it, giving the longest non-decreasing subsequence. On `[2, 2, 2]`, lower bound gives length 1, upper bound gives length 3. Picking the wrong bound is the most common bug in LIS implementations — decide first, then code it.

## Complexity Comparison

For `n = 100,000`, the `O(n^2)` DP needs about `10^10` comparisons (far too slow), while the `O(n log n)` method needs about `100,000 * 17 = 1.7 * 10^6` operations — it finishes in milliseconds. The DP version is still worth knowing: it reconstructs an actual subsequence easily by storing parent pointers, while the patience method needs extra bookkeeping (store the predecessor index of each element when it is placed) to recover one.

## Key Takeaways

- LIS on `[10, 9, 2, 5, 3, 7, 101, 18]` has length 4 (for example `[2, 3, 7, 18]`).
- The `O(n^2)` DP defines `dp[i]` as the LIS ending at `i` and takes the max over earlier smaller elements.
- The `O(n log n)` patience method keeps `tails[k]` as the smallest tail of a length-`k+1` subsequence and binary-searches the insertion point.
- Lower bound gives strictly increasing; upper bound gives non-decreasing — mixing them up is the classic bug.
- `tails` only guarantees the correct length, not a valid subsequence; add parent pointers if the actual sequence is needed.
