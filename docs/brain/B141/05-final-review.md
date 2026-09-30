# B141 Final Review — OCO Cascade Dual-Resubmit

**Block**: B141
**Phase**: 5 (Final Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-01
**Prior block**: B140-LaneA (reverted at fd4a439d — SIM Gate 1 FAIL)
**Ticket count**: 1 (T1, single-pipeline)
**Source**: `docs/brain/B141/02-architecture-plan.md` (REVIEW_PASS, Revision Cycle 1)

---

## Block Overview

B141 closes DW-B153 (OCO cascade on Stop1/Stop2 drag) via the dual-resubmit approach.
B140-LaneA's `acc.Change()` strategy was confirmed a silent no-op on ATM-owned Stop brackets
from AddOnBase context (SIM Gate 1 FAIL, commit fd4a439d reverted). B141 accepts the OCO cascade,
captures the linked target price before the cancel fires, and resubmits a standalone `PTT-TGT-Drag`
limit order at the captured price immediately after the cascade. The naked-position window is
eliminated by restoring the target order on every stop drag.

**Mandatory reads completed**:
- [02-architecture-plan.md](docs/brain/B141/02-architecture-plan.md): PLAN_COMPLETE (Revision Cycle 1)
- [04-ticket-review.md](docs/brain/B141/04-ticket-review.md): TICKET_REVIEW_PASS
- [ticket-1-completion.md](docs/brain/B141/ticket-1-completion.md): BUILD_PASS, 7/7 tests, 0 MISMATCH
- [ticket-1-verification.md](docs/brain/B141/ticket-1-verification.md): VERIFY_PASS, 0 violations
- [RULES_CATALOG.md](docs/standards/jane-street/RULES_CATALOG.md): Loaded and applied
- [B140-LaneA/06-deferred-backlog.md](docs/brain/B140-LaneA/06-deferred-backlog.md): Prior backlog loaded
- [CopyEngine.cs L2276-2540](src/PropTraderTools/CopyEngine.cs): Final state verified

---

## A. Coherence Check

### A.1 All 5 Code Changes Present in Source

Verification against `src/PropTraderTools/CopyEngine.cs` lines 2276-2499:

| Change | Expected | Source Location | Present? |
|--------|----------|-----------------|----------|
| Change 1: `SyncFollowerBracket` branch (3) modified | `CaptureLinkedTargetPrice` + `SyncAtmFollowerBracket` + `HasValue` guard | L2281-2288 | **YES** |
| Change 2: `CaptureLinkedTargetPrice` new method | `private double? CaptureLinkedTargetPrice(Account acc, string stopName)` | L2396-2407 | **YES** |
| Change 3: `TryParseStopSuffix` new static helper | `private static bool TryParseStopSuffix(string stopName, out string suffix)` | L2413-2423 | **YES** |
| Change 4: `IsTargetOrderLive` new static helper | `private static bool IsTargetOrderLive(Order o)` (expression body) | L2428-2429 | **YES** |
| Change 5: `ResubmitTargetAfterCascade` new method | Block A-Prime + Block B with `CreateOrder`+`Submit` | L2441-2499 | **YES** |

**Result: 5/5 changes present — PASS**

### A.2 SyncFollowerBracket Branch (3) Shape Matches Target

**Plan Section 4.1 target shape**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + B141
{
    double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, fo.Name);
    SyncAtmFollowerBracket(acc, fo, newPrice);
    if (capturedTargetPrice.HasValue)
        ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder);
    return;
}
```

**Actual source (L2281-2288)**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + DW-B153
{
    double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, fo.Name); // B141: capture before cascade
    SyncAtmFollowerBracket(acc, fo, newPrice);   // cascade kills linked target (accepted, by design)
    if (capturedTargetPrice.HasValue)            // B141: +1 branch -> CYC 8 (at limit -- no further branching may be added)
        ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder);
    return;
}
```

**Diff from plan**: Comment text substitutes `+ DW-B153` for `+ B141` — ticket review noted this as "semantically identical." Structure is identical: capture fires before `SyncAtmFollowerBracket`, `SyncAtmFollowerBracket` is unconditional, `ResubmitTargetAfterCascade` is gated on `HasValue`, `return;` terminates unconditionally.

**Key invariants verified**:
- `CaptureLinkedTargetPrice` called BEFORE `SyncAtmFollowerBracket`: **YES** (L2283 precedes L2284)
- `SyncAtmFollowerBracket` unconditional (not gated on HasValue): **YES** (L2284 not inside `if`)
- `ResubmitTargetAfterCascade` called ONLY when `capturedTargetPrice.HasValue`: **YES** (L2285 guard)
- `return;` present unconditionally: **YES** (L2287)

**Result: PASS — exact target shape**

### A.3 All 4 New Helpers Present with Exact Signatures

| Helper | Plan Signature | Source Line | Match? |
|--------|---------------|-------------|--------|
| `CaptureLinkedTargetPrice` | `private double? CaptureLinkedTargetPrice(Account acc, string stopName)` | L2396 | **YES** |
| `TryParseStopSuffix` | `private static bool TryParseStopSuffix(string stopName, out string suffix)` | L2413 | **YES** |
| `IsTargetOrderLive` | `private static bool IsTargetOrderLive(Order o)` | L2428 | **YES** |
| `ResubmitTargetAfterCascade` | `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder)` | L2441-2445 | **YES** |

**Result: 4/4 helpers present with exact signatures — PASS**

### A.4 No Stray B140 Code (acc.Change Branch) Remains

B140-LaneA introduced branch (3a) `if (!string.IsNullOrEmpty(fo.Oco))` routing to `acc.Change`. This was reverted at fd4a439d before B141 work began.

**Check at L2281-2320**: Source shows only the clean B141 state — branch (3) directly calls `CaptureLinkedTargetPrice` + `SyncAtmFollowerBracket` + conditional `ResubmitTargetAfterCascade`. No `(3a)` sub-branch. No `string.IsNullOrEmpty(fo.Oco)` condition. No `acc.Change(new Order[] { fo })` on the Stop bracket path. No `fo.StopPrice = newPrice` assignment in branch (3).

**Result: PASS — zero stray B140 code**

### A.5 Branch (3b) (ATM TGT Path) Unchanged

**Source L2289-2293**:
```csharp
if (!isStop && IsAtmSTPOrder(fo)) // (3b) DW-B137: ATM target cancel+resubmit
{
    SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder);
    return;
}
```

Verification report (ticket-1-verification.md): "Branch (3b) (`!isStop && IsAtmSTPOrder`) is UNCHANGED — L2289-2293: identical to pre-B141 spec." Confirmed by direct source read.

**Result: PASS — branch (3b) unmodified**

### A.6 No Regression to Other Branches

Branches 4 and 5 (L2295-2319):
- Branch (4) `if (isStop && IsTrailingStop(fo))` at L2295: unchanged
- Branch (5) inner `if (isStop)` at L2303 within try block: unchanged
- `catch (Exception ex)` at L2316: unchanged

Verification report: "No other branches in SyncFollowerBracket modified — L2295-2319: branches 4 and 5 + try/catch unchanged — PASS."

**Result: PASS — zero regression**

---

## B. Spec Requirements Satisfied

### B.1 DW-B153 CLOSED

**Status**: **CLOSED** (re-closed in B141 via dual-resubmit)

- B140-LaneA closed DW-B153 but that closure was invalidated by SIM Gate 1 FAIL (acc.Change is a no-op on ATM brackets).
- B141 re-closes DW-B153 via the correct mechanism: capture Target price → accept cascade → resubmit PTT-TGT-Drag.
- The root consequence of DW-B153 (follower naked position after stop drag) is now addressed.
- Evidence: `ResubmitTargetAfterCascade` creates and submits a `PTT-TGT-Drag` at the captured target price after every ATM Stop bracket cancel+resubmit event.
- `ticket-1-completion.md` and `ticket-1-verification.md` both confirm DW-B153 CLOSED.

### B.2 DW-B154 Documented

**Status**: **DOCUMENTED** (informational — no new code required)

- `acc.Change()` confirmed silent no-op on ATM-owned Stop brackets from AddOnBase context.
- Source comment at L2278: `// DW-B154: acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase (B140 SIM Gate 1 FAIL).`
- Architecture plan Section K documents the full fact with NT8 citation.
- No code fix required — this is an architectural constraint, not a bug. B141 dual-resubmit is the correct workaround pattern.

### B.3 FINAL_PASS Criteria Confirmed

| Gate | Status |
|------|--------|
| T1 BUILD_PASS | **YES** — 0 errors, 0 CS1503, 0 CS0246 |
| T1 VERIFY_PASS | **YES** — all 11 verification checks PASS |
| 7 scans zero | **YES** — all 7 scans confirmed by Layer 2 and Layer 3 |
| 7/7 xUnit tests | **YES** — 7/7 pass, 0 failures |
| Sync + MD5 verify | **YES** — 0 MISMATCH |
| 06-deferred-backlog.md | **YES** — written (see GATE below) |

---

## C. Cross-File JS-DNA Scan (Phase 5 Independent Scan)

### SCAN-01: lock() in B141 range (L2276-L2560)

**Command**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2560 }
```

**Reviewer independent grep result**: File-wide grep for `lock\s*(` returned 13 hits — all in comments (e.g., `// JS-021: no lock`, `// ConcurrentDictionary -- lock-free. No lock() anywhere.`). Zero actual `lock(` statements anywhere in the file. Zero hits in the B141 range L2276-L2560.

**JS-021 status**: **PASS — 0 actual lock() statements**

### SCAN-02: async void

No `async void` declarations in modified or new methods. Source L2396-2499 contains zero `async` keywords. One comment-only hit at L1632 (`// JS-033: Tick is not async void`) — not a declaration. Confirmed by both engineer (SCAN-02) and verifier (SCAN-06).

**JS-033 status**: **PASS — 0 async void declarations**

### SCAN-03: throw new in B141 range

Zero `throw new` statements in L2276-L2560. Both engineer and verifier confirmed 0 hits. All error paths use `try/catch` with `StatusUpdate?.Invoke(...)`.

**JS-001 status**: **PASS — 0 throw in hot path**

### SCAN-04: CYC (post-B141 independent count)

| Method | Branches (project convention: `&&`/`||`/`catch`=0) | CYC | Limit | Result |
|--------|-----------------------------------------------------|-----|-------|--------|
| `SyncFollowerBracket` (modified) | base(1)+fo-null(1)+price-delta(1)+ATM-STP(1)+HasValue-B141(1)+ATM-TGT(1)+IsTrailingStop(1)+isStop-inner(1) | **8** | 8 | **PASS — at limit** |
| `CaptureLinkedTargetPrice` | base(1)+if-TryParse(1)+foreach(1)+if-IsTargetLive(1) | **4** | 8 | **PASS** |
| `TryParseStopSuffix` | base(1)+if-null-len(1)+if-TryParse-range(1) | **3** | 8 | **PASS** |
| `IsTargetOrderLive` | base(1) — expression body, no `if` | **1** | 8 | **PASS** |
| `ResubmitTargetAfterCascade` | base(1)+foreach-APrime(1)+if-Working(1)+if-null-Target(1) | **4** | 8 | **PASS** |

All three layers (plan, engineer, verifier) report identical counts. Zero discrepancies.

**JS-041 status**: **PASS — all methods CYC <= 8**

### SCAN-05: ASCII-only

Zero non-ASCII characters in L2276-L2560. All new string literals verified: `"Target"`, `"PTT-TGT-Drag"`, `"B141 TGT CreateOrder returned null"`, `"B141 TGT resubmit after cascade -> "`, `"B141 TGT create error: "`, `"TGT pre-cancel error (B141): "`.

**ASCII-only status**: **PASS**

### SCAN-06: DateTime.Now

`NinjaTrader.Core.Globals.MaxDate` used at L2484 in `ResubmitTargetAfterCascade`. Verifier ran file-wide `DateTime\.Now[^U]` scan — 0 hits in B141 code.

**NT8 DateTime status**: **PASS — no DateTime.Now**

### SCAN-07: Additional NT8 checks

| Check | Source | Result |
|-------|--------|--------|
| CreateOrder arg12 = `(NinjaTrader.Cbi.CustomOrder)null` | L2485 | **PASS** |
| `oco=""` for PTT-TGT-Drag | L2482 | **PASS** |
| `acc.Submit(new[] { newTarget })` after CreateOrder | L2492 | **PASS** |
| PTT- prefix on new order | `"PTT-TGT-Drag"` L2483 | **PASS** |
| No `FontFamily` | Not applicable | **PASS** |
| No hardcoded `#RRGGBB` hex | Not applicable | **PASS** |
| No `Account.All` in constructor | Not in scope | **PASS** |
| No sealed `TradeCopierWindow` | Not in scope | **PASS** |
| No `async/await` in lifecycle methods | Not in scope | **PASS** |

**All NT8 checks: PASS**

---

## D. Cross-File Coherence (CopyEngine System)

### D.1 Single-File Scope

B141 modifies exactly one source file: `src/PropTraderTools/CopyEngine.cs`. Test file `tests/PropTraderTools.Tests/B141Tests.cs` is new. No other files touched.

### D.2 leaderOrder Scope Confirmed

`leaderOrder` is the first parameter of `SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` at L2254-2260. It is in scope throughout the method body. Its use in branch (3b) at L2291 (`SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder)`) was confirmed pre-B141. B141's use at L2286 (`ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder)`) is within the same scope — **PASS**.

### D.3 acc.Orders Snapshot Pattern (No lock Required)

Both `CaptureLinkedTargetPrice` and `ResubmitTargetAfterCascade` use `acc.Orders.ToList()` — the snapshot enumeration pattern. This is consistent with the existing codebase pattern (`CancelExistingPttStpDrag` at L2531, `SyncAtmFollowerTarget` at L2406/L2502). NT8-VERIFY-02 confirmed `acc.Orders` is `IEnumerable<Order>` from AddOnBase context. No `lock()` required on the NT8 dispatch thread. **PASS**.

### D.4 No Cross-File Pollution

`SyncFollowerBracket`, `CaptureLinkedTargetPrice`, `TryParseStopSuffix`, `IsTargetOrderLive`, and `ResubmitTargetAfterCascade` are all `private` or `private static`. Zero cross-file exposure. `IsTargetOrderLive` is static and has no shared state. No new public API surface introduced. **PASS**.

### D.5 TradeCopierPanel / TradeCopierWindow Untouched

B141 is scoped entirely to `CopyEngine.cs`. No `TradeCopierPanel.cs` or `TradeCopierWindow.cs` changes. Cross-file coherence is not degraded. **PASS**.

---

## E. Build and Test Confirmation

### E.1 Build

**Engineer (SCAN-06)**: `dotnet build src/PropTraderTools/PropTraderTools.csproj` — 0 errors, 1 pre-existing warning (xUnit2004 in B131Tests.cs).

**Verifier (independent `--no-incremental` run)**: 0 errors, 1 pre-existing warning (same xUnit2004, B131Tests.cs L165 — NOT introduced by B141).

**Result: BUILD PASS — 0 errors**

### E.2 Tests

**Engineer**: `dotnet test ... --filter "B141" --verbosity minimal` — Passed: 7, Failed: 0, Total: 7.

**Verifier (independent `--filter "T_B141"`)**: 7/7 pass, 0 failures, 0 errors.

All 7 test names confirmed:
- T_B141_01: CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice
- T_B141_02: CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice
- T_B141_03: CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice
- T_B141_04: CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull
- T_B141_05: SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound
- T_B141_06: SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent
- T_B141_07: SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled

Framework: xUnit only (no NUnit, no MSTest) — PASS (JS testing mandate).

**Result: TEST PASS — 7/7**

---

## F. Sync Verification

**Engineer**: `ptt-sync-and-verify.ps1` — Copied: 1, In-sync: 17, Excluded: 62. MD5: OK CopyEngine.cs. PASS (0 MISMATCH).

**Verifier (independent re-run)**: Copied: 0, In-sync: 18, Excluded: 62 (CopyEngine.cs already in-sync). MD5: OK CopyEngine.cs + 17 other files. PASS (0 MISMATCH).

**Result: SYNC PASS — 0 MISMATCH lines**

---

## G. Summary of Violation Findings

**Zero violations found.** No rule citations required.

| DNA Rule | Check | Status |
|----------|-------|--------|
| JS-021: No `lock()` | 0 actual lock() in file (all 13 grep hits are comments) | **PASS** |
| JS-001: No throw in hot path | 0 throw new in L2276-L2560; try/catch pattern used | **PASS** |
| JS-002: No reference null return | `double?` = nullable VALUE type (Nullable<double>); documented and architecturally sound | **PASS** |
| JS-033: No async void | 0 async void declarations; 1 comment-only hit (L1632) | **PASS** |
| JS-009: No mutable Dictionary for shared state | Not applicable (no new dictionaries) | **PASS** |
| JS-008: No mutable fields on struct | Not applicable (no new structs) | **PASS** |
| JS-010: No public constructor on singleton | Not applicable (no new singletons) | **PASS** |
| JS-041: CYC <= 8 | All 5 methods CYC 8/4/3/1/4 — all <= 8 | **PASS** |
| NT8: No async in lifecycle methods | None in scope | **PASS** |
| NT8: No Account.All in constructor | None in scope | **PASS** |
| NT8: No sealed TradeCopierWindow | None in scope | **PASS** |
| NT8: No FontFamily override | None in scope | **PASS** |
| NT8: No hardcoded #RRGGBB hex | None in scope | **PASS** |
| NT8: CreateOrder with PTT- prefix | "PTT-TGT-Drag" used | **PASS** |
| NT8: No DateTime.Now | Globals.MaxDate used | **PASS** |

---

## Section K: Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B153 | OCO cascade on Stop1/Stop2 drag — dual-resubmit closes | P0 | B141 | **CLOSED** |
| DW-B154 | acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase — documented | N/A | B141 | **DOCUMENTED** |
| DW-B140-01 | SIM Gate 1 (acc.Change non-no-op) — SIM ran, FAIL, question answered | P0 | B140 SIM | **CLOSED** (superseded) |
| DW-B140-02 | SIM Gate 2 (Stop3 via acc.Change) | P1 | B140 SIM | **CLOSED** (superseded) |
| DW-B140-03 | SIM Gate 3 (consecutive drags no cascade) | P1 | B140 SIM | **CLOSED** (superseded) |
| DW-B141-STP-CYC8-WALL | SyncFollowerBracket at CYC 8 wall — no further branching without extraction | P1 | next SyncFollowerBracket modifier | **OPEN** |
| DW-B141-SIM-01 | SIM Gate 1 (P0 BLOCKING): dual-resubmit verification — Stop1 drag, Target1 NOT cancelled, PTT-TGT-Drag appears at captured price | P0 | B141 SIM | **OPEN** |
| DW-B141-SIM-02 | SIM Gate 2: Stop2 drag — Target2 resubmit correct | P1 | B141 SIM | **OPEN** |
| DW-B141-SIM-03 | SIM Gate 3: two consecutive stop drags, no accumulation of orphan PTT-TGT-Drag orders | P1 | B141 SIM | **OPEN** |
| DW-B64-01 | HandleEntryChange not firing — drag sync broken | P0 | next P0 | **OPEN** |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | future | **OPEN** |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | future | **OPEN** |
| DW-B141 | Phase C re-confirmation (SIM Test A) | P1 | B135 SIM | **OPEN** |
| DW-B138 | Stop drag confirmed (SIM Test B) | P1 | B135 SIM | **OPEN** |
| B135-DEFER-01 | Gap B — two simultaneous entries | P1 | B138+ | **OPEN** |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | **OPEN** |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | **OPEN** |

**Notes**:
- DW-B141-SIM-01 (P0 BLOCKING): merge is gated on SIM Gate 1 passing. Fail protocol: STOP, document as DW-B155, Director resolution.
- DW-B141-STP-CYC8-WALL: any future PR touching `SyncFollowerBracket` must extract a branch before adding one.
- DW-B64-01 remains the top P0 non-SIM item (promoted from B140-LaneA carry-forward).

---

## Final Verdict

**FINAL_PASS**

All phase 5 gates satisfied:

| Gate | Result |
|------|--------|
| Coherence check (5 code changes present, shape exact) | PASS |
| Branch (3b) unchanged | PASS |
| No stray B140 code | PASS |
| No regression to branches 4/5 | PASS |
| DW-B153 CLOSED (dual-resubmit) | PASS |
| DW-B154 DOCUMENTED (informational) | PASS |
| JS-DNA cross-file scan — 0 violations | PASS |
| lock() scan — 0 actual lock() in file | PASS |
| Build — 0 errors | PASS |
| Tests — 7/7 pass | PASS |
| Sync + MD5 — 0 MISMATCH | PASS |
| Section K present | PASS |
| 06-deferred-backlog.md written | PASS |

**SIM Gates (DW-B141-SIM-01/02/03) are the Director's responsibility. SIM Gate 1 (P0) is blocking merge. No automated check substitutes for live NT8 SIM verification of OCO cascade behavior.**

---

*Produced by ptt-plan-reviewer, B141 Phase 5. Required gate artifact.*
