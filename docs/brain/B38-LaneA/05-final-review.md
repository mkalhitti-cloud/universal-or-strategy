# Final Review: B38-LaneA — Trim/Flatten Anchor Fix + BE-Stop TIF Fix

**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-28
**Build Tag Reviewed**: `PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28`
**Verdict**: FINAL_PASS

---

## A. Inputs Reviewed

| Artifact | Status |
|----------|--------|
| `docs/brain/B38-LaneA/02-architecture-plan.md` | READ |
| `docs/brain/B38-LaneA/04-ticket-review.md` | READ |
| `docs/brain/B38-LaneA/ticket-1-completion.md` | READ |
| `docs/brain/B38-LaneA/ticket-1-verification.md` | READ |
| `specs/002-trade-copier-spec.html` section-b38 (via plan §11 traceability) | READ (via plan) |
| `docs/standards/jane-street/RULES_CATALOG.md` (applied at plan review) | APPLIED |

---

## B. Cross-File Coherence Check

### B1 — PttTrim ↔ PttFlatten: Anchor formula parity (parallel helpers must stay in sync)

| File | Location | Formula | Coherent? |
|------|----------|---------|-----------|
| `PttTrim.cs` | Lines 97-98 | `ask - buffer * tickSize` (Long) / `bid + buffer * tickSize` (Short) | ✅ |
| `PttFlatten.cs` | Lines 94-95 | `ask - buffer * tickSize` (Long) / `bid + buffer * tickSize` (Short) | ✅ |

**Evidence**: SCAN-05 (Layer 3) confirmed PttTrim.cs:97-98 directly. Verifier Step 1 file-by-file check confirmed PttFlatten.cs independently as "identical pattern to PttTrim.cs." Parallel helpers are in sync.

**Result**: COHERENT — no drift between PttTrim and PttFlatten.

---

### B2 — PttTrim ↔ PttFlatten: Guard parity

| File | Location | Guard | Coherent? |
|------|----------|-------|-----------|
| `PttTrim.cs` | Line 85 | `tickSize > 0.0 && (isLong ? ask > 0.0 : bid > 0.0)` — no `buffer > 0 &&` | ✅ |
| `PttFlatten.cs` | Line 82 | Identical guard pattern, `buffer > 0 &&` absent | ✅ |

**Evidence**: SCAN-06 (Layer 3): 0 hits for `buffer > 0` in PttTrim.cs. Verifier Step 1 confirmed PttFlatten.cs guard independently.

**Result**: COHERENT.

---

### B3 — PttBreakEven: All 3 BE-stop sites use Gtc

| Method | Location | TIF | Coherent? |
|--------|----------|-----|-----------|
| `SubmitBeStopLocal` | PttBreakEven.cs:179 | `TimeInForce.Gtc` | ✅ |
| `SubmitBeTargetsLocal` (bare stop) | PttBreakEven.cs:317 | `TimeInForce.Gtc` | ✅ |
| `SubmitBeTargetsLocal` (per-pair loop) | PttBreakEven.cs:350 | `TimeInForce.Gtc` | ✅ |

**Evidence**: Verifier Step 1 confirmed all 3 sites with "0 hits in file" for TimeInForce.Day. SCAN-04 (Layer 3) confirmed 0 hits in PttBreakEven.cs.

**Result**: COHERENT — all 3 PttBreakEven stop-submission sites uniform.

---

### B4 — CopyEngine.SubmitBeStop: 2 sites consistent with PttBreakEven pattern

| Method | Location | TIF | Coherent? |
|--------|----------|-----|-----------|
| `SubmitBeStop` (bare stop) | CopyEngine.cs:1597 | `TimeInForce.Gtc` | ✅ |
| `SubmitBeStop` (per-pair loop) | CopyEngine.cs:1636 | `TimeInForce.Gtc` | ✅ |

**Evidence**: Verifier Step 1 confirmed both lines. SCAN-04 (Layer 3) confirmed 0 hits in CopyEngine.cs.
Mirror pattern to PttBreakEven (bare-stop + per-pair-loop) correctly reproduced.

**Result**: COHERENT — CopyEngine follower path mirrors PttBreakEven leader path.

---

### B5 — No TimeInForce.Day in any exit/stop order path

SCAN-04 (Layer 3) full PropTraderTools scan confirmed:
- `PttTrim.cs`: 0 hits
- `PttFlatten.cs`: 0 hits
- `PttBreakEven.cs`: 0 hits
- `CopyEngine.cs`: 0 hits
- `CopyEngineTests.cs`: hits are string literals inside `Assert.DoesNotContain()` — not executable code
- `TradeCopierPanel.cs:1397`: pre-existing PTT-Click **entry** order — intentionally Day; outside B38 scope

**Result**: CLEAN — no TimeInForce.Day remaining in any exit or stop order path.

---

### B6 — Build Tag

[`CopyEngine.cs:41`](src/PropTraderTools/CopyEngine.cs:41): `"PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28"` confirmed by verifier.

**Result**: CORRECT.

---

## C. Spec Requirement Coverage Matrix

| Defect ID | Spec Requirement | Files Changed | Satisfied? |
|-----------|-----------------|---------------|------------|
| DW-B32-TRIM-ANCHOR-01 | Long `ask - buf*tick` / Short `bid + buf*tick` in PttTrim + PttFlatten | PttTrim.cs:97-98, PttFlatten.cs:94-95 | ✅ SATISFIED |
| DW-B32-TRIM-TIF-01 | `TimeInForce.Gtc` in PttTrim + PttFlatten | PttTrim.cs:115, PttFlatten.cs:112 | ✅ SATISFIED |
| DW-B32-TRIM-MARKET-01 | Remove `buffer > 0 &&` guard so buffer=0 submits Limit | PttTrim.cs:85, PttFlatten.cs:82 | ✅ SATISFIED |
| DW-B38-STOP-TIF-01 | `TimeInForce.Gtc` in PttBreakEven (3 sites) + CopyEngine (2 sites) | PttBreakEven.cs:179/317/350, CopyEngine.cs:1597/1636 | ✅ SATISFIED |
| section-b38/build-tag | Build tag slug "trim-anchor-be-tif" | CopyEngine.cs:41 | ✅ SATISFIED |
| section-b38/tests | 6 new [Fact] methods, count 188 → 194 | CopyEngineTests.cs | ✅ SATISFIED |

**All 4 defects and 2 supporting requirements: 100% satisfied.**

---

## D. 7-Scan Results (Layer 3 Independent Verification)

Source: `ticket-1-verification.md` Step 2 (Layer 3 independent re-run, not engineer self-report).

| Scan | Command Target | Result | Status |
|------|---------------|--------|--------|
| SCAN-01 lock() | Full PropTraderTools `*.cs` | 0 actual lock statements; 3 comment-only hits | PASS (JS-021) |
| SCAN-02 async void | Full PropTraderTools `*.cs` | 0 hits | PASS (JS-033) |
| SCAN-03 return null | PttTrim.cs, PttFlatten.cs, PttBreakEven.cs | 6 hits — FindPositionLocal only (NT8-050 exemption) | PASS (JS-002) |
| SCAN-04 TimeInForce.Day | Full PropTraderTools `*.cs` | 0 in B38 files; TradeCopierPanel:1397 pre-existing entry (intentional) | PASS |
| SCAN-05 anchor formula | PttTrim.cs lines 97-98 | `ask - buffer * tickSize` / `bid + buffer * tickSize` | PASS |
| SCAN-06 guard | PttTrim.cs `buffer > 0` | 0 hits | PASS |
| SCAN-07 [Fact] count | CopyEngineTests.cs | 194 (was 188 + 6) | PASS |

**All 7 scans: ZERO violations in scope. Layer 2/Layer 3 cross-check: no discrepancies.**

---

## E. Jane Street DNA Final Compliance

| Rule ID | Rule | Check | Result |
|---------|------|-------|--------|
| JS-021 | No `lock()` anywhere in src/ | SCAN-01: 0 actual lock statements | PASS |
| JS-033 | No `async void` (non-event-handler) | SCAN-02: 0 hits | PASS |
| JS-001 | No `throw new XxxException` in hot paths | No throws added in any modified method | PASS |
| JS-002 | No `return null` for missing values | SCAN-03: FindPositionLocal only (NT8-050 exemption) | PASS |
| JS-008 | No mutable struct fields | No structs introduced or modified | PASS |
| JS-009 | No Dictionary for shared/thread-touched collections | No collections introduced | PASS |
| JS-010 | No public constructors on singleton/signal | No new constructors | PASS |
| JS-023 | UI updates from off-thread via Dispatcher.InvokeAsync | All methods synchronous on NT8 dispatch thread | PASS |

---

## F. NT8 API Constraint Compliance

| Rule | Constraint | B38 Result |
|------|-----------|------------|
| NT8-049 | CreateOrder arg6=limitPrice, arg7=stopPrice — never swap | PASS — only value of limitPrice changes, positions unchanged |
| NT8-007 | arg11 = `(NinjaTrader.Cbi.CustomOrder)null` | PASS — preserved |
| NT8-013 | arg10 = `DateTime.MaxValue` | PASS — preserved |
| NT8-014 | Signal names `PTT-Trim`, `PTT-Flatten`, `PTT-BE-Stop` | PASS — unchanged |
| NT8-050 | `FindPositionLocal` foreach pattern | PASS — unchanged |
| NT8-006 | No LINQ | PASS — none introduced |

---

## G. Cyclomatic Complexity Compliance

| Method | Post-B38 CYC | Jane Street Threshold | Result |
|--------|-------------|----------------------|--------|
| `TrimPositionLocal` | 5 | ≤ 8 | PASS |
| `FlattenPositionLocal` | 5 | ≤ 8 | PASS |
| `SubmitBeStopLocal` | 3 | ≤ 8 | PASS |
| `SubmitBeTargetsLocal` | unchanged | ≤ 8 | PASS |
| `SubmitBeStop` (CopyEngine) | unchanged | ≤ 8 | PASS |
| 6 new [Fact] methods | CYC=1 each | ≤ 8 | PASS |

No method exceeds CYC=8. Jane Street strict standard maintained.

---

## H. Hard-Link Sync Verification

`scripts\verify_links.ps1 -Fix` output (from verification):

```
OK      : 11
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 1  (CopyEngineTests.cs — test file, not deployed to NT8)
```

All 5 B38-modified deployable source files (PttTrim.cs, PttFlatten.cs, PttBreakEven.cs, CopyEngine.cs + unchanged TradeCopierPanel.cs) are hard-linked and in sync with the NinjaTrader deploy path.

---

## I. Scope Creep Check (V12.23)

B38 touched exactly the files specified in the architecture plan:
- `PttTrim.cs` — 3 changes (guard, anchor, TIF)
- `PttFlatten.cs` — 3 changes (guard, anchor, TIF)
- `PttBreakEven.cs` — 3 TIF changes + 1 comment update
- `CopyEngine.cs` — 2 TIF changes + 1 build tag
- `CopyEngineTests.cs` — 6 new [Fact] methods appended

No changes to: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `PttContracts.cs`, `IPttModule`, `Execute()`, `FindPositionLocal()`, `PttBus`, follower fan-out logic.

**Result**: No scope creep. V12.23 compliant.

---

## J. VERIFY_PASS Confirmation

`ticket-1-verification.md` verdict: **VERIFY_PASS**

Layer 2 vs Layer 3 cross-check: no discrepancies across all 7 scans.
File-by-file Step 1 verification: all 12 line-level checks passed.
verify_links.ps1: OK=11, DESYNC=0.

---

## K. Deferred Work (Section K — REQUIRED)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B32-TRIM-ANCHOR-01 | Anchor direction fix in PttTrim + PttFlatten (Long=ask-buf\*tick, Short=bid+buf\*tick) | P1 | B38-LaneA | CLOSED |
| DW-B32-TRIM-TIF-01 | TimeInForce.Gtc in PttTrim + PttFlatten | P1 | B38-LaneA | CLOSED |
| DW-B32-TRIM-MARKET-01 | Guard: remove `buffer > 0 &&` in PttTrim + PttFlatten | P1 | B38-LaneA | CLOSED |
| DW-B38-STOP-TIF-01 | TimeInForce.Gtc in PttBreakEven (3 sites) + CopyEngine (2 sites) | P1 | B38-LaneA | CLOSED |
| DW-B38-OOS-01 | TradeCopierPanel.cs:1397 `TimeInForce.Day` (PTT-Click **entry** order) — intentionally not changed in B38; entry orders are not exit/stop orders and session-scoped Day TIF is correct semantics for manual entry. Deferred until spec explicitly requires change. | P2 | future | OPEN |

**Notes on DW-B38-OOS-01**:
- `TradeCopierPanel.cs:1397` is a PTT-Click **entry** (opening) order, not an exit or stop.
- `TimeInForce.Day` is the correct and intentional TIF for a manually triggered entry order — it should not persist past session end.
- This line was explicitly identified as pre-existing and out-of-scope by both the engineer and verifier.
- It is recorded here for traceability, not because it is a defect in its current context.
- No action required unless a future spec change addresses entry order TIF policy.

**Other observations** — None. No additional deferred work identified. All B38 spec requirements are fully satisfied.

---

## L. Final Verdict

| Check | Result |
|-------|--------|
| Cross-file coherence (PttTrim ↔ PttFlatten anchor, guard) | PASS |
| Cross-file coherence (PttBreakEven 3 sites, CopyEngine 2 sites all Gtc) | PASS |
| No TimeInForce.Day in exit/stop order paths | PASS |
| 4 spec requirements satisfied | PASS |
| 7 scans all zero (Layer 3 independent) | PASS |
| Jane Street DNA (JS-021/033/001/002) | PASS |
| NT8 API constraints (NT8-049/007/013/014/050/006) | PASS |
| CYC <= 8 all methods | PASS |
| Hard-link sync (DESYNC=0) | PASS |
| Build tag CopyEngine.cs:41 | PASS |
| Scope creep gate V12.23 | PASS |
| VERIFY_PASS from ticket-1-verification.md | CONFIRMED |
| Section K (deferred work) | WRITTEN |
| 06-deferred-backlog.md | WRITTEN |

## FINAL_PASS
