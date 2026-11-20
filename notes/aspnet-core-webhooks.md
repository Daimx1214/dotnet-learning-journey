# ASP.NET Core Webhooks

A webhook is an HTTP callback: another service POSTs to your endpoint when something happens — a payment succeeds, a repo is pushed, an order ships. Your job is to prove the request is genuine, handle it exactly once, and answer fast.

## Receiving and Verifying Signatures

Anyone can POST to a public URL, so the sender signs the payload with a shared secret using HMAC, and sends the signature in a header. You recompute it and compare in constant time.

```csharp
app.MapPost("/webhooks/payments", async (HttpRequest request) =>
{
    request.EnableBuffering();
    using var reader = new StreamReader(request.Body, leaveOpen: true);
    var payload = await reader.ReadToEndAsync();
    request.Body.Position = 0;

    var signature = request.Headers["X-Signature"].ToString();
    var secret = builder.Configuration["Webhooks:Secret"]!;

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = "sha256=" + Convert.ToHexString(
        hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    // Fixed-time comparison: prevents timing attacks on the signature
    var valid = CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    if (!valid) return Results.Unauthorized();

    var evt = JsonSerializer.Deserialize<PaymentEvent>(payload);
    // ...handle below
    return Results.Ok();
});
```

Read the raw body before model binding consumes it (`EnableBuffering` lets you rewind). Always compare with `CryptographicOperations.FixedTimeEquals` — a plain `==` leaks information through timing. Keep the secret in configuration or a secret store, never in source.

## Idempotency: Handle Each Event Once

Senders retry. Network hiccups mean you will receive the same event twice, and sometimes out of order. Every event should carry a unique ID (Stripe calls it the event ID; many APIs send an `Idempotency-Key` header). Record processed IDs and skip duplicates.

```csharp
// Minimal idempotency check (persist this in a real database)
if (await db.ProcessedEvents.AnyAsync(e => e.EventId == evt.Id))
    return Results.Ok();   // already handled — acknowledge, don't reprocess

db.ProcessedEvents.Add(new ProcessedEvent { EventId = evt.Id });
await db.SaveChangesAsync();
// ...then process the event
```

Store the ID and process the event in the same database transaction where possible, so a crash cannot leave you with a recorded ID but unprocessed work — or vice versa. If events can arrive out of order, use a version/timestamp field to ignore stale updates that arrive after newer ones.

## Retries, Ordering, and Fast Responses

The sender expects a fast `2xx`. If your handler takes seconds — calling another API, writing to a database, sending email — the sender may time out and retry, delivering the event again. The correct pattern is: verify, enqueue, return `200` immediately, and process asynchronously.

```csharp
app.MapPost("/webhooks/orders", async (HttpRequest request, IBackgroundTaskQueue queue) =>
{
    var payload = await ReadAndVerifyAsync(request);   // signature check first
    if (payload is null) return Results.Unauthorized();

    await queue.QueueAsync(payload);                   // durable queue: DB table, Azure Queue, etc.
    return Results.Ok();                             // answer in milliseconds
});

// A BackgroundService drains the queue and does the slow work,
// retrying with backoff on transient failures.
```

Use a durable queue (a database table, Azure Service Bus, SQS) rather than an in-memory channel if losing an event matters — an in-memory queue dies with the process. Retry failed processing with exponential backoff, and after N failures move the event to a dead-letter store for manual inspection instead of retrying forever.

## Key Takeaways

- Verify every webhook with HMAC over the raw body and compare with `FixedTimeEquals`; never trust an unsigned request.
- Read the raw body with buffering enabled before model binding consumes it; keep the secret out of source code.
- Record each event's unique ID and skip duplicates — senders retry, so expect every event at least twice.
- Return `200` fast and process asynchronously through a durable queue; slow handlers cause duplicate deliveries.
- Handle out-of-order delivery with version/timestamp checks, and dead-letter events that keep failing.
- Log the event ID and signature result for every delivery — it is the only way to debug a missed or duplicate webhook.
