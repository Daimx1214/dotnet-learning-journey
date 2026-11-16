// GreedyExamples.cs
// A greedy algorithm makes the locally best choice at each step and never
// revisits it. That works only when the problem has the greedy-choice property:
// a locally optimal choice can always be extended to a globally optimal solution.
// Activity selection and fractional knapsack have it; 0/1 knapsack does not.
// Run with: dotnet run

Console.WriteLine("=== Activity Selection ===");
var activities = new List<(string Name, int Start, int End)>
{
    ("A", 1, 4), ("B", 3, 5), ("C", 0, 6), ("D", 5, 7),
    ("E", 3, 9), ("F", 5, 9), ("G", 6, 10), ("H", 8, 11),
    ("I", 8, 12), ("J", 2, 14), ("K", 12, 16)
};

List<(string Name, int Start, int End)> selected = SelectActivities(activities);
Console.WriteLine("Maximum non-overlapping activities: " + selected.Count);
foreach (var a in selected)
    Console.WriteLine($"  {a.Name}: {a.Start}:00 - {a.End}:00");
Console.WriteLine();

Console.WriteLine("=== Greedy Coin Change ===");
int[] coins = { 25, 10, 5, 1 };
int amount = 63;
Dictionary<int, int> change = MakeChange(coins, amount);
Console.WriteLine($"Change for {amount} cents (US coins):");
foreach (var kv in change)
    Console.WriteLine($"  {kv.Value} x {kv.Key}c");
Console.WriteLine($"  Total coins: {change.Values.Sum()}");
Console.WriteLine();

Console.WriteLine("=== Fractional Knapsack ===");
var items = new List<(string Name, double Value, double Weight)>
{
    ("Gold bar",    60, 10),
    ("Silver coin", 100, 20),
    ("Gemstone",    120, 30),
};
double capacity = 50;
double maxValue = FractionalKnapsack(items, capacity, out var taken);
Console.WriteLine($"Knapsack capacity: {capacity}kg, max value: {maxValue}");
foreach (var t in taken)
    Console.WriteLine($"  {t.Name}: {t.Fraction:P0} taken ({t.ValueTaken} value)");

// Activity selection: sort by finish time, then take every activity that
// starts after the last selected one finishes. Earliest finish leaves the
// most room for the rest — the greedy choice is provably optimal here.
static List<(string Name, int Start, int End)> SelectActivities(
    List<(string Name, int Start, int End)> activities)
{
    var sorted = activities.OrderBy(a => a.End).ToList();
    var selected = new List<(string Name, int Start, int End)>();

    int lastEnd = int.MinValue;
    foreach (var a in sorted)
    {
        if (a.Start >= lastEnd)
        {
            selected.Add(a);
            lastEnd = a.End;
        }
    }
    return selected;
}

// Coin change: take as many of the largest denomination as possible, then
// move to the next. Optimal for canonical coin systems like US coins,
// where each coin is a multiple of the smaller ones — but NOT in general:
// with coins {1, 3, 4} and amount 6, greedy gives 4+1+1 (3 coins) while
// 3+3 (2 coins) is optimal.
static Dictionary<int, int> MakeChange(int[] coins, int amount)
{
    var result = new Dictionary<int, int>();
    int remaining = amount;

    foreach (int coin in coins.OrderByDescending(c => c))
    {
        int count = remaining / coin;
        if (count > 0)
        {
            result[coin] = count;
            remaining -= count * coin;
        }
    }
    return result;
}

// Fractional knapsack: items may be split, so sort by value per unit weight
// and take as much of the best ratio as fits. O(n log n) for the sort.
// Contrast with 0/1 knapsack, where greedy by ratio FAILS and DP is needed.
static double FractionalKnapsack(
    List<(string Name, double Value, double Weight)> items,
    double capacity,
    out List<(string Name, double Fraction, double ValueTaken)> taken)
{
    taken = new List<(string, double, double)>();
    double totalValue = 0;
    double remaining = capacity;

    var byRatio = items.OrderByDescending(i => i.Value / i.Weight).ToList();
    foreach (var item in byRatio)
    {
        if (remaining <= 0)
            break;

        double fraction = Math.Min(1, remaining / item.Weight);
        double valueTaken = fraction * item.Value;
        totalValue += valueTaken;
        remaining -= fraction * item.Weight;
        taken.Add((item.Name, fraction, valueTaken));
    }
    return totalValue;
}
