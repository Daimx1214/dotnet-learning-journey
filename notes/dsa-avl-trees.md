# AVL Trees (DSA)

An AVL tree is a binary search tree that rebalances itself after every insert and delete, so its height stays O(log n) no matter the insertion order. It is named after Adelson-Velsky and Landis, who published it in 1962. A plain BST degrades to O(n) when fed sorted input; an AVL tree guarantees the height difference between any node's two subtrees is at most 1, so search, insert, and delete all stay O(log n).

## Balance Factor

Each node stores its height, and the balance factor is the height of its left subtree minus the height of its right subtree. A valid AVL node always has a balance factor of -1, 0, or +1. After an insertion, factors are recomputed bottom-up along the path to the root; the first node whose factor becomes -2 or +2 is fixed with a rotation.

```csharp
public class AvlNode
{
    public int Value { get; set; }
    public int Height { get; set; } = 1;
    public AvlNode? Left { get; set; }
    public AvlNode? Right { get; set; }
    public AvlNode(int value) => Value = value;
}

public static int Height(AvlNode? node) => node?.Height ?? 0;
public static int BalanceFactor(AvlNode? node) =>
    node is null ? 0 : Height(node.Left) - Height(node.Right);

private static void UpdateHeight(AvlNode node) =>
    node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
```

## Rotations

A rotation restructures nodes in O(1) time while preserving BST ordering. Four cases exist, named by the shape of the imbalance:

- **LL case**: left child of a left-heavy node is left-heavy. Fix with a single right rotation.
- **RR case**: mirror of LL. Fix with a single left rotation.
- **LR case**: left child is right-heavy. Fix with a left rotation on the child, then a right rotation on the node.
- **RL case**: mirror of LR. Fix with a right rotation on the child, then a left rotation on the node.

```csharp
// Right rotation: y becomes the right child of its left child x.
public static AvlNode RotateRight(AvlNode y)
{
    AvlNode x = y.Left!;
    y.Left = x.Right;
    x.Right = y;
    UpdateHeight(y);
    UpdateHeight(x);
    return x;
}

// Left rotation: x becomes the left child of its right child y.
public static AvlNode RotateLeft(AvlNode x)
{
    AvlNode y = x.Right!;
    x.Right = y.Left;
    y.Left = x;
    UpdateHeight(x);
    UpdateHeight(y);
    return y;
}
```

## Insert with Rebalancing

Insert works exactly like a BST insert, then walks back up the recursion, updating heights and rebalancing at the first unbalanced node. Only one node on the path can be unbalanced after a single insertion, so one rotation (or one double rotation) is enough.

```csharp
public static AvlNode Insert(AvlNode? node, int value)
{
    if (node is null) return new AvlNode(value);
    if (value < node.Value) node.Left = Insert(node.Left, value);
    else if (value > node.Value) node.Right = Insert(node.Right, value);
    else return node; // duplicate: ignore

    UpdateHeight(node);
    int balance = BalanceFactor(node);

    if (balance > 1 && value < node.Left!.Value) return RotateRight(node); // LL
    if (balance < -1 && value > node.Right!.Value) return RotateLeft(node); // RR
    if (balance > 1 && value > node.Left!.Value) // LR
    {
        node.Left = RotateLeft(node.Left);
        return RotateRight(node);
    }
    if (balance < -1 && value < node.Right!.Value) // RL
    {
        node.Right = RotateRight(node.Right);
        return RotateLeft(node);
    }
    return node;
}
```

## Complexity

| Operation | Average | Worst case |
|-----------|---------|------------|
| Search    | O(log n) | O(log n) |
| Insert    | O(log n) | O(log n) |
| Delete    | O(log n) | O(log n) |
| Space     | O(n) | O(n) |

An AVL tree is at most about 1.44 * log2(n) tall, so it is more strictly balanced than a red-black tree. That strictness makes lookups slightly faster but costs extra rotations on inserts and deletes.

## When to Use It

Use an AVL tree when reads dominate writes — for example, an in-memory index built once and queried constantly. Prefer a red-black tree (or .NET's `SortedSet<T>` / `SortedDictionary<TKey, TValue>`, which are red-black trees) when inserts and deletes are frequent, because red-black trees rotate less often. In practice, hand-rolling an AVL tree is a learning exercise; production .NET code should reach for the built-in sorted collections.

## Key Takeaways

- AVL trees keep every node's balance factor in {-1, 0, +1} by rotating after each insert and delete.
- Four imbalance cases (LL, RR, LR, RL) map to one single or one double rotation.
- Height stays O(log n), so search, insert, and delete are all O(log n) in the worst case.
- The price of strict balance is more rotations than a red-black tree, so AVL fits read-heavy workloads best.
