# Rabin–Karp String Matching (DSA)

Rabin–Karp finds all occurrences of a pattern in a text by comparing *hashes* instead of characters. It slides a window of the pattern's length across the text, maintaining the hash of the current window, and only compares characters directly when the hashes match. The average running time is O(n + m) for a text of length n and a pattern of length m — the naive comparison is O(n · m).

## The Polynomial Rolling Hash

The hash of a string is a polynomial in a base `b`, taken modulo a prime `q`:

```text
H(s) = (s[0]·b^(m-1) + s[1]·b^(m-2) + ... + s[m-1]) mod q
```

The key property is that the hash *rolls*: when the window moves one step right, the hash of the new window follows from the old one in constant time — drop the leading character, shift, and add the new one:

```text
H_next = ((H − s[i]·b^(m-1)) · b + s[i+m]) mod q
```

In code, precompute `b^(m-1) mod q` once, and keep every intermediate value non-negative by adding `q` before taking the modulus (C#'s `%` keeps the sign of the dividend, so a negative intermediate would corrupt the hash).

```csharp
public static List<int> RabinKarp(string text, string pattern, int prime = 101)
{
    const int Base = 256; // Alphabet size for extended ASCII.
    int m = pattern.Length, n = text.Length;
    var matches = new List<int>();
    if (m == 0 || m > n) return matches;

    long highestPower = 1;
    for (int i = 0; i < m - 1; i++)
        highestPower = highestPower * Base % prime;

    long patternHash = 0, windowHash = 0;
    for (int i = 0; i < m; i++)
    {
        patternHash = (patternHash * Base + pattern[i]) % prime;
        windowHash = (windowHash * Base + text[i]) % prime;
    }

    for (int i = 0; i <= n - m; i++)
    {
        // Hash match is only a candidate — verify the characters.
        if (patternHash == windowHash && text.AsSpan(i, m).SequenceEqual(pattern))
            matches.Add(i);

        if (i < n - m)
        {
            windowHash = (windowHash - text[i] * highestPower % prime + prime) % prime;
            windowHash = (windowHash * Base + text[i + m]) % prime;
        }
    }
    return matches;
}
```

## Worked Example: Finding "cad" in "abracadabra"

Take base 256, modulus 101, and the pattern `"cad"` (c = 99, a = 97, d = 100). The pattern hash is computed left to right: start at 99, then (99 · 54 + 97) mod 101 = 90 (since 256 mod 101 = 54), then (90 · 54 + 100) mod 101 = 11. So `H("cad") = 11`.

Rolling the window across the text gives these hashes: `"abr"` = 4, `"bra"` = 30, `"rac"` = 17, `"aca"` = 41, `"cad"` = 11. The roll from `"aca"` to `"cad"` checks out: ((41 − 97 · 88) · 54 + 100) mod 101 = 11, where 88 is 256² mod 101 — the same value the direct computation gives. At index 4 the window hash equals the pattern hash, the character check confirms a real match, and Rabin–Karp reports index 4 — which is correct: `"abra" + "cad" + "abra"`.

## Hash Collisions and Double Hashing

Two different strings can share a hash — that is a *collision*, and it is the algorithm's one weakness. A collision produces a false candidate, not a wrong answer, because the character-by-character verification step rejects it. But if collisions are frequent (a bad modulus, or adversarial input), every window triggers a full O(m) verification and the runtime degrades to O(n · m). Two defences: choose a large prime modulus (or a 64-bit natural-overflow hash, where the modulus is 2⁶⁴ and overflow is the reduction), and use *double hashing* — two independent moduli, accepting a candidate only when both hashes match. With two 32-bit moduli the chance of a spurious match is around 1 in 2⁶⁴, negligible in practice. Note that the verification step is not optional: without it, the algorithm can return false positives.

## When to Use It

Rabin–Karp shines when the same text is searched for many patterns — hash the text's windows once and compare against many pattern hashes — and it is the natural fit for plagiarism detection and finding repeated substrings. For a single pattern, Knuth–Morris–Pratt (KMP) guarantees O(n + m) worst-case time with no collision risk, and .NET's built-in `string.IndexOf` is heavily optimized and usually faster in practice. Use Rabin–Karp when you need its multi-pattern advantage or when the rolling-hash idea itself is the point — for example, as a stepping stone to rolling hashes in other problems.

## Key Takeaways

- Rabin–Karp compares window hashes instead of characters, rolling the hash forward in O(1) per step for O(n + m) average time.
- The polynomial hash drops the leading character, shifts by the base, and adds the new character — all modulo a prime.
- Verified by hand: with base 256 and modulus 101, `H("cad") = 11`, and the rolling hash finds the match at index 4 of `"abracadabra"`.
- Hash collisions cause false candidates, never wrong answers — but only if you verify matching windows character by character.
- Double hashing (two independent moduli) makes spurious matches negligible; a 64-bit overflow hash is a simpler alternative.
- Prefer KMP or `string.IndexOf` for a single pattern; prefer Rabin–Karp when searching for many patterns in the same text.
