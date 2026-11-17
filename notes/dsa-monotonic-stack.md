# Monotonic Stack (DSA)

A monotonic stack is a stack whose elements stay in sorted order — increasing or decreasing from bottom to top. The trick is in the push: before pushing a new element, pop everything that would break the order. Each element is pushed once and popped once, so any algorithm built on this pattern runs in O(n) time. Monotonic stacks answer "find the next element that is bigger/smaller than this one" questions, which show up constantly in array problems.

## Increasing vs Decreasing

A *monotonic increasing* stack keeps values rising from bottom to top — every new element pops all larger elements above it. A *monotonic decreasing* stack does the mirror image, popping smaller elements. Which one you need depends on the question: to find the next *greater* element, use a decreasing stack (the top is the first larger value); to find the next *smaller* element, use an increasing stack. The stack stores indices, not values, so you can compute distances and widths as well as the values themselves.

```csharp
// Next Greater Element: for each value, the first larger value to its right (-1 if none).
public static int[] NextGreaterElement(int[] nums)
{
    int[] result = new int[nums.Length];
    var stack = new Stack<int>(); // Indices, with strictly decreasing values.

    for (int i = nums.Length - 1; i >= 0; i--)
    {
        while (stack.Count > 0 && nums[stack.Peek()] <= nums[i])
            stack.Pop();

        result[i] = stack.Count == 0 ? -1 : nums[stack.Peek()];
        stack.Push(i);
    }
    return result;
}
```

Work it by hand on `[2, 1, 2, 4, 3]`: scanning right to left, 3 sees an empty stack (−1); 4 pops 3 (−1); 2 sees 4 on top (4); 1 sees 2 on top (2); the leading 2 pops 1 and the other 2, then sees 4 (4). The result is `[4, 2, 4, -1, -1]`. Note the `<=` in the pop condition: equal values are popped because they are not *greater*, and popping them is also what makes the stack strictly decreasing.

## Daily Temperatures

The Daily Temperatures problem (LeetCode 739) asks, for each day, how many days until a warmer temperature. It is the same pattern, but the stack stores indices so the answer is a distance, not a value:

```csharp
public static int[] DailyTemperatures(int[] temperatures)
{
    int[] answer = new int[temperatures.Length];
    var stack = new Stack<int>(); // Indices of days awaiting a warmer day.

    for (int i = 0; i < temperatures.Length; i++)
    {
        while (stack.Count > 0 && temperatures[i] > temperatures[stack.Peek()])
        {
            int prev = stack.Pop();
            answer[prev] = i - prev; // A warmer day has arrived for day 'prev'.
        }
        stack.Push(i);
    }
    return answer;
}
```

Scanning left to right this time, each day resolves every earlier day it is warmer than. For `[73, 74, 75, 71, 69, 72, 76, 73]` the answer is `[1, 1, 4, 2, 1, 1, 0, 0]`: day 0 waits 1 day for 74, day 2 waits 4 days until 76, and the last two days never see a warmer temperature. The direction of the scan (left-to-right vs right-to-left) is a free choice — pick whichever makes the bookkeeping simpler for the problem at hand.

## Largest Rectangle in Histogram

The same structure solves the largest-rectangle problem: given bar heights, find the largest rectangle that fits under the histogram. A bar's rectangle extends left and right until it hits a shorter bar — exactly the "next smaller element" on both sides. Popping a bar from an increasing stack means the current bar is the first shorter bar to its right, and the new stack top is the first shorter bar to its left, so the width is known the moment the bar is popped:

```csharp
public static int LargestRectangleArea(int[] heights)
{
    var stack = new Stack<int>(); // Indices, with strictly increasing heights.
    int maxArea = 0;

    for (int i = 0; i <= heights.Length; i++)
    {
        int currentHeight = i == heights.Length ? 0 : heights[i];
        while (stack.Count > 0 && heights[stack.Peek()] >= currentHeight)
        {
            int height = heights[stack.Pop()];
            int width = stack.Count == 0 ? i : i - stack.Peek() - 1;
            maxArea = Math.Max(maxArea, height * width);
        }
        stack.Push(i);
    }
    return maxArea;
}
```

For `[2, 1, 5, 6, 2, 3]` the answer is 10: the bars 5 and 6 form a 2-wide, 5-tall rectangle (2 × 5 = 10). The trailing zero-height sentinel iteration flushes the remaining bars out of the stack so none are forgotten. A runnable version of both algorithms, with these exact inputs, lives in `examples/MonotonicStackExamples.cs`.

## Why It Is O(n)

Every element is pushed onto the stack exactly once and popped at most once. The inner `while` loop looks expensive, but its total work across the whole run is bounded by the number of pops, which is at most n. The amortized cost per element is therefore constant, giving O(n) time and O(n) space in the worst case (a sorted input leaves every element on the stack at once). Compare that with the naive approach — scanning right from each element until a larger one appears — which is O(n²) on a decreasing input like `[5, 4, 3, 2, 1]`.

## Key Takeaways

- A monotonic stack keeps its elements sorted by popping everything that would break the order before each push.
- Use a decreasing stack for next-greater-element problems and an increasing stack for next-smaller-element problems.
- Store indices, not values, so answers can be distances (Daily Temperatures) or widths (largest rectangle).
- The `<=` vs `<` in the pop condition decides how equal values are treated — match it to whether "greater" means strictly greater.
- Each element is pushed and popped at most once, so the pattern is O(n) time and O(n) space, versus O(n²) for the naive scan.
- Daily Temperatures, next greater/smaller element, and largest rectangle in a histogram are all the same underlying pattern.
