# Bloom Filters (DSA)

A Bloom filter is a space-efficient probabilistic data structure that answers one question: "is this item definitely not in the set, or possibly in it?" It can say "definitely not" with certainty, and "possibly yes" with a small, tunable chance of being wrong — a false positive. It never produces a false negative.

## How It Works

A Bloom filter is a bit array of `m` bits, all initially 0, plus `k` independent hash functions. To add an item, hash it with each of the `k` functions and set the `k` resulting bit positions to 1. To test membership, hash the item the same way: if all `k` bits are 1, the item is *possibly* present; if any bit is 0, the item is *definitely absent*.

```text
Add("apple"):  h1 -> bit 3,  h2 -> bit 9,  h3 -> bit 14   (set to 1)
Add("banana"): h1 -> bit 2,  h2 -> bit 9,  h3 -> bit 21   (set to 1)
Test("cherry"): h1 -> bit 3, h2 -> bit 9, h3 -> bit 21   (all 1 -> "possibly present",
                                                            even though cherry was never added)
```

The false positive above happens because cherry's three bit positions were all set by other items. As more items are added, more bits become 1 and false positives get more likely.

## Sizing Math

For `n` expected items and a target false-positive probability `p`, the optimal size and hash count are:

```text
m = -(n * ln p) / (ln 2)^2     bits in the array
k = (m / n) * ln 2             number of hash functions
```

For 1,000,000 items at a 1% false-positive rate: `m ≈ 9.6 million bits ≈ 1.2 MB` and `k ≈ 7`. A `HashSet<string>` holding the same million strings would need tens of megabytes. That is the whole point: a Bloom filter trades a small error rate for an order of magnitude less memory. The actual false-positive rate after inserting `n` items is approximately `(1 - e^(-kn/m))^k`.

## C# Sketch

```csharp
public class BloomFilter
{
    private readonly BitArray _bits;
    private readonly int _hashCount;

    public BloomFilter(int bitCount, int hashCount)
    {
        _bits = new BitArray(bitCount);
        _hashCount = hashCount;
    }

    public void Add(string item)
    {
        foreach (int index in GetIndexes(item))
            _bits[index] = true;
    }

    public bool MightContain(string item)
    {
        foreach (int index in GetIndexes(item))
            if (!_bits[index]) return false; // definitely absent

        return true; // possibly present
    }

    private IEnumerable<int> GetIndexes(string item)
    {
        // Double hashing: two base hashes simulate k functions.
        int h1 = item.GetHashCode();
        int h2 = (h1 >> 16) ^ 0x9E3779B9;
        for (int i = 0; i < _hashCount; i++)
            yield return Math.Abs((h1 + i * h2) % _bits.Length);
    }
}
```

Note that `string.GetHashCode()` is randomized per process in .NET, so this sketch is fine for learning but a real implementation should use a stable hash such as SHA-256 or MurmurHash truncated to the needed range.

## Bloom Filter vs HashSet

| | Bloom filter | HashSet<T> |
|---|---|---|
| Answer | "definitely not / possibly yes" | Exact yes / no |
| False positives | Yes, tunable | None |
| False negatives | Never | Never |
| Memory for 1M items | ~1.2 MB at 1% error | Tens of MB |
| Deletion | Not supported (clearing a bit could remove other items) | Supported |
| Iteration | Impossible (items are not stored) | Supported |

A counting Bloom filter (small counters instead of bits) supports deletion, at the cost of more memory.

## When to Use It

Use a Bloom filter as a cheap pre-check before an expensive lookup: "is this username taken?" before hitting the database, "have we seen this URL?" in a web crawler before fetching it, or "is this key in the SSTable?" in databases like Cassandra and RocksDB, where a negative answer avoids a disk read entirely. Do not use one when an exact answer is required — for example, never use it to decide whether a payment was already processed.

## Key Takeaways

- A Bloom filter answers "definitely not" or "possibly yes" — false positives are possible, false negatives are not.
- Size it with `m = -(n ln p) / (ln 2)^2` and `k = (m/n) ln 2`; 1M items at 1% error costs about 1.2 MB.
- It is a pre-filter for expensive lookups, not a replacement for exact storage like a HashSet.
- Deletion is not supported in a plain Bloom filter; use a counting variant if you need it.
