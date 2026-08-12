using FluentAssertions;
using NSubstitute;
using QuotesApi.Models.Collections;
using QuotesApi.Services;

namespace Quotes.Tests.Unit;

public sealed class CollectionTests
{
    [Fact]
    public void Constructor_ValidNameAndOwner_TrimsNameAndStoresOwner()
    {
        // Arrange
        const string name = "  Favourites  ";

        // Act
        var collection = new Collection(name, 42);

        // Assert
        collection.Name.Should().Be("Favourites");
        collection.OwnerId.Should().Be(42);
        collection.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ab")]
    public void Constructor_InvalidShortName_ThrowsArgumentException(string name)
    {
        // Arrange
        var act = () => new Collection(name, 1);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("name");
    }

    [Fact]
    public void Constructor_NameLongerThan80Characters_ThrowsArgumentException()
    {
        // Arrange
        var act = () => new Collection(new string('N', 81), 1);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("name");
        exception.Message.Should().Contain("between 3 and 80");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveOwnerId_ThrowsArgumentException(int ownerId)
    {
        // Arrange
        var act = () => new Collection("Favourites", ownerId);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("ownerId");
    }

    [Fact]
    public void AddItem_ValidQuoteId_UsesSubstitutedClockValue()
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 8, 12, 10, 30, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        // Act
        collection.AddItem(7, clock.UtcNow);

        // Assert
        collection.Items.Should().ContainSingle();
        collection.Items.Single().QuoteId.Should().Be(7);
        collection.Items.Single().AddedAt.Should().Be(now.UtcDateTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_NonPositiveQuoteId_ThrowsArgumentException(int quoteId)
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        var act = () => collection.AddItem(quoteId, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("quoteId");
    }

    [Fact]
    public void AddItem_DuplicateQuoteId_ThrowsInvalidOperationException()
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        collection.AddItem(7, DateTimeOffset.UtcNow);
        var act = () => collection.AddItem(7, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        exception.Message.Should().Be("The quote is already in the collection.");
    }

    [Fact]
    public void AddItem_MoreThan50Items_ThrowsInvalidOperationException()
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        for (var quoteId = 1; quoteId <= 50; quoteId++)
            collection.AddItem(quoteId, DateTimeOffset.UtcNow);

        var act = () => collection.AddItem(51, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        exception.Message.Should().Be("A collection cannot contain more than 50 items.");
    }

    [Fact]
    public void RemoveItem_ExistingQuoteId_RemovesItem()
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        collection.AddItem(7, DateTimeOffset.UtcNow);

        // Act
        collection.RemoveItem(7);

        // Assert
        collection.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_MissingQuoteId_ThrowsInvalidOperationException()
    {
        // Arrange
        var collection = new Collection("Favourites", 1);
        var act = () => collection.RemoveItem(7);

        // Act
        var exception = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        exception.Message.Should().Be("The quote is not in the collection.");
    }
}
