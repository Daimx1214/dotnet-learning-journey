# SQL Server Service Broker

Service Broker is SQL Server's built-in asynchronous messaging system.
Instead of polling a table or calling a web service synchronously, one
database sends a message to a queue in another database, and an
activated stored procedure processes it when it arrives. It is reliable
messaging with transactions: a message is sent and received inside
ordinary transactions, so nothing is lost or duplicated.

## The Building Blocks

A Service Broker application is built from four objects:

- **Message type** — the name and validation of a message's payload
  (often `VALIDATION = WELL_FORMED_XML` or `NONE`).
- **Contract** — which message types each side may send. A contract
  names the message types the initiator sends and the ones the target
  sends back.
- **Queue** — where messages wait. A queue is a table-like object you
  can `RECEIVE` from.
- **Service** — a named endpoint bound to a queue and a contract.

```sql
CREATE MESSAGE TYPE [OrderMessage]
    VALIDATION = WELL_FORMED_XML;

CREATE CONTRACT [OrderContract]
    ([OrderMessage] SENT BY INITIATOR);

CREATE QUEUE OrderQueue;
CREATE QUEUE OrderReplyQueue;

CREATE SERVICE OrderService
    ON QUEUE OrderQueue ([OrderContract]);

CREATE SERVICE OrderReplyService
    ON QUEUE OrderReplyQueue ([OrderContract]);
```

## SEND and RECEIVE

Sending is a transaction like any other `INSERT` — it commits or rolls
back with the surrounding work:

```sql
BEGIN TRANSACTION;

DECLARE @handle UNIQUEIDENTIFIER;
BEGIN DIALOG CONVERSATION @handle
    FROM SERVICE OrderService
    TO SERVICE 'OrderReplyService'
    ON CONTRACT [OrderContract]
    WITH ENCRYPTION = OFF;

SEND ON CONVERSATION @handle
    MESSAGE TYPE [OrderMessage]
    (N'<Order><Id>42</Id><Total>199.99</Total></Order>');

COMMIT TRANSACTION;
```

Receiving takes messages off the queue, usually one batch at a time with
a `WAITFOR` timeout so the procedure blocks politely instead of spinning:

```sql
WAITFOR (
    RECEIVE TOP (1)
        conversation_handle,
        message_type_name,
        CAST(message_body AS NVARCHAR(MAX)) AS body
    FROM OrderQueue
), TIMEOUT 5000;
```

A received message is removed from the queue inside the transaction. If
the transaction rolls back, the message returns to the queue — that is
how at-least-once delivery is guaranteed.

## Activation: Automatic Processing

A queue can start a stored procedure automatically whenever a message
arrives, so nothing polls:

```sql
CREATE PROCEDURE ProcessOrders
AS
BEGIN
    DECLARE @handle UNIQUEIDENTIFIER;
    DECLARE @messageType NVARCHAR(128);
    DECLARE @body NVARCHAR(MAX);

    WHILE (1 = 1)
    BEGIN
        BEGIN TRANSACTION;

        WAITFOR (
            RECEIVE TOP (1)
                @handle = conversation_handle,
                @messageType = message_type_name,
                @body = CAST(message_body AS NVARCHAR(MAX))
            FROM OrderQueue
        ), TIMEOUT 1000;

        IF (@@ROWCOUNT = 0)
        BEGIN
            ROLLBACK TRANSACTION;
            BREAK;
        END;

        IF (@messageType = N'OrderMessage')
        BEGIN
            -- Process the order here: insert into Orders, call
            -- another system, and so on.
            PRINT 'Processing: ' + @body;
        END
        ELSE IF (@messageType =
            N'http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog')
        BEGIN
            END CONVERSATION @handle;
        END

        COMMIT TRANSACTION;
    END
END;
GO

ALTER QUEUE OrderQueue
    WITH ACTIVATION (
        STATUS = ON,
        PROCEDURE_NAME = ProcessOrders,
        MAX_QUEUE_READERS = 5,
        EXECUTE AS SELF
    );
```

`MAX_QUEUE_READERS` caps the parallel activated procedures; five readers
process up to five messages concurrently.

## When to Use It

- **Decoupling slow work** — an order is accepted in milliseconds and
  the invoice, email, and warehouse steps run asynchronously.
- **Reliable cross-database messaging** — messages survive restarts and
  are delivered exactly once per successful transaction.
- **Throttling** — a queue naturally smooths bursts; readers process at
  a steady rate instead of the system collapsing under a spike.

It is a poor fit when you need sub-second latency guarantees, complex
routing, or integration with non-SQL Server systems — a dedicated broker
such as RabbitMQ or Azure Service Bus is usually a better choice there.

## Key Takeaways

- Service Broker is asynchronous, transactional messaging inside SQL
  Server: message types, contracts, queues, and services.
- `SEND` puts a message on a queue; `RECEIVE` takes it off, inside a
  transaction, so failures roll the message back onto the queue.
- Queue activation runs a stored procedure automatically on message
  arrival; `MAX_QUEUE_READERS` controls parallelism.
- Conversations (`BEGIN DIALOG` / `END CONVERSATION`) give each exchange
  a handle and a lifecycle.
- Use it to decouple slow work and smooth bursts; prefer a dedicated
  message broker for cross-platform or sub-second-latency needs.
