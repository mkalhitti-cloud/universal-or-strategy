# Deferred Backlog — PTT-REPAIRS-12-SKIP-REMOVAL

**Epic:** PTT-REPAIRS-12-SKIP-REMOVAL
**Phase 5 author:** PTT Plan Reviewer
**Source blocks reviewed:** PTT-REPAIRS-11-BINDING-FLAGS-03 (prior block, via 06-deferred-backlog.md), PTT-REPAIRS-12-SKIP-REMOVAL (full Phase 5)

---

## Block: PTT-REPAIRS-12-SKIP-REMOVAL

### Items Closed This Block

| ID | Description | Priority | Closed By | Status |
|----|-------------|----------|-----------|--------|
| DW-09-04 | Remove all remaining obfuscation-Skip annotations (~137 tests across `CopyEngineTests.cs`). All 137 targeted annotations processed: 133 converted to active `[Fact]` (passing); 4 legitimately rolled back (SelectBeRefPriceByDirection_* tests — runtime obfuscation rename confirmed still active). HARD REQUIREMENT Failed=0 satisfied. | P2 | T1+T2+T3+T4 (PTT-REPAIRS-12-SKIP-REMOVAL) | **CLOSED** |

### Items Deferred This Block

4 new deferred items introduced by T2 rollbacks. All 4 are `SelectBeRefPriceByDirection_*` tests in `BwaveCycT1R1BeHelperTests`. Root cause: `GetMethod("SelectBeRefPriceByDirection")` returns null at runtime — AgileDotNetRT obfuscation has renamed this method and the new name is unknown. Resolution requires investigating the obfuscation-renamed symbol name (e.g., via `typeof(CopyEngine).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)` enumeration or inspection of the obfuscated assembly).

| ID | Description | Priority | Target Block | Status |
|----|-------------|----------|--------------|--------|
| DW-12-02-1 | `SelectBeRefPriceByDirection_ShouldReturnBid_WhenLongAndBidIsPositive` (L6505, `BwaveCycT1R1BeHelperTests`) — fails after obfuscation-skip removal; `GetMethod("SelectBeRefPriceByDirection")` returns null; method has been obfuscation-renamed. Still annotated `[Fact(Skip = "obfuscation:...")]`. | P2 | future | OPEN |
| DW-12-02-2 | `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenLongAndBidIsZero` (L6515, `BwaveCycT1R1BeHelperTests`) — same root cause as DW-12-02-1. Still annotated `[Fact(Skip = "obfuscation:...")]`. | P2 | future | OPEN |
| DW-12-02-3 | `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenShortAndAskIsPositive` (L6525, `BwaveCycT1R1BeHelperTests`) — same root cause as DW-12-02-1. Still annotated `[Fact(Skip = "obfuscation:...")]`. | P2 | future | OPEN |
| DW-12-02-4 | `SelectBeRefPriceByDirection_ShouldReturnBid_WhenShortAndAskIsZero` (L6535, `BwaveCycT1R1BeHelperTests`) — same root cause as DW-12-02-1. Still annotated `[Fact(Skip = "obfuscation:...")]`. | P2 | future | OPEN |

---

## Block: PTT-REPAIRS-11-BINDING-FLAGS-03 (Prior Block — Carry-Forward)

### Items Closed in PTT-REPAIRS-11-BINDING-FLAGS-03

| ID | Description | Priority | Closed By | Status |
|----|-------------|----------|-----------|--------|
| DW-09-03 | Remove incorrect obfuscation Skip from `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` in `BwaveCycTaR2HelperTests`. Fix root cause: `BindingFlags.Instance` cannot resolve `internal static` method — changed to `BindingFlags.Static` via new `GetStaticMethod` helper. | P1 | T1 (PTT-REPAIRS-11-BINDING-FLAGS-03) | **CLOSED** |

---

## Block: PTT-REPAIRS-11-BINDING-FLAGS-02 (Prior-Prior Block — Carry-Forward)

### Items Closed in PTT-REPAIRS-11-BINDING-FLAGS-02

| ID | Description | Priority | Closed By | Status |
|----|-------------|----------|-----------|--------|
| DW-09-02 | Fix `LogBeSlotEviction` binding flags in `BwaveCycTaR3HelperTests`; remove 2 obfuscation Skips. | P1 | T1 (PTT-REPAIRS-11-BINDING-FLAGS-02) | **CLOSED** |

---

## Persistent Open Items (Unrelated to Repair Series)

| ID | Description | Priority | Target Block | Status |
|----|-------------|----------|--------------|--------|
| DW-BWAVE-01 | Production logic for the ~70 stubs in `BwaveCycT*` helper classes — separate feature epic, not a repair. Stubs are tested for existence and parameter signatures only; actual implementation deferred to feature phase. | P3 | future | OPEN |

---

## Master Open Backlog (All Blocks Combined)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-12-02-1 | `SelectBeRefPriceByDirection_ShouldReturnBid_WhenLongAndBidIsPositive` (L6505) — obfuscation-renamed method; investigate AgileDotNetRT renamed symbol | P2 | future | OPEN |
| DW-12-02-2 | `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenLongAndBidIsZero` (L6515) — same root cause as DW-12-02-1 | P2 | future | OPEN |
| DW-12-02-3 | `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenShortAndAskIsPositive` (L6525) — same root cause as DW-12-02-1 | P2 | future | OPEN |
| DW-12-02-4 | `SelectBeRefPriceByDirection_ShouldReturnBid_WhenShortAndAskIsZero` (L6535) — same root cause as DW-12-02-1 | P2 | future | OPEN |
| DW-BWAVE-01 | Production logic for ~70 BwaveCycT* stubs — feature epic, not a repair | P3 | future | OPEN |

---

## Closed Items Registry (All Blocks)

| ID | Closed By | Epic |
|----|-----------|------|
| DW-09-02 | T1 | PTT-REPAIRS-11-BINDING-FLAGS-02 |
| DW-09-03 | T1 | PTT-REPAIRS-11-BINDING-FLAGS-03 |
| DW-09-04 | T1+T2+T3+T4 | PTT-REPAIRS-12-SKIP-REMOVAL |

---

*Last updated: PTT-REPAIRS-12-SKIP-REMOVAL Phase 5*
*Authored by PTT Plan Reviewer*
