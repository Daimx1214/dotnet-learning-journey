# System.IO.Pipelines

`System.IO.Pipelines` is a library for high-performance parsing of streams of data. Instead of reading bytes into arrays and copying them around, it gives you a `PipeReader` and a `PipeWriter` that work with buffers you do not own, so parsing code can look at incoming bytes without extra allocations.

## Why It Beats Stream for Parsing

A `Stream` forces you to decide how much to read and where to put it. If a message is split across two reads, you must copy the leftover bytes into a new buffer before the next read. `PipeReader` solves this with `ReadOnlySequence<byte>`: a sequence that can span several memory segments, so a partial message stays where it is until the rest arrives. You only advance the reader past the bytes you have consumed, and the pipe reuses the underlying memory.

## A Small Line-Reader Demo

The example below reads newline-terminated lines from a pipe. `ReadAsync` returns a `ReadResult` whose `Buffer` is a `ReadOnlySequence<byte>`. `SequenceReader<byte>` makes it easy to scan that sequence for a delimiter without copying.

```csharp
using System.Buffers;
using System.IO.Pipelines;
using System.Text;

var pipe = new Pipe();
await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("hello\nworld\n"));
pipe.Writer.Complete();

while (true)
{
    ReadResult result = await pipe.Reader.ReadAsync();
    ReadOnlySequence<byte> buffer = result.Buffer;
    var reader = new SequenceReader<byte>(buffer);

    while (reader.TryReadTo(out ReadOnlySpan<byte> line, (byte)'\n'))
    {
        Console.WriteLine(Encoding.UTF8.GetString(line)); // hello, then world
    }

    // Tell the pipe how much we consumed; the rest stays buffered.
    pipe.Reader.AdvanceTo(reader.Position, buffer.End);

    if (result.IsCompleted)
    {
        break;
    }
}
```

The output is:

```text
hello
world
```

## Backpressure Basics

A pipe has a bounded size (by default, each segment is at least 4096 bytes and the pipe pauses the writer when too much unread data accumulates). When the writer's buffer is full, `FlushAsync` waits until the reader catches up. That is backpressure: a fast producer cannot run away from a slow consumer and grow memory without limit. You control it with `PipeOptions` (`PauseWriterThreshold` and `ResumeWriterThreshold`). Always call `Complete()` on the writer when you are done, and `Complete()` on the reader when you stop reading, so the other side is not left waiting forever.

## Key Takeaways

- `PipeReader`/`PipeWriter` let you parse streaming data without copying partial messages between buffers.
- `ReadOnlySequence<byte>` can span multiple segments, so a message split across reads is still one logical buffer.
- Call `AdvanceTo` with the consumed position so the pipe can reuse memory; unexamined bytes stay buffered.
- A full pipe pauses the writer, giving you backpressure for free instead of unbounded memory growth.
- Always complete both sides of the pipe so awaiting readers and writers can finish cleanly.
