# C# Expression Trees (C#)

An expression tree is a LINQ query represented as data instead of code. Writing `Expression<Func<int, bool>> predicate = x => x > 5;` does not create a delegate — the compiler builds a tree of `Expression` nodes (`ParameterExpression`, `BinaryExpression`, `ConstantExpression`, …) describing the lambda. That tree can be inspected, rewritten, combined with other trees, and compiled into executable code at runtime. This is the mechanism that lets EF Core translate `Where(p => p.Price > 10)` into SQL: the provider walks the tree and emits SQL instead of running C#.

## Expression<T> vs Func<T>

The same lambda syntax produces two very different things depending on the declared type:

```csharp
// A delegate: compiled to IL, executed directly.
Func<int, bool> func = x => x > 5;
bool result = func(7); // true

// An expression tree: data describing the lambda, not executable code.
Expression<Func<int, bool>> expr = x => x > 5;
Console.WriteLine(expr.Body.NodeType);            // GreaterThan
Console.WriteLine(expr.Body);                     // (x > 5)
Console.WriteLine(expr.Parameters[0].Name);       // x

// Compile the tree into a delegate when you need to run it.
Func<int, bool> compiled = expr.Compile();
bool sameResult = compiled(7); // true
```

Compiling an expression tree costs far more than calling a delegate, so compile once and cache the result if the delegate will be reused.

## Building Trees by Hand

The `Expression` factory methods construct nodes explicitly. This is how dynamic filters are built at runtime — for example, a search screen where the user picks a field and an operator, and the code assembles the predicate instead of writing dozens of `if` branches.

```csharp
using System.Linq.Expressions;

// Builds: product => product.Price > threshold
public static Expression<Func<Product, bool>> PriceGreaterThan(decimal threshold)
{
    ParameterExpression product = Expression.Parameter(typeof(Product), "product");
    MemberExpression price = Expression.Property(product, nameof(Product.Price));
    ConstantExpression limit = Expression.Constant(threshold);
    BinaryExpression comparison = Expression.GreaterThan(price, limit);
    return Expression.Lambda<Func<Product, bool>>(comparison, product);
}

// Use it exactly like a hand-written lambda:
Expression<Func<Product, bool>> filter = PriceGreaterThan(100m);
List<Product> expensive = db.Products.Where(filter).ToList();
```

## Combining and Rewriting Trees

Because trees are data, predicates can be combined with `Expression.AndAlso` / `Expression.OrElse`, provided both share the same `ParameterExpression` instance. An `ExpressionVisitor` subclass can walk a tree and replace nodes — for example, swapping one parameter for another, or rewriting a custom method call into something a LINQ provider understands. EF Core itself is a giant expression visitor: it translates the tree into SQL and throws `InvalidOperationException` when it meets a node it cannot translate, which is why calling an arbitrary C# method inside `Where(...)` fails at runtime.

```csharp
// Combine two predicates that share one parameter instance.
public static Expression<Func<T, bool>> And<T>(
    Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
{
    // Rewrite 'right' to use 'left's parameter, then AND the bodies.
    var replacer = new ParameterReplacer(right.Parameters[0], left.Parameters[0]);
    Expression rightBody = replacer.Visit(right.Body);
    return Expression.Lambda<Func<T, bool>>(
        Expression.AndAlso(left.Body, rightBody), left.Parameters);
}

private sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from, _to;
    public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        => (_from, _to) = (from, to);

    protected override Expression VisitParameter(ParameterExpression node) =>
        node == _from ? _to : base.VisitParameter(node);
}
```

## Key Takeaways

- `Expression<Func<...>>` stores a lambda as a tree of nodes; `Func<...>` stores it as executable IL.
- LINQ providers (EF Core, LINQ to XML) walk the tree to translate it — usually to SQL — instead of running it.
- Build trees by hand with the `Expression` factory methods to create dynamic filters at runtime.
- Combine predicates with `AndAlso`/`OrElse` after unifying the parameter, and rewrite trees with an `ExpressionVisitor`.
- Compile a tree with `.Compile()` only when you need to execute it, and cache the delegate — compilation is expensive.
