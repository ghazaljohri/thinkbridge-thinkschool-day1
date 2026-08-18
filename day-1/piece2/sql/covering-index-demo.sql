-- Run after index-demo-setup.sql and the three indexes from index-demo-queries.sql
-- (clustered on ViewedAtUtc, non-clustered on QuoteId INCLUDE DurationMs, unique
-- non-clustered on Id). Real observed results from a live SQL Server 2022
-- container are noted inline as comments.
USE IndexDemo;
GO

-- ============================================================================
-- A gotcha worth knowing before hunting for a Key Lookup: every non-clustered
-- index implicitly carries the clustering key in its leaf rows (that's how SQL
-- Server does row lookups at all). So adding ViewedAtUtc to the SELECT list
-- does NOT force a lookup - it's already "free" via IX_QuoteViews_QuoteId,
-- since ViewedAtUtc is the clustering key.
-- ============================================================================
SET STATISTICS IO ON;
GO
SELECT QuoteId, ViewedAtUtc, DurationMs
FROM QuoteViews
WHERE QuoteId = 42
ORDER BY ViewedAtUtc DESC;
-- logical reads 2, plan: single Index Seek (no lookup - ViewedAtUtc was
-- already implicitly present as the clustering key)
GO
SET STATISTICS IO OFF;
GO

-- ============================================================================
-- BEFORE: a query that needs ViewerIp - genuinely not the clustering key and
-- not in the index - forces a real Key Lookup.
-- ============================================================================
SET STATISTICS IO ON;
GO
SELECT QuoteId, ViewerIp, DurationMs
FROM QuoteViews
WHERE QuoteId = 42;
-- logical reads 546
-- plan: Nested Loops -> Index Seek (IX_QuoteViews_QuoteId) -> Clustered Index
-- Seek (the Key Lookup, fetching ViewerIp from the clustered index)
GO
SET STATISTICS IO OFF;
GO

-- ============================================================================
-- FIX: widen the covering index's INCLUDE list so ViewerIp is served from the
-- index directly - eliminates the lookup entirely.
-- ============================================================================
DROP INDEX IX_QuoteViews_QuoteId ON QuoteViews;
CREATE NONCLUSTERED INDEX IX_QuoteViews_QuoteId
    ON QuoteViews(QuoteId) INCLUDE (DurationMs, ViewerIp);
GO

SET STATISTICS IO ON;
GO
SELECT QuoteId, ViewerIp, DurationMs
FROM QuoteViews
WHERE QuoteId = 42;
-- logical reads 5 (was 546)
-- plan: single Index Seek - no Nested Loops, no Clustered Index Seek. Proven
-- from the actual plan (SET STATISTICS XML ON), not inferred from IO alone:
-- EstimateRows 181.5 vs ActualRows 175, satisfied entirely from the index.
GO
SET STATISTICS IO OFF;
GO
