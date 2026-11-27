# Serialize and Deserialize a Binary Tree

To store a binary tree in a string (or send it over a network) you need a format that records the shape, not just the values. Preorder traversal with an explicit null marker does exactly that: a `#` records every missing child, so the structure is recoverable unambiguously.

## The format

Walk the tree in preorder (node, left, right). Write each value, and write `#` for every null child. Separate tokens with commas.

## Worked example

Take the tree in level-order notation `[1, 2, 3, null, null, 4, 5]`:

```text
        1
       / \
      2   3
         / \
        4   5
```

Serialize it step by step, preorder with `#` for nulls:

| Step | Visit | Output so far |
|------|-------|---------------|
| 1 | 1 | `1` |
| 2 | 2 | `1,2` |
| 3 | null (2's left) | `1,2,#` |
| 4 | null (2's right) | `1,2,#,#` |
| 5 | 3 | `1,2,#,#,3` |
| 6 | 4 | `1,2,#,#,3,4` |
| 7 | null (4's left) | `1,2,#,#,3,4,#` |
| 8 | null (4's right) | `1,2,#,#,3,4,#,#` |
| 9 | 5 | `1,2,#,#,3,4,#,#,5` |
| 10 | null (5's left) | `1,2,#,#,3,4,#,#,5,#` |
| 11 | null (5's right) | `1,2,#,#,3,4,#,#,5,#,#` |

Serialized form: **`"1,2,#,#,3,4,#,#,5,#,#"`**.

Count the tokens to sanity-check: the tree has 5 real nodes, and a binary tree with n nodes has n + 1 null children, so the string must have 5 + 6 = 11 tokens. It does: five numbers and six `#` markers.

Deserialization reads the tokens left to right with a queue (or an index pointer): take the next token; `#` means null, otherwise create a node and recursively build its left and right subtrees from the following tokens. Because preorder visits a node before its children, the tokens arrive in exactly the order the recursion consumes them, and the rebuilt tree is identical to the original.

## C# implementation

```csharp
public sealed class TreeNode
{
    public int Val;
    public TreeNode? Left;
    public TreeNode? Right;
    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)
        => (Val, Left, Right) = (val, left, right);
}

public sealed class Codec
{
    public string Serialize(TreeNode? root)
    {
        var parts = new List<string>();
        Preorder(root, parts);
        return string.Join(",", parts);

        static void Preorder(TreeNode? node, List<string> parts)
        {
            if (node is null) { parts.Add("#"); return; }
            parts.Add(node.Val.ToString());
            Preorder(node.Left, parts);
            Preorder(node.Right, parts);
        }
    }

    public TreeNode? Deserialize(string data)
    {
        // A queue hands tokens out in order; each recursive call takes the
        // next token, so no index bookkeeping is needed.
        var tokens = new Queue<string>(data.Split(','));
        return Build(tokens);

        static TreeNode? Build(Queue<string> tokens)
        {
            string token = tokens.Dequeue();
            if (token == "#") return null;
            return new TreeNode(
                int.Parse(token),
                Build(tokens),   // left subtree consumes the next tokens
                Build(tokens));  // right subtree consumes the ones after
        }
    }
}
```

Round-trip check on the worked example: `Deserialize("1,2,#,#,3,4,#,#,5,#,#")` rebuilds root 1, left child 2 (both children `#`), right child 3 with children 4 and 5 — the same tree — and serializing it again returns the identical string.

## Complexity and edge cases

- Time and space are both O(n): one token per node plus one per null child.
- An empty tree serializes to `"#"` and deserializes back to `null`.
- A skewed tree (a linked list) still works — the `#` markers record every missing side child.

## The BFS variant

Instead of preorder, you can serialize level by level (breadth-first), writing `#` for null children and stopping once the queue holds only nulls. The same example tree serializes to `"1,2,3,#,#,4,5"` — shorter, because trailing nulls can be dropped. It is the format LeetCode uses for input, but the code is slightly longer: you need a queue for serialization too, and deserialization pairs tokens up as left/right children. Preorder is the simpler one to implement correctly from scratch.

## Key Takeaways

- Preorder + `#` null markers records a tree's exact shape: node, left, right, with `#` for every missing child.
- Worked example: `[1, 2, 3, null, null, 4, 5]` serializes to `"1,2,#,#,3,4,#,#,5,#,#"` (11 tokens: 5 values + 6 nulls).
- A queue-based deserializer consumes tokens in preorder, rebuilding the identical tree.
- Both operations are O(n) time and space; an empty tree is just `"#"`.
- The BFS variant is shorter (`"1,2,3,#,#,4,5"`) but needs more bookkeeping.
