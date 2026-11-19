// Segment tree with lazy propagation: range-sum queries, point updates,
// and range-add updates over an int array.
//
// Verified by hand on { 1, 3, 5, 7, 9, 11 }:
//   sum of indices [1..4] = 3 + 5 + 7 + 9 = 24
//   after adding 10 to indices [1..3], the array is { 1, 13, 15, 17, 9, 11 }
//   sum of indices [1..4] = 13 + 15 + 17 + 9 = 54
//   total sum = 1 + 13 + 15 + 17 + 9 + 11 = 66

using System.Diagnostics;

var data = new[] { 1, 3, 5, 7, 9, 11 };
var tree = new SegmentTree(data);

int sumBefore = tree.Query(1, 4);
Console.WriteLine($"Sum [1..4] before update: {sumBefore}");
Debug.Assert(sumBefore == 24);

tree.RangeAdd(1, 3, 10);

int sumAfter = tree.Query(1, 4);
Console.WriteLine($"Sum [1..4] after adding 10 to [1..3]: {sumAfter}");
Debug.Assert(sumAfter == 54);

int total = tree.Query(0, 5);
Console.WriteLine($"Total sum after update: {total}");
Debug.Assert(total == 66);

// A point update on top of the lazy state: set index 0 to 100.
tree.UpdatePoint(0, 100);
int totalAfterPointUpdate = tree.Query(0, 5);
Console.WriteLine($"Total sum after setting index 0 to 100: {totalAfterPointUpdate}");
Debug.Assert(totalAfterPointUpdate == 165); // 100 + 13 + 15 + 17 + 9 + 11

Console.WriteLine("All segment tree checks passed.");

/// <summary>
/// Segment tree storing range sums, with lazy propagation for range adds.
/// Indices are 0-based and ranges are inclusive on both ends.
/// </summary>
public sealed class SegmentTree
{
    private readonly int[] _tree; // segment sums, 1-based node indexing
    private readonly int[] _lazy; // pending range-add for each node
    private readonly int _n;

    public SegmentTree(int[] data)
    {
        _n = data.Length;
        _tree = new int[4 * _n];
        _lazy = new int[4 * _n];
        Build(1, 0, _n - 1, data);
    }

    public int Query(int left, int right) => Query(1, 0, _n - 1, left, right);

    public void RangeAdd(int left, int right, int value) =>
        RangeAdd(1, 0, _n - 1, left, right, value);

    public void UpdatePoint(int index, int value) =>
        UpdatePoint(1, 0, _n - 1, index, value);

    private void Build(int node, int l, int r, int[] data)
    {
        if (l == r)
        {
            _tree[node] = data[l];
            return;
        }

        int mid = (l + r) / 2;
        Build(node * 2, l, mid, data);
        Build(node * 2 + 1, mid + 1, r, data);
        _tree[node] = _tree[node * 2] + _tree[node * 2 + 1];
    }

    // Push a node's pending add down to its children.
    private void Push(int node, int l, int r)
    {
        if (_lazy[node] == 0 || l == r)
        {
            return;
        }

        int mid = (l + r) / 2;
        Apply(node * 2, l, mid, _lazy[node]);
        Apply(node * 2 + 1, mid + 1, r, _lazy[node]);
        _lazy[node] = 0;
    }

    private void Apply(int node, int l, int r, int value)
    {
        _tree[node] += value * (r - l + 1);
        _lazy[node] += value;
    }

    private int Query(int node, int l, int r, int ql, int qr)
    {
        if (qr < l || r < ql)
        {
            return 0;
        }

        if (ql <= l && r <= qr)
        {
            return _tree[node];
        }

        Push(node, l, r);
        int mid = (l + r) / 2;
        return Query(node * 2, l, mid, ql, qr)
             + Query(node * 2 + 1, mid + 1, r, ql, qr);
    }

    private void RangeAdd(int node, int l, int r, int ql, int qr, int value)
    {
        if (qr < l || r < ql)
        {
            return;
        }

        if (ql <= l && r <= qr)
        {
            Apply(node, l, r, value);
            return;
        }

        Push(node, l, r);
        int mid = (l + r) / 2;
        RangeAdd(node * 2, l, mid, ql, qr, value);
        RangeAdd(node * 2 + 1, mid + 1, r, ql, qr, value);
        _tree[node] = _tree[node * 2] + _tree[node * 2 + 1];
    }

    private void UpdatePoint(int node, int l, int r, int index, int value)
    {
        if (l == r)
        {
            _tree[node] = value;
            _lazy[node] = 0;
            return;
        }

        Push(node, l, r);
        int mid = (l + r) / 2;
        if (index <= mid)
        {
            UpdatePoint(node * 2, l, mid, index, value);
        }
        else
        {
            UpdatePoint(node * 2 + 1, mid + 1, r, index, value);
        }
        _tree[node] = _tree[node * 2] + _tree[node * 2 + 1];
    }
}
