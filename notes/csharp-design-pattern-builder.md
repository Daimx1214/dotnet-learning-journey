# Builder Pattern

The Builder pattern constructs a complex object step by step, separating the construction logic from the final representation. Instead of a constructor with ten parameters, or a chain of telescoping constructors, the caller configures one field at a time through a fluent API and calls `Build()` at the end.

## The Problem It Solves

An `EmailMessage` needs a recipient, a subject, a body, optional CC addresses, optional attachments, and a priority flag. A single constructor with all of these is unreadable, and making most of them optional with defaults hides which values were actually set. Telescoping constructors (one constructor per combination) explode in number and are easy to call wrong: two string parameters in a row can be swapped silently.

## Fluent Builder Example: Building an Email

The product is a simple immutable record of what the email contains:

```csharp
public sealed class EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public List<string> Cc { get; init; } = new();
    public List<string> Attachments { get; init; } = new();
    public bool IsHighPriority { get; init; }
}
```

The builder owns the mutable state and validates it at the end:

```csharp
public sealed class EmailMessageBuilder
{
    private string? _to;
    private string? _subject;
    private string? _body;
    private readonly List<string> _cc = new();
    private readonly List<string> _attachments = new();
    private bool _isHighPriority;

    public EmailMessageBuilder To(string address) { _to = address; return this; }
    public EmailMessageBuilder Subject(string subject) { _subject = subject; return this; }
    public EmailMessageBuilder Body(string body) { _body = body; return this; }
    public EmailMessageBuilder AddCc(string address) { _cc.Add(address); return this; }
    public EmailMessageBuilder Attach(string filePath) { _attachments.Add(filePath); return this; }
    public EmailMessageBuilder HighPriority() { _isHighPriority = true; return this; }

    public EmailMessage Build()
    {
        if (string.IsNullOrWhiteSpace(_to))
            throw new InvalidOperationException("Recipient is required.");
        if (string.IsNullOrWhiteSpace(_subject))
            throw new InvalidOperationException("Subject is required.");
        if (string.IsNullOrWhiteSpace(_body))
            throw new InvalidOperationException("Body is required.");

        return new EmailMessage
        {
            To = _to, Subject = _subject, Body = _body,
            Cc = _cc, Attachments = _attachments, IsHighPriority = _isHighPriority
        };
    }
}
```

Each method returns `this`, so calls chain into one readable statement:

```csharp
var email = new EmailMessageBuilder()
    .To("customer@example.com")
    .Subject("Your order shipped")
    .Body("Your order is on its way.")
    .AddCc("support@example.com")
    .HighPriority()
    .Build();
```

## The Director

When the same construction sequence is reused in several places, a **director** holds the recipe and the builder only executes it:

```csharp
public static class EmailDirector
{
    public static EmailMessage BuildOrderShippedEmail(string to, string orderId)
    {
        return new EmailMessageBuilder()
            .To(to)
            .Subject($"Order {orderId} shipped")
            .Body($"Your order {orderId} is on its way.")
            .Build();
    }
}
```

The director keeps callers from repeating the same five builder calls, and changing the standard email template means editing one method.

## Builder vs Telescoping Constructors

- Telescoping constructors force the caller to pass values in a fixed order and to supply dummy values for parameters they do not care about.
- A builder lets the caller set only the fields that matter, in any order, with readable method names at each step.
- A builder can validate the complete object once in `Build()`, instead of validating in every constructor overload.

## Where .NET Uses Builders Already

- `WebApplication.CreateBuilder(args)` and `Host.CreateApplicationBuilder()` build the host step by step.
- `StringBuilder` is the classic example: append pieces, then call `ToString()`.
- `HttpRequestMessage` is often assembled with an object initializer, but a builder is the better choice once headers, content, and retry settings pile up.

## Key Takeaways

- Builder separates how an object is constructed from what the object is, so complex objects stay readable to create.
- Fluent methods that return `this` make the construction read like a sentence.
- Validate the finished object once in `Build()`, and throw if a required field is missing.
- Use a director when the same construction recipe is repeated in more than one place.
- Reach for Builder when a constructor grows past three or four parameters, especially optional ones.
