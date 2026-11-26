# Command Pattern

The Command pattern turns a request into a standalone object that holds everything needed to perform it: the receiver, the parameters, and an `Execute` method. Once a request is an object, it can be stored, queued, logged, retried, and undone, which is impossible when the request is just a method call buried in a button handler.

## The Problem It Solves

A text editor has buttons, menu items, and keyboard shortcuts that all trigger the same actions: copy, paste, delete, bold. If each UI element calls the document directly, the same logic is copied into every handler, and undo becomes a special case bolted on afterwards. Wrapping each action in a command object gives one place for the logic, one place for undo, and a uniform way to trigger any action from anywhere.

## The Core Pieces

- **Command**: an interface with `Execute()` and usually `Undo()`.
- **Concrete command**: holds a reference to the receiver and the parameters.
- **Receiver**: the object that actually does the work (the document, the light, the service).
- **Invoker**: the button or queue that calls `Execute()` without knowing what the command does.
- **Client**: creates the command and hands it to the invoker.

## Example: Undo Stack in an Editor

The command interface and a simple document as the receiver:

```csharp
public interface IEditorCommand
{
    void Execute();
    void Undo();
}

public sealed class Document
{
    public string Text { get; private set; } = string.Empty;

    public void Insert(string text, int position) => Text = Text.Insert(position, text);
    public void Delete(int position, int length) => Text = Text.Remove(position, length);
}
```

A concrete command captures enough state to undo itself. The insert command remembers where it inserted and how long the text was:

```csharp
public sealed class InsertTextCommand : IEditorCommand
{
    private readonly Document _document;
    private readonly string _text;
    private readonly int _position;

    public InsertTextCommand(Document document, string text, int position)
    {
        _document = document;
        _text = text;
        _position = position;
    }

    public void Execute() => _document.Insert(_text, _position);
    public void Undo() => _document.Delete(_position, _text.Length);
}
```

The invoker keeps a history stack, which is what makes undo possible:

```csharp
public sealed class CommandHistory
{
    private readonly Stack<IEditorCommand> _undoStack = new();

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
    }

    public void Undo()
    {
        if (_undoStack.TryPop(out var command))
            command.Undo();
    }
}
```

Usage:

```csharp
var doc = new Document();
var history = new CommandHistory();

history.Execute(new InsertTextCommand(doc, "Hello", 0));
history.Execute(new InsertTextCommand(doc, " world", 5));
Console.WriteLine(doc.Text); // Hello world

history.Undo();
Console.WriteLine(doc.Text); // Hello
```

Each `Execute` pushes the command onto the stack, and each `Undo` pops the most recent one and reverses it. A redo stack works the same way: push undone commands onto a second stack and re-execute them.

## Queuing and Delayed Execution

Because a command is just an object, an invoker can hold it and run it later:

- A background worker can dequeue commands from a `Queue<ICommand>` and execute them one at a time.
- A job system can serialize a command (its type and parameters) and run it on another machine.
- A macro records a list of commands and replays them in order.
- Logging every executed command gives a free audit trail.

## Command in Real .NET Code

WPF and MAUI use `ICommand` directly: a button binds to a command object, and the UI calls `Execute` and checks `CanExecute` without knowing the handler. ASP.NET Core does not ship a command bus, but MediatR request handlers and queued `BackgroundService` work items follow the same idea: the request is an object that travels to the code that handles it.

## Key Takeaways

- Command turns a request into an object, so it can be stored, queued, logged, and undone.
- The invoker never knows what the command does; it only calls `Execute()`.
- Undo works by keeping executed commands on a stack and calling `Undo()` on the most recent one.
- A command must capture everything it needs to run and to reverse itself.
- Use it in editors, undo systems, queues, macros, and anywhere the same action is triggered from several places.
