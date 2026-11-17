# HTTP Resilience with Polly (ASP.NET Core)

Outgoing HTTP calls fail in ways your code cannot control: the downstream service is briefly down, a load balancer drops a connection, a slow dependency holds a request for 30 seconds. `Microsoft.Extensions.Http.Resilience` — the .NET 8 integration of Polly v8 — adds resilience policies to `HttpClient` declaratively: retries with backoff, circuit breakers, timeouts and hedging, wired up in `Program.cs` with no `try/catch` retry loops in your services.

## The Standard Resilience Handler

One line gives an `HttpClient` the standard set of policies: total timeout, retries with exponential backoff, and a circuit breaker. This is the right default for most clients calling a reasonably reliable API.

```csharp
// Program.cs
builder.Services.AddHttpClient<WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.example/");
})
.AddStandardResilienceHandler();

public sealed class WeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http) => _http = http;

    public async Task<string> GetForecastAsync(string city)
    {
        // Retries, circuit breaker and timeouts are applied automatically.
        HttpResponseMessage response = await _http.GetAsync($"/forecast/{city}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

`AddStandardResilienceHandler` configures, by default: a total request timeout of 30 seconds; up to 3 retries with exponential backoff (2s, 4s, 8s) plus jitter, only for transient failures (HTTP 5xx, 408, network errors and timeouts); and a circuit breaker that opens after a sustained failure rate, failing fast for 30 seconds before trying again. Hand-trace the retry timing: attempt 1 fails immediately, the first retry waits ~2s, the second ~4s, the third ~8s — so a request that fails three times takes roughly 14 seconds of backoff plus the time of the attempts themselves, all inside the 30-second total timeout.

## Retry, Circuit Breaker, Timeout and Hedging

Each policy solves a different failure mode:

- **Retry** — repeats a failed call, with backoff so a struggling service is not hammered. Only for transient failures and, crucially, only for idempotent operations (see below).
- **Circuit breaker** — after a configured failure rate, stops calling the dependency entirely for a break duration and fails fast instead. This protects both sides: your app stops wasting threads waiting on a dead service, and the service gets breathing room to recover. After the break, it lets one trial call through (half-open); if that succeeds, the circuit closes.
- **Timeout** — caps how long a single attempt may take, and (with the standard handler) how long the whole operation including retries may take. Without a timeout, one slow dependency can exhaust your thread pool.
- **Hedging** — sends the same request to the service again in parallel if the first attempt is slow, and uses whichever response arrives first. It trades extra load for lower tail latency, so it only makes sense for idempotent GETs against a service that can absorb the duplicate traffic.

For fine-grained control, `AddResilienceHandler` lets you compose the pipeline yourself instead of taking the standard defaults:

```csharp
builder.Services.AddHttpClient<OrdersService>()
    .AddResilienceHandler("orders-pipeline", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode is HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway),
        });
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,          // Open if half the calls fail in the window.
            MinimumThroughput = 10,      // Need at least 10 calls before judging.
            BreakDuration = TimeSpan.FromSeconds(15),
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(5));
    });
```

## When NOT to Retry

The most dangerous resilience mistake is retrying a request that is not safe to repeat. A `POST` that creates an order, charges a card, or sends an email is not idempotent: if the first attempt actually succeeded but the response was lost, a retry creates a second order, a second charge, or a second email. Rules of thumb:

- **Safe to retry:** `GET`, `HEAD`, `PUT` and `DELETE` — these are idempotent by definition, so repeating them is harmless.
- **Never retry blindly:** `POST` and `PATCH`, unless the API supports idempotency keys (a client-generated key the server uses to deduplicate repeats — Stripe's `Idempotency-Key` header is the canonical example).
- **Never retry** 4xx responses (except 408 Request Timeout and 429 Too Many Requests): a 400 or 404 will fail the same way every time, so retries only add load.
- **Never retry** inside a retry: if your code already retries, do not also add a resilient handler, or the attempts multiply (3 retries × 3 retries = 9 calls).

The standard handler already follows these rules — it retries only GETs by default and only on transient failures — which is another reason to prefer it over hand-rolled retry loops.

## Key Takeaways

- `Microsoft.Extensions.Http.Resilience` (Polly v8) adds retry, circuit breaker, timeout and hedging to `HttpClient` declaratively, with no retry loops in your services.
- `AddStandardResilienceHandler` is the right default: 30s total timeout, up to 3 retries with exponential backoff (2s, 4s, 8s), and a circuit breaker — for transient failures only.
- A circuit breaker fails fast after sustained failures, protecting both your app and the struggling dependency; it half-opens with a trial call before closing.
- Timeouts cap both single attempts and the whole operation, so one slow dependency cannot exhaust your thread pool.
- Never retry non-idempotent `POST`/`PATCH` calls unless the API supports idempotency keys — a lost response plus a retry means a duplicate order or charge.
- Do not stack retries: the standard handler already retries only idempotent requests on transient failures, so prefer it over hand-rolled loops.
