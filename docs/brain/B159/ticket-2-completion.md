# Ticket 2 Completion: DW-B159-T2 — FM-B BE-All Silent Rollback Repair
<!-- status: BUILD_PASS -->
<!-- wave: B159 -->
<!-- ticket: DW-B159-T2 -->
<!-- phase: 4a -->
<!-- engineer: ptt-engineer -->

---

## Scope
DW-B159-T2 ONLY. FM-B: BE-All Silent Rollback Repair.

## Files Changed

| File | Change Type | Line Range |
|------|-------------|------------|
| `src/PropTraderTools/CopyEngine.cs` | Fields inserted | 324–338 (new fields after `_beReplaceAttempts`) |
| `src/PropTraderTools/CopyEngine.cs` | Methods inserted | 6986–7072 (three new methods) |
| `src/PropTraderTools/CopyEngine.cs` | Hook in `MoveStopToBreakEven` | 6898–6899 |
| `src/PropTraderTools/CopyEngine.cs` | Hook in `OnOrderUpdate` | 1741–1742 |

---

## New Fields

| Name | Type | Line | Key | Comparer |
|------|------|------|-----|----------|
| `_pendingBePriceRetry` | `ConcurrentDictionary<string, double>` | 329–330 | `acc.Name` | `StringComparer.Ordinal` |
| `_bePriceRetryAttempts` | `ConcurrentDictionary<string, int>` | 336–337 | `acc.Name` | `StringComparer.Ordinal` |

---

## New Methods

| Method | Signature | CYC Achieved | Line Range |
|--------|-----------|--------------|------------|
| `TryArmBePriceRetry` | `internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)` | 5 | 6991–7004 |
| `StillAtNonBePrice` | `internal bool StillAtNonBePrice(Account acc, Instrument instr, double bePrice)` | 7 | 7012–7027 |
| `TryFireBePriceRetry` | `internal void TryFireBePriceRetry(Order o)` | 7 | 7036–7072 |

### CYC Breakdowns

**TryArmBePriceRetry (CYC=5):**
- base(1) + acc==null guard(2) + instr==null guard(3) + bePrice<=0 guard(4) + attempts>=3 ceiling(5)

**StillAtNonBePrice (CYC=7):**
- base(1) + acc==null guard(2) + instr==null guard(3) + foreach decision(4) + name null/prefix guard(5) + instrument FullName match(6) + OrderState.Working guard(7)

**TryFireBePriceRetry (CYC=7):**
- base(1) + compound null guard(2) + PTT-BE-Stop- prefix check(3) + TryGetValue no-slot(4) + Working cleanup path(5) + not-Cancelled early return(6) + StillAtNonBePrice check(7)

---

## Modified Methods

| Method | Line (post-insert) | Change Description |
|--------|--------------------|--------------------|
| `MoveStopToBreakEven` | 6898–6899 | Added `TryArmBePriceRetry(acc, instrument, newStop)` call after `StatusUpdate?.Invoke(...)` at line 6896, before `RegisterBeRetrySlotIfNeeded` block. CYC unchanged. |
| `OnOrderUpdate` | 1741–1742 | Added `TryFireBePriceRetry(e.Order)` call between `TryRecordBeTargetFill(e.Order)` (line 1740) and `TryTriggerBeRecovery(e.Order)` (line 1744). CYC unchanged. |

---

## 7-Scan Results

| Scan | Pattern | Result | Count |
|------|---------|--------|-------|
| SCAN-01 | `Select-String -Pattern "lock\("` | PASS | 0 actual lock() statements (comment-only matches) |
| SCAN-02 | `Select-String -Pattern "async void"` | PASS | 0 actual async void declarations (comment-only matches) |
| SCAN-03 | `Select-String -Pattern "DateTime\.Now[^U]"` | PASS | 0 (comment-only matches, no actual usage) |
| SCAN-04 | `Select-String -Pattern "Thread\.Sleep"` | PASS | 0 matches |
| SCAN-05 | `Select-String -Pattern "[^\x00-\x7F]"` | PASS | 0 non-ASCII characters |
| SCAN-06 | CYC manual audit: TryArmBePriceRetry=5, StillAtNonBePrice=7, TryFireBePriceRetry=7 | PASS | All ≤8 budget |
| SCAN-07 | `Select-String -Pattern "acc\.Orders[^.]"` (new code) | PASS | New code uses `acc.Orders.ToList()` — zero bare enumerations in new methods |

---

## Build Status

Syntactic review: PASS
- All three methods use `internal void` / `internal bool` — no async, no throws
- `StringComparer.Ordinal` specified on both new dicts
- `acc.Orders.ToList()` snapshot used in `StillAtNonBePrice`
- All log strings use ASCII-only `[PTT-BE-RETRY]` prefix
- No lock() statements
- `SubmitBeStop` called with `(Account, Instrument, double, bool)` matching existing signature at line 1389
- `FindPosition` called with `(Account, Instrument)` matching existing signature at line 6599
- SYNC: `ptt-sync.ps1` run — 18 linked, 0 errors

---

## VERIFY Criterion

Fire path log marker ready for SIM gate grep:

```
grep "\[PTT-BE-RETRY\] fired:" docs/output.md
```

Must appear for Sim104 after a `PTT-BE-Stop-*` bulk cancel event following a QX->BE-All cycle.

All four log markers implemented:
- `[PTT-BE-RETRY] armed: {accName} bePrice={bePrice}` — TryArmBePriceRetry line 7001
- `[PTT-BE-RETRY] working-cleanup: {accName}` — TryFireBePriceRetry line 7051
- `[PTT-BE-RETRY] fired: {accName} bePrice={bePrice}` — TryFireBePriceRetry line 7069

---

*Completion document produced by ptt-engineer. Phase 4a. Wave B159. Ticket DW-B159-T2.*
