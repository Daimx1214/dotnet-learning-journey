# P/Invoke and .NET Interop

P/Invoke (Platform Invoke) lets managed C# code call functions in unmanaged native libraries such as Win32 APIs or C libraries. It is the bridge between .NET and the operating system.

## The Classic Way: DllImport

Mark a static extern method with `[DllImport]` and the runtime generates a stub that marshals arguments across the managed/unmanaged boundary.

```csharp
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetTickCount();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
```

`SetLastError = true` lets you read the native error with `Marshal.GetLastWin32Error()`. `CharSet.Unicode` controls how strings are converted; the default on .NET Core is Unicode, but being explicit avoids surprises.

## Marshalling: How Data Crosses the Boundary

The marshaller converts managed types to native representations:

| C# type | Native type |
|---------|-------------|
| `int`, `uint` | 32-bit integer |
| `long` | 64-bit integer |
| `bool` | 4-byte Windows BOOL by default |
| `string` | null-terminated char* / wchar_t* |
| `IntPtr` | pointer-sized integer |
| `byte[]` | pointer to first element |

Value types are blittable when their memory layout is identical on both sides — `int`, `long`, and structs made only of blittable fields copy with no conversion. Strings and arrays are not blittable and require conversion, which costs time and allocations.

Structs need an explicit layout:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct Point
{
    public int X;
    public int Y;
}
```

`LayoutKind.Sequential` guarantees fields are laid out in declaration order, matching a C struct.

## The Modern Way: LibraryImport (.NET 7+)

`[LibraryImport]` is a source generator that produces the marshalling code at compile time instead of generating IL stubs at runtime. It is faster, trims better for Native AOT, and removes the runtime cost of stub generation.

```csharp
using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint GetTickCount();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
```

Differences from `DllImport`:

- The method must be `partial` and the containing type must be `partial`.
- String marshalling is set with `StringMarshalling` instead of `CharSet`.
- Only a supported set of types can be marshalled; exotic cases fall back to `DllImport` or custom marshallers.

For new code, prefer `LibraryImport`. For libraries that need custom marshalling or target older runtimes, `DllImport` still works.

## SafeHandle: Owning Native Resources

Never hold a native handle as a raw `IntPtr` in long-lived code. A `SafeHandle` owns the handle, releases it exactly once in its finalizer, and is safe against handle recycling attacks.

```csharp
internal sealed class SafeFileHandle : SafeHandle
{
    public SafeFileHandle() : base(IntPtr.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

    protected override bool ReleaseHandle()
    {
        return CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
```

Prefer the built-in `SafeFileHandle` from `Microsoft.Win32.SafeHandles` when it fits; write a custom subclass only for handle types the framework does not cover.

## Common Pitfalls

- **Calling convention**: the default is `CallingConvention.Winapi` (stdcall on Windows). A C library using cdecl needs `CallingConvention = CallingConvention.Cdecl`, or the stack gets corrupted.
- **String lifetime**: a string passed to native code is pinned only for the duration of the call. Native code must not keep the pointer.
- **Bool size**: C# `bool` marshals as a 4-byte BOOL. A C `bool` (1 byte) needs `[MarshalAs(UnmanagedType.U1)]`.
- **Struct packing**: if the native struct uses a non-default alignment, add `Pack = N` to `StructLayout`.

## Key Takeaways

- `DllImport` is the classic runtime-generated stub; `LibraryImport` (.NET 7+) generates marshalling code at compile time and is the modern default.
- Only blittable types cross the boundary for free; strings and arrays pay a conversion cost.
- Always set `SetLastError = true` when the native API reports errors via GetLastError.
- Own native handles with `SafeHandle`, never with a bare `IntPtr` field.
- Match the calling convention and the bool size to the native signature, or the failure will be silent and strange.
