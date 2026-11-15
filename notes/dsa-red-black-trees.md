# Red-Black Trees (DSA)

A red-black tree is a self-balancing binary search tree in which every node carries one extra bit of information — a colour, red or black — and the tree enforces a small set of colour rules that keep it roughly balanced. The guarantee is weaker than an AVL tree's, but inserts and deletes need fewer rotations, which is why .NET's `SortedSet<T>` and `SortedDictionary<TKey, TValue>` are implemented as red-black trees. Height is always O(log n), so search, insert, and delete all stay O(log n) in the worst case.

## The Five Rules

Every valid red-black tree satisfies all of these:

1. Every node is either red or black.
2. The root is black.
3. Every leaf (the null child pointer, called NIL) is black.
4. A red node cannot have a red child — no two red nodes are adjacent.
5. Every path from a node to any of its descendant NIL leaves contains the same number of black nodes (the *black-height*).

Rule 5 is the key: the longest root-to-leaf path can be at most twice the shortest, because red nodes cannot be adjacent, so the height is bounded by 2 * log2(n + 1). The tree is never perfectly balanced, but it is never badly unbalanced either.

## Node and Rotations

Rotations are the same re-linking operation used by AVL trees: a left or right rotation preserves BST ordering and runs in O(1) time. The node needs a colour and a parent pointer, because fixups walk upward after an insertion or deletion.

```csharp
public enum NodeColor { Red, Black }

public class RbNode
{
    public int Value { get; set; }
    public NodeColor Color { get; set; } = NodeColor.Red;
    public RbNode? Left { get; set; }
    public RbNode? Right { get; set; }
    public RbNode? Parent { get; set; }
    public RbNode(int value) => Value = value;
}

// Left rotation around x: y (x's right child) takes x's place.
public static void RotateLeft(RbNode x, ref RbNode? root)
{
    RbNode y = x.Right!;
    x.Right = y.Left;
    if (y.Left is not null) y.Left.Parent = x;
    y.Parent = x.Parent;
    if (x.Parent is null) root = y;
    else if (x == x.Parent.Left) x.Parent.Left = y;
    else x.Parent.Right = y;
    y.Left = x;
    x.Parent = y;
}

// RotateRight is the mirror image: swap Left and Right in the code above.
```

## Insert Fixup

A new node is inserted exactly like a normal BST insert, coloured red. Colouring it red keeps rule 5 intact, but it can break rule 4 if its parent is also red. The fixup walks up the tree, recolouring and rotating until rule 4 is restored; it terminates in O(log n) time and performs at most two rotations.

```csharp
public static void InsertFixup(RbNode node, ref RbNode? root)
{
    while (node.Parent?.Color == NodeColor.Red)
    {
        RbNode parent = node.Parent;
        RbNode grandparent = parent.Parent!;
        bool parentIsLeft = parent == grandparent.Left;

        // The "uncle" is the sibling of the parent.
        RbNode? uncle = parentIsLeft ? grandparent.Right : grandparent.Left;

        if (uncle?.Color == NodeColor.Red)
        {
            // Case 1: uncle is red — recolour and move the problem up.
            parent.Color = NodeColor.Black;
            uncle.Color = NodeColor.Black;
            grandparent.Color = NodeColor.Red;
            node = grandparent;
        }
        else
        {
            // Case 2: node is an "inner" child — rotate it into an outer position.
            if (parentIsLeft && node == parent.Right)
            {
                node = parent;
                RotateLeft(node, ref root);
                parent = node.Parent!;
                grandparent = parent.Parent!;
            }
            else if (!parentIsLeft && node == parent.Left)
            {
                node = parent;
                RotateRight(node, ref root);
                parent = node.Parent!;
                grandparent = parent.Parent!;
            }

            // Case 3: node is an "outer" child — recolour and rotate grandparent.
            parent.Color = NodeColor.Black;
            grandparent.Color = NodeColor.Red;
            if (parentIsLeft) RotateRight(grandparent, ref root);
            else RotateLeft(grandparent, ref root);
        }
    }
    root!.Color = NodeColor.Black; // Rule 2: root is always black.
}
```

## Delete Fixup

Deletion is harder than insertion. Removing a node is a standard BST delete; if the removed node was black, every path through that spot loses one black node and rule 5 breaks. The fixup treats the replacement node as "double black" and pushes that extra blackness upward with recolourings and rotations until it is absorbed — either by a red node (which turns black) or by reaching the root. It needs at most three rotations, but the case analysis (sibling red, sibling black with black children, sibling black with a red child) is considerably longer than the insert fixup, which is why many implementations lean on a sentinel NIL node instead of nulls.

## Red-Black vs AVL

| Aspect | Red-black tree | AVL tree |
|--------|---------------|----------|
| Balance guarantee | Height ≤ 2 * log2(n + 1) | Height ≤ 1.44 * log2(n) |
| Rotations per insert | At most 2 | At most 2 (often 1) |
| Rotations per delete | At most 3 | O(log n) |
| Lookup speed | Slightly slower | Slightly faster |
| Best for | Frequent inserts/deletes | Read-heavy workloads |

The practical rule of thumb: if the workload is insert/delete-heavy, red-black wins; if it is lookup-heavy, AVL wins. For real .NET code, use `SortedSet<T>` or `SortedDictionary<TKey, TValue>` — both are red-black trees — rather than hand-rolling either one.

## Key Takeaways

- Five colour rules bound the height to O(log n); rule 5 (equal black-height on every path) does the heavy lifting.
- New nodes are inserted red, so only rule 4 (no red-red parent/child) can break; the fixup repairs it with recolouring and at most two rotations.
- Deletion can break rule 5 and needs a longer fixup with the "double black" concept.
- Red-black trees rotate less than AVL trees, so they suit write-heavy workloads — and they power .NET's built-in sorted collections.
