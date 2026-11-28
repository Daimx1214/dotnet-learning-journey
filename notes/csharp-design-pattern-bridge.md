# Bridge Pattern in C#

The Bridge pattern separates an abstraction from its implementation so the two can vary independently. Instead of one class hierarchy that explodes with every combination, you get two smaller hierarchies connected by composition.

## The Problem

Imagine drawing shapes on different rendering backends: a circle on a raster screen, a circle on a vector printer, a square on both. With inheritance alone you need `RasterCircle`, `VectorCircle`, `RasterSquare`, `VectorSquare`, and a new class for every shape-backend pair. Adding one shape or one backend multiplies the class count.

## The Bridge Structure

The abstraction (the shape) holds a reference to an implementor (the renderer) and delegates the low-level work to it. The shape knows *what* to draw; the renderer knows *how* to draw it.

```csharp
// Implementor: the rendering backend
public interface IRenderer
{
    void RenderCircle(float radius);
    void RenderRectangle(float width, float height);
}

public sealed class RasterRenderer : IRenderer
{
    public void RenderCircle(float radius) =>
        Console.WriteLine($"Raster: drawing circle, radius {radius}");

    public void RenderRectangle(float width, float height) =>
        Console.WriteLine($"Raster: drawing rectangle {width}x{height}");
}

public sealed class VectorRenderer : IRenderer
{
    public void RenderCircle(float radius) =>
        Console.WriteLine($"Vector: drawing circle, radius {radius}");

    public void RenderRectangle(float width, float height) =>
        Console.WriteLine($"Vector: drawing rectangle {width}x{height}");
}

// Abstraction: the shape
public abstract class Shape
{
    protected readonly IRenderer Renderer;

    protected Shape(IRenderer renderer) => Renderer = renderer;

    public abstract void Draw();
}

public sealed class Circle : Shape
{
    private readonly float _radius;

    public Circle(IRenderer renderer, float radius) : base(renderer) =>
        _radius = radius;

    public override void Draw() => Renderer.RenderCircle(_radius);
}

public sealed class Rectangle : Shape
{
    private readonly float _width;
    private readonly float _height;

    public Rectangle(IRenderer renderer, float width, float height)
        : base(renderer)
    {
        _width = width;
        _height = height;
    }

    public override void Draw() => Renderer.RenderRectangle(_width, _height);
}
```

```csharp
Shape circle = new Circle(new VectorRenderer(), 5);
circle.Draw(); // Vector: drawing circle, radius 5

Shape rect = new Rectangle(new RasterRenderer(), 10, 4);
rect.Draw();   // Raster: drawing rectangle 10x4
```

With 3 shapes and 3 renderers you write 6 classes, not 9. Adding a fourth renderer costs one class, not three.

## Bridge vs Adapter

Both patterns involve an interface sitting between two parts, but their intent and timing differ:

| | Bridge | Adapter |
|---|---|---|
| Designed | Up front, before both sides exist | After the fact, to retrofit an existing class |
| Purpose | Let abstraction and implementation vary independently | Make an incompatible interface work with a client |
| Structure | Two parallel hierarchies linked by composition | One wrapper around one adaptee |

Use Bridge when you control both sides and expect both to grow. Use Adapter when you cannot change the class you need to use, for example a third-party library whose interface does not match yours.

## When to Use It

- You have two dimensions of variation, such as shape and renderer, or message and transport channel.
- You want to switch the implementation at runtime, for example choosing a renderer based on a config value.
- You want to hide implementation details from clients of the abstraction.

## Common Pitfalls

- Do not confuse the abstraction with a mere wrapper: the abstraction should add its own high-level behaviour, not just forward calls.
- Keep the implementor interface small and stable. If it changes often, every implementor breaks.
- A Bridge with only one implementor is over-engineering; add the second one only when the need is real.

## Key Takeaways

- Bridge uses composition to decouple an abstraction from its implementation so each can change independently.
- It prevents a combinatorial explosion of subclasses when two dimensions vary.
- Bridge is designed up front; Adapter retrofits an existing incompatible interface.
- In C#, the abstraction typically takes the implementor through its constructor as an interface.
