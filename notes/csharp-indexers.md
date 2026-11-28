# Indexers in C#

An indexer lets an object be accessed with array syntax, `obj[i]`, instead of a method call. It is syntactic sugar over a `get`/`set` pair, and it makes collection-like types read naturally.

## Basic Indexer

Declare an indexer with `this` followed by the parameter list:

```csharp
public sealed class Playlist
{
    private readonly List<string> _songs = new();

    public int Count => _songs.Count;

    public string this[int index]
    {
        get => _songs[index];
        set => _songs[index] = value;
    }

    public void Add(string song) => _songs.Add(song);
}

var playlist = new Playlist();
playlist.Add("Song A");
playlist.Add("Song B");
Console.WriteLine(playlist[0]); // Song A
playlist[1] = "Song C";
Console.WriteLine(playlist[1]); // Song C
```

The indexer forwards to the list, so out-of-range access throws `ArgumentOutOfRangeException` just as it would on the list itself.

## String Keys and Multi-Parameter Indexers

Indexers accept any parameter types, and more than one parameter:

```csharp
public sealed class Scoreboard
{
    private readonly Dictionary<string, int> _scores = new();

    // String key, with a default for missing players
    public int this[string player]
    {
        get => _scores.TryGetValue(player, out int score) ? score : 0;
        set => _scores[player] = value;
    }
}

public sealed class Grid
{
    private readonly int[,] _cells;

    public Grid(int rows, int cols) => _cells = new int[rows, cols];

    // Two parameters: row and column
    public int this[int row, int col]
    {
        get => _cells[row, col];
        set => _cells[row, col] = value;
    }
}

var grid = new Grid(3, 3);
grid[1, 2] = 7;
Console.WriteLine(grid[1, 2]); // 7
```

## Get and Set with Validation

The `get` and `set` accessors are ordinary code, so they can validate, normalize, or compute:

```csharp
public sealed class BoundedStack<T>
{
    private readonly T[] _items;
    private int _top;

    public BoundedStack(int capacity) => _items = new T[capacity];

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _top)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }
        set
        {
            if (index < 0 || index >= _items.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = value;
            if (index >= _top) _top = index + 1;
        }
    }
}
```

A read-only indexer omits the `set` accessor; a write-only indexer omits `get` (rare, and usually a design smell).

## Index and Range Support

Since C# 8, indexers can accept `System.Index` and `System.Range`, giving `^` (from end) and `..` (slice) syntax. Your type must declare the matching indexer signatures:

```csharp
public sealed class Deck
{
    private readonly string[] _cards = { "A", "K", "Q", "J", "10" };

    public string this[int index] => _cards[index];

    public string this[Index index] => _cards[index];

    public string[] this[Range range] => _cards[range];
}

var deck = new Deck();
Console.WriteLine(deck[^1]);      // 10 (last card)
string[] top = deck[..2];         // { "A", "K" }
```

If you only declare `this[int]`, `deck[^1]` does not compile: `^1` is an `Index`, not an `int`, and the compiler will not convert it implicitly.

## Indexers vs Methods

Use an indexer when the object is conceptually a collection or lookup table and the access is cheap and unsurprising. Use a method when the access does work, can fail in a domain-specific way, or takes parameters that are not a key:

| Prefer indexer | Prefer method |
|---|---|
| `cache[key]` | `repository.GetById(id)` (hits a database) |
| `grid[row, col]` | `parser.Parse(text)` (does work, can fail) |
| `scores[player]` | `service.CalculateTotal(items)` (a computation) |

An indexer should feel like reading a value, not like issuing a command. If getting the value can throw a domain exception or take seconds, a named method communicates that honestly.

A type can have several indexers with different parameter signatures (overloading), but only one indexer per parameter signature. Indexers cannot be `static`, and they cannot have `ref` parameters.

## Key Takeaways

- Declare an indexer with `this[parameters]`; it is a `get`/`set` pair behind array syntax.
- Indexers accept any parameter types, including strings and multiple parameters.
- Validate inside `get`/`set` just as you would in a property.
- Add `Index` and `Range` overloads explicitly to support `^` and `..` syntax.
- Use indexers for cheap, collection-like lookups; use methods when access does real work or can fail.
