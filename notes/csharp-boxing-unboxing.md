# Boxing and Unboxing in C#

Boxing converts a value type (such as `int` or a `struct`) into a reference type (`object` or an interface) by copying the value onto the heap inside an object wrapper. Unboxing does the reverse: it checks the wrapper's type and copies the value back out to the stack. Both operations cost time and memory, and boxing in particular has traps that are easy to miss.

## What Happens on the Heap

When a value type is boxed, the runtime allocates a new object on the heap, copies the value's bytes into it, and returns a reference. The original value and the boxed copy are independent afterwards — changing one does not change the other.

```csharp
int x = 5;
object boxed = x;   // Boxing: allocates a heap object holding a copy of 5.
x = 10;
Console.WriteLine(boxed); // 5 — the boxed copy was not affected.
Console.WriteLine(x);     // 10

int y = (int)boxed; // Unboxing: type-checked copy back out of the heap object.
Console.WriteLine(y); // 5
```

Unboxing is a cast, so it throws `InvalidCastException` if the type is wrong: `(long)boxed` fails even though `int` converts to `long` implicitly, because unboxing requires the exact type first — `(long)(int)boxed` is the correct two-step form.

## Performance Cost

Each boxing operation allocates a heap object (the value's bytes plus the object header) and creates work for the garbage collector later. That is cheap once, but expensive in a loop: boxing inside a loop that runs 1,000 times performs 1,000 separate heap allocations.

```csharp
// 1,000 allocations: every Add boxes the int into an object.
var list = new System.Collections.ArrayList();
for (int i = 0; i < 1000; i++)
{
    list.Add(i);
}
Console.WriteLine(list.Count); // 1000

// Zero boxing: List<int> stores the ints directly, with no heap wrapper per item.
var generic = new List<int>();
for (int i = 0; i < 1000; i++)
{
    generic.Add(i);
}
Console.WriteLine(generic.Count); // 1000
```

The same cost applies when a value type is passed to a method taking `object`, interpolated into a string without a matching overload, or stored in a non-generic collection. The fix is almost always the same: use the generic collection or the typed overload.

## Common Traps

Boxing in loops is the first trap, shown above. The second is reference equality on boxed values: each boxing creates a new object, so two boxes of equal values are different objects.

```csharp
object a = 1;
object b = 1;
Console.WriteLine(a == b);          // False — different heap objects.
Console.WriteLine(a.Equals(b));     // True — Equals compares the values.
Console.WriteLine((int)a == (int)b); // True — unbox first, then compare values.
```

The third trap is `GetHashCode` and `Equals` on boxed values. They work correctly — a boxed `int` returns the `int` hash code — but they run through a virtual dispatch on the heap object, and calling them repeatedly on values boxed in a loop pays the allocation cost every time. A subtler variant: a mutable struct boxed once and then mutated through the original variable does not change the boxed copy, which surprises developers who expect reference behaviour.

```csharp
object o = 42;
Console.WriteLine(o.GetHashCode()); // 42 — Int32.GetHashCode returns the value itself.
Console.WriteLine(o.Equals(42));    // True — boxed Equals unboxes and compares values.
Console.WriteLine(o.Equals(43));    // False.
```

## Struct vs Class

The distinction drives when boxing happens at all:

- A `struct` is a value type: assignment copies the value, it lives on the stack (or inline inside its container), and assigning it to `object` or an interface boxes it.
- A `class` is a reference type: assignment copies the reference, it always lives on the heap, and assigning it to `object` never boxes — the reference is used directly.

```csharp
struct PointStruct { public int X; public int Y; }
class PointClass { public int X; public int Y; }

var s = new PointStruct { X = 1, Y = 2 };
object boxedStruct = s;   // Boxing: a heap copy is made.
var c = new PointClass { X = 1, Y = 2 };
object sameRef = c;       // No boxing: sameRef and c point at the same object.
c.X = 99;
Console.WriteLine(((PointClass)sameRef).X); // 99 — the change is visible.
Console.WriteLine(((PointStruct)boxedStruct).X); // 1 — the boxed copy is untouched.
```

A struct that implements an interface boxes when it is used through that interface, for the same reason: the interface reference needs a heap object. Generics avoid this when the type parameter is the struct itself — `List<int>` and a generic method `void M<T>(T value)` with `T = int` do not box — because the JIT generates a specialized version for the value type.

## Key Takeaways

- Boxing copies a value type onto the heap inside a new object; unboxing copies it back out and requires an exact-type cast.
- Boxing in a loop allocates once per iteration — 1,000 iterations means 1,000 heap objects — so prefer generic collections such as `List<int>` over `ArrayList`.
- Two separately boxed equal values are not reference-equal (`==` is `False`) but are value-equal (`Equals` is `True`).
- Mutating a struct after boxing it never changes the boxed copy, because boxing copied the value.
- Classes never box on assignment to `object`; structs box whenever they are treated as `object` or an interface, but generics specialized for the value type avoid it.
