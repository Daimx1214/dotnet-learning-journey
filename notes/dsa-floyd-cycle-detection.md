# Floyd's Cycle Detection (Tortoise and Hare)

Floyd's algorithm detects a cycle in a linked list using two pointers moving at different speeds, in `O(n)` time and `O(1)` space. No hash set, no extra memory.

## The Idea

Send a slow pointer one step at a time and a fast pointer two steps at a time. If there is a cycle, the fast pointer laps the slow one and they eventually meet inside the cycle. If the fast pointer reaches the end, there is no cycle.

## Detecting a Cycle

```csharp
public static bool HasCycle(ListNode? head)
{
    var slow = head;
    var fast = head;

    while (fast is not null && fast.Next is not null)
    {
        slow = slow!.Next;
        fast = fast.Next.Next;
        if (slow == fast)
            return true;
    }

    return false;
}
```

The loop condition checks `fast` and `fast.Next` because the fast pointer moves two steps — either could be null on an acyclic list.

## Finding the Cycle Start

Once the pointers meet, reset one pointer to the head and move both one step at a time. They meet again exactly at the cycle's entry node.

```csharp
public static ListNode? FindCycleStart(ListNode? head)
{
    var slow = head;
    var fast = head;

    while (fast is not null && fast.Next is not null)
    {
        slow = slow!.Next;
        fast = fast.Next.Next;
        if (slow == fast)
        {
            slow = head;
            while (slow != fast)
            {
                slow = slow!.Next;
                fast = fast!.Next;
            }
            return slow;
        }
    }

    return null;
}
```

Why this works: say the list has `a` nodes before the cycle, the cycle has `c` nodes, and the pointers first meet `b` nodes into the cycle. The slow pointer has travelled `a + b` steps; the fast pointer has travelled `a + b + k*c` for some whole number of laps `k`. Since fast moves twice as fast, `2(a + b) = a + b + k*c`, so `a + b = k*c`, meaning `a = k*c - b`. Walking `a` steps from the head reaches the cycle start, and walking `a` steps from the meeting point (which is `b` into the cycle) also reaches the cycle start, because `b + a = k*c` is a whole number of laps.

## Finding the Cycle Length

From the meeting point, walk once around the cycle counting steps until you return to the same node:

```csharp
public static int CycleLength(ListNode meetingPoint)
{
    int length = 1;
    var current = meetingPoint.Next;
    while (current != meetingPoint)
    {
        length++;
        current = current!.Next;
    }
    return length;
}
```

## Hand-Verified Example

Linked list: `1 -> 2 -> 3 -> 4 -> 2` (node 4 points back to node 2).

- Cycle entry is node **2**.
- Cycle length is **3** (nodes 2, 3, 4).
- Distance from head to cycle entry: `a = 1` (just node 1).
- First meeting: slow and fast meet at node 3. Check: slow travels 1, 2, 3 (3 steps); fast travels 1, 3, 2, 4, 3... after 3 double-steps fast is at node 3 too. Then resetting slow to the head and moving both one step at a time: slow 1 -> 2, fast 3 -> 4 -> 2 — they meet at node 2 after 1 step from the head, exactly the cycle start.

## Complexity

Detection, cycle start, and cycle length are each `O(n)` time and `O(1)` space. The hash-set alternative is also `O(n)` time but needs `O(n)` space to store visited nodes.

## Key Takeaways

- Two pointers at speeds 1 and 2 must meet inside a cycle; the fast pointer reaching null proves there is none.
- Reset one pointer to the head after the first meeting to find the cycle start — both pointers then walk one step at a time.
- Cycle length is one lap from the meeting point back to itself.
- Verified: `1 -> 2 -> 3 -> 4 -> 2` has cycle start 2 and length 3.
- `O(n)` time, `O(1)` space — the classic reason to prefer it over a visited set.
