using FluentAssertions;
using QuotesApi.Models;

namespace QuotesApi.Tests.Domain;

public class QuoteTests
{
    [Fact]
    public void Create_should_create_valid_quote()
    {
        var quote = Quote.Create("Maya Angelou", "Do the best you can.", DateTimeOffset.UtcNow);

        quote.Author.Should().Be("Maya Angelou");
        quote.Text.Should().Be("Do the best you can.");
        quote.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_should_reject_empty_author()
    {
        var act = () => Quote.Create("", "Some quote", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_author_over_200_characters()
    {
        var author = new string('A', 201);

        var act = () => Quote.Create(author, "Some quote", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_empty_text()
    {
        var act = () => Quote.Create("Author", "", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_text_over_1000_characters()
    {
        var text = new string('A', 1001);

        var act = () => Quote.Create("Author", text, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_should_mark_quote_as_deleted()
    {
        var quote = Quote.Create("Author", "Some quote", DateTimeOffset.UtcNow);

        quote.SoftDelete();

        quote.IsDeleted.Should().BeTrue();
    }
}
