# Counting Inversions with Merge Sort

An inversion in an array is a pair of indices (i, j) with i < j but arr[i] > arr[j]: the pair is out of order. Counting inversions measures how far an array is from sorted order. A sorted array has 0 inversions; a reversed array of n elements has n times (n - 1) / 2, the maximum possible.

## The Idea

The naive approach checks every pair, which is O(n^2). Merge sort counts inversions in O(n log n) as a side effect of sorting. The trick is in the merge step: when merging two sorted halves, if an element from the right half is smaller than the remaining elements in the left half, it forms an inversion with every one of those remaining elements, because all of them are larger than it and all of them come before it.

## Pseudocode

```text
CountInversions(arr):
    return MergeSortAndCount(arr, 0, n - 1)

MergeSortAndCount(arr, left, right):
    if left >= right: return 0
    mid = (left + right) / 2
    count = MergeSortAndCount(arr, left, mid)
    count += MergeSortAndCount(arr, mid + 1, right)
    count += MergeAndCount(arr, left, mid, right)
    return count

MergeAndCount(arr, left, mid, right):
    i = left, j = mid + 1, k = left, count = 0
    while i <= mid and j <= right:
        if arr[i] <= arr[j]:
            temp[k++] = arr[i++]
        else:
            temp[k++] = arr[j++]
            count += (mid - i + 1)   // arr[j] is smaller than all remaining left elements
    copy remaining elements, copy temp back to arr
    return count
```

The line `count += (mid - i + 1)` is the whole insight. Both halves are sorted, so if `arr[j] < arr[i]`, then `arr[j]` is also smaller than `arr[i+1]`, `arr[i+2]`, ..., `arr[mid]`, which is `mid - i + 1` elements. Each of those pairs is an inversion, and they are all counted at once.

## C# Sketch

```csharp
long CountInversions(int[] arr)
{
    int[] temp = new int[arr.Length];
    return MergeSortAndCount(arr, temp, 0, arr.Length - 1);
}

long MergeSortAndCount(int[] arr, int[] temp, int left, int right)
{
    if (left >= right) return 0;
    int mid = (left + right) / 2;
    long count = MergeSortAndCount(arr, temp, left, mid);
    count += MergeSortAndCount(arr, temp, mid + 1, right);
    count += MergeAndCount(arr, temp, left, mid, right);
    return count;
}

long MergeAndCount(int[] arr, int[] temp, int left, int mid, int right)
{
    int i = left, j = mid + 1, k = left;
    long count = 0;
    while (i <= mid && j <= right)
    {
        if (arr[i] <= arr[j])
        {
            temp[k++] = arr[i++];
        }
        else
        {
            temp[k++] = arr[j++];
            count += (mid - i + 1);
        }
    }
    while (i <= mid) temp[k++] = arr[i++];
    while (j <= right) temp[k++] = arr[j++];
    Array.Copy(temp, left, arr, left, right - left + 1);
    return count;
}
```

The count is a `long` because the maximum number of inversions for large n exceeds the range of `int`.

## Worked Example: [2, 4, 1, 3, 5]

By hand, the inversions are the pairs (i, j) with i < j and arr[i] > arr[j]: (2, 1), (4, 1), (4, 3). That is **3 inversions**. Now the merge sort trace, which must agree.

**Split:** [2, 4, 1] and [3, 5].

**Left half [2, 4, 1]:** split into [2, 4] and [1].
- [2, 4] is already sorted: 0 inversions. Merge [2] and [4]: 2 <= 4, no inversion.
- Merge [2, 4] with [1]: compare 2 and 1, 1 is smaller, so 1 jumps ahead of both 2 and 4. Inversions found: (2, 1) and (4, 1), which is 2, matching `mid - i + 1 = 2` (both left elements are still unmerged). Left half becomes [1, 2, 4] with 2 inversions.

**Right half [3, 5]:** already sorted: 0 inversions.

**Final merge:** merge [1, 2, 4] with [3, 5].
- 1 <= 3: take 1.
- 2 <= 3: take 2.
- 4 > 3: take 3. Remaining left elements larger than 3: only 4, so 1 inversion, (4, 3).
- 4 <= 5: take 4, then 5.

Total: 2 (left half) + 0 (right half) + 1 (final merge) = **3 inversions**, matching the hand count: (2, 1), (4, 1), (4, 3).

## Complexity

The recursion is identical to merge sort: O(n log n) time and O(n) extra space for the temporary array. The counting adds only O(1) work per merge comparison, so it does not change the complexity.

## When to Use It

- Measuring how sorted an array is, for example in ranking similarity (Kendall tau distance is the inversion count between two rankings).
- Collaborative filtering: comparing two users' preference orders.
- Any problem where "how many pairs are out of order" is the question, such as counting swaps needed to sort by adjacent swaps (the inversion count equals the minimum number of adjacent swaps).

## Key Takeaways

- An inversion is a pair (i, j) with i < j but arr[i] > arr[j]; [2, 4, 1, 3, 5] has exactly 3: (2, 1), (4, 1), (4, 3).
- Merge sort counts inversions in O(n log n): when a right-half element is smaller than a left-half element, it inverts with every remaining left element at once.
- The count added at that step is `mid - i + 1`, the number of unmerged elements left in the left half.
- A sorted array has 0 inversions and a reversed array of n elements has n times (n - 1) / 2.
- Use `long` for the count, since the maximum exceeds the `int` range for large arrays.
