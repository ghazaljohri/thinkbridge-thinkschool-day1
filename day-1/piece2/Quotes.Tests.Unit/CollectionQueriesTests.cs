using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Models.Collections;
using QuotesApi.Queries;

namespace Quotes.Tests.Unit;

public sealed class CollectionQueriesTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public async Task GetDetailAsync_ExistingCollection_ReturnsItemsWithAuthorAndText()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var quote = Quote.Create("Marcus Aurelius", "You have power over your mind.", DateTimeOffset.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var collection = new Collection("Stoics", ownerId: 1);
        var addedAt = DateTimeOffset.UtcNow;
        collection.AddItem(quote.Id, addedAt);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var queries = new CollectionQueries(context);

        // Act
        var detail = await queries.GetDetailAsync(collection.Id, CancellationToken.None);

        // Assert
        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Stoics");
        detail.OwnerId.Should().Be(1);
        detail.Items.Should().ContainSingle();
        detail.Items.Single().Author.Should().Be("Marcus Aurelius");
        detail.Items.Single().Text.Should().Be("You have power over your mind.");
        detail.Items.Single().AddedAt.Should().Be(addedAt.UtcDateTime);
    }

    [Fact]
    public async Task GetDetailAsync_MissingCollection_ReturnsNull()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var queries = new CollectionQueries(context);

        // Act
        var detail = await queries.GetDetailAsync(999, CancellationToken.None);

        // Assert
        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetSummariesByOwnerAsync_MultipleCollections_ReturnsCountAndLastAddedPerCollection()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var quoteOne = Quote.Create("Author 1", "First quote", DateTimeOffset.UtcNow);
        var quoteTwo = Quote.Create("Author 2", "Second quote", DateTimeOffset.UtcNow);
        context.Quotes.AddRange(quoteOne, quoteTwo);
        await context.SaveChangesAsync();

        var owned = new Collection("Owned", ownerId: 1);
        var firstAddedAt = DateTimeOffset.UtcNow;
        var secondAddedAt = firstAddedAt.AddMinutes(5);
        owned.AddItem(quoteOne.Id, firstAddedAt);
        owned.AddItem(quoteTwo.Id, secondAddedAt);

        var empty = new Collection("Empty", ownerId: 1);
        var otherOwners = new Collection("Someone Else's", ownerId: 2);

        context.Collections.AddRange(owned, empty, otherOwners);
        await context.SaveChangesAsync();

        var queries = new CollectionQueries(context);

        // Act
        var summaries = await queries.GetSummariesByOwnerAsync(1, CancellationToken.None);

        // Assert
        summaries.Should().HaveCount(2);
        var ownedSummary = summaries.Single(s => s.Name == "Owned");
        ownedSummary.ItemCount.Should().Be(2);
        ownedSummary.LastAddedAt.Should().Be(secondAddedAt.UtcDateTime);

        var emptySummary = summaries.Single(s => s.Name == "Empty");
        emptySummary.ItemCount.Should().Be(0);
        emptySummary.LastAddedAt.Should().BeNull();
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
