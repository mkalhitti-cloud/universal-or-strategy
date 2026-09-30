# B126 Deferred Backlog

Block: B126
Date: 2026-08-29
Status: PIPELINE_COMPLETE

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefix constantification | B126-T1 |

---

## Items Carried Forward (Not In Scope — Deferred)

| Item | Description | Priority | Source Block |
|------|-------------|----------|--------------|
| DW-B58-02 | GlobalBe non-atomic lazy-init | P2 | B58/B126 |
| DW-B58-03 | RelayBe OcoGroup non-forwarding | P2 | B58/B126 |
| DW-B107 | MoveStopToBreakEven Step A stale PTT-BE-Target-* on followers | P2 | B107 |

---

## New Items Discovered This Block

| Item | Description | Priority | Notes |
|------|-------------|----------|-------|
| DW-B126-01 | Remaining raw "PTT-BE-Target-" string literals in PttBreakEven.cs (line 593), PttGlobalQuickExit.cs (lines 377, 588), and CopyEngine.cs (lines 1257, 3601) should be replaced with PttOrderNames.PttBeTargetPrefix. 5 occurrences across 3 files. Confirmed by grep during B126 final review. | P3 | Low urgency. No behavior risk (values are identical). Completes the constantification intent of DW-B58-01. PttOrderNames.PttBeTargetPrefix is now defined and available in PttContracts.cs. |
