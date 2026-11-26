# Template Method Pattern

The Template Method pattern defines the skeleton of an algorithm in a base class and lets subclasses override specific steps without changing the overall structure. The fixed sequence lives in one place, and each variation only fills in the parts that differ. It is the pattern behind most "override this method to customize" framework designs in .NET.

## The Problem It Solves

Three data-import jobs all follow the same steps: read the source, validate the rows, transform them, save them, and report the result. Copying the whole sequence into three classes means a change to the order (say, validating before transforming) must be made three times. A template method holds the sequence once, and each job only overrides the steps that are specific to its source.

## Example: Data-Import Pipeline

The abstract base class owns the skeleton. The template method itself is not virtual, so subclasses cannot reorder the steps:

```csharp
public abstract class DataImporter
{
    // The template method: the fixed skeleton of the algorithm.
    public void Import()
    {
        var raw = ReadSource();
        var rows = Parse(raw);
        Validate(rows);
        var records = Transform(rows);
        Save(records);
        OnCompleted(records.Count);
    }

    protected abstract string ReadSource();
    protected abstract List<string> Parse(string raw);
    protected abstract List<Record> Transform(List<string> rows);

    protected virtual void Validate(List<string> rows)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("No rows to import.");
    }

    protected virtual void Save(List<Record> records)
    {
        Console.WriteLine($"Saving {records.Count} records.");
    }

    // A hook: a method with a default no-op body that subclasses may override.
    protected virtual void OnCompleted(int count)
    {
    }
}

public sealed record Record(string Name, int Age);
```

One concrete importer fills in only the steps that differ:

```csharp
public sealed class CsvCustomerImporter : DataImporter
{
    private readonly string _filePath;

    public CsvCustomerImporter(string filePath) => _filePath = filePath;

    protected override string ReadSource() => File.ReadAllText(_filePath);

    protected override List<string> Parse(string raw)
        => raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    protected override List<Record> Transform(List<string> rows)
    {
        return rows.Select(row =>
        {
            var parts = row.Split(',');
            return new Record(parts[0].Trim(), int.Parse(parts[1].Trim()));
        }).ToList();
    }

    protected override void OnCompleted(int count)
    {
        Console.WriteLine($"Imported {count} customers from CSV.");
    }
}
```

A second importer, for JSON or an API, reuses the same skeleton and only overrides `ReadSource`, `Parse`, and `Transform`. The order of operations is identical, and the validation rule is shared unless a subclass chooses to override it.

## Abstract Steps vs Hooks

- **Abstract steps** must be implemented by every subclass. Use them for the parts that genuinely differ per variation, like reading a CSV file versus calling an API.
- **Virtual steps with a default body** can be used as-is or overridden. Use them for steps that are usually the same, like the validation rule above.
- **Hooks** are virtual methods with an empty default body, called at a specific point in the skeleton. Subclasses may override them to react, like `OnCompleted`, or to influence the flow. A hook that returns a `bool` can let a subclass skip an optional step.

## Template Method vs Strategy

- **Template Method** uses inheritance: the skeleton is fixed in the base class, and subclasses vary the steps. Best when the variations are known at design time and the structure must not change.
- **Strategy** uses composition: the whole algorithm is swapped at runtime. Best when the algorithm itself is chosen by the caller.

## Where .NET Uses It

- ASP.NET Core middleware and filters follow the same idea: the framework defines the pipeline order, and you fill in the step.
- `BackgroundService` defines the hosting skeleton, and you override `ExecuteAsync` to supply the work.
- Test frameworks define setup, test, and teardown order, and each test fills in the body.

## Key Takeaways

- Template Method fixes the algorithm's skeleton in a base class and lets subclasses override individual steps.
- Make the template method non-virtual so subclasses cannot reorder or skip steps.
- Abstract methods are mandatory steps; virtual methods with defaults are optional overrides; empty virtual methods are hooks.
- Use it when several variations share the same sequence and differ only in a few steps.
- Prefer Strategy (composition) when the algorithm itself must be swappable at runtime.
