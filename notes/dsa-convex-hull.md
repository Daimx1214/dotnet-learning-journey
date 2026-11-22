# Convex Hull

The **convex hull** of a set of points is the smallest convex polygon that
contains them all — imagine stretching a rubber band around the points and
letting it snap tight. The hull's vertices are a subset of the input points,
and every other point lies inside the polygon or on one of its edges.

## The Cross-Product Orientation Test

Every hull algorithm rests on one test: given three points `O`, `A`, `B`,
does the turn `O → A → B` go left, right, or straight?

```csharp
// > 0: counter-clockwise (left turn), < 0: clockwise (right turn), = 0: collinear
static long Cross(Point o, Point a, Point b)
    => (long)(a.X - o.X) * (b.Y - o.Y) - (long)(a.Y - o.Y) * (b.X - o.X);
```

Use `long` (not `int`) for the products: coordinates up to 10^9 overflow
32-bit multiplication. The sign alone is what matters — the magnitude is
twice the triangle's area.

## Graham Scan

Graham scan picks the lowest point (then leftmost) as the pivot, sorts the
rest by polar angle around it, then walks the sorted order pushing points
onto a stack and popping whenever the last turn is not a left turn. It runs
in O(n log n), dominated by the sort.

## Monotonic Chain (Andrew's Algorithm)

Andrew's algorithm is usually simpler to implement: sort by x (then y),
build the lower hull left to right, then the upper hull right to left,
keeping only left turns in each pass. Points making a clockwise turn or a
collinear middle point are popped. The two chains join into the hull.

```csharp
static List<Point> ConvexHull(List<Point> points)
{
    var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
    if (pts.Count <= 1) return pts;

    var lower = new List<Point>();
    foreach (var p in pts)
    {
        while (lower.Count >= 2 &&
               Cross(lower[^2], lower[^1], p) <= 0) // pop right turns and collinear middles
            lower.RemoveAt(lower.Count - 1);
        lower.Add(p);
    }

    var upper = new List<Point>();
    for (int i = pts.Count - 1; i >= 0; i--)
    {
        var p = pts[i];
        while (upper.Count >= 2 &&
               Cross(upper[^2], upper[^1], p) <= 0)
            upper.RemoveAt(upper.Count - 1);
        upper.Add(p);
    }

    lower.RemoveAt(lower.Count - 1);
    upper.RemoveAt(upper.Count - 1);
    lower.AddRange(upper);
    return lower;
}
```

The `<= 0` keeps only strictly left turns, which drops collinear points on
the hull edges. Use `< 0` instead if collinear edge points must be kept.

## Worked Example

Points: (0,0), (1,1), (2,2), (0,3), (3,1), (4,4), (3,3), (1,3).
Sorted by x then y: (0,0), (0,3), (1,1), (1,3), (2,2), (3,1), (3,3), (4,4).

**Lower hull** (left to right):

| Step | Point added | Cross check | Stack after |
|------|-------------|-------------|-------------|
| 1–2 | (0,0), (0,3) | — | (0,0), (0,3) |
| 3 | (1,1) | cross = −3 → pop (0,3) | (0,0), (1,1) |
| 4 | (1,3) | cross = 2 → keep | (0,0), (1,1), (1,3) |
| 5 | (2,2) | cross = −2 → pop (1,3); cross = 0 → pop (1,1) | (0,0), (2,2) |
| 6 | (3,1) | cross = −4 → pop (2,2) | (0,0), (3,1) |
| 7 | (3,3) | cross = 6 → keep | (0,0), (3,1), (3,3) |
| 8 | (4,4) | cross = −2 → pop (3,3) | (0,0), (3,1), (4,4) |

**Upper hull** (right to left) from (4,4): (3,3) is kept (cross = 2), (3,1)
is popped (cross = −2), (2,2) is popped as collinear on the line y = x
(cross = 0), (1,3) is kept (cross = 6), (1,1) is popped (cross = −2),
(0,3) is kept (cross = 12 after popping (1,3)), (0,0) is kept.

**Hull (counter-clockwise): (0,0), (3,1), (4,4), (0,3).**

Verify by hand: (3,3) lies on the segment (2,2)–(4,4) (the line y = x);
(1,1) and (2,2) lie inside the polygon (at x = 1 the hull spans y = 1/3 to
3.25; at x = 2 it spans y = 2/3 to 3.5); (1,3) sits below the top edge
(0,3)–(4,4). Every non-hull point is inside or on an edge, and no hull
vertex can be removed without losing area.

## Complexity and Variants

Both Graham scan and monotonic chain run in O(n log n) time and O(n) space
(the sort dominates). For points already sorted by x, the chain runs in
O(n). Special cases: all points collinear (the hull is just the two
endpoints), duplicate points (dedupe first), and n ≤ 2 (the hull is the
input itself).

The rotating-calipers technique walks the finished hull in O(n) to find the
diameter (farthest pair), the minimum-area enclosing rectangle, or the
widest strip — all useful once the hull is built.

## Key Takeaways

- The convex hull is the smallest convex polygon containing all points;
  the cross product's sign is the only test needed to build it.
- Andrew's monotonic chain builds a lower and an upper hull in O(n log n),
  popping right turns and collinear middles.
- The worked example yields the verified hull (0,0), (3,1), (4,4), (0,3),
  with (3,3) collinear on an edge and the rest strictly inside.
- Use `long` for cross products to avoid overflow, and decide up front
  whether collinear edge points are kept (`< 0`) or dropped (`<= 0`).
