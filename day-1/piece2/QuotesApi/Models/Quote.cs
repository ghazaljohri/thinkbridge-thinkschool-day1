namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; private set; }
    public string Author { get; private set; }
    public string Text { get; private set; }
    public bool IsDeleted { get; private set; }

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
    }

    public static Quote Create(string author, string text)
    {
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author is required.", nameof(author));

        if (author.Length > 200)
            throw new ArgumentException("Author must be 200 characters or fewer.", nameof(author));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        if (text.Length > 1000)
            throw new ArgumentException("Text must be 1000 characters or fewer.", nameof(text));

        return new Quote(author, text);
    }

    public void SoftDelete()
    {
        IsDeleted = true;
    }
}
