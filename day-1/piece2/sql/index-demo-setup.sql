CREATE DATABASE IndexDemo;
GO
USE IndexDemo;
GO

-- QuoteViews: a read/telemetry log for the Quotes API. Unlike the core Quotes
-- table (a few thousand rows at most), an event log like this is the kind of
-- table that genuinely reaches hundreds of thousands to millions of rows in a
-- real system - a realistic vehicle for an indexing exercise. Created as a heap
-- (no clustered index yet) so the "before" numbers are honest.
CREATE TABLE QuoteViews (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    QuoteId INT NOT NULL,
    ViewedAtUtc DATETIME2(3) NOT NULL,
    ViewerIp VARCHAR(45) NOT NULL,
    DurationMs INT NOT NULL
);
GO

-- Generate ~100k rows set-based via a cascading cross join (fast, no real
-- recursion depth), not a naive recursive CTE or a loop.
;WITH L0 AS (SELECT 1 AS c UNION ALL SELECT 1),
L1 AS (SELECT 1 AS c FROM L0 A CROSS JOIN L0 B),
L2 AS (SELECT 1 AS c FROM L1 A CROSS JOIN L1 B),
L3 AS (SELECT 1 AS c FROM L2 A CROSS JOIN L2 B),
L4 AS (SELECT 1 AS c FROM L3 A CROSS JOIN L3 B),
L5 AS (SELECT 1 AS c FROM L4 A CROSS JOIN L4 B),
Nums AS (SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N FROM L5)
INSERT INTO QuoteViews (QuoteId, ViewedAtUtc, ViewerIp, DurationMs)
SELECT
    1 + ABS(CHECKSUM(NEWID())) % 500,
    DATEADD(SECOND, N, '2026-01-01T00:00:00'),
    CONCAT(1 + ABS(CHECKSUM(NEWID())) % 255, '.', 1 + ABS(CHECKSUM(NEWID())) % 255, '.0.1'),
    10 + ABS(CHECKSUM(NEWID())) % 5000
FROM Nums;
GO

SELECT COUNT(*) AS TotalRows, COUNT(DISTINCT QuoteId) AS DistinctQuoteIds FROM QuoteViews;
GO
