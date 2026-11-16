# Unsafe Code and Pointers (C#)

C# is a memory-safe language by default: the garbage collector owns object lifetimes and the runtime checks array bounds. The `unsafe` keyword opts out of that safety net for a block of code, allowing raw pointers, pointer arithmetic, and direct memory access. It exists for the narrow cases where managed code cannot express what is needed — high-performance buffers, interop with native libraries, and bit-level manipulation — and it should be used sparingly, because every pointer bug the runtime would have caught becomes your problem.

## Enabling Unsafe Code

Unsafe code is off by default. A project enables it with `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the `.csproj`, or with the `-unsafe` compiler flag. Any method or block that uses pointers must be marked `unsafe`; the compiler refuses pointer types in safe code.

```xml
<!-- In the .csproj file: -->
<PropertyGroup>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

```csharp
public static unsafe void PrintAddress()
{
    int value = 42;
    int* pointer = &value; // Address-of operator — only legal in unsafe code.
    Console.WriteLine($"Value: {*pointer}, Address: {(long)pointer:X}");
}
```

## Pointer Types and Arithmetic

Pointers support the familiar C-style operations: dereference (`*p`), address-of (`&x`), increment, and arithmetic. Pointer arithmetic moves by the size of the pointed-to type, so `p + 1` on an `int*` advances four bytes, not one.

```csharp
public static unsafe int Sum(int* data, int length)
{
    int total = 0;
    for (int i = 0; i < length; i++)
        total += *(data + i); // Equivalent to data[i], but through pointer arithmetic.
    return total;
}

// Usage:
int[] numbers = [1, 2, 3, 4, 5];
fixed (int* p = numbers)
{
    Console.WriteLine(Sum(p, numbers.Length)); // Prints: 15
}
```

## The fixed Statement

The garbage collector moves objects in memory. A pointer to a managed object is only valid while the object is pinned, and that is what `fixed` does: it pins the object for the duration of the block so the GC cannot relocate it. Taking a pointer to a managed array or string without `fixed` is a compile error, and holding a pointer past the end of the `fixed` block is a bug that produces intermittent corruption.

```csharp
public static unsafe void ReverseInPlace(int[] array)
{
    fixed (int* start = array)
    {
        int* left = start;
        int* right = start + array.Length - 1;
        while (left < right)
        {
            (*left, *right) = (*right, *left);
            left++;
            right--;
        }
    } // The array is unpinned here; the pointers must not be used after this.
}
```

Value types on the stack (locals, `stackalloc` buffers) never move, so they do not need `fixed` — you can take their address directly inside an `unsafe` block.

## stackalloc and Span<T> Interop

`stackalloc` allocates a buffer on the stack, which is fast and allocation-free but limited in size and lifetime: the memory disappears when the method returns. Combined with `Span<T>`, it gives safe, bounds-checked access to a raw buffer, which is often the best of both worlds — no `unsafe` block is even needed for the span itself.

```csharp
public static void ProcessSmallBuffer()
{
    Span<int> buffer = stackalloc int[64]; // On the stack — no heap allocation.
    for (int i = 0; i < buffer.Length; i++)
        buffer[i] = i * i;

    // Span<T> can wrap an unsafe pointer without copying:
    unsafe
    {
        int* raw = stackalloc int[8];
        Span<int> wrapped = new Span<int>(raw, 8);
        wrapped[0] = 99;
    }
}
```

As a rule, prefer `Span<T>` over raw pointers: it carries its length, so indexing is bounds-checked, and it works in safe code. Reach for pointers only when `Span<T>` cannot express the operation — for example, when calling a native function that takes a pointer, or when doing arithmetic that spans do not support.

## When (Not) to Use Unsafe Code

**Legitimate uses:** P/Invoke with native libraries that take pointers, performance-critical inner loops over large buffers (image processing, serialization), and interop with memory-mapped hardware or files.

**Avoid it when:** a managed API already does the job (`Span<T>`, `Memory<T>`, `BinaryPrimitives`, `Array.Copy`), the performance gain is unmeasured, or the code is in a library consumed by others — unsafe code requires full trust and is banned in some sandboxed environments (such as certain plugin hosts). Unsafe code also opts out of bounds checking, so a single off-by-one error can corrupt memory silently instead of throwing `IndexOutOfRangeException`.

## Key Takeaways

- `unsafe` enables raw pointers and pointer arithmetic, but it must be switched on per project with `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `fixed` pins a managed object so the GC cannot move it while a pointer to it is in use; never use the pointer after the `fixed` block ends.
- `stackalloc` allocates a short-lived buffer on the stack, and `Span<T>` wraps raw memory with bounds checking — prefer spans over pointers wherever possible.
- Pointer arithmetic moves by the size of the pointed-to type, and there are no bounds checks: off-by-one errors corrupt memory silently.
- Use unsafe code only for native interop or measured performance hotspots; in every other case, the managed APIs are safer and usually just as fast.
