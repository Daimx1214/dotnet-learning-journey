# Flyweight Pattern in C#

The Flyweight pattern shares one object between many clients instead of
creating a separate object for each one. It works when you have a huge
number of similar objects and most of their state can be shared.

## Intrinsic vs extrinsic state

The key idea is to split an object's state in two:

- **Intrinsic state** is shared and never changes: the shape of a
  character glyph, the texture of a game tile, the colour of a chess piece.
  This state lives inside the flyweight.
- **Extrinsic state** is different for every use: the position of the
  character on the page, the coordinates of the tile, the square a chess
  piece sits on. This state lives outside the flyweight and is passed in
  by the caller each time it is used.

If a piece of state varies per use, it must not be stored in the shared
object, or every client would see every other client's changes.

## FlyweightFactory

A factory owns the shared objects and hands out the same instance for the
same intrinsic state. Clients never create flyweights directly:

```csharp
public sealed class Glyph
{
    // Intrinsic state: the character and its font. Shared by all uses.
    public char Character { get; }
    public string FontName { get; }

    public Glyph(char character, string fontName)
    {
        Character = character;
        FontName = fontName;
    }

    // Extrinsic state (x, y) is passed in by the caller on every use.
    public void Draw(int x, int y)
    {
        Console.WriteLine($"Draw '{Character}' in {FontName} at ({x}, {y}).");
    }
}

public sealed class GlyphFactory
{
    private readonly Dictionary<(char, string), Glyph> _glyphs = new();

    public Glyph GetGlyph(char character, string fontName)
    {
        var key = (character, fontName);
        if (!_glyphs.TryGetValue(key, out Glyph? glyph))
        {
            glyph = new Glyph(character, fontName);
            _glyphs[key] = glyph;
        }
        return glyph;
    }

    public int CreatedCount => _glyphs.Count;
}
```

Usage: a document with 10,000 letters needs only as many `Glyph`
objects as there are distinct characters, not 10,000:

```csharp
var factory = new GlyphFactory();

string text = "hello world";
for (int i = 0; i < text.Length; i++)
{
    // Each position is extrinsic state; the glyph itself is shared.
    Glyph glyph = factory.GetGlyph(text[i], "Consolas");
    glyph.Draw(i * 10, 0);
}

Console.WriteLine($"Glyphs created: {factory.CreatedCount}");
// Output: Glyphs created: 8 (h, e, l, o, space, w, r, d)
```

## When it pays off

- **Text rendering:** a word processor holds millions of characters, but
  only a few hundred distinct glyphs. Sharing glyphs saves most of the
  memory.
- **Games:** a map of thousands of tiles (grass, water, stone) shares one
  tile object per terrain type. Each tile's position is extrinsic.
- **Any case with many objects and a small set of distinct intrinsic
  states.** If the distinct states are nearly as numerous as the objects,
  sharing saves nothing.

## The memory trade-off

Flyweight trades memory for a little extra complexity and CPU time. The
factory adds a dictionary lookup on every request, and the caller must
supply extrinsic state on every call, which makes the calling code more
verbose. The shared objects must also be immutable (or at least never
mutated per client), or one client's change corrupts all the others.
Measure before applying it: for a few hundred objects the saving is
usually not worth the added indirection.

## When NOT to use it

- When the objects are few, or their state is mostly unique per object.
- When the shared state cannot be made immutable and callers would need
  to mutate it.
- When a simpler solution works: an `enum` plus a lookup table often
  covers the same need with far less code.

## Key Takeaways

- Split state into intrinsic (shared, stored in the flyweight) and
  extrinsic (per use, passed in by the caller).
- A FlyweightFactory returns the same instance for the same intrinsic
  state, so N uses need only as many objects as there are distinct states.
- The pattern pays off for huge numbers of similar objects, such as text
  glyphs or game tiles, and only when the shared state can stay immutable.
- It costs a factory lookup per use and more verbose call sites, so use it
  where the memory saving is real and measurable.
