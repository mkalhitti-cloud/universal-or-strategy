# PTT-REPAIRS-11-BINDING-FLAGS-02 — Deferred Backlog

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-02
**Block date:** 2025-07-30
**Written by:** ptt-plan-reviewer (Phase 5)
**Wave workspace:** `C:\WSGTA\universal-or-strategy\`

---

## Block PTT-REPAIRS-11-BINDING-FLAGS-02

### Items Closed This Block

| ID | Description | Resolution | Closed By |
|----|-------------|------------|-----------|
| DW-09-02 | Fix `LogBeSlotEviction` binding flags in `BwaveCycTaR3HelperTests`: add `GetStaticMethod` helper (`NonPublic\|Static`); remove `[Fact(Skip)]` from 2 affected tests. | Both tests pass: `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` and `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` PASSED. Test result: 26/0/488/514. All 7 scans zero. Build 0 errors. | T1 — PTT-REPAIRS-11-BINDING-FLAGS-02 |

### Items Carried Forward (OPEN)

| ID | Description | Priority | Prerequisite(s) | Target Block |
|----|-------------|----------|-----------------|--------------|
| DW-09-03 | Fix `GetSenderAccountName` binding flags in `BwaveCycTaR2HelperTests`: change `BindingFlags.NonPublic \| BindingFlags.Instance` to `BindingFlags.NonPublic \| BindingFlags.Static`. Remove obfuscation `[Fact(Skip)]` from 1 affected test. `ObfuscationAttribute` already present on the production method. | P1 | DW-09-02 CLOSED (satisfied) | PTT-REPAIRS-11-BINDING-FLAGS-03 or next repair epic |
| DW-09-04 | Remove all 137 remaining obfuscation-skip `[Fact(Skip)]` annotations from `CopyEngineTests.cs`. Each method must have: (a) production logic implemented, (b) binding flags verified correct, (c) `ObfuscationAttribute` confirmed present. Batch removal after all per-method prerequisites satisfied. | P2 | DW-09-02 CLOSED (satisfied); DW-09-03 CLOSED (outstanding) | future |

### New Items Discovered This Block

None. Primary path succeeded cleanly. Fallback path (DW-09-02-BLOCKED) was not triggered.

---

## Backlog Snapshot

| ID | Status | Priority | Target |
|----|--------|----------|--------|
| DW-09-02 | CLOSED (PTT-REPAIRS-11-BINDING-FLAGS-02) | P0 | — |
| DW-09-03 | OPEN | P1 | PTT-REPAIRS-11-BINDING-FLAGS-03 |
| DW-09-04 | OPEN | P2 | future |

---

*Artifact: `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-02/06-deferred-backlog.md`*
*Epic: PTT-REPAIRS-11-BINDING-FLAGS-02*
