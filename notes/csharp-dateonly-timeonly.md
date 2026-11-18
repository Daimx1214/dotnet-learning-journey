# DateOnly and TimeOnly in C#

`DateOnly` and `TimeOnly` arrived in .NET 6 to fix a long-standing annoyance: `DateTime` always carries both a date and a time, even when you only need one of them. A birthday is a date, not a moment in time — storing it as `DateTime` forces a meaningless midnight time component into your data, and a shop's opening time is a time of day, not a point on a calendar. `DateOnly` holds just a calendar date; `TimeOnly` holds just a time of day. Both are value types (structs), just like `DateTime`.

They matter in .NET backends because they make intent explicit in your models and your database: a `date` column instead of a `datetime`, a `time` column instead of a `datetime`, no more midnight-time bugs, and no more timezone confusion for values that never had a timezone in the first place.

## What they replace

Before .NET 6, the only options were `DateTime` (with a dummy time) or a `string` (with parsing everywhere). Both were error-prone: a `DateTime` birthday of `1998-05-12 00:00:00` could silently shift a day when converted across timezones, and comparing two dates required remembering to use `.Date` first.

```csharp
// Before: a birthday with a meaningless time component.
DateTime birthday = new DateTime(1998, 5, 12);      // 1998-05-12 00:00:00
DateTime today = DateTime.Today;
int age = today.Year - birthday.Year;                // easy to get wrong

// After: the type itself says "this is only a date".
DateOnly birthDate = new DateOnly(1998, 5, 12);
DateOnly todayDate = DateOnly.FromDateTime(DateTime.Today);
int ageYears = todayDate.Year - birthDate.Year;

// A time of day with no date attached at all.
TimeOnly shopOpens = new TimeOnly(9, 0);            // 09:00
TimeOnly shopCloses = new TimeOnly(17, 30);         // 17:30
bool isOpen = TimeOnly.FromDateTime(DateTime.Now) >= shopOpens
           && TimeOnly.FromDateTime(DateTime.Now) < shopCloses;
```

## Arithmetic

Both types support the arithmetic you would expect, and they deliberately do not support the operations that would be meaningless for them.

```csharp
DateOnly start = new DateOnly(2026, 11, 18);
DateOnly end = start.AddDays(14);          // 2026-12-02
DateOnly nextMonth = start.AddMonths(1);   // 2026-12-18
int daysBetween = end.DayNumber - start.DayNumber;  // 14
int dayOfYear = start.DayOfYear;           // 322
DayOfWeek weekday = start.DayOfWeek;       // Wednesday

TimeOnly lunch = new TimeOnly(12, 30);
TimeOnly later = lunch.AddHours(2);        // 14:30
TimeOnly plusMinutes = lunch.AddMinutes(45); // 13:15
TimeSpan untilClose = new TimeOnly(17, 30) - lunch;  // 05:00:00

// Combining them when you genuinely need a moment in time:
DateTime appointment = start.ToDateTime(new TimeOnly(10, 30));
// 2026-11-18 10:30:00 (Unspecified kind — no timezone implied)
```

Note what is missing on purpose: `DateOnly` has no `AddHours` and no timezone conversions, because a bare date has no hours and no timezone. If you find yourself needing those, you probably wanted a `DateTime` or a `DateTimeOffset` instead.

## Parsing and formatting

Parsing follows the same patterns as `DateTime`, with the same culture sensitivity — always pass an explicit format or culture for user input.

```csharp
// Parsing: TryParse avoids exceptions on bad input.
bool ok = DateOnly.TryParse("2026-11-18", out DateOnly parsed);
// Exact format — use this for API input and stored values.
DateOnly exact = DateOnly.ParseExact("18/11/2026", "dd/MM/yyyy",
    System.Globalization.CultureInfo.InvariantCulture);

// Formatting: same custom format strings as DateTime.
string iso = new DateOnly(2026, 11, 18).ToString("yyyy-MM-dd");  // "2026-11-18"
string friendly = new TimeOnly(14, 30).ToString("hh:mm tt");     // "02:30 PM"

// Round-tripping through strings is safe with the ISO formats above.
```

## JSON serialization

`System.Text.Json` supports both types out of the box in .NET 7 and later, serializing `DateOnly` as `"2026-11-18"` and `TimeOnly` as `"14:30:00"`. In .NET 6 you need custom converters — if your API targets .NET 6, add them explicitly or the serializer throws.

```csharp
// .NET 7+ — works with no extra code.
public record EventDto(DateOnly Date, TimeOnly StartTime, string Title);

string json = JsonSerializer.Serialize(
    new EventDto(new DateOnly(2026, 11, 18), new TimeOnly(10, 30), "Demo"));
// {"Date":"2026-11-18","StartTime":"10:30:00","Title":"Demo"}

EventDto back = JsonSerializer.Deserialize<EventDto>(json)!;
```

If your API is consumed by JavaScript clients, note that a `DateOnly` serializes to a plain date string, not a JavaScript `Date` — clients should treat it as a date string, not parse it into a `Date` object (which would reintroduce a timezone shift).

## EF Core mapping

EF Core 7 and later map `DateOnly` to SQL Server's `date` type and `TimeOnly` to `time` automatically — no value converter needed. In EF Core 6 you must add a value converter yourself.

```csharp
public class Booking
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }        // -> date column
    public TimeOnly StartTime { get; set; }   // -> time column
    public string CustomerName { get; set; } = string.Empty;
}

// EF Core 6 only — explicit converters (not needed in EF Core 7+):
// modelBuilder.Entity<Booking>()
//     .Property(b => b.Date)
//     .HasConversion(
//         d => d.ToDateTime(TimeOnly.MinValue),
//         d => DateOnly.FromDateTime(d));
```

Querying works with LINQ too: `context.Bookings.Where(b => b.Date == someDate)` translates to a comparison on the `date` column, and comparisons on `TimeOnly` translate to the `time` column.

## When to use them over DateTime

- **Use `DateOnly`** for birthdays, holidays, due dates, invoice dates, and any calendar date where the time of day is meaningless.
- **Use `TimeOnly`** for opening hours, daily schedules, alarm times, and recurring times of day.
- **Keep using `DateTime` / `DateTimeOffset`** when the value is a real moment in time — a created timestamp, a log entry, a meeting scheduled across timezones. A meeting at "2026-11-18 10:30" in a specific timezone is a moment, not a bare date plus a bare time.
- **Never use them to dodge timezone problems.** If a value represents an instant, store it as `DateTimeOffset` (or UTC `DateTime`); splitting it into `DateOnly` + `TimeOnly` loses the timezone information you will need later.

## Key Takeaways

- `DateOnly` and `TimeOnly` (.NET 6+) represent a calendar date and a time of day with no timezone and no dummy component, replacing the old habit of using `DateTime` at midnight.
- Both are structs with sensible arithmetic: `AddDays` / `AddMonths` / `DayNumber` on dates, `AddHours` / subtraction returning `TimeSpan` on times — and deliberately no timezone conversions.
- Parse with `ParseExact` and an explicit culture for user input; format with ISO strings (`yyyy-MM-dd`) for storage and APIs.
- `System.Text.Json` supports both natively in .NET 7+; .NET 6 needs custom converters.
- EF Core 7+ maps them automatically to SQL Server `date` and `time` columns; EF Core 6 needs a value converter.
- Use them for dates and times of day that are not moments in time; use `DateTimeOffset` (or UTC `DateTime`) for real instants.
