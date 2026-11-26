# Proxy Pattern

The Proxy pattern provides a stand-in object that controls access to another object. The proxy has the same interface as the real object, so the client cannot tell them apart, but the proxy adds something around the call: delaying creation, checking permission, caching the result, or logging the access.

## The Problem It Solves

A document viewer opens a file with fifty high-resolution images. Loading all fifty at startup makes the app slow to open, even though the user may only look at the first page. A proxy stands in for each image, loads the real image only when it is actually displayed, and then hands the work to the real object. The client code never changes: it asks the proxy for the image exactly as it would ask the real one.

## The Main Kinds of Proxy

- **Virtual (lazy) proxy**: delays creating an expensive object until it is first needed.
- **Protection proxy**: checks whether the caller is allowed to use the real object.
- **Caching proxy**: stores the result of an expensive call and returns it on repeat requests.
- **Remote proxy**: stands in for an object in another process or on another machine.
- **Logging proxy**: records calls before forwarding them, useful for diagnostics.

## Example: Lazy Image Loading

The shared interface, and the real object, which is expensive because loading from disk happens in the constructor:

```csharp
public interface IImage
{
    void Display();
}

public sealed class RealImage : IImage
{
    private readonly string _fileName;

    public RealImage(string fileName)
    {
        _fileName = fileName;
        Console.WriteLine($"Loading {_fileName} from disk...");
    }

    public void Display() => Console.WriteLine($"Displaying {_fileName}.");
}
```

The proxy implements the same interface and creates the real image only on first use:

```csharp
public sealed class ImageProxy : IImage
{
    private readonly string _fileName;
    private RealImage? _realImage;

    public ImageProxy(string fileName) => _fileName = fileName;

    public void Display()
    {
        _realImage ??= new RealImage(_fileName);
        _realImage.Display();
    }
}
```

A document can hold fifty proxies and pay the loading cost only for the images the user actually views:

```csharp
var images = new List<IImage>
{
    new ImageProxy("page1.png"),
    new ImageProxy("page2.png"),
    new ImageProxy("page3.png")
};

images[0].Display();
// Output: Loading page1.png from disk...
//         Displaying page1.png.
// page2 and page3 cost nothing until displayed.
```

## Protection Proxy Example

The same shape controls access instead of timing:

```csharp
public sealed class DocumentProxy : IDocument
{
    private readonly RealDocument _document;
    private readonly string _currentUserRole;

    public DocumentProxy(RealDocument document, string currentUserRole)
    {
        _document = document;
        _currentUserRole = currentUserRole;
    }

    public string Read()
    {
        if (_currentUserRole != "Admin" && _currentUserRole != "Editor")
            throw new UnauthorizedAccessException("You do not have permission to read this document.");
        return _document.Read();
    }
}
```

The client depends only on `IDocument`, so swapping the proxy in or out changes no calling code.

## Caching Proxy

A caching proxy remembers the last result and skips the real call on repeats:

```csharp
public sealed class CachedWeatherService : IWeatherService
{
    private readonly IWeatherService _inner;
    private string? _cachedForecast;
    private DateTime _cachedAt;

    public CachedWeatherService(IWeatherService inner) => _inner = inner;

    public string GetForecast()
    {
        if (_cachedForecast is not null && DateTime.UtcNow - _cachedAt < TimeSpan.FromMinutes(10))
            return _cachedForecast;
        _cachedForecast = _inner.GetForecast();
        _cachedAt = DateTime.UtcNow;
        return _cachedForecast;
    }
}
```

The second call within ten minutes returns the cached value and never reaches the real service.

## Proxy in .NET

- `Lazy<T>` is the built-in lazy proxy: the value is created only on first access through `.Value`.
- Entity Framework Core uses proxies for lazy loading of navigation properties, loading related data only when it is read.
- Castle DynamicProxy and similar libraries generate proxies at runtime for interception, which is how many AOP and mocking tools work.

## Key Takeaways

- Proxy stands in for a real object with the same interface, adding control around the call.
- Virtual proxies delay expensive creation until first use; protection proxies check permission; caching proxies avoid repeat work.
- The client never knows it is talking to a proxy, so proxies can be added or removed without changing calling code.
- `Lazy<T>` is the built-in .NET form of the lazy proxy.
- Do not use a proxy when the real object is cheap: the extra layer then buys nothing.
