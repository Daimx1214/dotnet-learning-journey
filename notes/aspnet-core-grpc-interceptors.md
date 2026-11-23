# gRPC Interceptors in ASP.NET Core

Interceptors are the gRPC equivalent of ASP.NET Core middleware: code that runs around every RPC call, on the server or the client, without touching the service logic. Logging, authentication, validation, and error translation all belong in interceptors.

## Server Interceptors

A server interceptor derives from `Interceptor` and overrides the handler methods. `UnaryServerHandler` is the one most services need; streaming calls have their own overrides (`ServerStreamingServerHandler`, `ClientStreamingServerHandler`, `DuplexStreamingServerHandler`).

```csharp
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
        => _logger = logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var method = context.Method; // e.g. "/orders.OrderService/GetOrder"
        _logger.LogInformation("gRPC call started: {Method}", method);

        try
        {
            var response = await continuation(request, context);
            _logger.LogInformation("gRPC call succeeded: {Method} ({Status})",
                method, context.Status.StatusCode);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "gRPC call failed: {Method} ({Status})",
                method, ex.StatusCode);
            throw;
        }
    }
}
```

The pattern is always the same: do work before `continuation`, await it, do work after. Forgetting to await `continuation` means the service method never runs.

## Registering with AddInterceptor

Interceptors are registered on the gRPC service, in the order they should run. They resolve from dependency injection, so a logger or a database context can be constructor-injected.

```csharp
// Program.cs
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<AuthInterceptor>();   // runs after logging
});

builder.Services.AddSingleton<LoggingInterceptor>();
builder.Services.AddSingleton<AuthInterceptor>();
```

Registration order is execution order for the "before" half and reverse order for the "after" half — the same onion model as middleware. With the registration above, a call flows: Logging (before) → Auth (before) → service method → Auth (after) → Logging (after).

## Authentication Interceptor

Auth is the most common server interceptor. It reads metadata (gRPC headers), validates a token, and rejects the call before the service method runs.

```csharp
public class AuthInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var token = context.RequestHeaders
            .FirstOrDefault(h => h.Key == "authorization")?.Value;

        if (string.IsNullOrEmpty(token) || !IsValid(token))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated, "Missing or invalid token."));
        }

        return await continuation(request, context);
    }

    private static bool IsValid(string token)
        => token.StartsWith("Bearer ", StringComparison.Ordinal);
}
```

Throwing an `RpcException` with a `Status` is how a server interceptor fails a call: the client receives that status code, not a generic transport error. Never throw a raw `Exception` from an interceptor for an expected failure — the client sees `StatusCode.Unknown` and loses the reason.

## Client Interceptors

Client interceptors wrap outgoing calls: they can attach metadata (such as an auth token) to every call, add retries, or log. They derive from the same `Interceptor` base class and override `AsyncUnaryCall` (and the streaming equivalents).

```csharp
public class AuthClientInterceptor : Interceptor
{
    private readonly string _token;

    public AuthClientInterceptor(string token) => _token = token;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        headers.Add("authorization", $"Bearer {_token}");

        var options = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options);

        return continuation(request, newContext);
    }
}

// Registration on the client
var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new OrderService.OrderServiceClient(
    channel.Intercept(new AuthClientInterceptor("secret-token")));
```

## Worked Example: Tracing Interceptor Order by Hand

Register two server interceptors, `Logging` then `Auth`, and trace one successful unary call and one unauthenticated call:

Successful call — the "before" halves run in registration order, the "after" halves in reverse:

1. Logging (before): logs "call started".
2. Auth (before): token valid, calls `continuation`.
3. Service method runs and returns.
4. Auth (after): returns the response unchanged.
5. Logging (after): logs "call succeeded".

Total interceptor method invocations: **2 before + 2 after = 4** (plus the service method itself).

Unauthenticated call — Auth throws before reaching the service:

1. Logging (before): logs "call started".
2. Auth (before): token missing, throws `RpcException (Unauthenticated)`.
3. Logging's `catch` (after): logs the failure and rethrows.

Total: the service method runs **0** times, and Logging's after-half still runs once because the exception propagates through it. That is why logging belongs outside auth: the failure is recorded even when the call never reaches the service.

## Exception Handling

- Expected failures (bad input, unauthenticated, not found) become `RpcException` with the matching `StatusCode`: `InvalidArgument`, `Unauthenticated`, `NotFound`.
- Unexpected exceptions should be caught by an exception-handling interceptor and translated to `StatusCode.Internal` with a generic message — never leak stack traces or internal details to the client.
- An interceptor that swallows an exception and returns a default response hides a failure from the caller; rethrow (or throw an `RpcException`) unless the design explicitly calls for a fallback.

## Key Takeaways

- Interceptors are gRPC middleware: derive from `Interceptor`, override `UnaryServerHandler` (server) or `AsyncUnaryCall` (client), and always await the continuation.
- Register with `AddInterceptor` in execution order; the "after" halves run in reverse, like middleware.
- Auth belongs in an interceptor that reads `context.RequestHeaders` and throws `RpcException` with `Unauthenticated` — never a raw exception.
- Client interceptors attach per-call metadata, such as a bearer token, through `channel.Intercept(...)`.
- The order trace proves the onion model: 4 interceptor invocations around a successful call, and 0 service executions when auth rejects it.
