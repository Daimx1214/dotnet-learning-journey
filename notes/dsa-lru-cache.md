# LRU Cache (DSA)

An LRU (Least Recently Used) cache is a fixed-capacity cache that evicts the item that has not been used for the longest time when it runs out of room. It is the classic LeetCode design problem (LeetCode 146) and the eviction policy behind many real caches, because recently used items are the most likely to be used again.

## The Design: Dictionary + Linked List

Two structures combine to give O(1) for every operation:

- A `Dictionary<TKey, LinkedListNode<(TKey, TValue)>>` maps each key to its node, so lookup is O(1).
- A `LinkedList<(TKey, TValue)>` keeps items in usage order: most recently used at the head, least recently used at the tail. Moving a node to the head and removing the tail are both O(1) because a linked list node can be unlinked and re-inserted without shifting anything.

Neither structure alone is enough: a dictionary cannot track usage order, and a linked list cannot find a key without scanning. Together they cover each other's weakness.

## C# Implementation

```csharp
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = new();
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();

    public LruCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public TValue? Get(TKey key)
    {
        if (!_map.TryGetValue(key, out var node)) return default;

        // A hit counts as use: move the node to the front.
        _order.Remove(node);
        _order.AddFirst(node);
        return node.Value.Value;
    }

    public void Put(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            // Update the value and mark it recently used.
            _order.Remove(existing);
            _map.Remove(key);
        }
        else if (_map.Count == _capacity)
        {
            // Evict the least recently used item: the tail node.
            var lru = _order.Last!;
            _order.RemoveLast();
            _map.Remove(lru.Value.Key);
        }

        var node = _order.AddFirst((key, value));
        _map[key] = node;
    }
}
```

Walk through it with capacity 2: `Put(1, "a")`, `Put(2, "b")` fills the cache as `[2, 1]`. `Get(1)` moves 1 to the front: `[1, 2]`. `Put(3, "c")` evicts the tail, key 2, leaving `[3, 1]` — key 1 survives because it was used more recently.

## Complexity

| Operation | Time | Why |
|---|---|---|
| `Get` | O(1) | Dictionary lookup + linked-list move |
| `Put` (new key) | O(1) | Dictionary insert + head insert, tail removal if full |
| `Put` (existing key) | O(1) | Dictionary lookup + move to head |

Space is O(capacity). Note the subtle bug to avoid: `LinkedList<T>.Remove(node)` is O(1) only when you pass the node itself; `Remove(value)` scans the list and is O(n).

## Real Uses

LRU eviction shows up wherever a small fast store fronts a slower one: database buffer pools, operating-system page replacement, web caches, and image caches in mobile apps. In .NET, `MemoryCache` does not use strict LRU — it evicts by priority and expiration under memory pressure — so if you need exact LRU behaviour, implement it yourself as above. This implementation is not thread-safe; wrap `Get`/`Put` in a `lock` if the cache is shared across threads.

## Key Takeaways

- LRU evicts the least recently used item; a `Dictionary` plus a `LinkedList` gives O(1) `Get` and `Put`.
- Every access — hit or write — moves the item to the head; eviction always removes the tail.
- `LinkedList.Remove(node)` is O(1) with the node reference, O(n) with a value — always keep the node.
- Use it when recent access predicts future access; add locking if the cache is shared between threads.
