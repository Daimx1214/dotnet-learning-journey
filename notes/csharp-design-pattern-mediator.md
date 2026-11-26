# Mediator Pattern

The Mediator pattern defines an object that coordinates communication between a group of related objects, called colleagues. Instead of every object talking directly to every other object, each one talks only to the mediator, and the mediator routes the messages. This turns a tangled web of references into a clean hub-and-spoke shape.

## The Problem It Solves

In a chat room, ten users each messaging the other nine directly means up to 90 direct references. Adding one more user means wiring it to all ten existing ones. The same problem appears in UI forms, game systems, and services that keep calling each other: every new participant multiplies the connections.

With a mediator, each colleague holds one reference (to the mediator), and the mediator holds the routing rules. Adding a colleague means registering it once.

## Example: A Chat Room

The mediator interface is the only contract colleagues need:

```csharp
public interface IChatMediator
{
    void Send(string message, ChatUser sender);
    void Register(ChatUser user);
}
```

Colleagues never reference each other:

```csharp
public sealed class ChatUser
{
    private readonly IChatMediator _mediator;
    public string Name { get; }

    public ChatUser(IChatMediator mediator, string name)
    {
        _mediator = mediator;
        Name = name;
    }

    public void Send(string message)
    {
        Console.WriteLine($"{Name} sends: {message}");
        _mediator.Send(message, this);
    }

    public void Receive(string message, string from)
        => Console.WriteLine($"{Name} received from {from}: {message}");
}
```

The mediator owns the routing rule, here broadcast to everyone except the sender:

```csharp
public sealed class ChatRoom : IChatMediator
{
    private readonly List<ChatUser> _users = new();

    public void Register(ChatUser user) => _users.Add(user);

    public void Send(string message, ChatUser sender)
    {
        foreach (var user in _users)
            if (!ReferenceEquals(user, sender))
                user.Receive(message, sender.Name);
    }
}
```

Usage:

```csharp
var room = new ChatRoom();
var ali = new ChatUser(room, "Ali");
var sara = new ChatUser(room, "Sara");
room.Register(ali);
room.Register(sara);

ali.Send("Hello everyone!");
// Sara received from Ali: Hello everyone!
```

Changing the rule (private messages, moderation, logging) means editing `ChatRoom` only. The users never change.

## Mediator in ASP.NET Core: MediatR

The real-world version of this pattern in .NET is the MediatR library. A controller sends a request to `IMediator`, and MediatR finds the matching handler:

```csharp
public sealed record GetOrderQuery(int OrderId) : IRequest<Order?>;

public sealed class GetOrderHandler : IRequestHandler<GetOrderQuery, Order?>
{
    public Task<Order?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        // Load the order from the database and return it.
        throw new NotImplementedException();
    }
}
```

```csharp
[HttpGet("{id:int}")]
public async Task<IActionResult> Get(int id, IMediator mediator)
{
    var order = await mediator.Send(new GetOrderQuery(id));
    return order is null ? NotFound() : Ok(order);
}
```

The controller knows nothing about the handler, the database, or the query logic. MediatR is the mediator, the query is the message, and the handler is the colleague.

## Mediator vs Observer

- **Mediator** centralizes communication: colleagues talk through one hub, and the hub decides who gets what.
- **Observer** broadcasts: a subject notifies all subscribers directly, and subscribers decide what to do with the event.

Use a mediator when the interaction rules themselves are complex and belong in one place. Do not use it for two objects talking to each other, and do not let one mediator grow into a god object that knows every rule in the system.

## Key Takeaways

- Mediator replaces many-to-many references between objects with one-to-many references through a hub.
- Colleagues stay decoupled: each one only knows the mediator interface.
- Routing, filtering, and logging rules live in one place, so they change in one place.
- MediatR is the standard ASP.NET Core form of the pattern: controllers send requests, handlers answer them.
- Do not use it for simple one-to-one communication, and do not let one mediator absorb the whole system.
