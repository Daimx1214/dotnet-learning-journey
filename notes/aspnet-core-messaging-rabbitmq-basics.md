# RabbitMQ for .NET

RabbitMQ is a message broker: producers publish messages to exchanges, exchanges route them to queues, and consumers read from queues. It fits work queues, background processing, and service-to-service messaging where at-least-once delivery and explicit acknowledgements are wanted.

## Exchanges, Queues, and Bindings

A **producer** never sends directly to a queue. It publishes to an **exchange** with a routing key, and the exchange routes the message to queues bound to it:

- **Direct**: routes to queues whose binding key exactly matches the routing key.
- **Fanout**: routes to every bound queue, ignoring the routing key. Good for broadcasting an event to several services.
- **Topic**: routes by pattern matching on dot-separated words. `orders.*` matches `orders.created` but not `orders.created.eu`. `#` matches zero or more words.
- **Headers**: routes on message header values instead of the routing key. Rarely needed.

A **binding** connects a queue to an exchange with a key or pattern. A queue can be bound to several exchanges, and an exchange can route to many queues.

## Publishing and Consuming with RabbitMQ.Client

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync("orders", ExchangeType.Topic, durable: true);
await channel.QueueDeclareAsync("order-processing", durable: true);
await channel.QueueBindAsync("order-processing", "orders", "orders.created");

var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));
await channel.BasicPublishAsync("orders", "orders.created", body);
```

Durable exchanges and queues survive a broker restart, but messages also need to be published as persistent to survive. A consumer acknowledges each message after processing:

```csharp
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, args) =>
{
    try
    {
        await ProcessOrderAsync(args.Body.ToArray());
        await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
    }
    catch
    {
        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
    }
};

await channel.BasicConsumeAsync("order-processing", autoAck: false, consumer);
```

`autoAck: false` is essential: with auto-ack, a message is considered done the moment it is delivered, so a crash loses it. Acknowledge only after the work succeeded.

## Ack, Nack, and Dead-Letter Queues

- **Ack**: the message was processed; RabbitMQ removes it from the queue.
- **Nack with requeue**: the message failed but should be tried again. Use sparingly: a message that always fails will loop forever.
- **Nack without requeue** (or reject): the message is dropped, unless the queue has a dead-letter exchange configured.

A **dead-letter queue (DLQ)** catches messages that were rejected, expired, or exceeded their retry limit:

```csharp
var args = new Dictionary<string, object?>
{
    ["x-dead-letter-exchange"] = "orders.dlx",
    ["x-message-ttl"] = 60000
};
await channel.QueueDeclareAsync("order-processing", durable: true, arguments: args);
```

The DLQ is where failed messages wait for inspection instead of vanishing. Monitor its depth: a growing DLQ means a real bug, not a transient error.

## MassTransit

Writing raw `RabbitMQ.Client` code is verbose and error-prone. **MassTransit** is the usual .NET abstraction: it declares exchanges and queues from conventions, handles serialization, retries, and dead-lettering, and adds patterns like request-response and sagas:

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost");
        cfg.ReceiveEndpoint("order-processing", e =>
            e.ConfigureConsumer<OrderConsumer>(context));
    });
});
```

The consumer is a plain class implementing `IConsumer<OrderCreated>`. Start with MassTransit for a real project; use the raw client only to learn the underlying model.

## When to Use RabbitMQ vs Kafka

RabbitMQ fits task queues, RPC-style messaging, and routing-heavy topologies where each message goes to one consumer. Kafka fits durable event streams where several consumers replay history. They overlap, but the routing model and the retention model are the deciding factors.

## Key Takeaways

- Producers publish to exchanges, exchanges route to queues through bindings, and consumers read from queues.
- Direct, fanout, and topic exchanges cover exact, broadcast, and pattern routing.
- Always use manual ack and acknowledge only after the work succeeded, or a crash loses messages.
- Dead-letter queues catch rejected and expired messages so failures are visible instead of silent.
- MassTransit is the standard .NET abstraction over the raw client for real projects.
