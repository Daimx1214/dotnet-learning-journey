# DateTime, TimeZone Handling in C#

.NET has five types for dates and times, and choosing the wrong one is the most common source of time bugs. The rule that prevents most of them: store UTC, convert to local only for display.

## The Five Types

| Type | Stores | Use for |
|---|---|---|
| `DateTime` | Date + time, with a `Kind` flag | Legacy code; local times tied to one machine |
| `DateTimeOffset` | Date + time + UTC offset | Timestamps, anything stored or sent across machines |
| `DateOnly` | Date only | Birthdays, holidays, calendar dates |
| `TimeOnly` | Time of day only | Opening hours, daily schedules |
| `TimeSpan` | A duration | Differences, timeouts, intervals |

`DateTime` and `DateTimeOffset` both hold a date and a time. The difference is that `DateTimeOffset` also records the offset from UTC, so the same instant is unambiguous no matter where the code runs. A `DateTime` with `Kind = Local` means something different on a server in Karachi than on a laptop in London.

## The UTC Storage Rule

Store every timestamp as UTC, and convert to a local time zone only at the moment you show it to a user:

```csharp
// Storing: always UTC
DateTimeOffset createdAt = DateTimeOffset.UtcNow;

// Displaying: convert to the user's zone at the last moment
TimeZoneInfo userZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
DateTimeOffset local = TimeZoneInfo.ConvertTime(createdAt, userZone);
Console.WriteLine(local); // e.g. 2026-11-28 19:47:00 +05:00
```

`DateTime.UtcNow` is cheaper than `DateTime.Now` because it skips the time-zone lookup. In a web API, never return `DateTime.Now` to a client; the client cannot know which machine's "now" it was.

## Why DateTimeKind Matters

`DateTime.Kind` is `Unspecified`, `Utc`, or `Local`, and it changes how conversions behave:

```csharp
var unspecified = new DateTime(2026, 11, 28, 19, 47, 0); // Kind = Unspecified
var utc = new DateTime(2026, 11, 28, 14, 47, 0, DateTimeKind.Utc);

Console.WriteLine(unspecified.Kind); // Unspecified
Console.WriteLine(utc.ToUniversalTime()); // 2026-11-28 14:47:00 (unchanged)

// Danger: treating an unspecified value as local shifts it by the machine offset
DateTime converted = unspecified.ToUniversalTime(); // assumes local time: 14:47 UTC on a +05:00 machine
```

If you read a `datetime` column from a database into a `DateTime`, its `Kind` is `Unspecified`, and any call to `ToUniversalTime()` or `ToLocalTime()` silently assumes it is local time. That assumption is wrong whenever the stored value was UTC. `DateTime.SpecifyKind` fixes the flag without changing the value:

```csharp
DateTime fromDb = reader.GetDateTime(0);
DateTime asUtc = DateTime.SpecifyKind(fromDb, DateTimeKind.Utc);
```

`DateTimeOffset` avoids this whole class of bug, because the offset travels with the value.

## TimeZoneInfo Conversion

`TimeZoneInfo` is the conversion engine. Use IANA or Windows zone IDs depending on the platform (`"Pakistan Standard Time"` on Windows, `"Asia/Karachi"` on Linux/macOS; .NET 6+ maps between them with `TimeZoneInfo.TryConvertIanaIdToWindowsId`).

```csharp
TimeZoneInfo karachi = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

DateTimeOffset instant = new DateTimeOffset(2026, 11, 28, 14, 47, 0, TimeSpan.Zero);
Console.WriteLine(TimeZoneInfo.ConvertTime(instant, karachi)); // 19:47 +05:00
Console.WriteLine(TimeZoneInfo.ConvertTime(instant, london));  // 14:47 +00:00
```

The same instant, two displays. Keep the instant (`DateTimeOffset`) as the source of truth and derive the displays.

## Daylight Saving Pitfalls

Daylight saving creates two traps:

1. **Ambiguous times:** when clocks go back, one local time occurs twice (e.g. 01:30 happens once in daylight time and once in standard time). `TimeZoneInfo.IsAmbiguousTime` detects this, and `GetAmbiguousTimeOffsets` returns both possible offsets. A `DateTimeOffset` disambiguates because it carries its offset.
2. **Invalid times:** when clocks go forward, one local time never occurs (e.g. 02:30 is skipped). `TimeZoneInfo.IsInvalidTime` detects it. Converting such a time throws or silently shifts, depending on the API, so validate user-entered times before converting.

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
var sketchy = new DateTime(2026, 3, 8, 2, 30, 0);
Console.WriteLine(zone.IsInvalidTime(sketchy)); // True: clocks jump 02:00 -> 03:00
```

Pakistan does not observe daylight saving, so these traps do not arise for `Asia/Karachi`, but any app with users in DST zones must handle them.

## DateOnly and TimeOnly

For values that are not instants, do not use `DateTime` at all. A birthday is a date, not a moment, and storing it as `DateTime` invites accidental time-zone shifts:

```csharp
DateOnly birthday = new DateOnly(2004, 3, 15);
TimeOnly opensAt = new TimeOnly(9, 0);
TimeOnly closesAt = new TimeOnly(17, 30);

bool isOpen = TimeOnly.FromDateTime(DateTime.Now) is >= opensAt and <= closesAt;
```

`DateOnly` supports arithmetic (`AddDays`, `DayOfWeek`) and comparisons without any time component getting in the way.

## Key Takeaways

- Store timestamps as UTC (`DateTimeOffset.UtcNow`); convert to local time only for display.
- Prefer `DateTimeOffset` over `DateTime` for anything stored, sent, or compared across machines.
- `DateTime.Kind` changes conversion behaviour; database values arrive as `Unspecified`, so fix the kind or use `DateTimeOffset`.
- Use `TimeZoneInfo.ConvertTime` for display conversions, and check `IsAmbiguousTime` / `IsInvalidTime` around daylight saving transitions.
- Use `DateOnly` for calendar dates and `TimeOnly` for times of day that are not instants.
