# Binary Search on the Answer

Some problems ask for the smallest (or largest) value that satisfies a condition, and the answer is not in any array — it is a number in a range. When the condition is monotonic, you can binary-search the answer itself.

## The Monotonic-Predicate Pattern

Define a predicate `Feasible(x)`: "can I do it with value x?" Monotonic means once it becomes true, it stays true for all larger x (or vice versa). Then binary search over the range of possible answers:

```csharp
// Smallest x in [lo, hi] such that Feasible(x) is true.
int BinarySearchAnswer(int lo, int hi, Func<int, bool> feasible)
{
    while (lo < hi)
    {
        int mid = lo + (hi - lo) / 2;
        if (feasible(mid))
            hi = mid;       // mid works — try smaller
        else
            lo = mid + 1;   // mid fails — must go larger
    }
    return lo;
}
```

The search space is the range of answers, not an input array, so the complexity is O(log(range) × cost of one feasibility check).

## Worked Example: Koko Eating Bananas

Koko has piles `[3, 6, 7, 11]` and `h = 8` hours. She eats at speed `k` bananas per hour and finishes a pile in `ceil(pile / k)` hours. Find the minimum integer speed that finishes all piles within 8 hours.

The predicate "can finish in ≤ 8 hours at speed k" is monotonic: if speed 4 works, every faster speed works too. Binary search k in [1, 11] (11 is the largest pile — any faster speed behaves the same as 11).

Hand-check the key speeds:

- Speed 4: hours = ceil(3/4) + ceil(6/4) + ceil(7/4) + ceil(11/4) = 1 + 2 + 2 + 3 = **8** ≤ 8 ✓ — feasible.
- Speed 3: hours = ceil(3/3) + ceil(6/3) + ceil(7/3) + ceil(11/3) = 1 + 2 + 3 + 4 = **10** > 8 ✗ — not feasible.

So 4 works and 3 does not, and monotonicity means no speed below 4 can work. The answer is **4**.

```csharp
int MinEatingSpeed(int[] piles, int h)
{
    int lo = 1, hi = piles.Max();
    while (lo < hi)
    {
        int mid = lo + (hi - lo) / 2;
        long hours = 0;
        foreach (var p in piles)
            hours += (p + mid - 1) / mid; // ceil(p / mid) without floating point
        if (hours <= h) hi = mid;
        else lo = mid + 1;
    }
    return lo;
}
```

Note the `long hours` accumulator: with large piles, the sum of hours can overflow a 32-bit `int`.

## Other Classic Problems

The same pattern appears whenever "minimum X such that it is possible" is monotonic:

- **Ship Packages Within D Days** — minimum ship capacity so all packages ship in D days; feasible(capacity) checks whether greedy loading fits in D days.
- **Split Array Largest Sum** — minimum largest subarray sum when splitting into k subarrays; feasible(maxSum) checks whether greedy splitting needs ≤ k parts.
- **Minimum Days to Make m Bouquets** — earliest day with m bouquets of k adjacent flowers; feasible(day) counts bouquets from bloomed flowers.

In each case, the hard part is writing the O(n) feasibility check; the binary search wrapper is the same every time.

## Key Takeaways

- Binary search on the answer works when the feasibility predicate is monotonic — once true, it stays true.
- The search range is the range of possible answers; complexity is O(log(range) × check cost).
- Koko eating bananas, piles `[3, 6, 7, 11]`, h = 8: speed 4 takes 1 + 2 + 2 + 3 = 8 hours (feasible), speed 3 takes 10 hours (not feasible) — answer **4**.
- Use integer ceiling `(p + k - 1) / k` instead of floating-point division, and a `long` accumulator to avoid overflow.
- Recognize the pattern by the wording: "minimum speed / capacity / days such that ..." almost always means binary search on the answer.
