# GraphQL Basics in ASP.NET Core (ASP.NET Core)

GraphQL is a query language for APIs. Instead of many REST endpoints that each return a fixed shape, a GraphQL server exposes a single endpoint and lets the client ask for exactly the fields it needs — no more, no less. The client writes a query, the server resolves it against a schema.

## Schema, Types, Queries, Mutations

The **schema** is the contract: it lists every type and every operation. A **type** is an object with fields, like `Book` with `id`, `title`, and `author`. A **query** reads data; a **mutation** writes data. There is no separate endpoint per resource — one endpoint serves all of them.

```graphql
# The client asks for exactly these fields, nothing else.
query {
  books {
    id
    title
    author {
      name
    }
  }
}

# A mutation changes data and returns the new state.
mutation {
  addBook(title: "Clean Code", authorId: 3) {
    id
    title
  }
}
```

The same `books` query can ask for `title` only on a mobile client and for ten fields on a desktop client, without any server change. That is the core win over REST: the client controls the response shape.

## GraphQL vs REST

REST gives each resource an endpoint and a fixed response shape, which is simple and cacheable, but clients often over-fetch (unused fields) or under-fetch (needing three round trips for related data). GraphQL fixes both by letting the client shape one request, at the cost of a more complex server: every field needs a resolver, caching is harder (everything is a POST to one URL), and a deeply nested query can be expensive, so servers add query depth and complexity limits.

Use GraphQL when clients have genuinely different data needs — a web app, a mobile app, and a partner integration hitting the same backend. Use REST when the API surface is small, responses are uniform, and HTTP caching matters.

## Hot Chocolate Setup

Hot Chocolate is the standard GraphQL server library for .NET. The setup is code-first: you write plain C# classes and it builds the schema from them.

```csharp
// Program.cs
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();
app.MapGraphQL(); // Exposes the endpoint at /graphql
```

```csharp
// A Query type is a plain class. Each public method is a field.
public class Query
{
    public List<Book> GetBooks() => new()
    {
        new Book(1, "Clean Code", new Author(3, "Robert C. Martin")),
        new Book(2, "The Pragmatic Programmer", new Author(4, "David Thomas")),
    };

    public Book? GetBookById(int id) =>
        GetBooks().FirstOrDefault(b => b.Id == id);
}

public record Book(int Id, string Title, Author Author);
public record Author(int Id, string Name);

public class Mutation
{
    public Book AddBook(string title, int authorId)
    {
        // Persist the book, then return it.
        return new Book(99, title, new Author(authorId, "Unknown"));
    }
}
```

Method names map to schema fields by convention: `GetBooks` becomes the `books` field, `GetBookById` becomes `bookById`. Hot Chocolate also ships Banana Cake Pop, a built-in GraphiQL-style explorer at `/graphql` in development, so you can try queries without a client.

## Resolvers and the N+1 Problem

Each field is resolved by a method call. If `books` returns 100 books and each book's `author` field hits the database, you get 101 queries — the classic N+1 problem. The fix is a **DataLoader**, which batches the author lookups into one query:

```csharp
public class BookType : ObjectType<Book>
{
    protected override void Configure(IObjectTypeDescriptor<Book> descriptor)
    {
        descriptor.Field(b => b.Author).ResolveWith<Resolvers>(r =>
            r.GetAuthorAsync(default!, default!));
    }
}
```

In practice, keep resolvers thin, load related data in the parent query with `Include` or a DataLoader, and set a max query depth so a malicious client cannot nest `author { books { author { ... } } }` forever.

## Key takeaways

- GraphQL trades fixed REST endpoints for one endpoint where the client chooses the fields.
- Queries read, mutations write, and the schema is the contract between client and server.
- Hot Chocolate builds the schema from plain C# classes; `MapGraphQL()` exposes it at `/graphql`.
- Watch for N+1 queries in resolvers and cap query depth — flexibility without limits is a denial-of-service risk.
