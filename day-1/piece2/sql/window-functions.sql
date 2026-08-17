-- 1) ROW_NUMBER: sequential recency rank per author, then isolate each author's
-- SECOND most-recent quote - something COUNT/GROUP BY alone can't express, since
-- it needs a per-partition sequence, not just an aggregate.
WITH RankedQuotes AS (
    SELECT
        Author,
        Text,
        CreatedAtUtc,
        ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAtUtc DESC) AS RecencyRank
    FROM Quotes
    WHERE IsDeleted = 0
)
SELECT 'ROW_NUMBER (all ranks)' AS Demo, Author, Text, CreatedAtUtc, RecencyRank
FROM RankedQuotes
ORDER BY Author, RecencyRank;

SELECT 'ROW_NUMBER (2nd most recent only)' AS Demo, Author, Text, CreatedAtUtc, RecencyRank
FROM (
    SELECT Author, Text, CreatedAtUtc,
           ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAtUtc DESC) AS RecencyRank
    FROM Quotes
    WHERE IsDeleted = 0
) r
WHERE RecencyRank = 2
ORDER BY Author;

-- 2) RANK vs DENSE_RANK: rank authors by quote count, showing how RANK leaves a
-- gap after a tie (two authors tied at 3 both get rank 1, next gets rank 3) while
-- DENSE_RANK doesn't (next gets rank 2).
WITH AuthorCounts AS (
    SELECT Author, COUNT(*) AS QuoteCount
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY Author
)
SELECT
    'RANK vs DENSE_RANK' AS Demo,
    Author,
    QuoteCount,
    RANK() OVER (ORDER BY QuoteCount DESC) AS QuoteCountRank,
    DENSE_RANK() OVER (ORDER BY QuoteCount DESC) AS QuoteCountDenseRank
FROM AuthorCounts
ORDER BY QuoteCountRank, Author;

-- 3) LAG/LEAD: how many days since this author's previous quote, and until their
-- next one - a gap analysis that's awkward without window functions (would need a
-- self-join on "the row before this one per author").
SELECT
    'LAG/LEAD' AS Demo,
    Author,
    Text,
    CreatedAtUtc,
    LAG(CreatedAtUtc) OVER (PARTITION BY Author ORDER BY CreatedAtUtc) AS PreviousQuoteAt,
    DATEDIFF(
        DAY,
        LAG(CreatedAtUtc) OVER (PARTITION BY Author ORDER BY CreatedAtUtc),
        CreatedAtUtc
    ) AS DaysSincePreviousQuote,
    LEAD(CreatedAtUtc) OVER (PARTITION BY Author ORDER BY CreatedAtUtc) AS NextQuoteAt
FROM Quotes
WHERE IsDeleted = 0
ORDER BY Author, CreatedAtUtc;

-- 4) Running total: cumulative quotes published over time across all authors,
-- via SUM() OVER (ORDER BY ...) - the classic senior-level replacement for a
-- self-join or correlated subquery computing "everything up to and including
-- this row."
WITH DailyCounts AS (
    SELECT
        CAST(CreatedAtUtc AS DATE) AS QuoteDate,
        COUNT(*) AS QuotesThatDay
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY CAST(CreatedAtUtc AS DATE)
)
SELECT
    'Running total' AS Demo,
    QuoteDate,
    QuotesThatDay,
    SUM(QuotesThatDay) OVER (ORDER BY QuoteDate) AS RunningTotalQuotes
FROM DailyCounts
ORDER BY QuoteDate;
