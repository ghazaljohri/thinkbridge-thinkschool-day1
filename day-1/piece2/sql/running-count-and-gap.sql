-- Per author, each quote with a running count of quotes-so-far and the gap in
-- days since that author's previous quote.
SELECT
    Author,
    Text,
    CreatedAtUtc,
    COUNT(*) OVER (
        PARTITION BY Author
        ORDER BY CreatedAtUtc
        ROWS UNBOUNDED PRECEDING
    ) AS RunningQuoteCount,
    DATEDIFF(
        DAY,
        LAG(CreatedAtUtc) OVER (PARTITION BY Author ORDER BY CreatedAtUtc),
        CreatedAtUtc
    ) AS DaysSincePreviousQuote
FROM Quotes
WHERE IsDeleted = 0
ORDER BY Author, CreatedAtUtc;
