# Apache Kafka Basics for .NET

Apache Kafka is a distributed event streaming platform. Producers append events to topics, Kafka stores them durably, and consumers read them at their own pace. It fits event-driven systems where several services need the same events, or where events must be replayed after a failure.

## Topics, Partitions, and Offsets

A **topic** is a named log of events, for example `orders`. A topic is split into **partitions**, and each partition is an ordered, append-only log. Every event in a partition has an **offset**, a position number that consumers track. Kafka keeps events for a configured retention period (often days), so a consumer can re-read old events by resetting its offset.

Partitions are the unit of parallelism: events in one partition are ordered, but events across partitions are not. The partition for an event is chosen by its key. Events with the same key (for example the same order id) always land in the same partition and stay in order.

## Producers

A producer sends events to a topic, usually with a key:

```csharp
using var producer = new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    Acks = Acks.All,
    EnableIdempotence = true
}).Build();

var result = await producer.ProduceAsync("orders", new Message<string, string>
{
    Key = order.Id.ToString(),
    Value = JsonSerializer.Serialize(order)
});
```

`Acks.All` waits until all in-sync replicas confirm the write, which is the durable setting. `EnableIdempotence` makes retries safe: Kafka deduplicates retried sends by producer id and sequence number, so a retried produce does not create a duplicate event.

## Consumers and Consumer Groups

A consumer reads events and tracks its position with committed offsets. Consumers belong to a **consumer group**: Kafka assigns each partition to one member of the group, so the group scales by adding members up to the partition count. Two different groups each get every event independently.

```csharp
using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "order-processing",
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false
}).Build();

consumer.Subscribe("orders");

while (true)
{
    var result = consumer.Consume();
    await ProcessOrderAsync(result.Message.Value);
    consumer.Commit(result);
}
```

`EnableAutoCommit = false` with a manual `Commit` after processing means the offset only advances once the work is done. If the consumer crashes before committing, it re-reads the event after restart.

## Delivery Guarantees

Kafka's default is **at-least-once** delivery: an event can be delivered twice (for example, the process succeeds but the commit is lost). Exactly-once delivery is possible with transactions in newer Kafka versions, but it is complex and rarely needed. The practical rule is to design consumers to be **idempotent**: processing the same event twice must be harmless. Check whether the order id was already processed before applying the side effect.

## Failure Modes Worth Knowing

- A consumer that crashes before committing reprocesses events; make handlers idempotent.
- A rebalance (a member joining or leaving the group) can cause a brief duplicate delivery.
- A producer that cannot reach a broker throws; retry with backoff, and rely on idempotence to avoid duplicates.
- A topic with one partition cannot scale past one consumer in a group, so choose partition counts up front.

## When to Use Kafka

- Several services need the same stream of events.
- Events must be replayed, audited, or reprocessed after a bug fix.
- High-throughput event ingestion where a database queue would be the bottleneck.

For simple point-to-point work queues with one consumer, a message broker like RabbitMQ is usually simpler.

## Key Takeaways

- Topics are split into ordered partitions; the event key decides the partition and preserves order per key.
- Consumer groups split partitions across members, so a group scales up to the partition count.
- The default guarantee is at-least-once, so consumers must be idempotent.
- `Acks.All` plus `EnableIdempotence` makes producers durable and safe to retry.
- Commit offsets only after processing, so a crash re-reads unprocessed events instead of losing them.
