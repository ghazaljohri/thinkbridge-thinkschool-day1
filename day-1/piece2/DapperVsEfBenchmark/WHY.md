# When to Reach for Dapper

`GET /api/authors/summary` is the one read path in this API that already had to leave
LINQ behind (see `AuthorEndpointExtensions.cs`): EF's SQLite provider can't translate a
"first row per group ordered by a `DateTimeOffset`" query, so the Day 11 fix hand-writes
the SQL and runs it through `db.Database.SqlQueryRaw<AuthorSummary>(...)`. That makes it
the fairest query in the codebase to re-run through Dapper - the SQL is already fixed and
identical either way, so the comparison isolates the execution path, not the query shape.

`DapperVsEfBenchmark/Program.cs` runs that exact same CTE two ways against a SQLite
database seeded with 500 authors x 20 quotes each (10,000 rows), 30 timed iterations
after a warm-up pass:

```
EF Core (SqlQueryRaw<T>, same SQL): 30 runs
  average:    2.176 ms   best:    1.456 ms

Dapper (QueryAsync<T>, same SQL): 30 runs
  average:    1.287 ms   best:    1.254 ms
```

Dapper comes out roughly 40% faster on average and far more consistent run-to-run (its
best and average are close together; EF's are not). None of that gap comes from the SQL -
it's identical in both runs. It comes from what EF does around the query: standing up a
`DbContext` (model validation, change tracker, service provider resolution) versus Dapper
opening a bare `SqliteConnection` and mapping rows straight into a record.

## The rule

Default to EF. Drop to Dapper only when both of these are true:

1. **The query already had to become raw SQL.** If EF's LINQ translator can express the
   query, keep it as LINQ - a hand-written string loses compile-time checking, migrations
   awareness, and the ability to compose further `Where`/`Include` calls, and Dapper isn't
   going to out-run EF's own SQL translation by any amount that matters.
2. **The path is hot enough that the *remaining* overhead - DbContext setup and
   materialization, not the query itself - shows up in a profile.** A ~1ms difference is
   real but invisible on an admin report hit once a minute; it matters on a path called
   thousands of times a second.

If a query fails #1, fix the query or the model, don't reach for a new library. If it
passes #1 but not #2, leave it on `SqlQueryRaw` - it's already paid the "raw SQL" cost
that Dapper would also require, without adding a second data-access library to the
project. Dapper earns its place only at the intersection of both: already-raw SQL, on a
path where milliseconds are actually being counted.
