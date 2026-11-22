# EF Core Migrations in Team Workflows

Migrations work smoothly for one developer. With a team, two people
create migrations at the same time, snapshots conflict, and production
needs a deployment-safe path. A few conventions keep the workflow sane.

## The Snapshot and Merge Conflicts

Every migration has a designer file plus a model snapshot
(`YourContextModelSnapshot.cs`) that records the current model. When two
branches each add a migration, the snapshots conflict on merge.

The fix after merging:

1. Resolve the snapshot conflict by keeping one side, then delete the
   losing migration's files if it is redundant — or keep both migrations
   and regenerate the snapshot.
2. Run `dotnet ef migrations add MergeFix` (or simply remove and re-add
   the conflicting migration) so the snapshot matches the merged model.
3. Verify with `dotnet ef migrations list` and a test database update
   before pushing.

Never hand-edit the snapshot to "fix" it — regenerate it instead.

## Idempotent Scripts for Production

Never run `dotnet ef database update` against production from a laptop.
Generate a SQL script and let the deployment pipeline apply it:

```bash
# Full script from empty database to latest.
dotnet ef migrations script -o deploy.sql

# Idempotent script: safe to run repeatedly, only applies missing
# migrations. Use this in CI/CD.
dotnet ef migrations script --idempotent -o deploy.sql

# Script for just the new migrations, from the last release to now.
dotnet ef migrations script 20260101000000_AddOrders \
    20260201000000_AddShipments -o delta.sql
```

Review the generated SQL like any other code: it is the thing that will
actually run against production data.

## Migration Bundles

A migration bundle is a self-contained executable that applies
migrations without the .NET SDK or source code on the target machine:

```bash
dotnet ef migrations bundle --self-contained -r linux-x64 -o efbundle
./efbundle --connection "Server=...;Database=...;"
```

Bundles are ideal for locked-down production servers and container
deployments where the SDK is not installed.

## Applying Migrations in CI/CD

A reliable pipeline pattern:

1. Build the app and generate the idempotent script as a build artifact.
2. A deployment step runs the script against the target database.
3. Only then does the new app version start.

Keep migrations backward compatible with the currently running app
version: add a nullable column in one release, start writing to it in
the next, and remove the old column later. A migration that renames a
column while the old app is still running will break it.

## Seeding vs Migrations

Seed data in `OnModelCreating` with `HasData` only for small, static
reference data (roles, lookup values). `HasData` tracks rows by primary
key, so changing a seeded value generates a migration — fine for a
handful of rows, noisy for hundreds.

For larger or environment-specific seed data, use a separate seeding
step at startup or a dedicated script, not migrations. Migrations should
describe schema; seed scripts describe data.

## Key Takeaways

- Snapshot conflicts are normal in teams: resolve by regenerating the
  snapshot, never by hand-editing it.
- Deploy with idempotent scripts (`Script-Migration --idempotent`) or
  migration bundles, never with `dotnet ef database update` from a
  laptop.
- Review generated SQL before it runs against production.
- Make migrations backward compatible: add nullable, deploy, then remove
  the old shape in a later release.
- Keep `HasData` seeding small and static; use a separate seeding step
  for anything larger or environment-specific.
