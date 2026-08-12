using FluentAssertions;
using QuotesApi.Models.Collections;

namespace Quotes.Tests.Unit;

public sealed class CollectionItemTests
{
    [Fact]
    public void Constructor_ValidQuoteId_StoresUtcTimestamp()
    {
        // Arrange
        var addedAt = new DateTimeOffset(2026, 8, 12, 15, 30, 0, TimeSpan.FromHours(5.5));

        // Act
        var item = new CollectionItem(9, addedAt);

        // Assert
        item.QuoteId.Should().Be(9);
        item.AddedAt.Should().Be(addedAt.UtcDateTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveQuoteId_ThrowsArgumentException(int quoteId)
    {
        // Arrange
        var act = () => new CollectionItem(quoteId, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("quoteId");
    }
}
