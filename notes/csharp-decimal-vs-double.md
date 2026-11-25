# decimal vs double vs float

C# offers three floating point types, and picking the wrong one is a classic source of subtle bugs. `float` and `double` store numbers in binary; `decimal` stores them in base 10. That single difference decides almost everything about when to use each.

## How They Store Numbers

`float` is a 32-bit IEEE 754 binary floating point number, with about 7 significant decimal digits of precision. `double` is the 64-bit version, with about 15-17 significant digits. Both represent a value as a binary fraction times a power of two, so many ordinary decimal numbers cannot be represented exactly.

`decimal` is a 128-bit base-10 floating point number with 28-29 significant digits. It stores a value as an integer mantissa times a power of ten, so decimal fractions like 0.1 are exact.

```csharp
float f = 0.1f;
double d = 0.1;
decimal m = 0.1m;

Console.WriteLine(f); // 0.1 (displayed rounded, stored approximately)
Console.WriteLine(d); // 0.1 (displayed rounded, stored approximately)
Console.WriteLine(m); // 0.1 (stored exactly)
```

## The Classic Example: 0.1 + 0.2

```csharp
double a = 0.1 + 0.2;
Console.WriteLine(a);            // 0.30000000000000004
Console.WriteLine(a == 0.3);     // False

decimal b = 0.1m + 0.2m;
Console.WriteLine(b);            // 0.3
Console.WriteLine(b == 0.3m);    // True

float c = 0.1f + 0.2f;
Console.WriteLine(c);            // 0.3 (rounded on display)
Console.WriteLine(c == 0.3f);    // True here, but do not rely on it
```

Neither 0.1 nor 0.2 has an exact binary representation, so `double` adds two slightly-off values and gets a slightly-off result. The error is tiny, about 1 part in 10^16, but it breaks equality checks and accumulates over many operations. `float` hides the error more often because it prints fewer digits, which makes it more dangerous, not safer.

## When to Use Each

Use `decimal` for money and anything where a human expects exact decimal arithmetic:

```csharp
decimal price = 19.99m;
decimal taxRate = 0.07m;
decimal total = price * (1 + taxRate); // 21.3893m, exact to the cent when rounded
decimal rounded = Math.Round(total, 2); // 21.39
```

Use `double` for scientific and engineering work, graphics, physics, and statistics, where values are measurements with inherent error and speed matters more than exact decimal representation. `Math.Sin`, `Math.Sqrt` and the rest of `System.Math` work on `double`.

Use `float` when memory or bandwidth dominates: large arrays of sensor data, GPU buffers, machine learning tensors, or game coordinates. A `float` array is half the size of a `double` array, and GPUs are built for 32-bit floats.

## Precision and Range

| Type | Size | Significant digits | Approximate range |
|------|------|--------------------|-------------------|
| `float` | 4 bytes | 7 | 1.5 x 10^-45 to 3.4 x 10^38 |
| `double` | 8 bytes | 15-17 | 5.0 x 10^-324 to 1.7 x 10^308 |
| `decimal` | 16 bytes | 28-29 | 1.0 x 10^-28 to 7.9 x 10^28 |

`decimal` has far more precision in the range humans care about, but a much smaller range than `double`. It cannot represent extremely large or extremely small magnitudes, and it has no infinity or NaN.

## Performance Note

`double` arithmetic is done in hardware by the CPU's floating point unit, so it is very fast. `decimal` arithmetic is done in software by the runtime, and it is typically 10 to 20 times slower than `double` for the same operation. That gap rarely matters for a checkout total computed once per order, but it rules `decimal` out for tight numerical loops over millions of values. `float` can be faster than `double` in vectorized (SIMD) code because twice as many values fit in one register, which is why it dominates ML and graphics.

## Comparing Doubles Safely

Never compare `double` values with `==` after arithmetic. Compare with a tolerance instead:

```csharp
double result = 0.1 + 0.2;
double expected = 0.3;
double tolerance = 1e-9;

bool closeEnough = Math.Abs(result - expected) < tolerance; // True
```

For money, avoid the problem entirely by using `decimal` and rounding explicitly at the point where a value becomes a charge.

## Key Takeaways

- `float` and `double` are binary floating point: 0.1 cannot be stored exactly, so 0.1 + 0.2 is 0.30000000000000004 in `double`.
- `decimal` is base-10 floating point with 28-29 digits: 0.1m + 0.2m is exactly 0.3m, which is why money belongs in `decimal`.
- Use `decimal` for money and exact decimal math, `double` for science and graphics, `float` for large arrays and GPU work.
- `decimal` is 10 to 20 times slower than `double` because it is computed in software, not hardware.
- Never test `double` equality with `==` after arithmetic; use a tolerance, or switch to `decimal` when exactness matters.
