# Huffman Coding

Huffman coding is a lossless compression method that assigns shorter bit codes to frequent symbols and longer codes to rare ones. The result is a prefix-free code: no code is a prefix of another, so a bit stream can be decoded unambiguously from left to right.

## Frequency Table

Compression starts by counting how often each symbol occurs. The worked example in this note uses these frequencies, which sum to `5 + 9 + 12 + 13 + 16 + 45 = 100`:

| Symbol | Frequency |
|--------|-----------|
| a | 5 |
| b | 9 |
| c | 12 |
| d | 13 |
| e | 16 |
| f | 45 |

Symbol `f` alone is 45% of the input, so it deserves the shortest code. Symbols `a` and `b` together are only 14%, so they can afford the longest codes.

## Priority-Queue Tree Build

Repeatedly take the two lowest-frequency nodes from a min-heap, merge them into a parent node whose frequency is their sum, and push the parent back. Each merge creates one internal node; with 6 symbols there are exactly 5 merges.

```csharp
// Sketch: Node is a small class/record with Frequency, Symbol (null for internal
// nodes), Left, and Right. The heap orders nodes by Frequency.
var heap = new PriorityQueue<Node, int>();
foreach (var (symbol, frequency) in frequencies)
{
    heap.Enqueue(new Node(symbol, frequency), frequency);
}

while (heap.Count > 1)
{
    Node left = heap.Dequeue();
    Node right = heap.Dequeue();
    var parent = new Node(null, left.Frequency + right.Frequency)
    {
        Left = left,
        Right = right
    };
    heap.Enqueue(parent, parent.Frequency);
}

Node root = heap.Dequeue(); // The Huffman tree.
```

## Worked Example with Frequencies a:5, b:9, c:12, d:13, e:16, f:45

Trace the merges, always taking the two smallest available frequencies:

1. Merge `a:5` and `b:9` into node `ab:14`. Heap now holds `12, 13, 14, 16, 45`.
2. Merge `c:12` and `d:13` into node `cd:25`. Heap now holds `14, 16, 25, 45`.
3. Merge `ab:14` and `e:16` into node `abe:30`. Heap now holds `25, 30, 45`.
4. Merge `cd:25` and `abe:30` into node `cdabe:55`. Heap now holds `45, 55`.
5. Merge `f:45` and `cdabe:55` into the root `100`. Heap holds `100`.

Assign `0` to a left branch and `1` to a right branch (taking `f` as the left child of the root, as in the trace above):

| Symbol | Code | Length | Frequency x Length |
|--------|------|--------|--------------------|
| f | `0` | 1 | `45 x 1 = 45` |
| c | `100` | 3 | `12 x 3 = 36` |
| d | `101` | 3 | `13 x 3 = 39` |
| e | `111` | 3 | `16 x 3 = 48` |
| a | `1100` | 4 | `5 x 4 = 20` |
| b | `1101` | 4 | `9 x 4 = 36` |

Total weighted path length: `45 + 36 + 39 + 48 + 20 + 36 = 224`. Verify the addition step by step: `45 + 36 = 81`; `81 + 39 = 120`; `120 + 48 = 168`; `168 + 20 = 188`; `188 + 36 = 224`.

Cross-check with the merge costs: the sum of the internal-node frequencies is `14 + 25 + 30 + 55 + 100 = 224`. Both methods agree, which confirms the total. For comparison, a fixed 3-bit code for 6 symbols would cost `100 x 3 = 300` bits, so Huffman saves `300 - 224 = 76` bits, about 25% on this input. The average code length is `224 / 100 = 2.24` bits per symbol.

## Prefix-Free Property

No code in the table is a prefix of another: `0` starts with `0` while every other code starts with `1`; `100` and `101` differ in their last bit; `1100` and `1101` differ in their last bit; `111` is not a prefix of any 4-bit code. This holds by construction — every symbol is a leaf of the tree, and a leaf's code is the path from the root, so one leaf's path can never be a prefix of another leaf's path (that would require one leaf to be an ancestor of the other). Prefix-freedom is what makes decoding possible without separators: read bits until they match a leaf code, output the symbol, and start again at the root.

## Encode and Decode

```csharp
// Codes from the worked example.
var codes = new Dictionary<char, string>
{
    ['f'] = "0", ['c'] = "100", ['d'] = "101",
    ['e'] = "111", ['a'] = "1100", ['b'] = "1101"
};

string Encode(string input) =>
    string.Concat(input.Select(ch => codes[ch]));

// "face" encodes as f(0) + a(1100) + c(100) + e(111) = "01100100111".
// Length check: 1 + 4 + 3 + 3 = 11 bits, and the string above has 11 characters.

string Decode(string bits, Node root)
{
    var result = new System.Text.StringBuilder();
    Node current = root;
    foreach (char bit in bits)
    {
        current = bit == '0' ? current.Left! : current.Right!;
        if (current.Symbol is char symbol)
        {
            result.Append(symbol);
            current = root;
        }
    }
    return result.ToString();
}

// Decode("01100100111") walks 0 -> f, 1100 -> a, 100 -> c, 111 -> e: "face".
```

Encoding is a dictionary lookup per symbol; decoding walks the tree one bit at a time and resets to the root at each leaf. In practice the tree (or the code table) must be stored with the compressed data, which costs extra space — Huffman coding pays off when the input is large enough that the savings outweigh that overhead.

## Key Takeaways

- Huffman coding gives the most frequent symbols the shortest codes, using a greedy merge of the two lowest frequencies.
- For frequencies `a:5, b:9, c:12, d:13, e:16, f:45`, the verified total weighted path length is `224` bits (average `2.24` bits per symbol), versus `300` bits for a fixed 3-bit code.
- The merge-cost cross-check (`14 + 25 + 30 + 55 + 100 = 224`) confirms the total.
- Codes are prefix-free because every symbol is a leaf, so decoding needs no separators.
- The code table must travel with the compressed data, so Huffman coding suits larger inputs where the savings exceed that overhead.
