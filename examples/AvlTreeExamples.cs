// AVL Tree Examples
//
// An AVL tree is a self-balancing binary search tree: after every insert
// (or delete) the height difference between the left and right subtree
// of every node is at most 1. When an insert breaks that rule the tree
// rebalances with one of four rotations: LL, RR, LR, RL.
//
// Demo: insert 10, 20, 30, 40, 50, 25 and print the tree.
//
// Hand-traced rotations:
//   * Insert 10, 20, 30 -> node 10 is right-heavy by 2, so an RR (left)
//     rotation runs and the root becomes 20 (left 10, right 30).
//   * Insert 40, then 50 -> node 30 is right-heavy by 2, so an RR (left)
//     rotation runs and 40 becomes the right child of 20 (left 30,
//     right 50).
//   * Insert 25 -> node 20 is right-heavy by 2 and its right child 40 is
//     left-heavy, so an RL rotation runs (right rotation on 40, then a
//     left rotation on 20) and the root becomes 30.
// Final tree: root 30, left subtree 20 (10, 25), right subtree 40 (50).
// In-order traversal: 10 20 25 30 40 50. Height: 3. Root: 30.
//
// Expected console output:
//   In-order: 10 20 25 30 40 50
//   Root: 30
//   Height: 3
//   Is balanced: True

class AvlNode
{
    public int Value;
    public AvlNode? Left;
    public AvlNode? Right;
    public int Height = 1;

    public AvlNode(int value) => Value = value;
}

class AvlTree
{
    public AvlNode? Root { get; private set; }

    public void Insert(int value) => Root = Insert(Root, value);

    private static int Height(AvlNode? node) => node?.Height ?? 0;

    private static int BalanceFactor(AvlNode? node) =>
        node is null ? 0 : Height(node.Left) - Height(node.Right);

    private static void UpdateHeight(AvlNode node) =>
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));

    private static AvlNode RotateRight(AvlNode y)
    {
        AvlNode x = y.Left!;
        AvlNode? t2 = x.Right;
        x.Right = y;
        y.Left = t2;
        UpdateHeight(y);
        UpdateHeight(x);
        return x;
    }

    private static AvlNode RotateLeft(AvlNode x)
    {
        AvlNode y = x.Right!;
        AvlNode? t2 = y.Left;
        y.Left = x;
        x.Right = t2;
        UpdateHeight(x);
        UpdateHeight(y);
        return y;
    }

    private static AvlNode Insert(AvlNode? node, int value)
    {
        if (node is null)
            return new AvlNode(value);

        if (value < node.Value)
            node.Left = Insert(node.Left, value);
        else if (value > node.Value)
            node.Right = Insert(node.Right, value);
        else
            return node; // Duplicate values are ignored.

        UpdateHeight(node);
        int balance = BalanceFactor(node);

        // LL case: left-heavy, new value in the left subtree's left side.
        if (balance > 1 && value < node.Left!.Value)
            return RotateRight(node);

        // RR case: right-heavy, new value in the right subtree's right side.
        if (balance < -1 && value > node.Right!.Value)
            return RotateLeft(node);

        // LR case: left-heavy, new value in the left subtree's right side.
        if (balance > 1 && value > node.Left!.Value)
        {
            node.Left = RotateLeft(node.Left);
            return RotateRight(node);
        }

        // RL case: right-heavy, new value in the right subtree's left side.
        if (balance < -1 && value < node.Right!.Value)
        {
            node.Right = RotateRight(node.Right);
            return RotateLeft(node);
        }

        return node;
    }

    public IEnumerable<int> InOrder()
    {
        var result = new List<int>();
        Walk(Root, result);
        return result;

        static void Walk(AvlNode? node, List<int> acc)
        {
            if (node is null) return;
            Walk(node.Left, acc);
            acc.Add(node.Value);
            Walk(node.Right, acc);
        }
    }

    public bool IsBalanced() => Check(Root).balanced;

    private static (bool balanced, int height) Check(AvlNode? node)
    {
        if (node is null) return (true, 0);
        var left = Check(node.Left);
        var right = Check(node.Right);
        bool balanced = left.balanced && right.balanced
            && Math.Abs(left.height - right.height) <= 1;
        return (balanced, 1 + Math.Max(left.height, right.height));
    }
}

var tree = new AvlTree();
foreach (int value in new[] { 10, 20, 30, 40, 50, 25 })
    tree.Insert(value);

Console.WriteLine("In-order: " + string.Join(" ", tree.InOrder()));
Console.WriteLine("Root: " + tree.Root!.Value);
Console.WriteLine("Height: " + tree.Root.Height);
Console.WriteLine("Is balanced: " + tree.IsBalanced());
