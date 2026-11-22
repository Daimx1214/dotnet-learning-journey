# Blazor Hosting Models

Blazor is ASP.NET Core's UI framework for building interactive web apps
in C# instead of JavaScript. The same component model runs under three
hosting models, and the choice decides where the code executes, how fast
the first page appears, and how the app scales.

## Blazor Server

The app runs on the server. The browser holds a lightweight page and a
SignalR connection; every button click and keystroke travels to the
server, and UI updates (diffs) travel back.

- **Pros:** tiny download, instant startup, full access to server
  resources (databases, files), no WebAssembly download.
- **Cons:** needs a constant SignalR connection — a dropped connection
  freezes the UI; server CPU and memory scale with every connected user;
  latency is visible on slow networks.

Best for internal tools and dashboards on a reliable network, where
startup speed matters more than offline use.

## Blazor WebAssembly

The app runs entirely in the browser. The .NET runtime and your compiled
assemblies download as WebAssembly on first load, then the app runs
client-side with no server round-trip per interaction.

- **Pros:** works offline after the first load, no per-user server cost,
  can be hosted as static files on any CDN.
- **Cons:** large first download (runtime + assemblies), slower first
  paint, no direct access to server resources — data comes through APIs.

Best for public-facing apps and PWAs where users tolerate a loading
screen in exchange for a snappy, offline-capable client.

## Blazor Web App (.NET 8 Auto)

.NET 8 introduced a unified Blazor Web App template with a per-page or
per-component render mode, including **Auto**: the page first renders
interactively on the server (fast first paint), then the WebAssembly
runtime downloads in the background and later visits run client-side.

```razor
@rendermode InteractiveAuto
```

Render modes can be mixed in one app: a static landing page, a
server-interactive admin page, and a WebAssembly-interactive dashboard.

## Prerendering

All three models support prerendering: the server renders the initial
HTML before the interactive runtime connects, so the user sees content
immediately and search engines can index the page. With prerendering,
component lifecycle methods run twice (once on the server, once when the
interactive runtime starts) — keep them idempotent and avoid side
effects such as double database writes.

## Trade-offs at a Glance

| | Server | WebAssembly | Auto (.NET 8) |
| --- | --- | --- | --- |
| Code runs | Server | Browser | Both |
| First paint | Fast | Slow (download) | Fast |
| Offline | No | Yes | After first load |
| Server cost per user | High | None | Medium |
| Needs constant connection | Yes | No | No, after download |
| Direct DB access | Yes | No | Server parts only |

## Key Takeaways

- Blazor Server runs on the server over SignalR: fast startup, but
  needs a live connection and scales with server resources.
- Blazor WebAssembly runs in the browser: offline-capable and cheap to
  host, but the first download is large.
- .NET 8's Auto render mode gives the best of both: server rendering
  first, WebAssembly taking over once downloaded.
- Prerendering improves first paint and SEO, but lifecycle methods run
  twice, so keep them idempotent.
- Pick Server for internal tools, WebAssembly for public/offline apps,
  and Auto when you want fast first paint without giving up client-side
  interactivity.
