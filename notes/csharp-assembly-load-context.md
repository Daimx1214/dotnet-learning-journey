# AssemblyLoadContext

`AssemblyLoadContext` is the .NET Core replacement for AppDomains. It controls how assemblies are loaded, lets you load the same assembly twice in isolation, and, since .NET Core 3.0, lets you unload assemblies again. The main use case is plugins.

## The Default Context

Every application has a default `AssemblyLoadContext` that loads the app's own assemblies and its dependencies. `Assembly.Load` loads into the default context. Assemblies loaded there can never be unloaded for the lifetime of the process, which is fine for an application's own code but a problem for code you want to replace at runtime.

## Custom Contexts for Plugins

A plugin host creates one context per plugin, so each plugin gets its own copy of its dependencies and can be unloaded independently:

```csharp
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        return null; // fall back to the default context
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}
```

`AssemblyDependencyResolver` reads the plugin's `.deps.json` file and maps assembly names to file paths, including native libraries. Returning `null` from `Load` means "I do not have this assembly, try the default context", which is how shared contracts (the interface the host and plugin both use) resolve to the single copy loaded by the host.

## Collectible Contexts and Unloading

A context created with `isCollectible: true` can be unloaded, but only when nothing references its assemblies anymore:

```csharp
var context = new PluginLoadContext(pluginPath);
Assembly pluginAssembly = context.LoadFromAssemblyPath(pluginPath);

Type pluginType = pluginAssembly.GetType("MyPlugin.Greeter")!;
IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
plugin.Run();

// Drop every reference, then unload.
plugin = null!;
pluginType = null!;
pluginAssembly = null!;
context.Unload();

// Unloading is asynchronous; the context finishes once the GC collects it.
for (int i = 0; i < 10 && context.IsAlive; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
}
```

Unloading fails silently in practice when a stray reference survives: a static event subscription, a cached `Type`, a leaked `Assembly` reference, or an object that escaped into the host. The usual debugging trick is to keep a `WeakReference` to the context and check `IsAlive` after forcing a collection. If it stays alive, something still holds a reference.

## Assembly.LoadFrom Pitfalls

`Assembly.LoadFrom(path)` looks convenient but has sharp edges:

- It loads into the default context, so the assembly can never be unloaded.
- If the same assembly is already loaded from a different path, the runtime may return the already-loaded copy or throw, depending on the version and identity, which produces confusing "type X is not assignable to type X" errors when two copies of the same interface exist.
- Its dependencies resolve from the application base directory first, not from the plugin's folder, so a plugin that ships its own version of a library can silently pick up the host's version instead.

Prefer `AssemblyLoadContext.LoadFromAssemblyPath` inside a custom context, with an `AssemblyDependencyResolver` handling dependency lookup. That gives each plugin its own dependency closure.

## Sharing Types Between Host and Plugin

The host and the plugin must agree on the contract types. The standard approach is to put the shared interfaces in a small assembly that the host loads in the default context, and make the plugin reference that same assembly. When the plugin context's `Load` returns `null` for the contract assembly, the runtime resolves it from the default context, so both sides see the identical `Type` object. If the plugin loads its own copy of the contract assembly instead, casts between host and plugin types fail even though the names match.

## When to Use It

- Plugin systems where plugins are loaded, updated, or removed without restarting the host.
- Scripting or code-generation hosts that compile user code at runtime and need to reclaim it.
- Test runners and tools that load user assemblies in isolation.
- Loading two incompatible versions of the same library side by side, one per context.

## Key Takeaways

- `AssemblyLoadContext` replaces AppDomains: custom contexts isolate a plugin's assemblies and, when collectible, allow unloading.
- `AssemblyDependencyResolver` maps assembly names to paths from the plugin's `.deps.json`, including native libraries.
- Unloading only completes when every reference to the context's types is gone; a single leaked reference keeps the whole context alive.
- `Assembly.LoadFrom` loads into the default context, never unloads, and can resolve dependencies from the wrong folder.
- Shared host-plugin contracts must resolve to one copy in the default context, or casts between the two sides fail despite identical names.
