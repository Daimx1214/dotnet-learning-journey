using System;
using System.Collections.Generic;

// A min-heap: the smallest element is always at the root.
// Insert and extract-min both run in O(log n); peek is O(1).
// The heap is stored in a List<T>, where the children of the
// element at index i live at 2i + 1 and 2i + 2.
public class MinHeap<T> where T : IComparable<T>
{
    private readonly List<T> _items = new();

    public int Count => _items.Count;

    public T Peek()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Heap is empty.");
        return _items[0];
    }

    public void Insert(T value)
    {
        _items.Add(value);
        BubbleUp(_items.Count - 1);
    }

    public T ExtractMin()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Heap is empty.");

        T min = _items[0];
        _items[0] = _items[^1];
        _items.RemoveAt(_items.Count - 1);

        if (_items.Count > 0)
            BubbleDown(0);

        return min;
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_items[index].CompareTo(_items[parent]) >= 0)
                break;

            (_items[index], _items[parent]) = (_items[parent], _items[index]);
            index = parent;
        }
    }

    private void BubbleDown(int index)
    {
        while (true)
        {
            int left = 2 * index + 1;
            int right = 2 * index + 2;
            int smallest = index;

            if (left < _items.Count && _items[left].CompareTo(_items[smallest]) < 0)
                smallest = left;
            if (right < _items.Count && _items[right].CompareTo(_items[smallest]) < 0)
                smallest = right;

            if (smallest == index)
                break;

            (_items[index], _items[smallest]) = (_items[smallest], _items[index]);
            index = smallest;
        }
    }
}

public static class Program
{
    public static void Main()
    {
        // --- Hand-rolled MinHeap demo ---
        var heap = new MinHeap<int>();
        foreach (int value in new[] { 9, 4, 7, 1, 3, 6 })
            heap.Insert(value);

        Console.WriteLine($"MinHeap peek: {heap.Peek()}"); // 1
        Console.Write("MinHeap extract order: ");
        while (heap.Count > 0)
            Console.Write(heap.ExtractMin() + " "); // 1 3 4 6 7 9
        Console.WriteLine();

        // --- .NET built-in PriorityQueue demo ---
        // PriorityQueue<TElement, TPriority> is a min-heap too:
        // the element with the SMALLEST priority dequeues first.
        var tasks = new PriorityQueue<string, int>();
        tasks.Enqueue("Write report", 3);
        tasks.Enqueue("Fix production bug", 1);
        tasks.Enqueue("Review pull request", 2);
        tasks.Enqueue("Refactor tests", 4);

        Console.WriteLine($"Built-in peek: {tasks.Peek()}"); // Fix production bug
        Console.WriteLine("Built-in dequeue order:");
        while (tasks.Count > 0)
            Console.WriteLine($"  - {tasks.Dequeue()}");
        // Fix production bug, Review pull request, Write report, Refactor tests

        // --- A practical use: top-K largest values with a min-heap ---
        int[] numbers = { 5, 12, 3, 21, 8, 17, 9 };
        var top3 = new PriorityQueue<int, int>();
        foreach (int n in numbers)
        {
            top3.Enqueue(n, n);
            if (top3.Count > 3)
                top3.Dequeue(); // drop the smallest of the four
        }
        Console.Write("Top 3 largest: ");
        while (top3.Count > 0)
            Console.Write(top3.Dequeue() + " "); // 12 17 21
        Console.WriteLine();
    }
}
