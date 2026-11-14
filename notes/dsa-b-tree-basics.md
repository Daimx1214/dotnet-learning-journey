# B-Tree Basics (DSA)

A B-tree is a self-balancing search tree designed for storage that lives on disk, not in RAM. Binary search trees work well in memory, but every pointer hop on disk costs a seek — roughly 10 ms on a spinning drive, still microseconds of latency on SSD. A B-tree fixes this by making each node hold many keys and many children, so the tree is very wide and very shallow: a database index with millions of rows is typically only 3–4 levels deep, so any lookup needs only 3–4 disk reads.

## Why Not a Binary Tree?

A binary tree node holds one key and two children, so a tree of 1 million keys is about 20 levels deep — 20 disk reads per lookup. A B-tree node holds hundreds of keys, so the same 1 million keys fit in a tree 3 levels deep. The key insight is that reading one 4 KB or 8 KB disk page costs almost the same whether it holds 1 key or 200, so packing keys into pages amortizes the cost of the read.

## Order, Degree, and Node Layout

A B-tree of order `m` (sometimes called minimum degree `t`, where `m = 2t`) follows these rules:

- Every node holds at most `m - 1` keys and at most `m` children.
- Every node except the root holds at least `t - 1` keys (at least half full).
- The root holds at least 1 key.
- All leaves are at the same depth, so the tree is always balanced.
- Keys inside a node are sorted, and the children between two keys hold values in that range.

```csharp
// A B-tree node: keys sorted, children[i] holds values
// between keys[i - 1] and keys[i].
public class BTreeNode
{
    public List<int> Keys { get; } = new();
    public List<BTreeNode> Children { get; } = new();
    public bool IsLeaf => Children.Count == 0;
}
```

The order is chosen to match the storage page size: with 8 KB pages and 16-byte keys, a node holds roughly 400 keys, so a 3-level tree indexes about 400 × 400 × 400 = 64 million keys.

## Search

Search starts at the root and, inside each node, binary-searches the sorted keys to find the child that could hold the value. It repeats down one path until the key is found or a leaf is reached. Because the height is O(log_t n) and each node search costs O(log t), total search time is O(log n) — but with a much smaller constant in disk reads than a binary tree.

```csharp
public static bool Search(BTreeNode node, int key)
{
    int i = 0;
    while (i < node.Keys.Count && key > node.Keys[i]) i++;

    if (i < node.Keys.Count && key == node.Keys[i]) return true;

    return node.IsLeaf ? false : Search(node.Children[i], key);
}
```

## Insert and Split

Insert searches for the leaf where the key belongs and inserts it in sorted order. If the leaf overflows (it now holds `m` keys, one too many), it splits: the median key moves up to the parent, and the node becomes two half-full nodes. If the parent then overflows, it splits too, and the split can cascade all the way to the root — in which case a new root is created and the tree grows one level taller. Splits keep every node at least half full, which is what guarantees the shallow height.

```csharp
// Split a full child at index i of parent. The median key moves up.
public static void SplitChild(BTreeNode parent, int i, int order)
{
    BTreeNode full = parent.Children[i];
    BTreeNode right = new BTreeNode();
    int median = order / 2;
    int medianKey = full.Keys[median];

    right.Keys.AddRange(full.Keys.GetRange(median + 1, full.Keys.Count - median - 1));
    full.Keys.RemoveRange(median, full.Keys.Count - median);

    if (!full.IsLeaf)
    {
        right.Children.AddRange(full.Children.GetRange(median + 1, full.Children.Count - median - 1));
        full.Children.RemoveRange(median + 1, full.Children.Count - median - 1);
    }

    parent.Keys.Insert(i, medianKey);
    parent.Children.Insert(i + 1, right);
}
```

Deleting is the mirror image: removing a key from a node that would fall below half full triggers a borrow from a sibling or a merge of two siblings, again cascading upward if needed.

## B-Trees in Databases

B-trees — more precisely B+ trees, where all data lives in the leaves and leaves are linked for range scans — are the default index structure in SQL Server, PostgreSQL, MySQL (InnoDB), and SQLite. When you create an index on a column, the database builds a B+ tree over that column's values: point lookups walk down the tree, and range queries (`WHERE age BETWEEN 20 AND 30`) walk to the first matching leaf and then follow the leaf links. The clustered index of a table is a B+ tree whose leaves hold the actual rows, which is why choosing the right clustered key matters for performance.

## Key Takeaways

- B-trees exist because disk reads are expensive: wide nodes mean a shallow tree and few reads per lookup.
- Every node is at least half full, all leaves are at the same depth, and the height is O(log_t n).
- Overflow is fixed by splitting a node and promoting its median key to the parent.
- B+ trees (data in linked leaves) are the index structure behind SQL Server, PostgreSQL, MySQL, and SQLite.
