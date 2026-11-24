# Kadane's Algorithm

Kadane's algorithm finds the maximum sum of any contiguous subarray in linear time, `O(n)`, using a single pass and constant extra space.

## The Idea

Walk the array once, keeping two values:

- `currentSum` — the best subarray sum ending at the current position.
- `maxSum` — the best subarray sum seen anywhere so far.

At each element, decide: extend the current subarray by adding this element, or start fresh from this element alone. Take whichever is larger.

```csharp
public static int MaxSubarraySum(int[] nums)
{
    int currentSum = nums[0];
    int maxSum = nums[0];

    for (int i = 1; i < nums.Length; i++)
    {
        currentSum = Math.Max(nums[i], currentSum + nums[i]);
        maxSum = Math.Max(maxSum, currentSum);
    }

    return maxSum;
}
```

## Hand-Verified Example

Input: `[-2, 1, -3, 4, -1, 2, 1, -5, 4]`

| i | nums[i] | currentSum | maxSum |
|---|---------|------------|--------|
| 0 | -2 | -2 | -2 |
| 1 | 1 | 1 | 1 |
| 2 | -3 | -2 | 1 |
| 3 | 4 | 4 | 4 |
| 4 | -1 | 3 | 4 |
| 5 | 2 | 5 | 5 |
| 6 | 1 | 6 | 6 |
| 7 | -5 | 1 | 6 |
| 8 | 4 | 5 | 6 |

Maximum subarray sum is **6**, from the subarray `[4, -1, 2, 1]` (indices 3–6): 4 + (-1) + 2 + 1 = 6.

## The All-Negative Edge Case

A common buggy variant initializes `currentSum` and `maxSum` to 0 and resets `currentSum` to 0 whenever it goes negative. On an all-negative array that variant returns 0 — the sum of the empty subarray — which is wrong when the problem requires a non-empty subarray.

The correct version initializes both values to `nums[0]` and never resets to 0. Verify with `[-3, -1, -2]`:

| i | nums[i] | currentSum | maxSum |
|---|---------|------------|--------|
| 0 | -3 | -3 | -3 |
| 1 | -1 | -1 | -1 |
| 2 | -2 | -2 | -1 |

Result: **-1**, the single element `-1`. That is the least-bad subarray, and it is the correct answer for the non-empty formulation.

If the problem allows the empty subarray (answer 0 when everything is negative), say so explicitly and use the reset-to-zero variant. The two versions answer different questions.

## Tracking the Subarray Itself

To return the subarray, not just its sum, track where the current run started:

```csharp
public static (int Sum, int Start, int End) MaxSubarray(int[] nums)
{
    int currentSum = nums[0], maxSum = nums[0];
    int start = 0, bestStart = 0, bestEnd = 0;

    for (int i = 1; i < nums.Length; i++)
    {
        if (nums[i] > currentSum + nums[i])
        {
            currentSum = nums[i];
            start = i;
        }
        else
        {
            currentSum += nums[i];
        }

        if (currentSum > maxSum)
        {
            maxSum = currentSum;
            bestStart = start;
            bestEnd = i;
        }
    }

    return (maxSum, bestStart, bestEnd);
}
```

On `[-2, 1, -3, 4, -1, 2, 1, -5, 4]` this returns `(6, 3, 6)`.

## Complexity and Limits

Time is `O(n)`, space is `O(1)`. The algorithm is a textbook case of dynamic programming with a one-element state: the best answer ending at position `i` depends only on the best answer ending at `i - 1`.

Kadane's solves the 1-D maximum subarray problem only. The 2-D version (maximum submatrix) is `O(n^3)` by applying Kadane's to compressed columns.

## Key Takeaways

- One pass, two variables: extend the running subarray or restart at the current element.
- Initialize to `nums[0]`, not 0, so all-negative input returns the largest (least negative) element: -1 for `[-3, -1, -2]`.
- Verified: `[-2, 1, -3, 4, -1, 2, 1, -5, 4]` gives 6, from `[4, -1, 2, 1]`.
- The reset-to-zero variant answers a different question (empty subarray allowed) — pick the variant that matches the problem statement.
- Time `O(n)`, space `O(1)`.
