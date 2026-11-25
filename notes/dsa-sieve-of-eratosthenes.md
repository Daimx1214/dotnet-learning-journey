# Sieve of Eratosthenes

The Sieve of Eratosthenes finds all primes up to n by elimination: start with every number marked as prime, then cross out the multiples of each prime, smallest first. Whatever is never crossed out is prime. It runs in O(n log log n), which is nearly linear, and it is the standard way to generate primes up to millions.

## The Idea

The key insight is that a composite number always has a prime factor no larger than its square root. So to sieve up to n, you only need to cross out multiples of primes up to the square root of n. Every composite up to n is a multiple of one of those primes, so it gets crossed out.

A second insight: when crossing out multiples of a prime p, start at p times p, not at 2p. All smaller multiples of p were already crossed out by smaller primes. For example, the multiples of 5 below 25 (10, 15, 20) are also multiples of 2 or 3, so they are already gone.

## Pseudocode

```text
Sieve(n):
    isPrime = [true] * (n + 1)
    isPrime[0] = isPrime[1] = false
    for p from 2 to sqrt(n):
        if isPrime[p]:
            for multiple from p * p to n, step p:
                isPrime[multiple] = false
    return [i for i in 2..n if isPrime[i]]
```

## C# Sketch

```csharp
List<int> Sieve(int n)
{
    var isPrime = new bool[n + 1];
    Array.Fill(isPrime, true);
    if (n >= 0) isPrime[0] = false;
    if (n >= 1) isPrime[1] = false;

    for (int p = 2; p * p <= n; p++)
    {
        if (!isPrime[p]) continue;
        for (int multiple = p * p; multiple <= n; multiple += p)
        {
            isPrime[multiple] = false;
        }
    }

    var primes = new List<int>();
    for (int i = 2; i <= n; i++)
    {
        if (isPrime[i]) primes.Add(i);
    }
    return primes;
}

Console.WriteLine(string.Join(", ", Sieve(30)));
// 2, 3, 5, 7, 11, 13, 17, 19, 23, 29
```

Note the loop bound `p * p <= n`: the outer loop stops at the square root of n, and the inner loop starts at `p * p`. Both follow from the two insights above.

## Worked Example: Primes up to 30

Start with 2 to 30 all marked prime. The square root of 30 is about 5.47, so we only sieve with p = 2, 3, 5.

**p = 2:** cross out multiples of 2 starting at 4: 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30.

**p = 3:** cross out multiples of 3 starting at 9: 9, 12, 15, 18, 21, 24, 27, 30. (6 is already gone; 12, 18, 24, 30 were crossed out by 2.)

**p = 5:** cross out multiples of 5 starting at 25: 25, 30. (10, 15, 20 were already crossed out by 2 or 3.)

Remaining unmarked numbers: **2, 3, 5, 7, 11, 13, 17, 19, 23, 29**. That is 10 primes up to 30. Check a few survivors by hand: 7 has no divisor other than 1 and 7, 11 is not divisible by 2, 3, or 5, and 29 is not divisible by 2, 3, or 5, so all three are prime. Every composite up to 30 (4, 6, 8, 9, 10, 12, 14, 15, 16, 18, 20, 21, 22, 24, 25, 26, 27, 28, 30) was crossed out.

## Complexity

The total work is n times the sum of 1/p over primes p up to n, which is O(n log log n). The `log log n` factor grows so slowly that for practical purposes the sieve is nearly linear. Memory is O(n) for the boolean array, one byte per number (or one bit with a `BitArray`).

## Optimizations

- **Odd-only sieve:** 2 is the only even prime, so store only odd numbers and halve the memory and work. Index i represents the number 2i + 1.
- **Wheel factorization:** skip multiples of 2, 3, and 5 up front, and only test numbers coprime to 30.
- **Segmented sieve:** when n is too large for one array, sieve the range in blocks, using the primes up to the square root of n (found by a small sieve) to cross out multiples in each block. This keeps memory at O(sqrt(n)) plus one block, and it is how primes up to 10^11 and beyond are generated.

## Key Takeaways

- The sieve crosses out multiples of each prime up to the square root of n; starting each inner loop at p times p avoids repeat work.
- Primes up to 30 are 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, a total of 10 primes.
- Time complexity is O(n log log n), nearly linear, and space is O(n) for the boolean array.
- The odd-only optimization halves memory and work, since 2 is the only even prime.
- For very large n, the segmented sieve processes the range in blocks and keeps memory at O(sqrt(n)) plus one block.
