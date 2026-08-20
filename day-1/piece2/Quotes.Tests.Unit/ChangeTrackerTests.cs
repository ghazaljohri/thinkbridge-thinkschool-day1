using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace Quotes.Tests.Unit;

public sealed class ChangeTrackerTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public async Task Query_SameKeyTwiceInSameContext_ReturnsSameInstance()
    {
        using var context = _fixture.CreateContext();
        var seeded = Quote.Create("Identity Author", "Identity resolution quote", DateTimeOffset.UtcNow);
        context.Quotes.Add(seeded);
        await context.SaveChangesAsync();

        // Two separate queries for the same primary key, same DbContext.
        var first = await context.Quotes.FirstAsync(q => q.Id == seeded.Id);
        var second = await context.Quotes.FirstAsync(q => q.Id == seeded.Id);

        // Identity resolution: the change tracker recognizes the same key and
        // hands back the SAME object, not a second copy built from the second
        // query's row.
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public async Task Query_Tracked_AppearsInChangeTracker()
    {
        using var context = _fixture.CreateContext();
        context.Quotes.Add(Quote.Create("Tracked Author", "Tracked quote", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await context.Quotes.ToListAsync();

        context.ChangeTracker.Entries<Quote>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Query_AsNoTracking_DoesNotAppearInChangeTracker()
    {
        using var context = _fixture.CreateContext();
        context.Quotes.Add(Quote.Create("Untracked Author", "Untracked quote", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await context.Quotes.AsNoTracking().ToListAsync();

        context.ChangeTracker.Entries<Quote>().Should().BeEmpty();
    }

    [Fact]
    public async Task ModifyTrackedEntity_SaveChanges_PersistsWithoutExplicitUpdate()
    {
        int quoteId;
        using (var context = _fixture.CreateContext())
        {
            var quote = Quote.Create("Mutable Author", "Original text", DateTimeOffset.UtcNow);
            context.Quotes.Add(quote);
            await context.SaveChangesAsync();
            quoteId = quote.Id;
        }

        using (var context = _fixture.CreateContext())
        {
            // Tracked query - EF is watching this instance.
            var quote = await context.Quotes.FirstAsync(q => q.Id == quoteId);
            typeof(Quote).GetProperty(nameof(Quote.Text))!.SetValue(quote, "Changed via tracked entity");
            await context.SaveChangesAsync();
        }

        using (var context = _fixture.CreateContext())
        {
            var reloaded = await context.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
            reloaded.Text.Should().Be("Changed via tracked entity");
        }
    }

    [Fact]
    public async Task ModifyNoTrackingEntity_SaveChanges_DoesNotPersist_ThisIsTheGotcha()
    {
        int quoteId;
        using (var context = _fixture.CreateContext())
        {
            var quote = Quote.Create("Gotcha Author", "Original text", DateTimeOffset.UtcNow);
            context.Quotes.Add(quote);
            await context.SaveChangesAsync();
            quoteId = quote.Id;
        }

        using (var context = _fixture.CreateContext())
        {
            // AsNoTracking() - EF never sees this instance, so mutating it and
            // calling SaveChangesAsync() is a silent no-op: there's no tracked
            // entry to flush. This is exactly the mistake AsNoTracking()
            // punishes if it's reached for reflexively on a write path.
            var quote = await context.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
            typeof(Quote).GetProperty(nameof(Quote.Text))!.SetValue(quote, "This change is lost");
            await context.SaveChangesAsync();
        }

        using (var context = _fixture.CreateContext())
        {
            var reloaded = await context.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
            reloaded.Text.Should().Be("Original text");
        }
    }

    public void Dispose() => _fixture.Dispose();

    private sealed class SqliteConnectionFixture : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public SqliteConnectionFixture()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            _connection.Open();
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new AppDbContext(options);
        }

        public void Dispose() => _connection.Dispose();
    }
}
