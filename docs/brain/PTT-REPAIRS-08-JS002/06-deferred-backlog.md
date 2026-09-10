# Deferred Backlog — PTT-REPAIRS-08-JS002

---

## Block: PTT-REPAIRS-08-JS002
Date: 2026-09-10
Epic: JS002 Method-Length Refactor — CopyEngine.cs

### Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-JS002-01 | Add 3 missing T2 xUnit tests to `CopyEngineTests.cs`: `FindLeaderCollateralOrder_NullAccount_ReturnsNull`, `FindPosition_NoMatch_ReturnsNull`, `IsFlat_NullPosition_ReturnsTrue` — all with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` and appropriate `Assert.Null` / `Assert.True` assertions | P0 | B5 (FINAL_FAIL resolution — this block) | OPEN |
| DW-JS002-02 | Add 2 missing T3 xUnit tests to `CopyEngineTests.cs`: `ResolveMultipliers_EmptyMultipliers_ReturnsNull` (Assert.Null — confirms empty guard returns null, not Array.Empty) and `ResolveMultipliers_ValidMultipliers_ReturnsArray` (Assert.NotNull + length check) — both with `[Fact(Skip = "NT8-runtime")]` | P0 | B5 (FINAL_FAIL resolution — this block) | OPEN |

### Notes

- `CopyEngine.cs` is **complete and correct**. All 14 code changes (5 `return default`
  substitutions + 7 return-type annotations + 1 variable annotation + 2 parameter annotations +
  1 caller annotation) are present and independently verified by ptt-verifier.

- The FINAL_FAIL is solely due to 5 missing tests in `CopyEngineTests.cs`. No `CopyEngine.cs`
  changes are required for FINAL_PASS.

- The T3 verifier explicitly deferred the 2 missing T3 tests to Phase 5 (ticket-3-verification.md
  Section 5 Note). The 3 missing T2 tests were not caught by the T2 verifier's retry pass
  (which focused only on the 2 tests that triggered its VERIFY_FAIL cycle). Both gaps are now
  formally captured here.

- Once DW-JS002-01 and DW-JS002-02 are implemented and verified (SCAN-4 dotnet build + SCAN-5
  dotnet test: 19 passed, no new regressions, +5 skipped vs current 430 baseline), this epic
  block may be re-submitted for FINAL_PASS.

- No lock() code calls, no JS rule violations, no NT8 API surface violations, and no ASCII
  compliance issues remain in the live source. Lock-free ConcurrentDictionary architecture
  is intact.

---

## Block: PTT-REPAIRS-08-JS002 (Retry — FINAL_PASS)
Date: 2026-09-10
Epic: JS002 Method-Length Refactor — CopyEngine.cs
Status: FINAL_PASS (supersedes prior FINAL_FAIL entry)

### Deferred Items

None — all items from prior block resolved.

| ID | Item | Resolution |
|----|------|------------|
| DW-JS002-01 | 3 missing T2 xUnit tests | CLOSED — all 3 added by fix session, present at L8256, L8267, L8278 in CopyEngineTests.cs |
| DW-JS002-02 | 2 missing T3 xUnit tests | CLOSED — both added by fix session, present at L8306, L8317 in CopyEngineTests.cs |

### Notes

- All 14 code changes in `CopyEngine.cs` were correct from the T1/T2/T3 engineer sessions.
  The FINAL_FAIL was solely due to 5 missing tests in `CopyEngineTests.cs`.
- The fix session added all 5 missing tests with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor
  requires NT8 host")]` attributes, consistent with the pre-existing test file pattern.
- All 7 scans (SCAN-1 through SCAN-7) passed in every session including the fix session.
- `CopyEngine.cs` lock-free architecture intact: 0 live lock() code calls confirmed by
  reviewer grep (71 hits all in compliance comment annotations).
- NT8-pattern `return null` sites in `ResolveNullFollowerSlot` (L5955, L5977) preserved
  verbatim per REQ-NT8-CONTRACT.
- No deferred items carry over to the next block. This epic is fully closed.
