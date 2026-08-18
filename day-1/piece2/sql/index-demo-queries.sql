-- Run against QuoteViews from index-demo-setup.sql (~100k rows, no indexes yet).
-- Real observed results from running this against a live SQL Server 2022
-- container are noted inline as comments.
USE IndexDemo;
GO

-- ============================================================================
-- BEFORE: heap, no indexes. Every query costs the same - a full table scan -
-- regardless of how selective the filter is.
-- ============================================================================
SET STATISTICS IO ON;
GO

SELECT COUNT(*), AVG(DurationMs) FROM QuoteViews WHERE QuoteId = 42;
-- logical reads 572, plan: Table Scan

SELECT COUNT(*), AVG(DurationMs)
FROM QuoteViews
WHERE ViewedAtUtc >= '2026-01-02T00:00:00' AND ViewedAtUtc < '2026-01-03T00:00:00';
-- logical reads 572, plan: Table Scan

SELECT * FROM QuoteViews WHERE Id = 55000;
-- logical reads 572, plan: Table Scan
GO

SET STATISTICS IO OFF;
GO

-- ============================================================================
-- Clustered index on ViewedAtUtc - not the identity Id. Event-log tables are
-- almost always queried and inserted in time order, so clustering by time
-- keeps range scans physically sequential.
-- ============================================================================
CREATE CLUSTERED INDEX IX_QuoteViews_ViewedAtUtc ON QuoteViews(ViewedAtUtc);
GO

SET STATISTICS IO ON;
GO
SELECT COUNT(*), AVG(DurationMs)
FROM QuoteViews
WHERE ViewedAtUtc >= '2026-01-02T00:00:00' AND ViewedAtUtc < '2026-01-03T00:00:00';
-- logical reads 86 (was 572), plan: Clustered Index Seek
GO
SET STATISTICS IO OFF;
GO

-- ============================================================================
-- Non-clustered index #1: covering index for "views of this quote" filters.
-- INCLUDE (DurationMs) means COUNT(*)/AVG(DurationMs) never touch the base
-- table at all.
-- ============================================================================
CREATE NONCLUSTERED INDEX IX_QuoteViews_QuoteId ON QuoteViews(QuoteId) INCLUDE (DurationMs);
GO

SET STATISTICS IO ON;
GO
SELECT COUNT(*), AVG(DurationMs) FROM QuoteViews WHERE QuoteId = 42;
-- logical reads 2 (was 572), plan: Index Seek (single operator, no lookup)
GO
SET STATISTICS IO OFF;
GO

-- ============================================================================
-- Non-clustered index #2: unique index on Id for point lookups.
-- ============================================================================
CREATE UNIQUE NONCLUSTERED INDEX UX_QuoteViews_Id ON QuoteViews(Id);
GO

SET STATISTICS IO ON;
GO
SELECT * FROM QuoteViews WHERE Id = 55000;
-- logical reads 5 (was 572), plan: Index Seek -> Nested Loops -> Clustered
-- Index Seek (a Key Lookup) - costs more than the QuoteId query above because
-- SELECT * needs columns the non-clustered index doesn't carry, so SQL Server
-- has to seek the clustered index a second time to fetch the rest of the row.
GO
SET STATISTICS IO OFF;
GO

-- To capture the actual (not estimated) execution plan as XML instead of just
-- IO stats, replace STATISTICS IO with:
--   SET STATISTICS XML ON;
-- and inspect the returned plan's RelOp/@PhysicalOp and EstimateRows/@ActualRows
-- attributes - that's what confirmed Table Scan vs Index Seek vs Clustered
-- Index Seek above, and that estimated row counts closely matched actual ones.
