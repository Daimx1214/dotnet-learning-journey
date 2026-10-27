# C# — Value Types vs Reference Types

- Value types (int, double, bool, struct) store the data itself; assigning one variable to another copies the value.
- Reference types (class, string, arrays) store a reference to the object; two variables can point to the same object.
- Value types are typically stored on the stack, reference type objects on the heap.
- Passing a value type to a method passes a copy, unless ref or out is used.
- string is a reference type but behaves like a value type because it is immutable.

Example: int a = 5; int b = a; changing b does not change a — but two class variables sharing one object will both see changes made through either.
