# ref structs in C#

A `ref struct` is a value type that is guaranteed to live only on the stack. That guarantee is what makes `Span<T>` safe: a span is a view over memory that must never outlive the memory it points at, and the compiler enforces that by restricting where `ref struct` values can go.

## Why ref structs Exist

`Span<T>` can point at stack memory (`stackalloc`), an array, or unmanaged memory. If a span pointing at a stack buffer could be stored in a heap object, the stack frame could return while the object still held the span — a dangling pointer. The compiler prevents this by making `Span<T>` a `ref struct` and forbidding every escape route.

## A ref struct Wrapper

```csharp
public ref struct IntSpanView
{
    private readonly Span<int> _span;

    public IntSpanView(Span<int> span) => _span = span;

    public int Sum()
    {
        int total = 0;
        foreach (int value in _span)
        {
            total += value;
        }
        return total;
    }
}
```

A `ref struct` may hold a `Span<T>` field — that is one of the few things a `ref struct` can do that a regular struct cannot.

## Worked Example: stackalloc and Sum

```csharp
Span<int> numbers = stackalloc int[] { 1, 2, 3, 4 };

int sum = 0;
foreach (int n in numbers)
{
    sum += n;
}
Console.WriteLine(sum); // 10

var view = new IntSpanView(numbers);
Console.WriteLine(view.Sum()); // 10
```

Checked by hand: 1 + 2 = 3, 3 + 3 = 6, 6 + 4 = 10. Both the direct loop and the wrapper print `10`. The buffer lives on the stack, so no heap allocation happens at all.

## The Rules

A `ref struct` cannot:

- **Be boxed.** Assigning one to `object` or an interface type is a compile error — boxing would move it to the heap.
- **Be a field of a class or a regular struct.** A heap object could outlive the stack frame, so the compiler forbids it. (A `ref struct` *can* hold another `ref struct` as a field.)
- **Be captured by a lambda or used in an `async` method.** Both can outlive the current stack frame: a lambda may run later, and an `async` method's locals are hoisted into a heap state machine when it awaits.
- **Be a generic type argument**, except in a small set of specially allowed cases. `List<Span<int>>` does not compile. Types like `Span<T>` itself are permitted because the runtime knows they are stack-only.
- **Be used as an array element type, or as a field in a non-`ref` struct.** `Span<int>[]` is illegal for the same lifetime reason.
- **Implement an interface.** Interfaces imply boxing, which is forbidden.

```csharp
Span<int> span = stackalloc int[] { 1, 2, 3 };

// All of the following are compile errors:
object boxed = span;                 // cannot box
Func<int> f = () => span.Length;     // cannot capture in a lambda
List<Span<int>> list = new();        // cannot be a generic type argument
```

## ref struct vs Regular struct

| Aspect | Regular `struct` | `ref struct` |
|---|---|---|
| Storage | Stack or heap (when boxed or in a class field) | Stack only, always |
| Boxing | Allowed | Forbidden |
| Field of a class | Allowed | Forbidden |
| Field of another struct | Allowed | Only inside another `ref struct` |
| `Span<T>` field | Forbidden | Allowed |
| `async` / lambda capture | Allowed | Forbidden |
| Implements interfaces | Allowed | Forbidden |

A regular struct is a value type with no lifetime promises; a `ref struct` trades freedom for a compile-time guarantee that the value never escapes the stack.

## When to Use Them

Reach for `ref struct` when you are writing performance-sensitive code that works with spans — parsers, serializers, and hot loops over buffers. For ordinary application code, `Span<T>` and `ReadOnlySpan<T>` already cover the common cases, and you rarely need to declare your own `ref struct`. If you do, keep it small, keep its methods synchronous, and never try to store it anywhere.

## Key Takeaways

- A `ref struct` is guaranteed stack-only, which is what makes `Span<T>` memory-safe.
- Worked example: `stackalloc` `Span<int>` over `[1, 2, 3, 4]` sums to `10` (1 + 2 + 3 + 4).
- A `ref struct` may hold a `Span<T>` field; a regular struct may not.
- Rules: no boxing, no field of a class or regular struct, no `async` or lambda capture, no generic type argument except for span-like types, no interface implementation.
- Use `ref struct` for span-based hot paths (parsers, serializers); ordinary code should just use `Span<T>` directly.
