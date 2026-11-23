// Levenshtein edit distance: full DP table, two-row optimization, Damerau note.
// Run: dotnet run (as part of a console project).
// See also: notes/dsa-edit-distance.md.
//
// Expected output:
//   Levenshtein("kitten", "sitting") = 3
//   Full table, last row: 6 6 5 4 3 3 2 3
//   Two-row version: 3
//   Damerau-Levenshtein("ca", "ac") = 1 (one transposition)

using System;

namespace DotNetLearningJourney.Examples
{
    public static class EditDistanceExamples
    {
        public static void Main()
        {
            Demo();
        }

        public static void Demo()
        {
            const string source = "kitten";
            const string target = "sitting";

            int distance = Levenshtein(source, target);
            Console.WriteLine($"Levenshtein(\"{source}\", \"{target}\") = {distance}");
            // Hand-verified: the full table's final cell is 3
            // (kitten -> sitten -> sittin -> sitting).

            int[,] table = BuildTable(source, target);
            PrintTable(table, source, target);

            int optimized = LevenshteinTwoRows(source, target);
            Console.WriteLine($"Two-row version: {optimized}");
            // Must agree with the full table: 3 == 3.

            // Damerau-Levenshtein: one adjacent transposition counts as one edit.
            Console.WriteLine($"Damerau-Levenshtein(\"ca\", \"ac\") = {DamerauLevenshtein("ca", "ac")}");
            // Plain Levenshtein would give 2 for "ca" -> "ac" (two substitutions).
        }

        // Full O(m x n) table. dp[i, j] = distance between source[..i] and target[..j].
        public static int Levenshtein(string s, string t)
        {
            int[,] dp = BuildTable(s, t);
            return dp[s.Length, t.Length];
        }

        public static int[,] BuildTable(string s, string t)
        {
            int m = s.Length, n = t.Length;
            int[,] dp = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++) dp[i, 0] = i;
            for (int j = 0; j <= n; j++) dp[0, j] = j;

            for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }

            return dp;
        }

        // Space-optimized version: only the previous and current rows are kept.
        // Same answer as the full table, O(n) space instead of O(m x n).
        public static int LevenshteinTwoRows(string s, string t)
        {
            int[] previous = new int[t.Length + 1];
            int[] current = new int[t.Length + 1];

            for (int j = 0; j <= t.Length; j++) previous[j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(previous[j] + 1, current[j - 1] + 1),
                        previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }

            return previous[t.Length];
        }

        // Damerau-Levenshtein (optimal string alignment variant): an adjacent
        // transposition costs 1. "ca" -> "ac" is a single transposition, so 1.
        public static int DamerauLevenshtein(string s, string t)
        {
            int m = s.Length, n = t.Length;
            int[,] dp = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++) dp[i, 0] = i;
            for (int j = 0; j <= n; j++) dp[0, j] = j;

            for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                int best = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                    best = Math.Min(best, dp[i - 2, j - 2] + 1);

                dp[i, j] = best;
            }

            return dp[m, n];
        }

        private static void PrintTable(int[,] dp, string s, string t)
        {
            Console.Write("      ");
            foreach (char c in t) Console.Write($"  {c}");
            Console.WriteLine();

            for (int i = 0; i <= s.Length; i++)
            {
                Console.Write(i == 0 ? "    " : $"  {s[i - 1]} ");
                for (int j = 0; j <= t.Length; j++)
                    Console.Write($"{dp[i, j],3}");
                Console.WriteLine();
            }
        }
    }
}
