# Matrix Chain Multiplication

Given a chain of matrices `A1 * A2 * ... * An` where `Ai` has dimensions `p[i-1] x p[i]`, matrix multiplication is associative but the cost depends on the parenthesization. The goal is to find the parenthesization that minimizes the total number of scalar multiplications. Multiplying an `a x b` matrix by a `b x c` matrix costs `a * b * c` scalar multiplications, and the cost difference between parenthesizations can be enormous.

## The Dynamic Programming Recurrence

Let `m[i, j]` be the minimum cost of multiplying matrices `Ai` through `Aj`. The recurrence is:

```text
m[i, i] = 0
m[i, j] = min over k in [i, j-1] of ( m[i, k] + m[k+1, j] + p[i-1] * p[k] * p[j] )
```

The table is filled by increasing chain length, from length 2 up to `n`. The split point `k` that achieves the minimum is stored in a second table `s[i, j]` so the optimal parenthesization can be reconstructed. The algorithm runs in `O(n^3)` time and `O(n^2)` space. Brute force over all parenthesizations would be Catalan-numbered — for `n = 10` there are already 4,862 parenthesizations, and for `n = 20` there are over 1.7 billion.

## Worked Example

Dimensions `p = [10, 30, 5, 60]`, so `A1` is `10x30`, `A2` is `30x5`, `A3` is `5x60`.

```text
Option 1: (A1 * A2) * A3
  A1*A2: 10*30*5   = 1,500   -> 10x5 matrix
  * A3:  10*5*60   = 3,000
  Total = 4,500

Option 2: A1 * (A2 * A3)
  A2*A3: 30*5*60   = 9,000   -> 30x60 matrix
  A1 * : 10*30*60  = 18,000
  Total = 27,000
```

The minimum cost is **4500**, achieved by `(A1 * A2) * A3`. The wrong parenthesization costs 6 times more (27,000 vs 4,500). This is why the DP matters: with 4 matrices the gap can already be an order of magnitude, and it grows with chain length.

## Implementation in C#

```csharp
// Matrix chain multiplication: minimum scalar multiplications.
// p[i-1] x p[i] is the size of matrix Ai. Returns (minCost, parenthesization).
static (int Cost, string Parens) MatrixChainOrder(int[] p)
{
    int n = p.Length - 1;
    var m = new int[n + 1, n + 1];
    var s = new int[n + 1, n + 1];

    for (int len = 2; len <= n; len++)          // chain length
    {
        for (int i = 1; i <= n - len + 1; i++)
        {
            int j = i + len - 1;
            m[i, j] = int.MaxValue;
            for (int k = i; k < j; k++)
            {
                int cost = m[i, k] + m[k + 1, j] + p[i - 1] * p[k] * p[j];
                if (cost < m[i, j])
                {
                    m[i, j] = cost;
                    s[i, j] = k;
                }
            }
        }
    }

    string Build(int i, int j) =>
        i == j ? $"A{i}" : $"({Build(i, s[i, j])}{Build(s[i, j] + 1, j)})";

    return (m[1, n], Build(1, n));
}

// MatrixChainOrder(new[] { 10, 30, 5, 60 }) -> (4500, "((A1A2)A3)")
```

## A Larger Trace

With `p = [5, 10, 3, 12, 5, 50, 6]` (6 matrices), the DP table gives a minimum cost of 2,010, achieved by `((A1(A2A3))((A4A5)A6))`. Filling the table by hand: `m[1,2] = 5*10*3 = 150`, `m[2,3] = 10*3*12 = 360`, `m[3,4] = 3*12*5 = 180`, `m[4,5] = 12*5*50 = 3000`, `m[5,6] = 5*50*6 = 1500`. Then length-3 chains, and so on up to `m[1,6] = 2010`. The point of the trace is the fill order: no cell is computed before the shorter chains it depends on, which is exactly why the outer loop runs over chain length.

## Where This Shows Up in Practice

- Query optimizers in databases choose join orders with the same DP shape: the cost of joining tables depends on the order, just like matrix dimensions.
- Optimal binary search trees and optimal polygon triangulation use the identical interval-DP recurrence with different cost functions.
- In graphics and ML pipelines, chains of matrix transforms are parenthesized once at setup time; a bad order can turn a per-frame `O(n)` cost into something far worse.

## Common Pitfalls

- Off-by-one errors on the `p` array: `p` has `n + 1` entries for `n` matrices, and `Ai` is `p[i-1] x p[i]`.
- Filling the table row by row instead of by chain length reads uncomputed cells and silently produces wrong answers.
- Integer overflow: costs grow as the product of three dimensions, so use `long` when dimensions are large (e.g. dimensions above ~1000 can overflow `int` on a chain of 5+ matrices).
- Forgetting that the recurrence minimizes over the split point `k` — greedy choices like "always multiply the smallest pair first" fail on simple inputs.

## Key Takeaways

- Matrix chain multiplication is an interval DP: `m[i,j] = min over k of m[i,k] + m[k+1,j] + p[i-1]*p[k]*p[j]`, filled by increasing chain length in `O(n^3)`.
- With dims `[10, 30, 5, 60]` the minimum cost is 4500 via `(A1*A2)*A3`, versus 27,000 for the other parenthesization — a 6x difference.
- The split table `s[i,j]` is what turns a minimum cost into an actual parenthesization; store it during the DP.
- The same interval-DP shape solves join ordering, optimal BSTs, and polygon triangulation.
- Watch the `p` indexing and use `long` for large dimensions to avoid overflow.
