# Counting Sort and Radix Sort

Most sorting algorithms compare elements and cannot beat O(n log n). Counting
sort and radix sort are different: they exploit the *values* of the keys
instead of comparing them, which makes linear-time sorting possible when the
keys live in a small, known range.

## When Linear-Time Sorting Works

The O(n log n) lower bound applies only to comparison-based sorting. If keys
are integers in a small range `[0, k)`, counting sort runs in O(n + k). If keys
are fixed-width numbers or strings, radix sort runs in O(d · (n + k)), where
`d` is the number of digits and `k` is the digit range (usually 10 or 256).

The catch: `k` must stay small. Counting sort on 32-bit integers would need a
count array of 2^32 entries, which is why radix sort processes one digit at a
time instead.

## Counting Sort

Count how many times each key occurs, turn the counts into starting positions
with a prefix sum, then place each element. Scanning the input left to right
while writing into the output keeps the sort **stable** (equal elements keep
their original relative order).

```csharp
int[] CountingSort(int[] input, int maxValue)
{
    int[] count = new int[maxValue + 1];
    foreach (int x in input) count[x]++;

    // count[v] becomes the starting index of value v in the output
    int total = 0;
    for (int v = 0; v <= maxValue; v++)
    {
        int c = count[v];
        count[v] = total;
        total += c;
    }

    int[] output = new int[input.Length];
    foreach (int x in input)
    {
        output[count[x]] = x;
        count[x]++;
    }
    return output;
}
```

### Worked Example

Sort `[4, 2, 2, 8, 3, 3, 1]` (range 0–8):

| Value | 1 | 2 | 3 | 4 | 8 |
|-------|---|---|---|---|---|
| Count | 1 | 2 | 2 | 1 | 1 |
| Start index | 0 | 1 | 3 | 5 | 6 |

Placing left to right: `4 → [5]`, `2 → [1]`, `2 → [2]`, `8 → [6]`,
`3 → [3]`, `3 → [4]`, `1 → [0]`. Result: `[1, 2, 2, 3, 3, 4, 8]`.
The two `2`s and the two `3`s keep their input order, so the sort is stable.

## Radix Sort (LSD)

Radix sort sorts digit by digit, least significant digit first, using a
**stable** subroutine (counting sort) on each digit. Stability is what makes
it work: after sorting by the tens digit, numbers with equal tens digits stay
ordered by their units digit.

Sort `[170, 45, 75, 90, 802, 24, 2, 66]`:

| Pass | Digit | Result |
|------|-------|--------|
| 1 | Units | `[170, 90, 802, 2, 24, 45, 75, 66]` |
| 2 | Tens | `[802, 2, 24, 45, 66, 170, 75, 90]` |
| 3 | Hundreds | `[2, 24, 45, 66, 75, 90, 170, 802]` |

Check pass 2: the tens digits are `7, 9, 0, 0, 2, 4, 7, 6` for the
pass-1 order, and sorting stably by them gives `802, 2, 24, 45, 66, 170, 75, 90`.
Note that `170` comes before `75` here because both have tens digit 7 and
`170` was earlier after pass 1.

```csharp
void RadixSort(int[] a)
{
    int max = a.Max();
    for (int exp = 1; max / exp > 0; exp *= 10)
        CountingSortByDigit(a, exp); // stable counting sort on (x / exp) % 10
}
```

An MSD (most significant digit first) variant exists too, but it needs
recursion or buckets per digit and is harder to implement in place.

## Stability Matters

Stability is not a nice-to-have here, it is the mechanism. If the digit sort
were unstable, the ordering built by earlier passes would be destroyed and the
final result would be wrong. The same idea powers `OrderBy().ThenBy()` in
LINQ: each `ThenBy` refines, rather than replaces, the previous ordering.

## Complexity and Limits

| Algorithm | Time | Extra space | Stable |
|-----------|------|-------------|--------|
| Counting sort | O(n + k) | O(n + k) | Yes |
| Radix sort (base 10) | O(d · (n + 10)) | O(n + 10) | Yes |

Limits to remember:

- Counting sort needs a non-negative integer range (offset negatives first).
- Both are poor when `k` is huge relative to `n` — sorting 10 values in the
  range 0–1,000,000 wastes a million-entry count array.
- Radix sort on variable-length strings needs padding or an MSD approach.
- Neither is in-place in the simple form; both allocate an output array.

## Key Takeaways

- Comparison sorting is capped at O(n log n); counting and radix sort escape
  that bound by using the values of the keys, not comparisons.
- Counting sort runs in O(n + k) and is stable when elements are placed in
  input order using prefix-sum starting positions.
- Radix sort applies a stable counting sort digit by digit, LSD first, in
  O(d · (n + k)) time.
- Both need a small digit/key range to be worth it; a huge `k` kills the
  advantage.
