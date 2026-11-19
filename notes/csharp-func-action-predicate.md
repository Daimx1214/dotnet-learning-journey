# Func<T>, Action<T>, and Predicate<T>

`Func<T>`, `Action<T>`, and `Predicate<T>` are the built-in generic delegate types. Instead of declaring a custom delegate for every callback, you reuse these: `Func` returns a value, `Action` returns nothing, and `Predicate` returns a `bool` that answers a yes/no question about its input.

## The Three Delegates

`Func<T, TResult>` takes up to 16 inputs and returns a result; the last type argument is always the return type. `Action<T>` takes inputs and returns `void`. `Predicate<T>` takes one input and returns `bool`, and is the delegate behind `List<T>.Find` and `Array.Find`.

```csharp
Func<int, int> square = x => x * x;
Console.WriteLine(square(7)); // 49

Action<string> greet = name => Console.WriteLine($"Hello, {name}!");
greet("Daim"); // Hello, Daim!

Predicate<int> isEven = n => n % 2 == 0;
Console.WriteLine(isEven(4)); // True
Console.WriteLine(isEven(5)); // False
```

## Method-Group Conversion

A method whose signature matches a delegate can be assigned directly, without a lambda — this is method-group conversion. It keeps code short when a named method already does the job.

```csharp
static bool IsLong(string s) => s.Length > 5;
static int Double(int n) => n * 2;

Predicate<string> longCheck = IsLong;   // method group, no lambda needed
Func<int, int> doubler = Double;

Console.WriteLine(longCheck("hello world")); // True
Console.WriteLine(doubler(21));             // 42
```

## A LINQ-Style Filtering Example

LINQ's `Where` takes a `Func<T, bool>` — a predicate in `Func` clothing. Writing your own filter with the same shape shows how the pattern works: the caller supplies the test, the method supplies the loop.

```csharp
static IEnumerable<T> Filter<T>(IEnumerable<T> source, Func<T, bool> test)
{
    foreach (T item in source)
    {
        if (test(item))
        {
            yield return item;
        }
    }
}

int[] numbers = { 1, 2, 3, 4, 5, 6 };
int[] evens = Filter(numbers, n => n % 2 == 0).ToArray();
Console.WriteLine(string.Join(", ", evens)); // 2, 4, 6

string[] words = { "cat", "elephant", "dog", "giraffe" };
string[] longWords = Filter(words, w => w.Length > 4).ToArray();
Console.WriteLine(string.Join(", ", longWords)); // elephant, giraffe
```

## Key Takeaways

- `Func<T, TResult>` returns a value (last type argument is the return type); `Action<T>` returns `void`.
- `Predicate<T>` returns `bool` and is the classic test delegate behind `List<T>.Find` and friends.
- A matching named method can be assigned directly to a delegate — method-group conversion, no lambda needed.
- LINQ methods like `Where` are built on `Func<T, bool>`; supplying the test and letting the method loop is the reusable pattern.
- Reach for these built-in delegates before declaring a custom delegate type — they are the shared vocabulary of C# callbacks.
