# Trapping Rain Water

Given an array of bar heights (each element is the height of a vertical bar of width 1), compute how much rain water is trapped between the bars after it rains. Water collects in the valleys: at any position, the water level is limited by the shorter of the tallest bar to the left and the tallest bar to the right, minus the bar's own height.

This is a classic two-pointer problem. The naive approach precomputes the tallest bar on each side for every position; the two-pointer version gets the same answer with constant extra space, and it is the version interviewers usually want.

## The key idea: prefix and suffix maximums

At position `i`, water can only be trapped if there are taller bars on both sides. The water level at `i` is:

```text
water[i] = max(0, min(leftMax[i], rightMax[i]) - height[i])
```

where `leftMax[i]` is the tallest bar at or before `i` and `rightMax[i]` is the tallest bar at or after `i`. The total is the sum over all positions. Computing both max arrays takes O(n) time and O(n) space — correct, but the arrays are not actually needed.

```csharp
// O(n) time, O(n) space: the straightforward version with max arrays.
public static int TrapWithArrays(int[] height)
{
    int n = height.Length;
    if (n < 3) return 0;

    int[] leftMax = new int[n];
    int[] rightMax = new int[n];

    leftMax[0] = height[0];
    for (int i = 1; i < n; i++)
        leftMax[i] = Math.Max(leftMax[i - 1], height[i]);

    rightMax[n - 1] = height[n - 1];
    for (int i = n - 2; i >= 0; i--)
        rightMax[i] = Math.Max(rightMax[i + 1], height[i]);

    int total = 0;
    for (int i = 0; i < n; i++)
        total += Math.Max(0, Math.Min(leftMax[i], rightMax[i]) - height[i]);

    return total;
}
```

## The two-pointer solution

The insight: you do not need both maximums at once. Move two pointers inward from the ends, always advancing the side whose current max is smaller. If `leftMax < rightMax`, then the water level on the left pointer is decided by `leftMax` alone — the right side is guaranteed to be at least as tall, because `rightMax` is the tallest bar seen from the right and it is already bigger than `leftMax`. So you can compute the trapped water at the left pointer immediately, then move it inward. The mirror logic applies on the right side.

```csharp
// O(n) time, O(1) extra space: two pointers, one pass.
public static int Trap(int[] height)
{
    int n = height.Length;
    if (n < 3) return 0;

    int left = 0, right = n - 1;
    int leftMax = 0, rightMax = 0;
    int total = 0;

    while (left < right)
    {
        if (height[left] < height[right])
        {
            // Left side is the limiting side: its water level is leftMax.
            if (height[left] >= leftMax)
                leftMax = height[left];       // new tallest on the left
            else
                total += leftMax - height[left]; // trapped above this bar
            left++;
        }
        else
        {
            // Right side is the limiting side (or they are equal).
            if (height[right] >= rightMax)
                rightMax = height[right];
            else
                total += rightMax - height[right];
            right--;
        }
    }

    return total;
}
```

Why the comparison `height[left] < height[right]` decides the side: whichever pointer is at the shorter bar, that bar's side has the smaller maximum so far, and the other side is guaranteed to have a bar at least as tall as the current pointer's height — so the water above the shorter bar is fully determined by its own side's max. Moving the taller side's pointer would be premature, because its water level depends on a max we have not finished discovering yet.

## Worked example

Input: `[0, 1, 0, 2, 1, 0, 1, 3, 2, 1, 2, 1]` — expected answer **6**.

```text
Heights:  0  1  0  2  1  0  1  3  2  1  2  1
Water:    0  0  1  0  1  2  1  0  0  1  0  0   -> total 6

Position by position (min(leftMax, rightMax) - height):
  i=2:  min(1, 3) - 0 = 1
  i=4:  min(2, 3) - 1 = 1
  i=5:  min(2, 3) - 0 = 2
  i=6:  min(2, 3) - 1 = 1
  i=9:  min(3, 2) - 1 = 1
  All other positions trap 0.
  Total = 1 + 1 + 2 + 1 + 1 = 6
```

The deepest pool is at index 5: it sits between the height-2 bar at index 3 and the height-3 bar at index 7, so it holds 2 units. The bar at index 3 itself traps nothing (it is a local peak), and the ends trap nothing because water needs a taller bar on both sides.

## Edge cases

- Fewer than 3 bars: no valley can exist, answer is 0.
- Strictly increasing or strictly decreasing heights: no valleys, answer is 0.
- All bars the same height: flat surface, answer is 0.
- A single tall bar in the middle with low bars on both sides traps nothing — a valley needs a wall on both sides.

## Complexity

The two-pointer solution runs in **O(n) time** (a single pass) and **O(1) extra space** (two pointers and two running maximums). The array version is also O(n) time but needs O(n) space for the two max arrays.

## Key Takeaways

- Water at position `i` is `min(leftMax, rightMax) - height[i]`, floored at 0; the total is the sum over all positions.
- The two-pointer solution always advances the side with the smaller current bar, because that side's water level is already fully determined by its own max.
- Worked example `[0,1,0,2,1,0,1,3,2,1,2,1]` traps 6 units, with the deepest pool (2 units) at index 5.
- The prefix/suffix max version is easier to understand and equally fast, but uses O(n) space; the two-pointer version uses O(1) space.
- Fewer than 3 bars, monotonic heights, or flat heights all trap 0 — a valley needs a taller bar on both sides.
- Time is O(n) and extra space is O(1) for the two-pointer version.
