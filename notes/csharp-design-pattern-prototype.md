# Prototype Pattern in C#

The Prototype pattern creates a new object by copying an existing one
(the prototype) instead of building it from scratch. It is useful when
creating an object is expensive, or when you need many objects that start
almost identical and differ only in a few fields.

## The idea

Instead of calling `new` and setting every property again, you clone a
ready-made instance and then change what differs:

```csharp
public sealed class Enemy
{
    public string Type { get; set; } = "Goblin";
    public int Health { get; set; } = 100;
    public List<string> Weapons { get; set; } = new() { "Dagger" };
}
```

A prototype goblin is configured once, then each spawn clones it and
tweaks only what is different.

## `ICloneable` pitfalls

The built-in `ICloneable` interface looks like the obvious tool, but it
has two real problems:

- Its `Clone()` method returns `object`, so every call needs a cast.
- It never says whether the copy is shallow or deep. Two implementations
  can behave differently, and callers cannot tell which one they get.

For those reasons, avoid `ICloneable` in new code. Define your own typed
copy method instead, with a name that states what it does.

## `MemberwiseClone`: shallow vs deep copy

`object.MemberwiseClone()` creates a shallow copy: value-type fields are
copied, but reference-type fields still point to the same objects. That
is the trap most people hit:

```csharp
public sealed class Enemy
{
    public string Type { get; set; } = "Goblin";
    public int Health { get; set; } = 100;
    public List<string> Weapons { get; set; } = new() { "Dagger" };

    public Enemy ShallowCopy()
    {
        // MemberwiseClone copies the Weapons reference, not the list.
        return (Enemy)MemberwiseClone();
    }
}

var original = new Enemy();
Enemy copy = original.ShallowCopy();
copy.Weapons.Add("Bow");

// Both lists are the same object, so the original now has "Bow" too.
Console.WriteLine(original.Weapons.Count); // Output: 2
Console.WriteLine(copy.Weapons.Count);     // Output: 2
```

A deep copy must also clone the reference-type fields:

```csharp
public Enemy DeepCopy()
{
    var clone = (Enemy)MemberwiseClone();
    clone.Weapons = new List<string>(Weapons); // New list, same items.
    return clone;
}
```

Deep copy only goes as far as you write it. If a list holds mutable
objects, each of those needs copying too, or the copy still shares state.

## Copy constructors

A copy constructor is explicit, typed, and easy to read. It states
exactly what is copied and how deep the copy goes:

```csharp
public sealed class Player
{
    public string Name { get; }
    public int Level { get; }
    public List<string> Items { get; }

    public Player(string name, int level, List<string> items)
    {
        Name = name;
        Level = level;
        Items = items;
    }

    // Copy constructor: deep-copies the mutable list.
    public Player(Player other)
        : this(other.Name, other.Level, new List<string>(other.Items))
    {
    }
}
```

## Records and `with` expressions: the modern C# alternative

Records give you value-based copying for free. A `with` expression makes
a shallow copy of a record and changes only the listed properties:

```csharp
public sealed record PlayerRecord(string Name, int Level, List<string> Items);

var basePlayer = new PlayerRecord("Ayesha", 5, new List<string> { "Sword" });

// New record, same values except Level. Clean and type-safe.
PlayerRecord leveledUp = basePlayer with { Level = 6 };

Console.WriteLine(leveledUp.Name);  // Output: Ayesha
Console.WriteLine(leveledUp.Level); // Output: 6
```

Note that `with` is also a shallow copy: `leveledUp.Items` is the same
list object as `basePlayer.Items`. For immutable data, such as strings
and numbers, that is fine. If the record holds a mutable list, copy the
list yourself in the `with` expression when the copy must be independent.

## When to use Prototype

- Creating the object is expensive: parsing, loading from a database, or
  a long setup that a prototype has already done once.
- You need many near-identical objects: game enemies, document templates,
  default configurations with small per-instance changes.
- The exact type is only known at runtime: cloning a prototype avoids a
  chain of `if` statements or a switch over types.

## When NOT to use it

- When a plain constructor call is short and clear. Cloning adds a
  concept for no benefit.
- When the object graph is deep and mutable. Writing a correct deep copy
  by hand is error-prone; a factory or builder is usually safer.
- When the shared mutable state of a shallow copy would cause bugs. If in
  doubt, test the copy: mutate the clone and check the original.

## Key Takeaways

- Prototype creates objects by copying a configured instance instead of
  building from scratch, which helps when setup is expensive or many
  near-identical objects are needed.
- Avoid `ICloneable`: it returns `object` and never promises shallow or
  deep copying.
- `MemberwiseClone` is shallow: reference-type fields are shared, so
  mutating the clone's list mutates the original's list too. Deep copy
  means copying those fields as well.
- In modern C#, prefer a copy constructor for explicit control, or a
  record with a `with` expression for clean, type-safe shallow copies of
  mostly immutable data.
