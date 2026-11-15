# Prefix Sums (DSA)

A prefix sum array turns repeated range-sum queries into O(1) lookups. The idea is to precompute, once, the running total of an array — then the sum of any range is just the difference of two precomputed values. It is one of the highest-value tricks in DSA: trivial to implement, and it appears in dozens of LeetCode problems.

## The Basic Idea

For an array `nums`, define `prefix[i]` as the sum of `nums[0..i-1]` (with `prefix[0] = 0`). Then the sum of the range `nums[l..r]` is `prefix[r + 1] - prefix[l]`.

```csharp
int[] nums = { 2, 4, 1, 3, 5 };
int[] prefix = new int[nums.Length + 1]; // prefix[0] = 0
for (int i = 0; i < nums.Length; i++)
    prefix[i + 1] = prefix[i] + nums[i];
// prefix = { 0, 2, 6, 7, 10, 15 }

int RangeSum(int l, int r) => prefix[r + 1] - prefix[l];
RangeSum(1, 3); // 4 + 1 + 3 = 8  ->  prefix[4] - prefix[1] = 10 - 2
RangeSum(0, 4); // 15
RangeSum(2, 2); // 1
```

Building the array costs O(n) time and O(n) space; each query is O(1). The extra leading zero is not cosmetic — it makes the formula work for ranges starting at index 0 without a special case. Without prefix sums, `q` queries over an array of length `n` cost O(n × q); with them, O(n + q).

## Subarray Sum Equals K (LeetCode 560)

The prefix-sum idea also solves counting problems when combined with a hash map. If `prefix[j] - prefix[i] == k`, then the subarray `nums[i..j-1]` sums to `k`. So for each prefix sum seen so far, count how many times `prefix - k` has occurred before:

```csharp
public int SubarraySum(int[] nums, int k)
{
    var counts = new Dictionary<int, int> { [0] = 1 };
    int prefix = 0, answer = 0;

    foreach (int n in nums)
    {
        prefix += n;
        if (counts.TryGetValue(prefix - k, out int c)) answer += c;
        counts[prefix] = counts.GetValueOrDefault(prefix) + 1;
    }

    return answer;
}
```

This runs in O(n) time and O(n) space, and it handles negative numbers correctly — unlike a two-pointer approach, which only works when all numbers are non-negative. The `counts[0] = 1` seed handles subarrays that start at index 0.

## 2D Prefix Sums

The same trick extends to matrices. Define `prefix[i, j]` as the sum of the rectangle from `(0, 0)` to `(i - 1, j - 1)`. The sum of any rectangle `(r1, c1)` to `(r2, c2)` is then:

```csharp
// sum = P[r2+1, c2+1] - P[r1, c2+1] - P[r2+1, c1] + P[r1, c1]
public int SumRegion(int r1, int c1, int r2, int c2) =>
    _prefix[r2 + 1, c2 + 1]
    - _prefix[r1, c2 + 1]
    - _prefix[r2 + 1, c1]
    + _prefix[r1, c1]; // add back the overlap subtracted twice
```

The final `+ P[r1, c1]` is the inclusion-exclusion correction: the top strip and the left strip overlap in the top-left rectangle, which gets subtracted twice. Build the table in O(rows × cols) with `P[i+1, j+1] = matrix[i, j] + P[i, j+1] + P[i+1, j] - P[i, j]`, and every query is O(1). This is LeetCode 304 (Range Sum Query 2D — Immutable).

## Difference Arrays

The inverse trick: to add `v` to many ranges efficiently, keep a difference array `diff` where `diff[l] += v` and `diff[r + 1] -= v`. A single prefix-sum pass over `diff` at the end reconstructs the final array. Applying `q` range updates naively costs O(n × q); with a difference array it costs O(n + q).

```csharp
// Add 10 to range [1, 3] of a length-5 array, then rebuild.
int[] diff = new int[6];
diff[1] += 10; diff[4] -= 10;           // range [1, 3] += 10
int[] result = new int[5];
int running = 0;
for (int i = 0; i < 5; i++)
{
    running += diff[i];
    result[i] = running;
}
// result = { 0, 10, 10, 10, 0 }
```

This pattern is the core of LeetCode 1109 (Corporate Flight Bookings) and 370 (Range Addition), and of the "sweep line" technique in general.

## Key Takeaways

- A prefix sum array answers any range-sum query in O(1) after O(n) preprocessing; the leading zero removes the index-0 special case.
- Pair prefix sums with a hash map to count subarrays summing to `k` in O(n), even with negative numbers.
- 2D prefix sums answer rectangle queries in O(1) with inclusion-exclusion; remember to add back the doubly-subtracted corner.
- Difference arrays are the inverse: many range updates in O(n + q) by marking starts and ends, then one prefix pass.
