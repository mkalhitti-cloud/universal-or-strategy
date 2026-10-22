# B211 Plan Review
## PTT-COPIER-B211: Move Target to Ask / Move Target to Bid (PTT-MA)

**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (Second Pass — Post-Revision)  
**Input**: docs/brain/B211/02-architecture-plan.md (revision 1, status "REVIEW_PASS (V1/V2/V3/V4 fixed)")  
**Spec Sources**:  
  - `c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html` §B211 (lines 43255-43740) — authoritative HTML spec  
  - `docs/brain/B211-orchestrator-prompt.md` — orchestrator brief  
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md`  
**Date**: 2026-07-07  
**Pass count**: 2 (REVIEW_FAIL → REVIEW_PASS)  

---

## VERDICT

**REVIEW_PASS**

All 4 prior blocking violations (V1, V2) and advisory items (V3, V4) are confirmed resolved.  
No new blocking violations introduced in the revision.  
One minor advisory (V5) noted — does not block.  
Phase 3 (ticket generation) is UNLOCKED.

---

## Violation Resolution Confirmation

### V1 — JS-002 — TryFindWorkingTarget pattern — RESOLVED

**Evidence in revised plan**:
- Signature (plan line 344): `private static bool TryFindWorkingTarget(Account acc, Instrument instr, out NinjaTrader.Cbi.Order target)` — TryGet pattern, returns `bool`.
- Body (plan lines 406-419): `target = default(NinjaTrader.Cbi.Order); ... return target != null;` — no null returned from the method itself.
- Execute caller (plan line 357): `if (!TryFindWorkingTarget(acc, instr, out var target)) { log "[PTT-MA] no target found..."; return; }`
- JS Pre-Check table (plan line 69): "JS-002: TryGet pattern -- no null return." PASS stated.

**Verdict**: V1 RESOLVED. JS-002 compliant. ✓

---

### V2 — Spec Compliance Gap — try/catch around NT8 API calls — RESOLVED

**Evidence in revised plan**:
- Execute body (plan lines 364-375):
  ```csharp
  try
  {
      acc.Cancel(new NinjaTrader.Cbi.Order[] { target });
      var newOrder = acc.CreateOrder(instr, action, OrderType.Limit, OrderEntry.Manual, TimeInForce.Day,
                     qty, exitPrice, 0.0, string.Empty, orderName,
                     NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
      if (newOrder != null) acc.Submit(new[] { newOrder });
  }
  catch (Exception ex)
  {
      Output.Process("[PTT-MA] Execute exception: " + ex.Message, PrintTo.OutputTab1);
  }
  ```
  No rethrow. Log-and-swallow only. All three NT8 calls (Cancel, CreateOrder, Submit) inside try block.
- JS Pre-Check table (plan line 68): "NT8 API calls (Cancel/CreateOrder/Submit) wrapped in try/catch in Execute — log-and-swallow, no rethrow. PASS" stated.

**Verdict**: V2 RESOLVED. Spec §JS-001 checklist satisfied. ✓

---

### V3 — CYC Table Inaccuracy — OnAskClick/OnBidClick — RESOLVED

**Evidence in revised plan**:
- CYC Budget Table (plan line 87): `OnAskClick/OnBidClick | TradeCopierPanel.cs | **4 each** | 8 | (1)instrument null, (2)?? null-coalescing operator, (3)leader null`

CYC=4 now correctly stated. The `??` null-coalescing operator is correctly counted as a conditional branch.

**Verdict**: V3 RESOLVED. CYC table accurate. ✓

---

### V4 — Spec Deviation — Clamp Range — RESOLVED

**Evidence in revised plan**:
- OnAskUp (plan lines 223-226): `_askBuffer = Math.Max(Math.Min(_askBuffer + 1, 20), -10);` — range **[-10, +20]**
- OnAskDown (plan lines 232-235): `_askBuffer = Math.Max(Math.Min(_askBuffer - 1, 20), -10);` — same range
- OnBidUp (plan lines 270-273): `_bidBuffer = Math.Max(Math.Min(_bidBuffer + 1, 20), -10);` — same range
- OnBidDown (plan lines 280-283): `_bidBuffer = Math.Max(Math.Min(_bidBuffer - 1, 20), -10);` — same range
- Rationale comment present (plan lines 220-222 / 229-231 / 269-270 / 278-279):
  "V4 rationale: lower clamp is -10 per spec WPF template (HTML line 43463). Upper clamp +20 per orchestrator prompt direction. Director approved."

Lower clamp -10 matches spec WPF template (HTML 43463: `if (_askBuffer > -10) _askBuffer--`).  
Upper clamp +20: spec shows no upper bound in the template; +20 extended per orchestrator prompt with explicit Director-approval note. Acceptable.

**Verdict**: V4 RESOLVED. Clamp matches spec lower bound; upper bound deviation documented and approved. ✓

---

## Previously-Passing Checks — Re-Confirmed

### LANE-SPLIT GATE

| Check | Result |
|-------|--------|
| Gate result stated | PASS — "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" (plan line 21) |
| Q1=YES → default rule fires correctly | PASS — Q1=YES (same method, within 50 lines) triggers single-pipeline |
| No lane split attempted | PASS — gate correctly resolves to SINGLE-PIPELINE |

### JS Rules Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_askBuffer`/`_bidBuffer` declared `volatile int`. AllAccounts is lock-free ConcurrentDictionary. No `lock()` in any new code. `Interlocked.Increment(ref _maSeq)` for sequence counter. | PASS |
| JS-001 (no throw new) | Guard-return throughout. NT8 calls wrapped in try/catch log-and-swallow. No `throw new ...`. | PASS |
| JS-002 (no null return) | All new methods return `void` or `bool` (TryGet pattern). No null returned from any new method. | PASS |
| JS-033 (no async void) | All click handlers synchronous `void`. `OnAskAllUp/Down` delegate to `OnAskUp/Down` synchronously. No `async`. | PASS |
| ASCII-only | "PTT-MA-Ask-", "PTT-MA-Bid-", "[PTT-MA]", "[PTT-MA-ALL]", "[PTT-MA-ALL] useAsk=". `useAsk` is bool → `.ToString()` yields "True"/"False" (ASCII). Button labels "Ask +0", "Bid +0", "Ask-ALL +0", "Bid-ALL +0". All ASCII. | PASS |
| CYC <= 8 (global limit — all methods) | Execute CYC=6, TryGetExitPrice CYC=7 (see V5 advisory), TryFindWorkingTarget CYC=6, OnAskClick/OnBidClick CYC=4, spinners CYC=1, UpdateAskLabels/UpdateBidLabels CYC=1. All ≤ 8. | PASS |
| DateTime.Now absent | `NinjaTrader.Core.Globals.MaxDate` for expiry. No `DateTime.Now`. | PASS |
| No FontFamily override | NTButtonStyle resource reference only. | PASS |
| No hardcoded hex | BrushTeal reused. No new hex color literals. | PASS |
| CreateOrder with PTT- prefix | "PTT-MA-Ask-" and "PTT-MA-Bid-" both PTT-prefixed. SCAN-05 clear. | PASS |
| AtmStrategyChangeStopTarget absent | Explicitly listed as banned/not-used. | PASS |
| AtmStrategyCreate() absent | Explicitly listed as banned/not-used. | PASS |
| Account.Change() absent | Explicitly listed as banned/not-used. | PASS |
| async/await in lifecycle methods | None present. | PASS |
| sealed TradeCopierWindow | Not sealed. No change. | PASS |

### Spec Coverage Matrix (Full — from HTML spec)

| Spec Requirement | Addressed? | Plan Section | Notes |
|----------------|------------|-------------|-------|
| Row N+1: Ask / Ask-ALL buttons | YES | §1 TradeCopierPanel | ✓ |
| Row N+2: Bid / Bid-ALL buttons | YES | §1 TradeCopierPanel | ✓ |
| Directly below BE/BE-ALL row | YES | BuildUI: `root.Children.Add(_askRowPanel)` after `_beRowPanel` | ✓ |
| Same NTButtonStyle teal template as BE | YES | BrushTeal reused, same RepeatButton pattern | ✓ |
| volatile int buffers: _askBuffer, _bidBuffer | YES | Plan lines 150-151 | ✓ |
| Trading Intent (long→Bid primary, short→Ask primary) | YES | Plan data flow diagram | ✓ (informational) |
| Behaviour Rule 1: no-op if no Working target | YES | Execute step (4): TryFindWorkingTarget false → log+return | ✓ |
| Behaviour Rule 2: no-op if no open position | YES | Execute step (3): FindPositionPublic null/qty==0 → log+return | ✓ |
| Behaviour Rule 3: no BeSwapInProgress guard needed | YES | Plan line 71 + T8 test | ✓ |
| Behaviour Rule 4: re-entrant clicks valid | YES | Implicit — each click cancels current Working target regardless of prefix | ✓ |
| Behaviour Rule 5: PTT-MA-* NOT protected by FIX-G-2 | YES | T8: IsBeSwapGtcSweepGuard returns false for "PTT-MA-Ask-1" | ✓ |
| Partial fill: qty = Quantity - Filled (P0) | YES | Plan line 358: explicit formula `qty = target.Quantity - target.Filled` | ✓ |
| NT8 API: Account.Cancel + CreateOrder + Submit | YES | §NT8 API Surface, confirmed AddOnBase-available | ✓ |
| AtmStrategyChangeStopTarget() BANNED | YES | BANNED API list | ✓ |
| try/catch around NT8 API calls | YES (V2 fixed) | Execute body lines 364-375 | ✓ |
| Order names PTT-MA-Ask-{N} / PTT-MA-Bid-{N} | YES | PttOrderNames constants MaAskPrefix + MaBidPrefix | ✓ |
| PttContracts.cs: MaAskPrefix + MaBidPrefix | YES | §4 | ✓ |
| PttMoveTargetOne.cs (NEW) mirrors PttBeSwapOne | YES | §2 | ✓ |
| PttMoveTargetAll.cs (NEW) mirrors PttBeSwapAll | YES | §3 | ✓ |
| AllAccounts() reused (not re-implemented) | YES | PttMoveTargetAll.Execute calls CopyEngine.Instance.AllAccounts(instr) | ✓ |
| 8+ xUnit [Fact] tests named | YES | §5 T1-T8 with Setup/Assert | ✓ |
| DW-B211-03 resolved by architect | YES | Architect Decisions: LAST-in-iteration strategy with rationale | ✓ |
| DW-B211-05 resolved by architect | YES | Architect Decisions: CONFIRM SHARED BUFFERS with rationale | ✓ |
| DW-B211-01, -02, -04 carried forward | ADVISORY | Plan §B211 Deferred Items: "No deferred items" — DW-01/-02/-04 not listed in plan body. These are spec-defined items not requiring V1 resolution. Must appear in 06-deferred-backlog.md at Ph5. | Advisory |

### NT8 API Verification

| API | Status |
|-----|--------|
| `acc.Cancel(new Order[] { target })` | PASS — AddOnBase-available (CopyEngine.cs line 2189, 2253) |
| `acc.CreateOrder(...)` 13-arg | PASS — AddOnBase-available (CopyEngine.cs line 3997-4009) |
| `acc.Submit(new[] { order })` | PASS — AddOnBase-available (CopyEngine.cs line 4016) |
| `AtmStrategyChangeStopTarget()` | PASS — not referenced. Correctly listed as banned. |
| `AtmStrategyCreate()` | PASS — not referenced. Correctly listed as banned. |
| `Account.Change()` | PASS — not referenced. Correctly listed as banned. |
| `Interlocked.Increment(ref _maSeq)` | PASS — lock-free atomic counter. JS-023 compliant. |

### Architect Decisions

| Decision | Resolved? | Assessment |
|----------|-----------|-----------|
| DW-B211-03: Target order identification | YES | "Last Working PTT-prefixed Limit exit in acc.Orders" — LAST-in-iteration = most recently submitted. PTT-prefix filter excludes ATM brackets. Rationale sound. ACCEPTED. |
| DW-B211-05: Shared vs independent buffers | YES | SHARED (_askBuffer for Ask+Ask-ALL; _bidBuffer for Bid+Bid-ALL). Rationale sound (mirrors BE/BE-ALL pattern, simpler, no independent-buffer constraint for B211). ACCEPTED. |

### Structural Checks

| Check | Status |
|-------|--------|
| ptt-sync requirement stated for engineers | PASS — Plan §ptt-sync: `powershell -File .\scripts\ptt-sync.ps1` |
| Ph4b deletion audit requirement stated | PASS — Plan §Ph4b Deletion Audit: git diff command present |
| 8+ [Fact] tests with names and Setup/Assert | PASS — T1 through T8 fully specified |
| xUnit only (no NUnit, no MSTest) | PASS — explicitly stated |
| T1/T2 stub engineering note | PASS — plan flags static-class stub challenges, defers to engineer to follow B209Tests.cs pattern |

---

## Advisory Item (Non-Blocking)

### V5 — CYC Table Inaccuracy — TryGetExitPrice [Advisory — non-blocking]

**Location**: Plan CYC Budget Table (plan line 83), `PttMoveTargetOne.TryGetExitPrice` row.

**Stated**: CYC=6.  
**Actual**: CYC=7.

The plan's own inline recount at line 393 states "CYC=7" after simplifying the null-coalescing chain. The table entry was not updated to match. Under the decision-counting method (applied consistently with the prior review):
- base(1) + `??` tickSize(1) + `<=0`(1) + `md==null`(1) + `useAsk ?`(1) + `side==null`(1) + `basePrice<=0`(1) = **CYC=7**

CYC=7 is **under the global limit of 8** — this is not a DNA FAIL. The table understates by 1 but the method is compliant.

**Action for engineer**: Update CYC Budget Table row during implementation to show CYC=7. No redesign required.

---

## Notes on Accepted Patterns

1. **`OnAskAllUp/Down` delegate pattern** — `OnAskAllUp => OnAskUp(sender, e)` ensures shared buffer and both labels updated atomically via `UpdateAskLabels()`. Correct. ✓
2. **`UpdateAskLabels()` / `UpdateBidLabels()`** — Called from UI thread click handlers only. No Dispatcher.InvokeAsync needed (JS-023 clear). ✓
3. **`bufferTicks` parameter on `PttMoveTargetAll.Execute`** — Extends spec template's 2-arg signature to 3 args. The buffer feature is core to the spec; passing it through to `PttMoveTargetOne.Execute → TryGetExitPrice` is architecturally correct. ✓
4. **`acc.Orders.ToList()`** — Lock-free NT8 read pattern consistent with CopyEngine.cs usage. ✓
5. **Shared buffer vs spec note at HTML 43434 ("independent volatile int buffer")** — DW-B211-05 explicitly grants architect authority to decide; architect chose shared per DW-B211-05 resolution. Spec note at HTML 43473-43476 confirms shared is the accepted default. ✓
6. **Null guards on `OnAskClick/OnBidClick`** — More defensive than spec template (which shows no guards). Correct engineering for production AddOn. Accepted. ✓
7. **DW-B211-01, -02, -04 carry-forward** — Spec-defined deferred items. Must appear in 06-deferred-backlog.md at Ph5. Advisory only. ✓

---

## Phase Gate

| Gate | Status |
|------|--------|
| Phase 2 REVIEW_PASS | **GRANTED** |
| Phase 3 (ticket generation) | **UNLOCKED** |
| Prior violations V1-V4 | ALL RESOLVED |
| New blocking violations | NONE |
| Advisory items | V5 (CYC table +1 — non-blocking) |

---

*ptt-plan-reviewer — Phase 2 (Second Pass) — 2026-07-07*
