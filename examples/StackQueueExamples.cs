using System;
using System.Collections.Generic;

class StackQueueExamples
{
    static void Main()
    {
        StackDemo();
        Console.WriteLine();
        QueueDemo();
        Console.WriteLine();
        BalancedBracketsDemo();
    }

    static void StackDemo()
    {
        Console.WriteLine("--- Stack (LIFO) ---");

        var stack = new Stack<string>();
        stack.Push("first");
        stack.Push("second");
        stack.Push("third");

        Console.WriteLine($"Count after 3 pushes: {stack.Count}");
        Console.WriteLine($"Peek (top, not removed): {stack.Peek()}");
        Console.WriteLine($"Pop: {stack.Pop()}");
        Console.WriteLine($"Pop: {stack.Pop()}");
        Console.WriteLine($"Count after 2 pops: {stack.Count}");
        Console.WriteLine($"Contains \"first\": {stack.Contains("first")}");

        // Undo is the classic stack use case: last action undone first.
        var undo = new Stack<string>();
        undo.Push("type 'Hello'");
        undo.Push("bold selection");
        Console.WriteLine($"Undo: {undo.Pop()}");
        Console.WriteLine($"Undo: {undo.Pop()}");
    }

    static void QueueDemo()
    {
        Console.WriteLine("--- Queue (FIFO) ---");

        var queue = new Queue<string>();
        queue.Enqueue("order #1");
        queue.Enqueue("order #2");
        queue.Enqueue("order #3");

        Console.WriteLine($"Count after 3 enqueues: {queue.Count}");
        Console.WriteLine($"Peek (front, not removed): {queue.Peek()}");
        Console.WriteLine($"Dequeue: {queue.Dequeue()}");
        Console.WriteLine($"Dequeue: {queue.Dequeue()}");
        Console.WriteLine($"Count after 2 dequeues: {queue.Count}");

        // A print queue: jobs are served in the order they arrived.
        var printQueue = new Queue<string>();
        printQueue.Enqueue("report.pdf");
        printQueue.Enqueue("invoice.pdf");
        while (printQueue.Count > 0)
        {
            Console.WriteLine($"Printing: {printQueue.Dequeue()}");
        }
    }

    // A bracket string is balanced when every opener has a matching closer
    // in the correct (innermost-first) order — exactly what a stack checks.
    static bool IsBalanced(string input)
    {
        var pairs = new Dictionary<char, char>
        {
            [')'] = '(',
            [']'] = '[',
            ['}'] = '{',
        };

        var stack = new Stack<char>();

        foreach (char c in input)
        {
            if (c == '(' || c == '[' || c == '{')
            {
                stack.Push(c);
            }
            else if (pairs.ContainsKey(c))
            {
                // A closer with nothing to match, or the wrong opener.
                if (stack.Count == 0 || stack.Pop() != pairs[c])
                {
                    return false;
                }
            }
            // Other characters are ignored.
        }

        // Balanced only if every opener was closed.
        return stack.Count == 0;
    }

    static void BalancedBracketsDemo()
    {
        Console.WriteLine("--- Balanced brackets checker ---");

        string[] samples =
        {
            "({[]})",        // balanced
            "([)]",          // wrong order
            "((a + b) * c)", // balanced, ignores other chars
            "{[}",           // missing closer
            "",              // empty is balanced
            "(((",           // unclosed openers
        };

        foreach (string sample in samples)
        {
            Console.WriteLine($"\"{sample}\" -> balanced: {IsBalanced(sample)}");
        }
    }
}
