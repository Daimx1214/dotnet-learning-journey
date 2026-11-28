# ASP.NET Core Blazor Render Modes

Blazor render modes decide where a component renders and where its interactivity runs: on the server, in the browser via WebAssembly, or not at all (static HTML). Choosing the mode is a trade-off between first-load speed, interactivity latency, download size, and SEO.

## The Four Render Modes

| Mode | Where it renders | Interactivity | Download |
|---|---|---|---|
| Static SSR | Server, once | None (plain HTML) | Smallest |
| Interactive Server | Server, over SignalR | Server-side, every event round-trips | Small |
| Interactive WebAssembly | Browser, after download | Client-side, no round-trip | Largest (runtime + DLLs) |
| Interactive Auto | Server first, then WebAssembly | Server first, then client after download | Large, but deferred |

## Static SSR

Static server-side rendering produces plain HTML with no interactivity. It is the default when no render mode is set, and it is the fastest option for content pages.

```razor
@page "/about"

<h1>About Us</h1>
<p>We build .NET apps.</p>
```

Static pages support enhanced navigation and form handling through `<form>` posts, but a `@onclick` handler does nothing, because no interactive runtime is attached. Use static SSR for marketing pages, blogs, and documentation.

## Interactive Server

The component renders on the server and stays connected over a SignalR circuit. Every button click and keystroke travels to the server and the DOM diff travels back.

```razor
@page "/counter"
@rendermode InteractiveServer

<h1>Counter</h1>
<p>Current count: @count</p>
<button @onclick="Increment">Click me</button>

@code {
    private int count;
    private void Increment() => count++;
}
```

Latency is the cost: each interaction takes a network round-trip, typically 30-100 ms on a good connection, and the server holds one circuit per connected user, which consumes memory. The benefit is a tiny initial download and instant startup, and the full .NET runtime and server resources are available.

## Interactive WebAssembly

The component and the .NET runtime download to the browser and run entirely there. After the first download, interactions are instant with no server round-trip.

```razor
@page "/counter"
@rendermode InteractiveWebAssembly

<h1>Counter</h1>
<p>Current count: @count</p>
<button @onclick="Increment">Click me</button>

@code {
    private int count;
    private void Increment() => count++;
}
```

The first load downloads the runtime and your assemblies, often 2-10 MB, so first paint is slower, especially on mobile networks. It also cannot directly reach server-only resources such as a database; it must call an API. Use it for apps that must work offline or need instant interactions after load.

## Interactive Auto

Auto starts on Interactive Server for a fast first render, then downloads the WebAssembly runtime in the background and switches to it on the next visit.

```razor
@page "/dashboard"
@rendermode InteractiveAuto
```

This gives the best of both worlds at the price of complexity: the component must work in both runtimes, and any state that lives only in server memory is lost on the switch unless you persist it. Use Auto when you want fast startup and fast interactions, and your components are runtime-agnostic.

## Per-Page vs Per-Component

Render modes can be set per page with `@rendermode` at the top of a `.razor` file, or per component when it is used inside another page:

```razor
@page "/products"

<h1>Products</h1>
<ProductList />               @* inherits the page mode (static) *@
<LiveStockTicker @rendermode="InteractiveServer" /> @* interactive island *@
```

This "islands of interactivity" approach is the recommended pattern: keep most of the page static for speed and SEO, and make only the interactive widgets interactive.

You can also set a mode globally in `App.razor` for the whole app:

```razor
<Routes @rendermode="InteractiveServer" />
```

A page-level `@rendermode` overrides the global setting for that page.

## Trade-Offs at a Glance

- **Latency:** WebAssembly wins after load; Server adds a round-trip per event; static has no events.
- **Download size:** Static and Server are smallest; WebAssembly downloads the runtime; Auto downloads it in the background.
- **SEO:** Static SSR is best, because crawlers receive full HTML immediately. Interactive modes still server-render the first HTML, but crawlers see the pre-interactive markup.
- **Scalability:** WebAssembly scales best, because the server does no per-user work after the download. Server mode holds a SignalR circuit and memory per user.
- **Offline:** Only WebAssembly can keep working without a connection.

## Key Takeaways

- Static SSR is the default and the fastest choice for content with no interactivity.
- Interactive Server starts fast but every interaction costs a SignalR round-trip and server memory.
- Interactive WebAssembly is instant after a large first download and works offline.
- Interactive Auto starts on the server and switches to WebAssembly on later visits.
- Set `@rendermode` per page or per component, keeping most of the page static and only the interactive parts interactive.
