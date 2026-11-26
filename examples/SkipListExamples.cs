// Skip list demo: a generic SkipList<T> with Insert, Search, Delete and
// enumeration in sorted order. Levels are chosen with p = 0.5 and a fixed
// random seed, so every run prints exactly the same output.

using System;
using System.Collections;
using System.Collections.Generic;

var list = new SkipList<int>(seed: 42);

int[] values = { 3, 6, 7, 9, 12, 19, 17, 26, 21, 25 };
foreach (int value in values)
{
    list.Insert(value);
}

Console.WriteLine("After insert: " + string.Join(", ", list));
Console.WriteLine("Search(19) = " + list.Search(19));
Console.WriteLine("Search(15) = " + list.Search(15));

// Self-check: throw if any expected result differs.
int[] expectedAfterInsert = { 3, 6, 7, 9, 12, 17, 19, 21, 25, 26 };
int[] expectedAfterDelete = { 3, 6, 7, 9, 12, 17, 21, 25, 26 };

if (!Matches(list, expectedAfterInsert) || !list.Search(19) || list.Search(15))
{
    throw new InvalidOperationException("List contents after insert are wrong.");
}

list.Delete(19);

Console.WriteLine("After deleting 19: " + string.Join(", ", list));
Console.WriteLine("Search(19) = " + list.Search(19));

if (!Matches(list, expectedAfterDelete) || list.Search(19))
{
    throw new InvalidOperationException("List contents after deletion are wrong.");
}

Console.WriteLine("All checks passed.");

// Local helper for the self-checks above.
static bool Matches(SkipList<int> skipList, int[] expected)
{
    int index = 0;
    foreach (int value in skipList)
    {
        if (index >= expected.Length || value != expected[index])
        {
            return false;
        }
        index++;
    }
    return index == expected.Length;
}

/// <summary>
/// A generic skip list. Each node is a tower of forward pointers; the
/// bottom level holds every element in sorted order and higher levels act
/// as express lanes. Tower heights are random with p = 0.5.
/// </summary>
public sealed class SkipList<T> : IEnumerable<T> where T : IComparable<T>
{
    private const int MaxLevel = 16;
    private const double Probability = 0.5;

    private sealed class Node
    {
        public T Value;
        public Node?[] Forward;

        public Node(T value, int level)
        {
            Value = value;
            Forward = new Node?[level];
        }
    }

    private readonly Node _head;
    private readonly Random _random;
    private int _level = 1;

    public SkipList(int seed)
    {
        // The head is a sentinel with the default value; it is never
        // returned by enumeration.
        _head = new Node(default!, MaxLevel);
        _random = new Random(seed);
    }

    public int Count { get; private set; }

    public void Insert(T value)
    {
        var update = new Node?[MaxLevel];
        Node current = _head;

        // Find the predecessor at each level, top down.
        for (int i = _level - 1; i >= 0; i--)
        {
            while (current.Forward[i] is not null &&
                   current.Forward[i]!.Value.CompareTo(value) < 0)
            {
                current = current.Forward[i]!;
            }
            update[i] = current;
        }

        int newLevel = RandomLevel();
        if (newLevel > _level)
        {
            for (int i = _level; i < newLevel; i++)
            {
                update[i] = _head;
            }
            _level = newLevel;
        }

        var node = new Node(value, newLevel);
        for (int i = 0; i < newLevel; i++)
        {
            node.Forward[i] = update[i]!.Forward[i];
            update[i]!.Forward[i] = node;
        }

        Count++;
    }

    public bool Search(T value)
    {
        Node current = _head;
        for (int i = _level - 1; i >= 0; i--)
        {
            while (current.Forward[i] is not null &&
                   current.Forward[i]!.Value.CompareTo(value) < 0)
            {
                current = current.Forward[i]!;
            }
        }

        Node? candidate = current.Forward[0];
        return candidate is not null && candidate.Value.CompareTo(value) == 0;
    }

    public bool Delete(T value)
    {
        var update = new Node?[MaxLevel];
        Node current = _head;

        for (int i = _level - 1; i >= 0; i--)
        {
            while (current.Forward[i] is not null &&
                   current.Forward[i]!.Value.CompareTo(value) < 0)
            {
                current = current.Forward[i]!;
            }
            update[i] = current;
        }

        Node? target = current.Forward[0];
        if (target is null || target.Value.CompareTo(value) != 0)
        {
            return false;
        }

        for (int i = 0; i < _level; i++)
        {
            if (update[i]!.Forward[i] != target)
            {
                break;
            }
            update[i]!.Forward[i] = target.Forward[i];
        }

        while (_level > 1 && _head.Forward[_level - 1] is null)
        {
            _level--;
        }

        Count--;
        return true;
    }

    private int RandomLevel()
    {
        int level = 1;
        while (level < MaxLevel && _random.NextDouble() < Probability)
        {
            level++;
        }
        return level;
    }

    public IEnumerator<T> GetEnumerator()
    {
        // Walk the bottom level: every element, in sorted order.
        Node? current = _head.Forward[0];
        while (current is not null)
        {
            yield return current.Value;
            current = current.Forward[0];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
