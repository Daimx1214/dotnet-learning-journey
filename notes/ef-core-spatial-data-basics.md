# EF Core Spatial Data Basics

EF Core supports spatial data through the NetTopologySuite (NTS) package. It maps .NET geometry types to the `geography` and `geometry` column types in SQL Server and PostGIS, and translates spatial method calls into SQL so distance and containment queries run in the database.

## Setup

Install `NetTopologySuite` and the provider package, for example `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`, then enable it on the context:

```csharp
options.UseSqlServer(connectionString,
    o => o.UseNetTopologySuite());
```

The core types live in `NetTopologySuite.Geometries`: `Point`, `LineString`, `Polygon`, `GeometryCollection`, and the base `Geometry`.

## Geography vs Geometry

SQL Server has two spatial types, and the choice matters:

- `geography` stores data on a round-earth model with latitude and longitude. Use it for real-world locations: stores, delivery addresses, GPS tracks. Distances come back in meters.
- `geometry` stores data on a flat plane. Use it for floor plans, game maps, or CAD data where the earth model is irrelevant.

Pick one per column and be consistent. Mixing them in a query is an error.

## SRID 4326

Every geometry carries a Spatial Reference System Identifier (SRID). SRID 4326 is WGS 84, the GPS coordinate system, and it is the default for `geography` columns. Always set it explicitly when creating points:

```csharp
var factory = NTSGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
var storeLocation = factory.CreatePoint(new Coordinate(-0.1278, 51.5074));
```

The coordinate order is longitude first, then latitude. Getting this backwards is the most common spatial bug: a point in London becomes a point in the ocean.

## Mapping an Entity

```csharp
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Point Location { get; set; } = null!;
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Store>()
        .Property(s => s.Location)
        .HasColumnType("geography");
}
```

EF Core maps `Point` to `geography` by default in SQL Server when NetTopologySuite is enabled, but `HasColumnType("geography")` makes the intent explicit. For a flat-plane column use `HasColumnType("geometry")`.

## Distance Queries

Spatial methods translate to SQL, so the filtering happens in the database:

```csharp
var center = factory.CreatePoint(new Coordinate(-0.1278, 51.5074));

var nearby = await db.Stores
    .Where(s => s.Location.Distance(center) < 5000)
    .OrderBy(s => s.Location.Distance(center))
    .ToListAsync();
```

With a `geography` column, `Distance` returns meters, so this finds stores within 5 km. Other translated methods include `Contains`, `Intersects`, `Within`, `Buffer` (a shape around a point), and `IsWithinDistance`.

## Spatial Indexes

Distance queries over a large table need a spatial index, or SQL Server scans every row:

```sql
CREATE SPATIAL INDEX IX_Stores_Location
ON Stores (Location);
```

For a spatial index to be used, the query should filter with a bounding shape where possible, and the column must have a declared bounding box in some providers. In SQL Server, keep the index on the same column type as the queries, and verify with the execution plan that the index is actually used.

## Common Pitfalls

- Longitude and latitude swapped when constructing a `Point`.
- Forgetting the SRID, which leaves it at 0 and makes distance results meaningless.
- Calling spatial methods on the client side after `ToList()`, which loads the whole table.
- Using `geometry` for GPS data and getting flat-earth distances.

## Key Takeaways

- NetTopologySuite adds `Point`, `Polygon`, and other geometry types to EF Core, mapped to `geography` or `geometry` columns.
- Use `geography` with SRID 4326 for real-world locations; distances come back in meters.
- Coordinates are longitude first, then latitude; swapping them is the classic mistake.
- Spatial methods like `Distance`, `Contains`, and `Within` translate to SQL and filter in the database.
- Add a spatial index on location columns or large tables will scan on every query.
