# Memento Pattern in C#

The Memento pattern captures an object's internal state so it can be restored later — the classic undo feature — without exposing the object's internals to the rest of the program.

## The Three Roles

- **Originator** — the object whose state is saved and restored. Here, the text editor.
- **Memento** — an opaque snapshot of the originator's state at one moment in time. Only the originator reads its contents.
- **Caretaker** — the object that holds the mementos (usually a stack) and asks the originator to save or restore. It never inspects what is inside a memento.

Keeping the snapshot opaque matters: the caretaker can store and hand back mementos, but it cannot read or tamper with the editor's text. That preserves encapsulation while still giving undo.

## The Originator and Its Memento

```csharp
public sealed class TextEditor
{
    // The state we want to be able to undo.
    public string Text { get; private set; } = string.Empty;

    public void Type(string text) => Text += text;

    // Create a snapshot of the current state.
    public EditorMemento Save() => new(Text);

    // Restore a previously saved snapshot.
    public void Restore(EditorMemento memento) => Text = memento.Text;

    // The memento is a simple immutable snapshot.
    // It is nested inside the originator to signal that only
    // the originator is meant to read its contents.
    public sealed record EditorMemento(string Text);
}
```

Note that `Text` has a private setter: outside code can read the current text but cannot rewrite it directly. The only way back to an earlier state is through `Restore`, driven by a memento.

## The Caretaker

The caretaker owns the undo history. It keeps a stack of mementos and asks the editor to save before each change and restore on undo:

```csharp
public sealed class History
{
    private readonly Stack<TextEditor.EditorMemento> _states = new();

    public void Save(TextEditor editor) => _states.Push(editor.Save());

    public void Undo(TextEditor editor)
    {
        if (_states.TryPop(out var memento))
        {
            editor.Restore(memento);
        }
    }
}
```

## Worked Example

```csharp
var editor = new TextEditor();
var history = new History();

// Initial state: ""
history.Save(editor);          // save ""

editor.Type("Hello");
history.Save(editor);          // save "Hello"

editor.Type(" World");
Console.WriteLine(editor.Text); // Hello World

history.Undo(editor);
Console.WriteLine(editor.Text); // Hello

history.Undo(editor);
Console.WriteLine(editor.Text); // (empty)
```

Output:

```text
Hello World
Hello

```

## The Empty-Initial-State Behaviour

This implementation saves the initial empty state before the first edit, so the second undo returns the editor to `""` — the genuinely empty starting state. That is the behaviour chosen here, and it is stated explicitly: **undoing past the first edit restores the empty initial text, and undoing again with an empty history does nothing** (the `TryPop` guard simply skips the restore).

An alternative design would stop at `"Hello"` and never return to empty. Either choice is defensible, but pick one and document it — a user pressing undo twice and getting different results from what they expected is a real usability bug.

## Undo vs Redo

The same structure extends to redo: keep a second stack, and when the user undoes, push the popped memento onto the redo stack. A new edit clears the redo stack, because the future that was undone no longer exists. The memento itself does not change; only the caretaker gains a second stack.

## When to Use It

- You need undo/redo (text editors, drawing tools, form wizards).
- You need to snapshot state for rollback, such as restoring a settings object if the user cancels a dialog.
- Direct access to the state would break encapsulation, so the snapshot must stay opaque.

## Trade-Offs

Every saved state costs memory — an editor that saves after every keystroke can grow a large history. Mitigate by saving only on meaningful boundaries (word, sentence, or command) or by capping the stack depth. Mementos are also snapshots in time: if the originator's state shape changes between versions, old mementos may not restore cleanly.

## Key Takeaways

- Memento splits undo into three roles: the originator owns the state, the memento is the opaque snapshot, and the caretaker holds the history stack.
- The caretaker never reads inside a memento, so the originator's encapsulation stays intact.
- Worked example: "Hello" → "Hello World" → undo returns "Hello" → a second undo returns "" (the empty initial state).
- Undoing with an empty history is a no-op, guarded by `TryPop`.
- Snapshots cost memory, so save at meaningful boundaries and cap the history depth.
