# Integration Testing in ASP.NET Core

Integration tests exercise the real application — the real routing, middleware, model binding, and controllers — over HTTP, but with test doubles swapped in for external dependencies. The standard tool is `WebApplicationFactory<TEntryPoint>` from `Microsoft.AspNetCore.Mvc.Testing`: it boots the app in memory with a `TestServer` and returns an `HttpClient` that talks to it without opening a network port.

## Setting Up WebApplicationFactory

The test project references `Microsoft.AspNetCore.Mvc.Testing` and the app project. A custom factory overrides `ConfigureWebHost` to swap services, most commonly replacing the real database with an in-memory one.

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration.
            var descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);

            // Register an in-memory database with a unique name per factory.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        });
    }
}
```

`Program` must be visible to the test project: either use the `InternalsVisibleTo` attribute or declare `public partial class Program { }` in the app. The in-memory provider is fine for controller and routing tests, but it does not enforce relational constraints — a test that passes against it can still fail against SQL Server. For tests that need real SQL behavior, swap in a Testcontainers SQL Server or SQLite instead.

## Writing the Tests

```csharp
using System.Net;
using System.Net.Http.Json;

public sealed class OrdersApiTests(AppFactory factory)
    : IClassFixture<AppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetOrders_ReturnsSeededOrders()
    {
        // Arrange: seed through the factory's service scope.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(new Order(1, "Daim", 49.99m));
        await db.SaveChangesAsync();

        // Act: real HTTP request against the in-memory TestServer.
        var response = await _client.GetAsync("/api/orders");

        // Assert: real status code, real JSON body.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        Assert.Single(orders!);
        Assert.Equal("Daim", orders![0].Customer);
    }
}
```

## Bypassing Authentication in Tests

Real auth (JWT bearer, cookies) usually cannot run in tests without a token issuer. The clean approach is to replace the authentication scheme in the factory with a test scheme that always succeeds, rather than disabling authorization globally.

```csharp
// In the factory's ConfigureWebHost, after the service swaps:
builder.ConfigureServices(services =>
{
    services.AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
            "Test", _ => { });
});

// Test handler: every request is authenticated as a fixed user.
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-1"),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

Then set the default scheme to `"Test"` in the factory (`services.PostConfigure<AuthenticationOptions>(o => o.DefaultScheme = "Test")`). Authorization policies still run — a test hitting an `[Authorize(Roles = "Admin")]` endpoint passes with the Admin claim and fails without it — so the tests still verify the authorization rules.

## Test Data and Isolation

Each test class should get a fresh database: a unique in-memory database name per factory instance (as above), or `EnsureDeleted` + `EnsureCreated` in the test constructor. Shared state between tests is the top cause of flaky integration suites. A suite of 200 integration tests against an in-memory database typically runs in 10–30 seconds; against a real containerized SQL Server it is 2–5 minutes, which belongs in CI rather than the inner loop.

## Common Pitfalls

- Calling `CreateClient()` per test instead of once per class: each call is cheap, but per-test clients hide shared-state bugs and slow the suite.
- Testing against the in-memory provider and assuming SQL semantics: no foreign keys, no unique constraints, no transactions — verify SQL-specific behavior against a real database.
- Seeding data through the API in one test and reading it in another: test order is not guaranteed, so each test must seed its own data.
- Forgetting to set `ASPNETCORE_ENVIRONMENT` to `Testing` in the factory: the app may load production config, real connection strings, or real secrets.

## Key Takeaways

- `WebApplicationFactory<T>` boots the real app in memory with a `TestServer` and gives an `HttpClient` for real HTTP calls without a network port.
- Swap external dependencies in `ConfigureWebHost`: replace the real `DbContext` with an in-memory database (unique name per factory) or a Testcontainers database for SQL fidelity.
- Bypass auth with a test authentication scheme that issues a fixed identity, not by disabling authorization — policies still get verified.
- Each test seeds its own data; shared state is the main source of flaky integration tests.
