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

-- ============================================================================
-- WRITE-SIDE COST: the same 10,000-row insert, measured once against the bare
-- heap (before any of the three indexes above existed) and once with all
-- three indexes live, table reset to the same 100,000-row starting size both
-- times for a fair comparison.
-- ============================================================================
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

;WITH L0 AS (SELECT 1 AS c UNION ALL SELECT 1),
L1 AS (SELECT 1 AS c FROM L0 A CROSS JOIN L0 B),
L2 AS (SELECT 1 AS c FROM L1 A CROSS JOIN L1 B),
L3 AS (SELECT 1 AS c FROM L2 A CROSS JOIN L2 B),
Nums AS (SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N FROM L3 A CROSS JOIN L3 B)
INSERT INTO QuoteViews (QuoteId, ViewedAtUtc, ViewerIp, DurationMs)
SELECT
    1 + ABS(CHECKSUM(NEWID())) % 500,
    DATEADD(SECOND, N, '2026-02-01T00:00:00'),
    CONCAT(1 + ABS(CHECKSUM(NEWID())) % 255, '.', 1 + ABS(CHECKSUM(NEWID())) % 255, '.0.1'),
    10 + ABS(CHECKSUM(NEWID())) % 5000
FROM Nums;
-- Heap (no indexes):        10,057 logical reads, ~25ms CPU.
-- All 3 indexes present:    86,801 reads on QuoteViews + 20,382 on a sort
--                            worktable (~107k total), ~89ms CPU - roughly
--                            3.5x the time and ~10x the logical I/O for the
--                            identical insert, since every row now has to
--                            maintain a clustered B-tree position plus update
--                            two non-clustered structures instead of just
--                            appending to a heap.
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO
