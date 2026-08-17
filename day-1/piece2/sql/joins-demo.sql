-- INNER JOIN: quotes that ARE saved into at least one collection.
SELECT 'INNER JOIN' AS Demo, q.Author, q.Text, c.Name AS CollectionName
FROM Quotes q
INNER JOIN CollectionItems ci ON ci.QuoteId = q.Id
INNER JOIN Collections c ON c.Id = ci.CollectionId
WHERE q.IsDeleted = 0
ORDER BY q.Author;

-- LEFT JOIN: quotes NOT saved into any collection (the unmatched right side is NULL).
SELECT 'LEFT JOIN (orphans)' AS Demo, q.Author, q.Text
FROM Quotes q
LEFT JOIN CollectionItems ci ON ci.QuoteId = q.Id
WHERE q.IsDeleted = 0
  AND ci.QuoteId IS NULL
ORDER BY q.Author;

-- CROSS JOIN + recursive CTE: an author x day activity matrix for the last 14 days,
-- showing 0 for days an author posted nothing (LEFT JOIN'd, not just cross-joined).
WITH DateSpine AS (
    SELECT CAST('2026-08-04' AS DATE) AS DayStart
    UNION ALL
    SELECT DATEADD(DAY, 1, DayStart)
    FROM DateSpine
    WHERE DayStart < '2026-08-17'
),
Authors AS (
    SELECT DISTINCT Author FROM Quotes WHERE IsDeleted = 0
),
AuthorDays AS (
    SELECT a.Author, d.DayStart
    FROM Authors a
    CROSS JOIN DateSpine d
)
SELECT
    ad.Author,
    ad.DayStart,
    COUNT(q.Id) AS QuotesPostedThatDay
FROM AuthorDays ad
LEFT JOIN Quotes q
    ON q.Author = ad.Author
    AND q.IsDeleted = 0
    AND CAST(q.CreatedAtUtc AS DATE) = ad.DayStart
GROUP BY ad.Author, ad.DayStart
HAVING COUNT(q.Id) > 0
ORDER BY ad.Author, ad.DayStart
OPTION (MAXRECURSION 100);
