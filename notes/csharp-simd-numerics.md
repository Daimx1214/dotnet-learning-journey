# SIMD and System.Numerics.Vector<T>

SIMD (Single Instruction, Multiple Data) lets one CPU instruction operate on several values at once. In .NET, `System.Numerics.Vector<T>` exposes this: a `Vector<float>` on a machine with AVX2 holds 8 floats and processes them in a single instruction, while `Vector.IsHardwareAccelerated` tells you whether the runtime actually uses SIMD or falls back to software. The speedup is real for tight numeric loops and zero for everything else — knowing which is which is the whole skill.

## The Basics

`Vector<T>` works with `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, and `double`. `Vector<T>.Count` reports how many elements fit in one hardware vector: 8 for `float` on AVX2 (256-bit), 4 for `float` on SSE2 (128-bit), and 2 for `double` on SSE2. Code should always loop in strides of `Vector<T>.Count` and handle the leftover tail elements with a scalar loop.

```csharp
using System.Numerics;

// Element-wise addition with SIMD, with a scalar tail for leftovers.
static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int i = 0;
    int n = a.Length;
    if (Vector.IsHardwareAccelerated)
    {
        int simdLength = n - n % Vector<float>.Count;
        for (; i < simdLength; i += Vector<float>.Count)
        {
            var va = new Vector<float>(a[i..]);
            var vb = new Vector<float>(b[i..]);
            (va + vb).CopyTo(result[i..]);
        }
    }
    for (; i < n; i++) // scalar tail
        result[i] = a[i] + b[i];
}
```

## A Measured Speedup Example

Summing 10 million floats: a plain scalar loop takes roughly 8–10 ms on a modern laptop CPU, while the SIMD version using `Vector<float>` with AVX2 takes roughly 2–3 ms — a 3–4x speedup, close to the theoretical 8x for 8-wide floats but limited by memory bandwidth. The same loop over 100 floats shows no measurable difference, because the setup cost dominates. Rule of thumb: SIMD pays off above a few thousand elements, or when the loop runs millions of times per second.

```csharp
// Dot product: the classic SIMD showcase.
static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    var acc = Vector<float>.Zero;
    int i = 0;
    int n = a.Length;
    int simdLength = n - n % Vector<float>.Count;
    for (; i < simdLength; i += Vector<float>.Count)
        acc += new Vector<float>(a[i..]) * new Vector<float>(b[i..]);
    float sum = Vector.Sum(acc); // horizontal add of the lanes
    for (; i < n; i++)
        sum += a[i] * b[i];
    return sum;
}

// DotProduct over 1,000,000 floats: scalar ~0.9 ms, SIMD ~0.25 ms (AVX2).
```

## Checking Hardware Support

```csharp
Console.WriteLine($"Hardware accelerated: {Vector.IsHardwareAccelerated}");
Console.WriteLine($"float lanes per vector: {Vector<float>.Count}");
Console.WriteLine($"double lanes per vector: {Vector<double>.Count}");
Console.WriteLine($"int lanes per vector: {Vector<int>.Count}");

// Typical output on an AVX2 machine:
// Hardware accelerated: True
// float lanes per vector: 8
// double lanes per vector: 4
// int lanes per vector: 8
```

On ARM64 (Apple Silicon, modern phones), `Vector<T>` maps to NEON with 128-bit vectors: 4 floats per vector. On a machine without SIMD support, `Vector.IsHardwareAccelerated` is false and `Vector<T>.Count` equals 1 — the same code still works, just without speedup. Always gate the SIMD path on `IsHardwareAccelerated` and keep the scalar fallback.

## When SIMD Helps and When It Does Not

SIMD helps for: element-wise arithmetic over large arrays, dot products, image pixel processing, audio sample processing, and checksum/hash loops. It does not help for: branching-heavy code (conditions break vectorization), pointer-chasing data structures (linked lists, trees), small arrays, or string processing (use `Span<T>` and `SearchValues<T>` instead). Memory bandwidth is the usual ceiling: once the loop is memory-bound, wider vectors stop helping — summing floats at 3 GB/s of bandwidth cannot go faster no matter how wide the vector is.

For finer control, `System.Runtime.Intrinsics` (`Avx2`, `Sse2`, `AdvSimd`) exposes individual CPU instructions, and `Vector128<T>` / `Vector256<T>` / `Vector512<T>` give fixed-width vectors. Prefer `Vector<T>` for portable code and intrinsics only when a specific instruction (like `_mm256_shuffle_epi8`) is required.

## Common Pitfalls

- Reading past the end of the array: `new Vector<float>(span)` requires at least `Vector<float>.Count` elements remaining — the tail loop exists for this reason.
- Forgetting the scalar tail silently drops the last few elements.
- Assuming `Vector<T>.Count` is constant: it varies by machine, so never hardcode 8 or 4.
- Using `Vector<T>` on `decimal` or `bool`: not supported, it throws `NotSupportedException`.
- Benchmarking with tiny inputs and concluding SIMD is slow: measure at realistic sizes with BenchmarkDotNet, not with a 10-element array.

## Key Takeaways

- `System.Numerics.Vector<T>` processes `Vector<T>.Count` elements per instruction; check `Vector.IsHardwareAccelerated` before relying on it.
- Always stride by `Vector<T>.Count` and finish with a scalar tail loop for leftover elements.
- Realistic speedups are 3–4x for large float arrays (e.g. summing 10M floats: ~9 ms scalar vs ~2.5 ms SIMD on AVX2), and negligible for small arrays.
- SIMD helps dense numeric loops and is useless for branching or pointer-chasing code; memory bandwidth is the usual ceiling.
- `Vector<T>.Count` varies by hardware — never hardcode it — and `Vector<T>` does not support `decimal` or `bool`.
