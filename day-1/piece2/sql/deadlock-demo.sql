-- Day 9: reproduce a classic two-resource deadlock across two sessions,
-- capture the deadlock graph (Extended Events + trace flag 1222 as a
-- cross-check), then fix it with consistent lock ordering.
--
-- Each "Session A" / "Session B" pair below is meant to run in two separate
-- sqlcmd/SSMS sessions at the same time, not as a single script - start
-- Session A first, then Session B ~1s later. Real observed timestamps/results
-- from running this against a live SQL Server 2022 container are noted
-- inline as comments.

-- ============================================================================
-- SETUP
-- ============================================================================
CREATE DATABASE DeadlockDemo;
GO
USE DeadlockDemo;
GO
CREATE TABLE Resources (Id INT PRIMARY KEY, Value INT NOT NULL);
INSERT INTO Resources (Id, Value) VALUES (1, 100), (2, 200);
GO

-- Trace flag 1222: writes deadlock graphs to the SQL Server error log.
DBCC TRACEON(1222, -1);
GO

-- Extended Events session capturing the deadlock report XML (the modern,
-- preferred way to capture it).
CREATE EVENT SESSION CaptureDeadlocks ON SERVER
ADD EVENT sqlserver.xml_deadlock_report
ADD TARGET package0.ring_buffer
WITH (MAX_DISPATCH_LATENCY = 1 SECONDS);
GO
ALTER EVENT SESSION CaptureDeadlocks ON SERVER STATE = START;
GO

-- ============================================================================
-- DEADLOCK: opposite lock ordering (A: 1 then 2, B: 2 then 1)
-- ============================================================================

-- --- Session A ---
USE DeadlockDemo;
BEGIN TRAN;
UPDATE Resources SET Value = Value + 1 WHERE Id = 1;
WAITFOR DELAY '00:00:03';
UPDATE Resources SET Value = Value + 1 WHERE Id = 2;
COMMIT;
GO

-- --- Session B, run ~1s after Session A starts ---
USE DeadlockDemo;
BEGIN TRAN;
UPDATE Resources SET Value = Value + 1 WHERE Id = 2;
WAITFOR DELAY '00:00:03';
UPDATE Resources SET Value = Value + 1 WHERE Id = 1;
COMMIT;
GO

-- Observed: A locks Id=1 (04:56:29.82), B locks Id=2 (04:56:30.77). A tries
-- Id=2 at 04:56:32.83 and blocks (B holds it); B tries Id=1 at 04:56:33.77 and
-- blocks (A holds it) - a circular wait. SQL Server's deadlock monitor kills
-- one:
--   Msg 1205, Level 13, State 51
--   Transaction (Process ID 78) was deadlocked on lock resources with
--   another process and has been chosen as the deadlock victim. Rerun the
--   transaction.
-- B's blocked update then succeeds at 04:56:38.88 once A's rollback releases
-- the lock. Final state after: Id=1=101, Id=2=201 (only B's transaction
-- committed; A's was fully rolled back, including its earlier Id=1 update).

-- Captured deadlock graph (both mechanisms agree, same timestamp ~04:56:38.87):
--
-- Extended Events (query the ring buffer):
--   SELECT CAST(target_data AS XML)
--   FROM sys.dm_xe_session_targets xt
--   JOIN sys.dm_xe_sessions xs ON xs.address = xt.event_session_address
--   WHERE xs.name = 'CaptureDeadlocks';
--
-- Key excerpt from the real captured XML:
--   <victim-list><victimProcess id="processe806ad088"/></victim-list>
--   ... spid="78" ... waitresource="KEY: 5:72057594045726720 (61a06abd401c)"
--   ... spid="82" ... waitresource="KEY: 5:72057594045726720 (8194443284a0)"
--   <resource-list>
--     <keylock ... indexname="PK__Resource..." ...>
--       <owner-list><owner id="processe804128c8" mode="X"/></owner-list>
--       <waiter-list><waiter id="processe806ad088" mode="X"/></waiter-list>
--     </keylock>
--     <keylock ... indexname="PK__Resource..." ...>
--       <owner-list><owner id="processe806ad088" mode="X"/></owner-list>
--       <waiter-list><waiter id="processe804128c8" mode="X"/></waiter-list>
--     </keylock>
--   </resource-list>
-- i.e. two KEY locks on the same PK index, each process owning one and
-- waiting on the other's - the textbook circular wait, made explicit.
--
-- Trace flag 1222 cross-check (SQL Server error log):
--   EXEC sp_readerrorlog 0, 1, N'deadlock-list';
-- returned a matching deadlock-list entry at the same timestamp.

-- ============================================================================
-- FIX: consistent lock ordering - both sessions now touch Id=1 before Id=2.
-- Reset first: UPDATE Resources SET Value = 100 WHERE Id = 1;
--              UPDATE Resources SET Value = 200 WHERE Id = 2;
-- ============================================================================

-- --- Session A (unchanged) ---
USE DeadlockDemo;
BEGIN TRAN;
UPDATE Resources SET Value = Value + 1 WHERE Id = 1;
WAITFOR DELAY '00:00:03';
UPDATE Resources SET Value = Value + 1 WHERE Id = 2;
COMMIT;
GO

-- --- Session B, run ~1s after Session A starts - NOW locks Id=1 first too ---
USE DeadlockDemo;
BEGIN TRAN;
UPDATE Resources SET Value = Value + 1 WHERE Id = 1;
WAITFOR DELAY '00:00:03';
UPDATE Resources SET Value = Value + 1 WHERE Id = 2;
COMMIT;
GO

-- Observed: no deadlock. A locks Id=1 at 05:01:52.58, holds it through its
-- work, commits at 05:01:55.58. B attempts Id=1 at 05:01:53.56 and genuinely
-- BLOCKS (not deadlocks) until A commits - B's next step doesn't run until
-- 05:01:58.59, exactly A's commit time (05:01:55.58) plus B's own 3s delay.
-- No Msg 1205 in either session. Final state: Id=1=102, Id=2=202 - both
-- transactions fully committed this time. Once every session acquires
-- resources in the same order, no cycle is possible - the second session
-- just queues behind the first instead of both waiting on each other.

-- ============================================================================
-- CLEANUP
-- ============================================================================
-- ALTER EVENT SESSION CaptureDeadlocks ON SERVER STATE = STOP;
-- DROP EVENT SESSION CaptureDeadlocks ON SERVER;
-- DBCC TRACEOFF(1222, -1);
