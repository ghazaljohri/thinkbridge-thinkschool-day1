-- Day 8: reproduce a dirty read, a non-repeatable read, and a phantom read with
-- two real concurrent sessions, then show which isolation level prevents each.
--
-- Each block below is meant to run in TWO SEPARATE sqlcmd/SSMS sessions at the
-- same time - "Session A" and "Session B" - not as a single script. Start
-- Session A first, then Session B a couple of seconds later while A is still
-- mid-transaction. Real observed timestamps/results from running this against
-- a live SQL Server 2022 container are noted inline as comments.

-- ============================================================================
-- SETUP
-- ============================================================================
CREATE DATABASE IsolationDemo;
GO
USE IsolationDemo;
GO
CREATE TABLE Accounts (Id INT PRIMARY KEY, Balance INT NOT NULL);
INSERT INTO Accounts (Id, Balance) VALUES (1, 1000), (2, 2000);
GO

-- ============================================================================
-- 1) DIRTY READ
-- Session A holds an uncommitted update for 10s, then rolls it back.
-- ============================================================================

-- --- Session A ---
USE IsolationDemo;
BEGIN TRAN;
UPDATE Accounts SET Balance = 999 WHERE Id = 1;
WAITFOR DELAY '00:00:10';
ROLLBACK TRAN;
GO

-- --- Session B, run ~3s after Session A starts, REPRODUCES the dirty read ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT Balance FROM Accounts WHERE Id = 1;
-- Observed: returns immediately with Balance = 999 - a value A never
-- committed (A rolls back a few seconds later, true value is 1000).
GO

-- --- Session B, run ~3s after Session A starts, PREVENTS the dirty read ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT Balance FROM Accounts WHERE Id = 1;
-- Observed: this SELECT blocks until Session A's transaction ends (~7s later
-- in this run), then returns Balance = 1000 - it never sees the uncommitted
-- 999. Session A rolled back at 04:26:18.408; this blocked read returned at
-- 04:26:18.413, 5ms later - proof READ COMMITTED waits for A's lock.
GO

-- ============================================================================
-- 2) NON-REPEATABLE READ
-- Session B reads the same row twice inside one transaction, with a 6s gap.
-- ============================================================================

-- --- Session B, REPRODUCES the non-repeatable read (start this first) ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE Id = 2;   -- first read: 2000
WAITFOR DELAY '00:00:06';
SELECT Balance FROM Accounts WHERE Id = 2;   -- second read: 2500 (changed!)
COMMIT;
GO

-- --- Session A, run ~2s after Session B starts ---
USE IsolationDemo;
UPDATE Accounts SET Balance = 2500 WHERE Id = 2;
-- Observed: commits instantly, unblocked, because READ COMMITTED doesn't hold
-- a lock on the row after Session B's first SELECT completes.
GO

-- Reset before the prevention run: UPDATE Accounts SET Balance = 2000 WHERE Id = 2;

-- --- Session B, PREVENTS the non-repeatable read (start this first) ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE Id = 2;   -- first read: 2000
WAITFOR DELAY '00:00:06';
SELECT Balance FROM Accounts WHERE Id = 2;   -- second read: still 2000
COMMIT;
GO

-- --- Session A, run ~2s after Session B starts (same UPDATE as above) ---
USE IsolationDemo;
UPDATE Accounts SET Balance = 2500 WHERE Id = 2;
-- Observed: this UPDATE now BLOCKS for ~4s until Session B's transaction
-- commits and releases its shared lock - REPEATABLE READ holds a read lock on
-- an already-read row for the whole transaction, so the second read matches
-- the first.
GO

-- ============================================================================
-- 3) PHANTOM READ
-- Session B re-runs the same range COUNT twice inside one transaction, with a
-- 6s gap, while Session A inserts a new row matching the range predicate.
-- ============================================================================

-- --- Session B, REPRODUCES the phantom read (start this first) ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT COUNT(*) FROM Accounts WHERE Balance > 1500;   -- first count: 1
WAITFOR DELAY '00:00:06';
SELECT COUNT(*) FROM Accounts WHERE Balance > 1500;   -- second count: 2 (phantom!)
COMMIT;
GO

-- --- Session A, run ~2s after Session B starts ---
USE IsolationDemo;
INSERT INTO Accounts (Id, Balance) VALUES (3, 5000);
-- Observed: commits instantly, unblocked - REPEATABLE READ only locks rows it
-- has already read, not the "gap" a new row can be inserted into, so a
-- brand-new matching row is invisible to A but shows up in B's second count.
GO

-- Reset before the prevention run: DELETE FROM Accounts WHERE Id = 3;

-- --- Session B, PREVENTS the phantom read (start this first) ---
USE IsolationDemo;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
SELECT COUNT(*) FROM Accounts WHERE Balance > 1500;   -- first count: 1
WAITFOR DELAY '00:00:06';
SELECT COUNT(*) FROM Accounts WHERE Balance > 1500;   -- second count: still 1
COMMIT;
GO

-- --- Session A, run ~2s after Session B starts (same INSERT as above) ---
USE IsolationDemo;
INSERT INTO Accounts (Id, Balance) VALUES (3, 5000);
-- Observed: this INSERT now BLOCKS for ~4s until Session B's transaction
-- commits - SERIALIZABLE takes a range/key-range lock covering the predicate,
-- so no new row can be inserted into the range Session B already scanned.
GO

-- ============================================================================
-- SUMMARY
--   Anomaly                Occurs at            Prevented at
--   Dirty read             READ UNCOMMITTED     READ COMMITTED
--   Non-repeatable read    READ COMMITTED       REPEATABLE READ
--   Phantom read           REPEATABLE READ      SERIALIZABLE
-- Each level prevents everything the level below it allows, at the cost of
-- holding locks longer - every "prevent" case above cost several real
-- seconds of blocking that the "reproduce" case didn't.
-- ============================================================================
