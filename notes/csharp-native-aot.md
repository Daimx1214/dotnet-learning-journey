# C# Native AOT Publishing

Native AOT compiles a .NET app ahead of time into a single native binary — no JIT, no runtime install needed on the target machine. It is the closest .NET gets to a Go- or Rust-style native executable.

## What It Is and When to Use It

With Native AOT, the IL is compiled to native code at publish time and linked with a trimmed runtime. The result starts in milliseconds, uses less memory, and ships as one self-contained file. It shines for CLI tools, short-lived functions, and containerised microservices where cold start matters.

It is a poor fit for apps that depend heavily on runtime code generation or dynamic loading: plugins loaded with `Assembly.LoadFrom`, heavy reflection over unknown types, or libraries that emit code at runtime. Regular ASP.NET Core APIs work well, but the app must be trim-compatible.

## Startup and Size Benefits

A Native AOT API can start in roughly 10–50 ms (versus hundreds of milliseconds for a JIT app that also warms up), and the published binary is typically 10–30 MB for a small API. There is no JIT warm-up, so the first request is as fast as the hundredth, and memory stays lower because no JIT-compiled code is generated at runtime.

The trade-off is publish time (native compilation is slower), a larger build machine requirement, and no dynamic features. It is a deployment-time choice, not a code rewrite — the same source usually works, but trim warnings must be addressed.

## Trim Compatibility and Reflection Limits

The trimmer removes IL that static analysis cannot prove is reachable. Code that uses reflection to find types at runtime — `Type.GetType(name)`, `Activator.CreateInstance`, serialising an unregistered type with `System.Text.Json` — can break because the needed code was trimmed away.

```csharp
// BAD: reflection-based JSON serialisation is not trim-safe
JsonSerializer.Serialize(order);

// GOOD: use a source-generated context (compile-time known types)
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(List<Order>))]
internal partial class AppJsonContext : JsonSerializerContext;

JsonSerializer.Serialize(order, AppJsonContext.Default.Order);
```

Annotate reflection-heavy code with `[DynamicallyAccessedMembers]` or `[RequiresUnreferencedCode]` where needed, and treat every trim warning (`IL2026`, `IL3050`, etc.) as a real bug to fix, not noise to suppress. Libraries that are not trim-compatible will produce warnings during publish.

## Publishing with PublishAot

Enable it in the project file, then publish for a specific runtime identifier.

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization> <!-- smaller binary, no ICU data -->
</PropertyGroup>
```

```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

`InvariantGlobalization` drops culture-specific data and shrinks the binary further when the app does not need locale-aware formatting. Always publish for an explicit RID (`linux-x64`, `win-x64`, `osx-arm64`); Native AOT has no runtime-agnostic output. For ASP.NET Core, also add the `Microsoft.AspNetCore.OpenApi` source-generator friendly setup and check the framework's AOT compatibility notes for the version you target.

## Key Takeaways

- Native AOT produces a single native binary with millisecond startup and no JIT warm-up.
- Best for CLIs, functions, and microservices where cold start and size matter; poor fit for plugin/dynamic-loading apps.
- The trimmer removes unreachable IL, so reflection over unknown types breaks unless the code is trim-safe.
- `System.Text.Json` needs source-generated `JsonSerializerContext` types for AOT; reflection-based serialisation is not supported.
- Publish with `<PublishAot>true</PublishAot>` and an explicit RID; treat trim warnings as bugs.
- `InvariantGlobalization` shrinks the binary further when locale-aware formatting is not needed.
