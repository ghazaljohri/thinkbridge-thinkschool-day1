using FluentAssertions;
using QuotesApi.Models.Collections;

namespace QuotesApi.Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void Empty_name_should_throw()
    {
        var act = () => new Collection("", 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_over_80_characters_should_throw()
    {
        var name = new string('a', 81);

        var act = () => new Collection(name, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fifty_first_item_should_throw()
    {
        var collection = new Collection("Test", 1);

        for (var i = 1; i <= 50; i++)
            collection.AddItem(i, DateTimeOffset.UtcNow);

        var act = () => collection.AddItem(51, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Duplicate_quote_id_should_throw()
    {
        var collection = new Collection("Test", 1);

        collection.AddItem(1, DateTimeOffset.UtcNow);

        var act = () => collection.AddItem(1, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Removing_non_existing_item_should_throw()
    {
        var collection = new Collection("Test", 1);

        var act = () => collection.RemoveItem(99);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Adding_then_removing_should_leave_zero_items()
    {
        var collection = new Collection("Test", 1);

        collection.AddItem(1, DateTimeOffset.UtcNow);
        collection.RemoveItem(1);

        collection.Items.Should().BeEmpty();
    }
}
