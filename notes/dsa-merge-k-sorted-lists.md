# Merge K Sorted Lists

Given k sorted linked lists, merge them into one sorted list. The clean solution uses a min-heap: always take the smallest available head, append it, and push its successor. In C# the built-in `PriorityQueue<TElement, TPriority>` is that heap.

## Worked example

Input lists:

```text
List 1: 1 -> 4 -> 5
List 2: 1 -> 3 -> 4
List 3: 2 -> 6
```

Merge step by step — the heap always holds the current head of each list:

| Step | Heap contents | Popped | Output so far |
|------|---------------|--------|---------------|
| 0 | {1, 1, 2} | — | [] |
| 1 | {1, 2, 4} | 1 | [1] |
| 2 | {2, 3, 4} | 1 | [1, 1] |
| 3 | {3, 4, 4} | 2 | [1, 1, 2] |
| 4 | {4, 4, 6} | 3 | [1, 1, 2, 3] |
| 5 | {4, 5, 6} | 4 | [1, 1, 2, 3, 4] |
| 6 | {5, 6} | 4 | [1, 1, 2, 3, 4, 4] |
| 7 | {6} | 5 | [1, 1, 2, 3, 4, 4, 5] |
| 8 | {} | 6 | [1, 1, 2, 3, 4, 4, 5, 6] |

Final merged list: **`[1, 1, 2, 3, 4, 4, 5, 6]`**. Count check: the inputs hold 3 + 3 + 2 = 8 nodes and the output holds 8 nodes — nothing lost, nothing duplicated.

## C# solution with PriorityQueue

```csharp
public sealed class ListNode
{
    public int Val;
    public ListNode? Next;
    public ListNode(int val = 0, ListNode? next = null) => (Val, Next) = (val, next);
}

public static ListNode? MergeKLists(IList<ListNode?> lists)
{
    // Element = the node; priority = its value. The queue is a min-heap.
    var heap = new PriorityQueue<ListNode, int>();

    foreach (var head in lists)
        if (head is not null)
            heap.Enqueue(head, head.Val);

    var dummy = new ListNode();
    var tail = dummy;

    while (heap.Count > 0)
    {
        var smallest = heap.Dequeue();
        tail.Next = smallest;
        tail = smallest;

        if (smallest.Next is not null)
            heap.Enqueue(smallest.Next, smallest.Next.Val);
    }
    return dummy.Next;
}
```

Trace the first two pops on the worked example: the heap starts with heads 1, 1, 2. Popping 1 (list 1's head) enqueues its successor 4; popping the next 1 (list 2's head) enqueues 3 — matching steps 1 and 2 of the table above.

## Complexity

Let N be the total number of nodes and k the number of lists. Every node is enqueued and dequeued once, each heap operation costs O(log k), and the heap never holds more than k nodes:

- **Time: O(N log k)**
- **Space: O(k)** for the heap (plus O(1) besides the output list, which reuses the input nodes)

Compare with merging lists one pair at a time — that costs O(kN) because the growing result is re-scanned for every list.

## Divide-and-conquer alternative

Merge the lists pairwise like a tournament bracket: merge list 1 with 2, 3 with 4, and so on, halving the count each round. After log k rounds only one list remains. The complexity is the same O(N log k), and it needs no heap — just a pairwise merge helper. It is the better choice when you cannot use a priority queue, or when the lists are arrays rather than linked lists. The heap version is usually simpler to write and to read.

## Edge cases

- All lists empty or the input list itself empty → return `null`.
- A single list → return it unchanged (the heap holds one element throughout).
- Duplicate values across lists are fine: the heap orders equal priorities arbitrarily, and equal values are interchangeable in sorted output.

## Key Takeaways

- A min-heap over the k current heads always yields the next smallest element.
- Worked example: `[[1,4,5],[1,3,4],[2,6]]` merges to `[1,1,2,3,4,4,5,6]` (8 nodes in, 8 out).
- `PriorityQueue<ListNode, int>` in C# is the heap — enqueue each head, then each successor.
- Complexity is O(N log k) time and O(k) space; pairwise merging one list at a time is O(kN).
- Divide and conquer reaches the same O(N log k) without a heap.
