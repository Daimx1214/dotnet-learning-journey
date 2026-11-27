# Longest Palindromic Substring

Given a string `s`, find its longest substring that reads the same forwards and backwards. The classic solutions are expand-around-center and dynamic programming. Both are O(n^2) time; expand-around-center uses O(1) extra space, DP uses O(n^2).

## How many centers are there?

A palindrome has a center. For a string of length `n` there are `n` odd-length centers (one character) and `n - 1` even-length centers (between two characters) — `2n - 1` centers in total. For `s = "babad"` (n = 5) that is `2 * 5 - 1 = 9` centers: 5 single characters plus 4 gaps between characters.

## Worked example: "babad"

The longest palindromic substring of `"babad"` is `"bab"` (length 3). `"aba"` is also a valid answer of the same length — the problem accepts either. Here is how expand-around-center finds it:

| Center | Expansion | Palindrome found |
|--------|-----------|------------------|
| `b` (index 0) | cannot expand | `"b"` (len 1) |
| gap 0–1 | `b` vs `a` differ | none |
| `a` (index 1) | `b` vs `b` match, then edges | `"bab"` (len 3) |
| gap 1–2 | `a` vs `b` differ | none |
| `b` (index 2) | `a` vs `a` match, then edges | `"aba"` (len 3) |
| gap 2–3 | `b` vs `a` differ | none |
| `a` (index 3) | `b` vs `d` differ | `"a"` (len 1) |
| gap 3–4 | `a` vs `d` differ | none |
| `d` (index 4) | cannot expand | `"d"` (len 1) |

The best length seen is 3, first achieved at center index 1 (`"bab"`). Note the even centers contribute nothing here — `"babad"` has no even-length palindrome longer than 1.

## The DP table approach

Define `dp[i][j] = true` when `s[i..j]` is a palindrome. The recurrence:

```text
dp[i][j] = (s[i] == s[j]) && (j - i <= 2 || dp[i+1][j-1])
```

- Length 1: always true (a single character is a palindrome).
- Length 2: true when the two characters match.
- Length 3+: true when the outer characters match *and* the inner substring is a palindrome.

For `s = "babad"`, fill the table by increasing substring length. The interesting cells:

- `dp[0][2]`: `s[0] = 'b'`, `s[2] = 'b'` match, inner `dp[1][1]` is true → `dp[0][2] = true` → substring `"bab"`.
- `dp[1][3]`: `s[1] = 'a'`, `s[3] = 'a'` match, inner `dp[2][2]` is true → `dp[1][3] = true` → substring `"aba"`.
- `dp[0][4]`: `s[0] = 'b'`, `s[4] = 'd'` differ → false.
- `dp[0][3]`: `s[0] = 'b'`, `s[3] = 'a'` differ → false.

The longest `true` cells are length 3 (`"bab"` and `"aba"`), matching the expand-around-center result. DP costs O(n^2) time and O(n^2) space, so prefer expand-around-center unless you need the whole table.

## Expand-around-center in C#

```csharp
public static string LongestPalindrome(string s)
{
    if (s.Length < 2) return s;

    int start = 0, maxLen = 1;

    for (int center = 0; center < s.Length; center++)
    {
        Expand(s, center, center);       // odd-length palindromes
        Expand(s, center, center + 1);   // even-length palindromes
    }
    return s.Substring(start, maxLen);

    void Expand(string str, int left, int right)
    {
        while (left >= 0 && right < str.Length && str[left] == str[right])
        {
            if (right - left + 1 > maxLen)
            {
                start = left;
                maxLen = right - left + 1;
            }
            left--;
            right++;
        }
    }
}
```

Each expansion runs in O(n) worst case and there are `2n - 1` centers, so total time is O(n^2) with O(1) extra space. (Manacher's algorithm reaches O(n) time, but it is considerably more intricate — learn this version first.)

## Key Takeaways

- A string of length `n` has `2n - 1` palindrome centers: `n` odd and `n - 1` even.
- "babad" has longest palindromic substring "bab" (length 3); "aba" is an equally valid answer.
- Expand-around-center is O(n^2) time and O(1) space — the practical default.
- The DP recurrence is `dp[i][j] = s[i] == s[j] && (j - i <= 2 || dp[i+1][j-1])`, at O(n^2) space.
- Expand from every center, track the longest span seen, and return that substring.
