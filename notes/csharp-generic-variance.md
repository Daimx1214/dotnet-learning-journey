# Covariance and Contravariance in C# Generics

Variance answers one question: when `Cat` derives from `Animal`, can `IEnumerable<Cat>` be used where `IEnumerable<Animal>` is expected? In C#, the answer depends on the interface or delegate declaration: `out` marks a type parameter as covariant (usable in output positions), `in` marks it as contravariant (usable in input positions), and no marker means the parameter is invariant.

## The out Keyword: Covariance

A covariant type parameter may only appear in output positions — return types and read-only properties. `IEnumerable<out T>` is the canonical example: it only ever hands `T` values out, never takes them in.

```csharp
IEnumerable<Cat> cats = new List<Cat> { new Cat() };
IEnumerable<Animal> animals = cats; // Legal: IEnumerable<out T> is covariant.
foreach (Animal a in animals)
{
    Console.WriteLine(a.Name);
}

// The same assignment also works through a method parameter:
void PrintAll(IEnumerable<Animal> items)
{
    foreach (Animal item in items)
    {
        Console.WriteLine(item.Name);
    }
}
PrintAll(cats); // Legal for the same reason.
```

This is safe because every `Cat` in the sequence is an `Animal`; the consumer can only read `Animal` values out and can never insert a `Dog` into what is really a list of cats.

## The in Keyword: Contravariance

A contravariant type parameter may only appear in input positions — method parameters. `IComparer<in T>` is the canonical example: it only ever takes `T` values in, never hands them out.

```csharp
// An animal comparer can compare any two animals, so it can compare two cats.
IComparer<Animal> animalComparer = Comparer<Animal>.Create(
    (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
IComparer<Cat> catComparer = animalComparer; // Legal: IComparer<in T> is contravariant.

var cats = new List<Cat> { new Cat { Name = "Mimi" }, new Cat { Name = "Bella" } };
cats.Sort(catComparer);
Console.WriteLine(cats[0].Name); // "Bella" — sorted by name, B before M.
```

This is safe because the comparer only receives `Cat` values, and every `Cat` is an `Animal` the comparer knows how to handle.

## Func and Action Variance

The built-in delegates are declared with variance: `Func<out TResult>` is covariant in its result, and `Func<in T, out TResult>` and `Action<in T>` are contravariant in their inputs.

```csharp
Func<Cat> getCat = () => new Cat();
Func<Animal> getAnimal = getCat; // Legal: Func is covariant in its return type.

Action<Animal> feedAnimal = a => Console.WriteLine($"Feeding {a.Name}");
Action<Cat> feedCat = feedAnimal; // Legal: Action is contravariant in its parameter.
feedCat(new Cat { Name = "Mimi" }); // Prints "Feeding Mimi".
```

Read the `Action` assignment right to left: an action that can feed any `Animal` can certainly feed a `Cat`, so it is usable as an `Action<Cat>`.

## Where Variance Is Illegal and Why

`List<T>` is invariant: `List<Cat>` cannot be assigned to `List<Animal>`, and the language forbids declaring it otherwise. The reason is that `List<T>` uses `T` in both directions — `Add(T)` takes `T` in and the indexer returns `T` out. If the assignment were allowed, this would compile and corrupt the list:

```csharp
var cats = new List<Cat>();
// List<Animal> animals = cats; // Does NOT compile: List<T> is invariant.
List<Animal> animals = new List<Animal>();
animals.Add(new Dog()); // Legal on a real List<Animal>.

// If the line above had compiled, this would put a Dog into a List<Cat>:
// animals.Add(new Dog()); // cats would then contain a Dog — a type-safety hole.
```

The same rule applies to any type parameter used in both input and output positions, to value types (variance only applies to reference types — `IEnumerable<int>` cannot convert to `IEnumerable<object>` by variance, because that would require boxing each element), and to classes: only interfaces and delegates can declare `in`/`out`. A method that takes `T` as a parameter and returns `T` cannot be variant in `T` for the same reason `List<T>` cannot.

```csharp
// Also illegal: IEnumerable<int> is not convertible to IEnumerable<object>.
// IEnumerable<object> objects = new List<int> { 1, 2, 3 }; // Does NOT compile.
IEnumerable<object> objects = new List<string> { "a", "b" }; // Legal: string is a reference type.
```

## Key Takeaways

- `out` (covariance) allows `IEnumerable<Cat>` where `IEnumerable<Animal>` is expected, because `T` only flows out.
- `in` (contravariance) allows `IComparer<Animal>` where `IComparer<Cat>` is expected, because `T` only flows in.
- `Func` is covariant in its return type and contravariant in its parameters; `Action` is contravariant in its parameters.
- `List<T>` is invariant because it uses `T` in both directions; allowing the conversion would let a `Dog` be added to a `List<Cat>`.
- Variance applies only to interfaces and delegates, and only for reference types — value types such as `int` do not participate.
