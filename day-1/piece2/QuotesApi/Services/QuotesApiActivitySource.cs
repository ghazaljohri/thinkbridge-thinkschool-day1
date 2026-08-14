using System.Diagnostics;

namespace QuotesApi.Services;

public static class QuotesApiActivitySource
{
    public const string Name = "QuotesApi";

    public static readonly ActivitySource Source = new(Name);
}
