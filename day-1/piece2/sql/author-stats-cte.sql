-- Each author with their quote count and their most-recent quote, in one statement,
-- using two CTEs (not a correlated subquery in the SELECT).
WITH AuthorStats AS (
    SELECT
        Author,
        COUNT(*) AS QuoteCount
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY Author
),
RankedQuotes AS (
    SELECT
        Author,
        Text,
        CreatedAtUtc,
        ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAtUtc DESC) AS rn
    FROM Quotes
    WHERE IsDeleted = 0
)
SELECT
    s.Author,
    s.QuoteCount,
    r.Text AS MostRecentQuoteText,
    r.CreatedAtUtc AS MostRecentQuoteAt
FROM AuthorStats s
INNER JOIN RankedQuotes r
    ON r.Author = s.Author
    AND r.rn = 1
ORDER BY s.QuoteCount DESC;
