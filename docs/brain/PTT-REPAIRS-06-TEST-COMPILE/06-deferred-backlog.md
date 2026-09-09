# PTT Deferred Backlog — PTT-REPAIRS-06-TEST-COMPILE block

## Block: PTT-REPAIRS-06-TEST-COMPILE (added 2025-07-22)

### DW-REPAIRS-06-01
- Description: EvictDedup CYC=13 refactoring (JS-013 violation, extraction required)
- File: src/PropTraderTools/CopyEngine.cs
- Carried from: DW-REPAIRS-04-BUG-E-04
- Priority: HIGH (JS-013 violation in production code)
- Status: OPEN

### DW-REPAIRS-06-02
- Description: Stale CYC comments at CopyEngine.cs lines 727, 738
- File: src/PropTraderTools/CopyEngine.cs
- Carried from: DW-BUG-F-04
- Priority: LOW (cosmetic)
- Status: OPEN

### DW-REPAIRS-06-03
- Description: 449 NT8-runtime test failures require NT8 mock/stub harness
- File: src/PropTraderTools/CopyEngineTests.cs
- Impact: 449 tests currently skipped/failing due to missing NT8 host in test runner
- Proposed resolution: PTT-REPAIRS-07-NT8-STUB epic — add NT8 mock harness or bulk Skip
- Priority: MEDIUM
- Status: OPEN

### DW-REPAIRS-06-04
- Description: Add EvictDedup_CancelledEntry_ClearsLastLeaderDirection [Fact] test
- File: src/PropTraderTools/CopyEngineTests.cs
- Context: Test runner now operational; this test validates CancelledEntry dedup logic
- Priority: MEDIUM
- Status: OPEN
