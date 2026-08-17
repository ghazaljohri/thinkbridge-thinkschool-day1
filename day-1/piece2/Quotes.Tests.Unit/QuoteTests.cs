using FluentAssertions;
using QuotesApi.Models;

namespace Quotes.Tests.Unit;

public sealed class QuoteTests
{
    [Fact]
    public void Create_ValidAuthorAndText_CreatesActiveQuote()
    {
        // Arrange
        const string author = "Maya Angelou";
        const string text = "Do the best you can.";

        // Act
        var quote = Quote.Create(author, text, DateTimeOffset.UtcNow);

        // Assert
        quote.Author.Should().Be(author);
        quote.Text.Should().Be(text);
        quote.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_MissingAuthor_ThrowsRequiredArgumentException(string author)
    {
        // Arrange
        var act = () => Quote.Create(author, "A valid quote.", DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("author");
        exception.Message.Should().Contain("Author is required.");
    }

    [Fact]
    public void Create_AuthorLongerThan200Characters_ThrowsArgumentException()
    {
        // Arrange
        var author = new string('A', 201);
        var act = () => Quote.Create(author, "A valid quote.", DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("author");
        exception.Message.Should().Contain("200 characters or fewer");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_MissingText_ThrowsRequiredArgumentException(string text)
    {
        // Arrange
        var act = () => Quote.Create("Author", text, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("text");
        exception.Message.Should().Contain("Text is required.");
    }

    [Fact]
    public void Create_TextLongerThan1000Characters_ThrowsArgumentException()
    {
        // Arrange
        var text = new string('T', 1001);
        var act = () => Quote.Create("Author", text, DateTimeOffset.UtcNow);

        // Act
        var exception = act.Should().Throw<ArgumentException>().Which;

        // Assert
        exception.ParamName.Should().Be("text");
        exception.Message.Should().Contain("1000 characters or fewer");
    }

    [Fact]
    public void SoftDelete_ActiveQuote_MarksQuoteDeleted()
    {
        // Arrange
        var quote = Quote.Create("Author", "A valid quote.", DateTimeOffset.UtcNow);

        // Act
        quote.SoftDelete();

        // Assert
        quote.IsDeleted.Should().BeTrue();
    }
}
