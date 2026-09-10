# Deferred Backlog — PTT-REPAIRS-11-BINDING-FLAGS-03

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-03  
**Phase 5 author:** PTT Plan Reviewer  
**Source blocks reviewed:** PTT-REPAIRS-11-BINDING-FLAGS-02 (via ticket-1-completion.md), PTT-REPAIRS-11-BINDING-FLAGS-03 (full Phase 5)

---

## Block: PTT-REPAIRS-11-BINDING-FLAGS-03

### Items Closed This Block

| ID | Description | Priority | Closed By | Status |
|----|-------------|----------|-----------|--------|
| DW-09-03 | Remove incorrect obfuscation Skip from `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` in `BwaveCycTaR2HelperTests`. Fix root cause: `BindingFlags.Instance` cannot resolve `internal static` method — changed to `BindingFlags.Static` via new `GetStaticMethod` helper. | P1 | T1 (PTT-REPAIRS-11-BINDING-FLAGS-03) | **CLOSED** |

### Items Deferred This Block

None. Contingency NOT triggered. No new deferred items introduced by this epic.

---

## Block: PTT-REPAIRS-11-BINDING-FLAGS-02 (Prior Block — Carry-Forward)

> Note: PTT-REPAIRS-11-BINDING-FLAGS-02 did not produce a `06-deferred-backlog.md` (epic did not reach Phase 5). Open items are sourced from `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-02/ticket-1-completion.md` deferred items table.

### Items Closed in PTT-REPAIRS-11-BINDING-FLAGS-02

| ID | Description | Priority | Closed By | Status |
|----|-------------|----------|-----------|--------|
| DW-09-02 | Fix `LogBeSlotEviction` binding flags in `BwaveCycTaR3HelperTests`; remove 2 obfuscation Skips. | P1 | T1 (PTT-REPAIRS-11-BINDING-FLAGS-02) | **CLOSED** |

### Items Remaining Open (Carried Forward from PTT-REPAIRS-11-BINDING-FLAGS-02)

| ID | Description | Priority | Target Block | Status |
|----|-------------|----------|--------------|--------|
| DW-09-04 | Remove all remaining obfuscation-Skip annotations (≈137 tests across `CopyEngineTests.cs`). Systematic pass required: audit all `[Fact(Skip = "obfuscation: ...")]` annotations; apply `BindingFlags.NonPublic \| BindingFlags.Static` pattern where the target method is `internal static`; apply `BindingFlags.NonPublic \| BindingFlags.Instance` where target is `internal` non-static. | P2 | future | **OPEN** |

---

## Master Open Backlog (All Blocks Combined)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-09-04 | Remove all remaining obfuscation-Skip annotations (≈137 tests) in `CopyEngineTests.cs` — systematic BindingFlags audit | P2 | future | OPEN |

---

*Last updated: PTT-REPAIRS-11-BINDING-FLAGS-03 Phase 5*
