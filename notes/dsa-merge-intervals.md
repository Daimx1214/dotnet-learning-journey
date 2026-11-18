# Merge Intervals

Given a list of intervals `[start, end]`, merge all overlapping intervals into the smallest set of non-overlapping intervals that covers the same ranges. It is one of the most useful interval problems because the same sort-then-scan pattern solves a family of related problems: inserting an interval, counting overlaps, finding free time in a calendar, and merging meeting times.

Two intervals overlap when the start of the later one is less than or equal to the end of the earlier one (assuming the intervals are sorted by start). When they overlap, the merged interval runs from the earlier start to the later of the two ends.

## The algorithm: sort, then scan once

1. Sort the intervals by start time (and by end time as a tiebreaker).
2. Walk through the sorted list, keeping a `current` merged interval.
3. If the next interval starts after `current` ends, there is no overlap: push `current` to the result and start a new `current`.
4. Otherwise they overlap: extend `current`'s end to `Math.Max(current.End, next.End)` and continue.

```csharp
public record Interval(int Start, int End);

// Merges all overlapping intervals. Input is not modified.
public static List<Interval> Merge(List<Interval> intervals)
{
    if (intervals.Count <= 1)
        return new List<Interval>(intervals);

    var sorted = intervals
        .OrderBy(i => i.Start)
        .ThenBy(i => i.End)
        .ToList();

    var merged = new List<Interval>();
    Interval current = sorted[0];

    foreach (Interval next in sorted.Skip(1))
    {
        if (next.Start > current.End)
        {
            // No overlap: current is finished, start a new one.
            merged.Add(current);
            current = next;
        }
        else
        {
            // Overlap: extend the current interval.
            current = new Interval(current.Start,
                Math.Max(current.End, next.End));
        }
    }

    merged.Add(current);
    return merged;
}
```

## Worked example

```text
Input:  [1,3], [2,6], [8,10], [15,18]

Sorted by start (already sorted):
  [1,3]  -> current = [1,3]
  [2,6]  -> 2 <= 3, overlap -> current = [1, max(3,6)] = [1,6]
  [8,10] -> 8 > 6, no overlap -> push [1,6], current = [8,10]
  [15,18]-> 15 > 10, no overlap -> push [8,10], current = [15,18]
  End    -> push [15,18]

Output: [1,6], [8,10], [15,18]
```

A second example shows why the `Math.Max` on the end matters: `[1,4], [2,3]` — the second interval is entirely inside the first, so the merged interval stays `[1,4]` instead of shrinking to `[1,3]`. A common bug is writing `current.End = next.End` instead of taking the max.

## Complexity

Sorting dominates: **O(n log n)** time, where n is the number of intervals. The scan itself is linear, O(n), and the result list uses O(n) space in the worst case (no overlaps at all). If the input is already sorted by start, the whole problem is O(n).

## Variants

### Insert interval

Insert one new interval into a list of already-sorted, non-overlapping intervals, merging as needed. You do not need to re-sort: walk the list once, copying intervals that end before the new one starts, merging everything that overlaps it, then copying the rest.

```csharp
// Inserts newInterval into sorted, non-overlapping intervals.
public static List<Interval> Insert(List<Interval> intervals, Interval newInterval)
{
    var result = new List<Interval>();
    int i = 0;

    // All intervals ending before the new one starts: copy as-is.
    while (i < intervals.Count && intervals[i].End < newInterval.Start)
        result.Add(intervals[i++]);

    // Merge every interval that overlaps the new one.
    while (i < intervals.Count && intervals[i].Start <= newInterval.End)
    {
        newInterval = new Interval(
            Math.Min(newInterval.Start, intervals[i].Start),
            Math.Max(newInterval.End, intervals[i].End));
        i++;
    }
    result.Add(newInterval);

    // Everything after: copy as-is.
    while (i < intervals.Count)
        result.Add(intervals[i++]);

    return result;
}
```

Example: intervals `[1,3], [6,9]`, insert `[2,5]` → `[1,5], [6,9]`.

### Counting overlapping intervals

To count how many intervals overlap at the busiest point (for example, the minimum number of meeting rooms needed), sort all start times and all end times separately and sweep through them with two pointers, incrementing a counter at each start and decrementing at each end. The maximum counter value is the answer. This runs in O(n log n) and is the classic "meeting rooms II" problem.

### Intervals that only touch

Whether `[1,4]` and `[4,5]` should merge depends on the problem's definition of overlap. The code above merges them, because `4 <= 4` counts as overlapping. If your problem treats touching intervals as separate (for example, a meeting ending at 4 and another starting at 4 do not conflict), change the condition to `next.Start > current.End` for merging only when strictly overlapping — i.e. use `>=` in the no-overlap branch. Decide this before coding, and state it in a comment.

## Key Takeaways

- Merge intervals in two steps: sort by start time, then scan once, extending the current interval while the next one overlaps it.
- Two intervals overlap when the next start is <= the current end; the merged end is the max of the two ends, not just the later interval's end.
- Worked example: `[1,3], [2,6], [8,10], [15,18]` merges to `[1,6], [8,10], [15,18]`.
- The sort dominates the cost: O(n log n) time, O(n) space; O(n) if the input is already sorted.
- Insert interval reuses the same idea without re-sorting: copy the intervals before, merge the overlapping ones, copy the intervals after.
- The overlapping-count variant (minimum meeting rooms) uses a two-pointer sweep over separately sorted starts and ends.
