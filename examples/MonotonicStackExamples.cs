// Monotonic Stack Examples — a stack whose elements stay in sorted order.
//
// A monotonic stack keeps its elements increasing (or decreasing) from bottom
// to top. Each element is pushed and popped at most once, so algorithms built
// on it run in O(n) time. Two classic uses are shown below:
//   1. Next Greater Element — for each value, the first larger value to its right.
//   2. Largest Rectangle in Histogram — the biggest rectangle under the bars.
// Run with: dotnet run

int[] numbers = [2, 1, 2, 4, 3];
int[] nextGreater = NextGreaterElement(numbers);
Console.WriteLine($"Next greater element of [{string.Join(", ", numbers)}]: [{string.Join(", ", nextGreater)}]");

int[] histogram = [2, 1, 5, 6, 2, 3];
int largest = LargestRectangleArea(histogram);
Console.WriteLine($"Largest rectangle in [{string.Join(", ", histogram)}]: {largest}");

static int[] NextGreaterElement(int[] nums)
{
    int[] result = new int[nums.Length];
    var stack = new Stack<int>(); // Indices, with strictly decreasing values.

    // Scan right to left: the stack always holds candidates to the right.
    for (int i = nums.Length - 1; i >= 0; i--)
    {
        // Pop everything that cannot be the answer: smaller or equal values
        // are blocked by nums[i] for every element further left.
        while (stack.Count > 0 && nums[stack.Peek()] <= nums[i])
            stack.Pop();

        result[i] = stack.Count == 0 ? -1 : nums[stack.Peek()];
        stack.Push(i);
    }

    return result;
}

static int LargestRectangleArea(int[] heights)
{
    var stack = new Stack<int>(); // Indices, with strictly increasing heights.
    int maxArea = 0;

    // The extra iteration with height 0 flushes every bar left in the stack.
    for (int i = 0; i <= heights.Length; i++)
    {
        int currentHeight = i == heights.Length ? 0 : heights[i];

        while (stack.Count > 0 && heights[stack.Peek()] >= currentHeight)
        {
            int height = heights[stack.Pop()];
            // Width spans from the new stack top (exclusive) to i (exclusive).
            int width = stack.Count == 0 ? i : i - stack.Peek() - 1;
            maxArea = Math.Max(maxArea, height * width);
        }

        stack.Push(i);
    }

    return maxArea;
}
