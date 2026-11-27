# Abstract Factory Pattern in C#

The Abstract Factory pattern provides an interface for creating **families of related objects** without specifying their concrete classes. The client asks a factory for a button, a checkbox, and a window — and gets a matching set for Windows or macOS — without ever knowing which operating system it is running on.

## The Problem It Solves

Imagine a UI library that must render native-looking controls on Windows and macOS. If client code writes `if (os == "Windows") new WinButton() else new MacButton()` everywhere, every new platform (or new control) means touching code in dozens of places. Abstract Factory moves that decision to one place: pick the factory once, then use it everywhere.

## The Pattern at a Glance

```csharp
// Abstract products
public interface IButton { void Render(); }
public interface ICheckbox { void Render(); }

// Abstract factory — creates a FAMILY of products
public interface IUiFactory
{
    IButton CreateButton();
    ICheckbox CreateCheckbox();
}
```

## Concrete Factories and Products

```csharp
// Windows family
public sealed class WinButton : IButton
{
    public void Render() => Console.WriteLine("Windows Button");
}

public sealed class WinCheckbox : ICheckbox
{
    public void Render() => Console.WriteLine("Windows Checkbox");
}

public sealed class WinFactory : IUiFactory
{
    public IButton CreateButton() => new WinButton();
    public ICheckbox CreateCheckbox() => new WinCheckbox();
}

// macOS family
public sealed class MacButton : IButton
{
    public void Render() => Console.WriteLine("macOS Button");
}

public sealed class MacCheckbox : ICheckbox
{
    public void Render() => Console.WriteLine("macOS Checkbox");
}

public sealed class MacFactory : IUiFactory
{
    public IButton CreateButton() => new MacButton();
    public ICheckbox CreateCheckbox() => new MacCheckbox();
}
```

## Client Code — Platform Agnostic

The client depends only on the abstract factory and abstract products. It never mentions `WinButton` or `MacButton`:

```csharp
public sealed class Dialog
{
    private readonly IButton _button;
    private readonly ICheckbox _checkbox;

    public Dialog(IUiFactory factory)
    {
        _button = factory.CreateButton();
        _checkbox = factory.CreateCheckbox();
    }

    public void Render()
    {
        _button.Render();
        _checkbox.Render();
    }
}

IUiFactory factory = OperatingSystem.IsWindows()
    ? new WinFactory()
    : new MacFactory();

var dialog = new Dialog(factory);
dialog.Render();
```

## Worked Output

Running the client with `WinFactory` prints:

```text
Windows Button
Windows Checkbox
```

Running the same client with `MacFactory` prints:

```text
macOS Button
macOS Checkbox
```

The `Dialog` class is identical in both runs — only the factory injected at the top changes. That is the whole point: one client, many product families.

## Abstract Factory vs Factory Method

| Aspect | Factory Method | Abstract Factory |
|---|---|---|
| Creates | One product | A family of related products |
| Structure | One method, subclasses override it | One interface with several creation methods |
| Guarantees | Nothing about related products | Products from one factory belong together |
| Example | `CreateButton()` overridden per OS | `IUiFactory` with `CreateButton()` + `CreateCheckbox()` |

Use Factory Method when you need one product and the subclass decides the type. Use Abstract Factory when products must be used together and stay consistent — a Windows button with a Windows checkbox, never a Windows button with a macOS checkbox.

## When to Use It

- Your code must work with several families of related products (UI toolkits, database providers, cloud services).
- You want to guarantee that products used together are compatible.
- You want to add a new product family (say, a Linux factory) without touching existing client code.

## Trade-Offs

Adding a new product to the family is expensive: every concrete factory must implement the new `CreateX()` method. Adding a new family, by contrast, is cheap — one new factory class and you are done. The pattern also adds indirection, so do not reach for it when a single product type is all you need.

## Key Takeaways

- Abstract Factory creates a family of related objects through one interface, hiding the concrete classes from the client.
- The client picks a factory once; every product it creates is guaranteed to belong to the same family.
- Factory Method creates one product; Abstract Factory creates a family of related products.
- The worked example renders "Windows Button" with `WinFactory` and "macOS Button" with `MacFactory` from identical client code.
- Adding a new family is cheap (one factory class); adding a new product to the family touches every factory.
