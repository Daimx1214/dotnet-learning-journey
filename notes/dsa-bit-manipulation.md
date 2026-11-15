# Bit Manipulation (DSA)

Bit manipulation works directly on the binary representation of integers. It is fast — every operation is a single CPU instruction — and it appears constantly in LeetCode problems, low-level code, and interview questions. The core idea is simple: an `int` is 32 bits, and each bit can be read, set, cleared, or flipped independently.

## The Basic Operators

| Operator | Meaning | Example (`n = 0b1010`, 10) |
|---|---|---|
| `&` AND | 1 only where both bits are 1 | `n & 1` → 0 (even) |
| `\|` OR | 1 where either bit is 1 | `n \| 1` → `0b1011` (11) |
| `^` XOR | 1 where the bits differ | `n ^ 1` → `0b1011` (11) |
| `~` NOT | Flips every bit | `~n` → -11 (two's complement) |
| `<<` | Shift left (multiply by 2 per step) | `n << 1` → 20 |
| `>>` | Shift right (divide by 2 per step) | `n >> 1` → 5 |
| `>>>` | Unsigned shift right (C# 11, fills with 0) | `-8 >>> 1` → large positive |

In C#, `>>` on a signed integer is arithmetic: it fills the vacated bits with the sign bit, so negative numbers stay negative. `>>>` always fills with 0.

## Checking, Setting, Clearing, Toggling a Bit

```csharp
int n = 0b1010; // 10

bool isBitSet = (n & (1 << 3)) != 0;  // true: bit 3 is 1
int setBit    = n | (1 << 0);         // 0b1011 = 11: set bit 0
int clearBit  = n & ~(1 << 3);        // 0b0010 = 2:  clear bit 3
int toggleBit = n ^ (1 << 1);         // 0b1000 = 8:  flip bit 1
```

The pattern is what matters: `x | (1 << i)` sets bit `i`, `x & ~(1 << i)` clears it, `x ^ (1 << i)` toggles it, and `(x & (1 << i)) != 0` tests it. One warning: `1 << 31` overflows an `int`, so use `1L << i` with `long` for bit 31 and above.

## XOR Tricks

XOR is the most useful operator in interview problems because `a ^ a = 0` and `a ^ 0 = a`:

```csharp
// Single Number (LeetCode 136): every value appears twice except one.
int SingleNumber(int[] nums)
{
    int result = 0;
    foreach (int n in nums) result ^= n; // pairs cancel out
    return result;
}

// Swap two variables without a temporary.
a ^= b; b ^= a; a ^= b;

// Check whether two numbers differ in exactly one bit.
bool differsByOneBit = (a ^ b) is not 0 && ((a ^ b) & ((a ^ b) - 1)) == 0;
```

## Counting Set Bits

```csharp
// Brian Kernighan's algorithm: n & (n - 1) clears the lowest set bit.
// It loops once per set bit, not once per bit position.
int CountSetBits(int n)
{
    int count = 0;
    while (n != 0)
    {
        n &= n - 1;
        count++;
    }
    return count;
}

// Built-in, .NET 7+: fastest option, uses a hardware instruction.
int fast = BitOperations.PopCount((uint)42); // 3
```

Related facts worth remembering: `n & (n - 1) == 0` (for `n > 0`) means `n` is a power of two; `n & 1` tests odd/even; `n & -n` isolates the lowest set bit.

## Common LeetCode Patterns

- **Single Number / Missing Number:** XOR everything; pairs cancel, the survivor is the answer.
- **Power of Two (231):** `n > 0 && (n & (n - 1)) == 0`.
- **Number of 1 Bits (191) / Hamming Distance (461):** count set bits in `n` or in `x ^ y`.
- **Reverse Bits (190):** shift bits out of `n` one at a time and build the result from the other end.
- **Subsets (78):** iterate masks from `0` to `(1 << n) - 1`; bit `i` set means "include element `i`".
- **Sum of Two Integers (371):** add with `a ^ b` (sum without carry) and `(a & b) << 1` (carry), repeated until the carry is 0.

## Key Takeaways

- `&`, `|`, `^`, `~`, `<<`, `>>` manipulate individual bits; each is a single CPU instruction.
- Set, clear, toggle, and test bit `i` with `(1 << i)` combined with `|`, `& ~`, `^`, and `&`.
- XOR cancels pairs (`a ^ a = 0`), which solves Single Number and similar problems in O(1) space.
- `n & (n - 1)` clears the lowest set bit — the basis for fast bit counting and the power-of-two test.
