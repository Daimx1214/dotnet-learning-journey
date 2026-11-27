# The Visitor Pattern in C#

The Visitor pattern lets you add new operations to a family of types without modifying those types. Instead of a `switch` on node type inside every operation, each node accepts a visitor, and the visitor implements one method per node type. It is the classic way to keep expression trees, ASTs, and document models open for extension.

## The problem: operations scattered over a switch

```csharp
// Fragile: every new operation needs another switch, and every new
// node type breaks every existing switch.
string Print(Node n) => n switch
{
    NumberNode x => x.Value.ToString(),
    AddNode a => $"({Print(a.Left)} + {Print(a.Right)})",
    MultiplyNode m => $"({Print(m.Left)} * {Print(m.Right)})",
    _ => throw new NotSupportedException()
};
```

## The visitor version

```csharp
// The element hierarchy: each node knows how to accept a visitor.
public abstract record Node
{
    public abstract T Accept<T>(INodeVisitor<T> visitor);
}

public sealed record NumberNode(int Value) : Node
{
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record AddNode(Node Left, Node Right) : Node
{
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

public sealed record MultiplyNode(Node Left, Node Right) : Node
{
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

// One method per node type — the compiler enforces completeness.
public interface INodeVisitor<T>
{
    T Visit(NumberNode node);
    T Visit(AddNode node);
    T Visit(MultiplyNode node);
}
```

## Two visitors: evaluator and printer

```csharp
public sealed class Evaluator : INodeVisitor<int>
{
    public int Visit(NumberNode node) => node.Value;
    public int Visit(AddNode node) =>
        node.Left.Accept(this) + node.Right.Accept(this);
    public int Visit(MultiplyNode node) =>
        node.Left.Accept(this) * node.Right.Accept(this);
}

public sealed class Printer : INodeVisitor<string>
{
    public string Visit(NumberNode node) => node.Value.ToString();
    public string Visit(AddNode node) =>
        $"({node.Left.Accept(this)} + {node.Right.Accept(this)})";
    public string Visit(MultiplyNode node) =>
        $"({node.Left.Accept(this)} * {node.Right.Accept(this)})";
}
```

## Worked example: (2 + 3) * 4

```csharp
Node expr = new MultiplyNode(
    new AddNode(new NumberNode(2), new NumberNode(3)),
    new NumberNode(4));

int value = expr.Accept(new Evaluator());   // 20
string text = expr.Accept(new Printer());   // "((2 + 3) * 4)"

Console.WriteLine($"{text} = {value}");    // ((2 + 3) * 4) = 20
```

Check the arithmetic by hand: `2 + 3 = 5`, then `5 * 4 = 20`. The printer wraps each binary operation in parentheses, so the output is exactly `((2 + 3) * 4)` — the inner `(2 + 3)` plus the outer `(... * 4)`.

## Double dispatch, explained

A normal virtual call dispatches on one thing: the receiver's runtime type. A visitor needs two: the node's type *and* the operation. That is double dispatch, and it happens in two steps:

1. `node.Accept(visitor)` — virtual dispatch on the **node** type picks the right `Accept` override.
2. Inside `Accept`, `visitor.Visit(this)` — overload resolution on the **static** type of `this` (e.g. `AddNode`) picks the right `Visit` method.

Neither step alone is enough; together they route to the exact operation for the exact node type, with no casts and no `switch`.

## Visitor vs switch: when to use which

| Situation | Better choice |
|-----------|---------------|
| Operations change often, node types are stable | Visitor |
| Node types change often, operations are few | Switch / pattern matching |
| You need the compiler to flag a missing case | Either — exhaustive `switch` or the visitor interface both do this |
| A one-off traversal in a small codebase | Switch — less ceremony |

The trade-off is real: adding a new node type means adding a method to the visitor interface and updating *every* visitor. Adding a new operation, though, means writing one new class and touching nothing else.

## Key Takeaways

- The Visitor pattern separates operations from the element hierarchy: nodes accept visitors, visitors implement one method per node type.
- Double dispatch is two steps: virtual dispatch on the node, then overload dispatch on the visitor.
- Worked result: `(2 + 3) * 4` evaluates to 20 and prints as `((2 + 3) * 4)`.
- Use a visitor when operations grow more often than node types; use a switch when node types grow more often.
- The compiler enforces completeness — a new node type breaks the build until every visitor handles it.
