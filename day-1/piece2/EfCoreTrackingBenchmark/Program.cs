using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

const int RowCount = 10_000;
const int Iterations = 5;

var dbPath = Path.Combine(Path.GetTempPath(), $"tracking-benchmark-{Guid.NewGuid():N}.db");
var connectionString = $"Data Source={dbPath}";

Console.WriteLine($"Seeding {RowCount:N0} rows into {dbPath}...");
await SeedAsync(connectionString, RowCount);

Console.WriteLine();
Console.WriteLine("Warming up (JIT, query plan cache, file cache)...");
await ReadTrackedAsync(connectionString);
await ReadNoTrackingAsync(connectionString);

var tracked = new List<(TimeSpan Elapsed, long AllocatedBytes)>();
var noTracking = new List<(TimeSpan Elapsed, long AllocatedBytes)>();

for (var i = 0; i < Iterations; i++)
{
    tracked.Add(await MeasureAsync(() => ReadTrackedAsync(connectionString)));
    noTracking.Add(await MeasureAsync(() => ReadNoTrackingAsync(connectionString)));
}

Report("Tracked (default)", tracked);
Report("AsNoTracking()", noTracking);

File.Delete(dbPath);

static async Task<(TimeSpan Elapsed, long AllocatedBytes)> MeasureAsync(Func<Task<int>> action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    var count = await action();
    sw.Stop();
    var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

    if (count != RowCount)
        throw new InvalidOperationException($"Expected {RowCount} rows, got {count}");

    return (sw.Elapsed, allocatedAfter - allocatedBefore);
}

static void Report(string label, List<(TimeSpan Elapsed, long AllocatedBytes)> results)
{
    var avgMs = results.Average(r => r.Elapsed.TotalMilliseconds);
    var avgBytes = results.Average(r => r.AllocatedBytes);
    Console.WriteLine();
    Console.WriteLine($"{label}: {Iterations} runs");
    foreach (var (elapsed, allocated) in results)
        Console.WriteLine($"  {elapsed.TotalMilliseconds,8:F1} ms   {allocated / 1024.0,10:F1} KB allocated");
    Console.WriteLine($"  ---- average: {avgMs,8:F1} ms   {avgBytes / 1024.0,10:F1} KB allocated");
}

static async Task SeedAsync(string connectionString, int rowCount)
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();

    // AutoDetectChangesEnabled = false during a large bulk insert avoids the
    // tracker re-scanning every already-added entity on each Add() call -
    // seeding isn't what we're measuring here, just getting to 10k rows fast.
    context.ChangeTracker.AutoDetectChangesEnabled = false;
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < rowCount; i++)
    {
        context.Quotes.Add(Quote.Create($"Author {i % 500}", $"Benchmark quote number {i}", now.AddSeconds(i)));
        if (i % 1000 == 999)
            await context.SaveChangesAsync();
    }

    await context.SaveChangesAsync();
}

static async Task<int> ReadTrackedAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    await using var context = new AppDbContext(options);
    var results = await context.Quotes.ToListAsync();
    return results.Count;
}

static async Task<int> ReadNoTrackingAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
    await using var context = new AppDbContext(options);
    var results = await context.Quotes.AsNoTracking().ToListAsync();
    return results.Count;
}
