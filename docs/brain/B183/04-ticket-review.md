# B183 Ticket Review
## ptt-ticket-reviewer Phase 3.5

**Status**: COMPLETE  
**Date**: 2026-07-07  
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)  
**Tickets reviewed**: `docs/brain/B183/04-tickets.md`  
**Plan reviewed**: `docs/brain/B183/02-architecture-plan.md`  
**Spec reviewed**: `docs/brain/B183/00-orchestrator-prompt.md`  
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## OVERALL VERDICT

```
TICKET_REVIEW_PASS: YES
```

All 7 tickets pass all mandatory checks. No TICKET_REVIEW_FAIL conditions found.  
Engineer may proceed to Phase 4a.

---

## T1 — Slate Gate Fields + Methods

**Ticket ID**: T1  
**Spec groups**: A1, A2  
**File**: `src/PropTraderTools/CopyEngine.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present at top of T1: `write_file` is BANNED, apply_diff ONLY. Single-hunk = instant VERIFY_FAIL.

### Approved Deletions Table
PASS — Table present, all entries NONE. Correct for B183 add-only scope.

### 7-Scan Checklist
PASS — All 7 scans listed with commands and pass conditions:
- Scan 1: lock() grep → 0 results ✅
- Scan 2: async void grep → 0 results ✅
- Scan 3: return null documented; T1 adds 0 new ✅
- Scan 4: CYC per method: TryMarkFollowerSlateNotReady=2, TrySetFollowerSlateReady=4, IsAccountFlat=3, HasAnyActiveOrders=5, OnFollowerPositionUpdate=3. All ≤ 8 ✅
- Scan 5: non-ASCII grep → 0 results ✅
- Scan 6: Manual symbol presence check listed ✅
- Scan 7: ptt-sync.ps1 → 0 errors ✅

### Ph4b Verifier Gate Reference
PASS — All 5 gates referenced by name with ticket-specific notes:
- Gate 1: hunk count > 1 ✅
- Gate 2: zero deletions ✅
- Gate 3: deletion count ≈ 0 add-only ✅
- Gate 4: manual symbol check (script not yet created) ✅
- Gate 5: ptt-sync ✅

### BUILD_PASS Criteria
PASS — 5 explicit criteria defined: 3 fields grep check, 5 methods grep check, compile caveat (T1-T5 together), all 7 scans, all 5 Ph4b gates.

### Traceability
PASS — T1 covers plan items A1 (3 fields) and A2 (5 methods) exactly. No phantom work. No plan items missing from T1.

### Spec Coverage
PASS — All A1+A2 symbols from plan §3 accounted for in T1.

### JS Pre-checks
PASS:
- JS-021: ConcurrentDictionary indexer/TryAdd — no lock(). Documented in field comments and per-method comments. ✅
- JS-001: All methods use early returns, zero throw. Documented. ✅
- JS-002: All methods void or bool. No null return. ✅
- JS-033: OnFollowerPositionUpdate is `private void` (not async void). Confirmed in code block. ✅
- JS-081: All key strings ASCII-only. Documented in field comments. ✅
- CYC ≤ 8: All 5 methods documented (2, 4, 3, 5, 3). PASS.

### NT8 Check
PASS — No lifecycle violations. acc.Positions enumeration in AddOnBase is valid. ConcurrentDictionary write is thread-safe; no Dispatcher needed. PositionEventArgs.Position.Account confirmed as the correct source.

### Insertion Point Correctness
PASS:
- Field insertion: After `_drainOwnedOrderIds` (HEAD line 385), before `// --- Status event ---` (HEAD line 387). Pre-insert verification commands provided. ✅
- Method insertion: After `BuildQxSnapshot` closing `}`, before `// CancelQxBrackets` comment (HEAD line ~910). Pre-insert verification commands provided. ✅

### Verbatim Body Spot-Check
PASS — T1 field declarations and method bodies are character-for-character matches with spec §A1 and §A2. Verified manually.

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).

**VERDICT: T1 — TICKET_REVIEW_PASS**

---

## T2 — Slate Gate Call Sites + Subscribe/Unsubscribe

**Ticket ID**: T2  
**Spec groups**: A3, A4, A5  
**File**: `src/PropTraderTools/CopyEngine.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present.

### Approved Deletions Table
PASS — Table present, all entries NONE. Note added clarifying that expr-body→block-body conversion is a form transformation, not a deletion. Correct.

### 7-Scan Checklist
PASS — All 7 scans listed:
- Scan 3: T2 adds exactly 1 new `return null;` in FindFollowerBracketOrder (A4 gate) — documented as compliant with `Order?` nullable contract. ✅
- Scan 4: FindFollowerBracketOrder (Account overload) CYC 1→2; CancelQxBrackets overloads unchanged (6 and 7). All ≤ 8. ✅
- All others: ✅

### Ph4b Verifier Gate Reference
PASS — All 5 gates referenced. Gate 2 note correctly clarifies that expr-body conversion modifies lines but does not delete a symbol declaration.

### BUILD_PASS Criteria
PASS — 8 explicit criteria defined, including compile caveat (T1-T5 same session).

### Traceability
PASS — T2 covers A3 (3 call sites), A4 (FindFollowerBracketOrder gate), A5 (Subscribe/Unsubscribe wiring). All plan items accounted for.

### Cross-ticket Dependencies
PASS — Dependency on T1 (fields + methods) and T4 (BuildLaneEKey for build) explicitly stated in ticket header.

### JS Pre-checks
PASS:
- JS-021: TryRemove is lock-free. Documented. ✅
- JS-002: `return null;` in FindFollowerBracketOrder gate is compliant — return type is `Order?` (nullable). Explicitly documented with Scan 3 note. ✅
- CYC: No method exceeds 8. CancelQxBrackets +0 (method calls, no branches). FlattenOneAccount +0. FindFollowerBracketOrder +1 (1→2). All documented. ✅

### NT8 Check
PASS — No lifecycle violations. Account.All access is in Subscribe/Unsubscribe (correct lifecycle). No async/await.

### Insertion Point Correctness
PASS:
- CancelQxBrackets 2-param: After `return; // (1)` at HEAD line ~921, before `var stale = new`. ✅
- CancelQxBrackets 3-param: After `return; // (1)` at HEAD line ~1004-1005, before `var stale = new`. ✅
- FlattenOneAccount: Before `CancelAllAccountOrders(acc, instrument)` (HEAD line ~4709). ✅
- FindFollowerBracketOrder: HEAD lines 3571-3582 exact SEARCH block provided. ✅
- Subscribe/Unsubscribe: Exact SEARCH+REPLACE blocks provided with lines 1353 and 1359. ✅

### Verbatim Body Spot-Check
PASS — Subscribe() and Unsubscribe() REPLACE blocks match spec §A5 exactly. FindFollowerBracketOrder block-body form matches spec §A4 exactly.

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).

**VERDICT: T2 — TICKET_REVIEW_PASS**

---

## T3 — Lane D

**Ticket ID**: T3  
**Spec groups**: C2, C3, C4  
**File**: `src/PropTraderTools/CopyEngine.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present.

### Approved Deletions Table
PASS — Table present, all entries NONE. Note clarifies loop body replacement is not a symbol-level deletion. Correct.

### 7-Scan Checklist
PASS — All 7 scans listed:
- Scan 4: TryLaneDHook=6, TryLaneDDispatchFollower=4. DispatchCopy CYC: "report measured value (must be ≤ 8)" — engineer must verify before inserting (pre-insert verification commands provided). DispatchToFollower: was CCN≤3, now CYC=4. All ≤ 8. ✅
- Scan 5: `"#"` separator (0x23) and `"|"` (0x7C) are ASCII-only. Documented. ✅
- All others: ✅

### Ph4b Verifier Gate Reference
PASS — All 5 gates referenced. Gate 1 note: 4 apply_diff hunks expected (correct count for 4 insertions). Gate 3: ~80 added lines estimated.

### BUILD_PASS Criteria
PASS — 9 explicit criteria defined, including DispatchCopy CYC ≤ 8 as a mandatory pass condition.

### Traceability
PASS — T3 covers C2 (TryLaneDHook + TryLaneDDispatchFollower), C3 (call site in DispatchCopy), C4 (Lane D skip gate + dispatched counter). All plan items accounted for.

### Cross-ticket Dependencies
PASS — Dependency on T1 (`_laneDDispatched` field) explicitly stated.

### JS Pre-checks
PASS:
- JS-021: ConcurrentDictionary TryAdd + ContainsKey are lock-free. Documented in code comments. ✅
- JS-001: TryLaneDHook uses early returns only. TryLaneDDispatchFollower returns false on all failure paths. Documented. ✅
- JS-081: String keys use `"#"` (0x23) and `"|"` (0x7C) — ASCII-only. Documented. ✅
- CYC: TryLaneDHook=6, TryLaneDDispatchFollower=4. Both ≤ 8. ✅
- JS-023: No UI update in TryLaneDHook/TryLaneDDispatchFollower — StatusUpdate raised inside SendCopyWithAtm (existing pattern). Documented in method comments. ✅

### Pre-Insert Verifications
PASS — All required pre-insert checks documented:
- SetLiveEntryDispatched exists: grep command provided ✅
- GetCloneAtmMode vs ResolveAtmMode: grep command provided, HEAD confirmed as ResolveAtmMode, adaptation documented ✅
- PassesDailyCapCheck exists: grep command provided ✅
- SendCopyWithAtm return type (bool): grep command provided, deviation path documented ✅
- DispatchCopy CYC pre-verification: manual count instruction provided ✅
- DispatchToFollower ResolveAtmMode call location: grep command provided ✅

### NT8 Check
PASS — No lifecycle violations. Background thread usage documented in method comments. No async/await.

### Method Name Adaptation
PASS — The spec uses `GetCloneAtmMode` (f01b5155 name); HEAD uses `ResolveAtmMode`. T3 ticket correctly uses `ResolveAtmMode` in the verbatim insert blocks AND provides a prominent warning box reminding engineer to confirm the name via pre-insert grep. ✅

### Insertion Point Correctness
PASS:
- TryLaneDHook+TryLaneDDispatchFollower: After DispatchCopy closing `}` (HEAD line ~2415). ✅
- TryLaneDHook call: After `int baseQty = ResolveBaseQty(...)` (HEAD line ~2373-2374). ✅
- DispatchCopy loop replacement: SEARCH block lines 2384-2410 exact. ✅
- Lane D skip gate: After `var mode = ResolveAtmMode(rule, acc.Name)` (HEAD line ~2506). ✅

### Verbatim Body Spot-Check
PASS — TryLaneDHook and TryLaneDDispatchFollower bodies match spec §C2 with the documented adaptation (ResolveAtmMode in place of GetCloneAtmMode). DispatchCopy loop REPLACE block matches spec §C4 exactly.

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).

**VERDICT: T3 — TICKET_REVIEW_PASS**

---

## T4 — Lane E

**Ticket ID**: T4  
**Spec groups**: D2  
**File**: `src/PropTraderTools/CopyEngine.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present.

### Approved Deletions Table
PASS — Table present, all entries NONE.

### 7-Scan Checklist
PASS — All 7 scans listed:
- Scan 4: IsLaneEEligible=8 (AT BUDGET, FROZEN), TryLaneEHook=5, TryLaneEDispatchFollower=5, BuildLaneEKey=2. All ≤ 8. ✅
- Scan 5: `"#LANE-E#"` and `"LANE-E#"` are ASCII-only. Documented. ✅
- All others: ✅

### Ph4b Verifier Gate Reference
PASS — All 5 gates referenced with note "All 5 gates." Gate 1 note: "single contiguous insertion, but must use apply_diff — count ≥ 1; Lane A engineer applies T1-T5 together so actual commit may bundle all hunks." 

**NOTE (advisory, not FAIL)**: The Gate 1 note uses "count ≥ 1" in context of explaining that the Lane A bundle will produce far more than 1 hunk. The globally-defined Gate 1 requirement is "Count > 1" and takes precedence. The T4 note is explanatory, not a redefinition. Engineer must apply the global Gate 1 definition (Count > 1). No structural violation present.

### BUILD_PASS Criteria
PASS — 6 explicit criteria defined, including IsLaneEEligible CYC=8 AT BUDGET documentation requirement.

### Traceability
PASS — T4 covers D2 (all 4 Lane E methods). Call sites already in T1 (OnFollowerPositionUpdate) and T2 (CancelQxBrackets). No missing items.

### Cross-ticket Dependencies
PASS — Dependency on T1 (fields), T3 (insertion anchor `TryLaneDDispatchFollower`) explicitly stated. Compile-order note explicitly identifies that T4 methods are CALLED from T1 and T2.

### JS Pre-checks
PASS:
- JS-021: ConcurrentDictionary operations (TryAdd, ContainsKey) lock-free. Documented in method comments. ✅
- JS-002: BuildLaneEKey returns string.Empty for null instr, never null. Documented. ✅
- JS-013: `||` operators in IsLaneEEligible are CYC budget items — each counts as +1. Documented in CYC comment. ✅
- JS-081: ASCII-only separators. Documented. ✅
- CYC: IsLaneEEligible=8 AT BUDGET with FROZEN warning box; TryLaneEHook=5; TryLaneEDispatchFollower=5; BuildLaneEKey=2. All ≤ 8. ✅
- IsLaneEEligible FROZEN warning: Explicitly stated as a warning box. ✅

### Pre-Insert Verifications
PASS — All required pre-insert checks documented (same as T3: GetCloneAtmMode/ResolveAtmMode, PassesDailyCapCheck, SendCopyWithAtm). `ResolveAtmMode` call signature adaptation note with 3 call sites identified and adaptation requirement documented. ✅

### NT8 Check
PASS — No lifecycle violations. Background thread usage documented. No async/await. Dispatcher not needed (ConcurrentDictionary writes are thread-safe).

### Insertion Point Correctness
PASS — Anchor: After `TryLaneDDispatchFollower` closing `}` (inserted by T3). Contiguous block. ✅

### Verbatim Body Spot-Check
PASS — IsLaneEEligible, TryLaneEHook, TryLaneEDispatchFollower, BuildLaneEKey bodies match spec §D2 with documented ResolveAtmMode adaptation. ✅

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).

**VERDICT: T4 — TICKET_REVIEW_PASS**

---

## T5 — FlatGuardLoop

**Ticket ID**: T5  
**Spec groups**: B1, B2  
**File**: `src/PropTraderTools/CopyEngine.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present.

### Approved Deletions Table
PASS — Table present, all entries NONE. Note clarifies that foreach loop body replacements are not symbol-level deletions. Correct.

### 7-Scan Checklist
PASS — All 7 scans listed:
- Scan 4: FlatGuardLoop=3. Flatten(Instrument) and Flatten(Account,Instrument) CYC unchanged or reduced. All ≤ 8. ✅
- Scan 5: `[FLAT-GUARD]` log string is ASCII-only. Documented. ✅
- All others: ✅

### Ph4b Verifier Gate Reference
PASS — All 5 gates referenced. Gate 2 note: foreach removals are NOT symbol-level deletions. Correctly noted.

### BUILD_PASS Criteria
PASS — 6 explicit criteria defined including build success note (T1-T5 together).

### Traceability
PASS — T5 covers B1 (FlatGuardLoop method) and B2 (2 call site replacements). All plan items accounted for.

### Cross-ticket Dependencies
PASS — "must be applied in same session as T1-T4 for build to succeed" explicitly stated.

### JS Pre-checks
PASS:
- JS-021: Static, no shared mutable state. Pure iteration. Documented. ✅
- JS-001: No throw. `NinjaTrader.Code.Output.Process` is not a throw (log statement). Documented. ✅
- CYC: FlatGuardLoop=3. ✅

### NT8 Check
PASS — `Output.Process` is thread-safe (safe from non-UI threads). No async/await. No Account.All outside lifecycle.

### Insertion Point Correctness
PASS:
- FlatGuardLoop method: Before `PttDragOrphanPredicates` class (before HEAD line 6386). Pre-insert verification grep command provided. ✅
- Flatten(Instrument) SEARCH+REPLACE: Exact HEAD content provided. ✅
- Flatten(Account,Instrument) SEARCH+REPLACE: Exact HEAD content provided. ✅

### Verbatim Body Spot-Check
PASS — FlatGuardLoop body matches spec §B1 exactly. Both FlatGuardLoop call-site REPLACE blocks match spec §B2 exactly.

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).

**VERDICT: T5 — TICKET_REVIEW_PASS**

---

## T6 — Guardian Script + Protocol Hardening

**Ticket ID**: T6  
**Files**: `scripts/verify_critical_symbols.ps1` (new), `AGENTS.md` (append), `docs/brain/regression-investigation-B179-B180.md` (update)  
**Type**: Non-src only

### Constraint 8 Status
PASS — Ticket explicitly states: "`write_file` ban does NOT apply to this ticket — no `.cs` files are touched. `write_file` is permitted for `scripts/verify_critical_symbols.ps1` (new file). `apply_diff` or `insert_content` is preferred for `AGENTS.md` and the regression doc (existing files)." Correct scoping.

### Approved Deletions Table
PASS — Table present, all entries NONE.

### 7-Scan Checklist
PASS — Ticket correctly states Scans 1–5 and 7 are N/A (no `.cs` file edited) and provides Scan 6 with caveat (script will FAIL until T1–T5 are applied and built). Additional non-scan verification steps listed for AGENTS.md and regression doc. ✅

### Ph4b Verifier Gates
NOT APPLICABLE — Gates 1–5 apply only to tickets touching `CopyEngine.cs` or `TradeCopierPanel.cs` (per global section: "Run for **every ticket** that touches `CopyEngine.cs` or `TradeCopierPanel.cs`"). T6 touches neither. No FAIL.

### BUILD_PASS Criteria
PASS — 4 explicit criteria defined: script syntactically valid + exit 0 after T1–T7, AGENTS.md 4 items, regression doc updated.

### Traceability
PASS — T6 covers "GUARDIAN SCRIPT" and "PROTOCOL HARDENING" sections of spec exactly. Script body reference (lines 776-886) provided. All 4 AGENTS.md bullet points reproduced verbatim. Regression doc entry text provided.

### Spec Coverage Check for Guardian Script
PASS:
- $requiredEngine_B183: 16 symbols ✅
- $requiredEngine_permanent: 12 symbols ✅
- $requiredPanel: 6 symbols ✅
- BUG3 visual check (instr2tBtn.Background = BrushTeal) ✅
- Single-hunk ban ✅
- Deletion audit ✅
- Commit size gate ✅
- Exit 0/1 semantics ✅

### File Routing
PASS — Script at `scripts/verify_critical_symbols.ps1`, non-src docs in correct paths. No .cs files created.

**VERDICT: T6 — TICKET_REVIEW_PASS**

---

## T7 — TradeCopierPanel.cs: BUG3 Visual + BE Arm/Disarm Events

**Ticket ID**: T7  
**Spec groups**: 7A (visual), 7B (BE events), 7C (structural, optional)  
**File**: `src/PropTraderTools/TradeCopierPanel.cs`

### Constraint 8 (apply_diff ban)
PASS — Explicit engineer instruction present for `TradeCopierPanel.cs`.

### B180-2 Constraint
PASS — Hard-stop box present listing all 5 B180-2 approved removals that MUST NOT be re-introduced: `ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected`. Pre-insert verification command to confirm none are in HEAD provided. ✅

### Approved Deletions Table
PASS — Table present, all entries NONE.

### 7-Scan Checklist
PASS — All 7 scans listed, correctly scoped to `TradeCopierPanel.cs`:
- Scan 1: lock() grep → 0 results in new lines ✅
- Scan 2: async void grep → 0 results in new lines ✅
- Scan 3: return null documented; T7 adds 0 new ✅
- Scan 4: CYC for OnPendingBeArmedDispatch, OnGlobalBeAllDisarmed, UpdateBeAllVisuals, DisarmBeAllOnFlat — each must be ≤ 8. "Verify and report in ticket-7-completion.md." CYC not pre-stated (correct: bodies must be obtained from f01b5155 at engineer time; spec also defers to engineer verification). ✅
- Scan 5: non-ASCII grep on TradeCopierPanel.cs ✅
- Scan 6: Guardian script after T6 creates it; confirms all 6 panel symbols + BrushTeal visual fix ✅
- Scan 7: ptt-sync.ps1 ✅

### Ph4b Verifier Gates (T7)
PASS — All 5 gates explicitly present with commands:
- Gate 1: command provided, Count > 1, expected ~7 hunks for 7A + method insertions for 7B ✅
- Gate 2: command provided, expected zero deleted symbol declarations ✅
- Gates 3, 4, 5: referenced with TradeCopierPanel.cs substitution note ✅

### BUILD_PASS Criteria
PASS — 13 explicit criteria defined covering: 7A visual checks (6 items), 7B method presence + event wiring + Dispatcher.InvokeAsync mandate, B180-2 exclusion check, guardian script exit 0, all 7 scans, all 5 Ph4b gates, ptt-sync.

### Traceability
PASS:
- 7A (visual fix): All 5 steps from plan §T7 Sub-scope 7A covered ✅
- 7B (BE events): All 4 methods + event wiring from plan §T7 Sub-scope 7B covered ✅
- 7C (optional structural): Correctly marked as engineer's discretion with decision criteria ✅

### JS Pre-checks
PASS:
- JS-021: No lock(). BE event subscriptions are +=/−= operations (thread-safe NT8 pattern). ✅
- JS-023: Dispatcher.InvokeAsync mandate for OnPendingBeArmedDispatch and OnGlobalBeAllDisarmed explicitly required. Both fire from NT8 account background thread; WPF property sets must use Dispatcher.InvokeAsync. ✅
- JS-033: async void ban — scan 2 confirms. ✅
- JS-081: No Unicode/emoji in new string literals. ✅
- CYC: Engineer must verify f01b5155 body CYC values and report. ✅

### NT8 Check
PASS:
- No FontFamily on WPF elements ✅
- No hardcoded hex colors (BrushTeal is a named brush, not a hex literal) ✅
- No sealed on TradeCopierPanel ✅
- No DateTime.Now usage ✅
- Dispatcher.InvokeAsync mandated (not Dispatcher.Invoke synchronous) ✅

### Source-of-Truth References
PASS — Engineer directed to `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs` for verbatim 7B method bodies. Exact line ranges provided (~1350-1395, ~1655-1665). ✅

### Insertion Point Correctness
PASS:
- 7A-1+7A-2: After SetResourceReference calls in BuildInstrRow (~lines 1193, 1204). Pre-insert verification grep provided. ✅
- 7A-3: 4 BrushInactive→BrushTeal changes at HEAD lines 1079, 1089, 1099, 1109. Grep verification command provided. ✅
- 7A-4: btn.Foreground fix at HEAD line ~1149. ✅
- 7A-5: btn.Background fix at HEAD line ~1153. ✅
- 7B: Insert after SubscribeEngineEvents/OnLoaded (with fallback logic if SubscribeEngineEvents doesn't exist). Pre-insert grep for SubscribeEngineEvents provided. ✅

### File Routing
PASS — `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace).

**VERDICT: T7 — TICKET_REVIEW_PASS**

---

## Aggregate Spec Coverage Check

### All 16 Missing Symbols from Spec §"THE 16 MISSING ITEMS"

| Symbol | Spec Group | Ticket | Status |
|--------|-----------|--------|--------|
| `_followerSlateReady` | A1 | T1 | ✅ |
| `_laneDDispatched` | A1 | T1 | ✅ |
| `_laneEDispatched` | A1 | T1 | ✅ |
| `TryMarkFollowerSlateNotReady` | A2 | T1 | ✅ |
| `TrySetFollowerSlateReady` | A2 | T1 | ✅ |
| `IsAccountFlat` | A2 | T1 | ✅ |
| `HasAnyActiveOrders` | A2 | T1 | ✅ |
| `OnFollowerPositionUpdate` | A2 | T1 | ✅ |
| Call sites in CancelQxBrackets (A3) | A3 | T2 | ✅ |
| Call site in FlattenOneAccount (A3) | A3 | T2 | ✅ |
| FindFollowerBracketOrder gate (A4) | A4 | T2 | ✅ |
| Subscribe/Unsubscribe PositionUpdate (A5) | A5 | T2 | ✅ |
| `FlatGuardLoop<TAccount>` | B1 | T5 | ✅ |
| FlatGuardLoop call sites (B2) | B2 | T5 | ✅ |
| `TryLaneDHook` | C2 | T3 | ✅ |
| `TryLaneDDispatchFollower` | C2 | T3 | ✅ |
| TryLaneDHook call in DispatchCopy (C3) | C3 | T3 | ✅ |
| Lane D skip gate + dispatched counter (C4) | C4 | T3 | ✅ |
| `IsLaneEEligible` | D2 | T4 | ✅ |
| `TryLaneEHook` | D2 | T4 | ✅ |
| `TryLaneEDispatchFollower` | D2 | T4 | ✅ |
| `BuildLaneEKey` | D2 | T4 | ✅ |

All 22 plan-tracked items (16 symbols + 6 call-site/gate items) covered exactly once. No duplicates. No gaps.

### Group-to-Ticket Routing
- Group A (T1, T2): T1 covers A1+A2 ✅; T2 covers A3+A4+A5 ✅
- Group B (T5): T5 covers B1+B2 ✅
- Group C (T3): T3 covers C2+C3+C4 ✅
- Group D (T4): T4 covers D2 ✅
- T6: Guardian script + protocol hardening ✅
- T7: BUG3 visual (7A) + BE arm/disarm events (7B) ✅

### TrySetSlateReadyOnDrainFill Check
PASS — Not mentioned in any ticket. Correctly excluded (spec Constraint 6: deliberately NOT in scope).

---

## Cross-Ticket Dependency Map

| Dependency | Stated | Location |
|------------|--------|----------|
| T2 depends on T1 (fields + methods) | ✅ | T2 header |
| T2 depends on T4 (BuildLaneEKey, for build) | ✅ | T2 header |
| T3 depends on T1 (_laneDDispatched) | ✅ | T3 header |
| T4 depends on T1 (fields _laneEDispatched, _followerSlateReady) | ✅ | T4 header |
| T4 depends on T3 (TryLaneDDispatchFollower anchor) | ✅ | T4 header |
| T4 methods called from T1 (OnFollowerPositionUpdate→TryLaneEHook) | ✅ | T4 compile-order note |
| T4 methods called from T2 (CancelQxBrackets→BuildLaneEKey) | ✅ | T4 compile-order note |
| T5 must be applied in same session as T1-T4 | ✅ | T5 header |
| T6 independent | ✅ | T6 header |
| T7 independent (Lane B) | ✅ | T7 header |

All cross-ticket dependencies fully declared.

---

## BUG Verification Path Coverage

### BUG1 (cancel+reorder not copying)
Call trace: T1 (TryMarkFollowerSlateNotReady) → T2 (CancelQxBrackets call site) → T2 (FindFollowerBracketOrder gate) → T1 (OnFollowerPositionUpdate → TrySetFollowerSlateReady) → T2 (Subscribe wiring) → T3 (TryLaneDHook in DispatchCopy) → T3 (Lane D skip gate in DispatchToFollower). **Traceable across T1+T2+T3.** ✅

### BUG2 (re-entry after close)
Call trace: T2 (FlattenOneAccount call site) → T1 (TryMarkFollowerSlateNotReady) → T4 (IsLaneEEligible gate=closed) → T4 (TryLaneEHook from OnFollowerPositionUpdate) → T5 (FlatGuardLoop prevents premature flatten). **Traceable across T1+T2+T4+T5.** ✅

### BUG3 (Quick2t/QAll2t colour + BE arm/disarm)
T7 7A (BrushTeal visual fix in BuildInstrRow, specs tuple, foreach) + T7 7B (OnPendingBeArmedDispatch, OnGlobalBeAllDisarmed, UpdateBeAllVisuals, DisarmBeAllOnFlat re-add with Dispatcher.InvokeAsync). **Traceable entirely within T7.** ✅

---

## Summary Table

| Ticket | Constraint 8 | Approved Deletions | 7-Scan | Ph4b Gates | BUILD_PASS | Traceability | JS Pre-check | CYC | NT8 | File Routing | Verdict |
|--------|-------------|-------------------|--------|-----------|----------|-------------|-------------|-----|-----|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T4 | PASS | PASS | PASS | PASS† | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T5 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T6 | N/A | PASS | PASS | N/A | PASS | PASS | N/A | N/A | N/A | PASS | **PASS** |
| T7 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS† | PASS | PASS | **PASS** |

**†T4 Ph4b Gate 1 note**: T4's gate note uses "count ≥ 1" in an explanatory context; the global Gate 1 definition (Count > 1) takes precedence. Advisory only — no structural violation.  
**†T7 Scan 4 CYC**: f01b5155 method CYC values not pre-determinable; engineer must verify and report. Correctly delegated. Advisory only.

---

## FINAL VERDICT

```
TICKET_REVIEW_PASS: YES
```

All 7 tickets are approved for Phase 4a engineering.

**Engineer lane instructions:**
- **Lane A (T1→T2→T3→T4→T5)**: Apply all 5 sets of insertions in a single editing session before first build.
- **Lane B (T6, T7)**: May proceed independently and in parallel with Lane A.
- **Global mandate**: apply_diff ONLY on CopyEngine.cs and TradeCopierPanel.cs. write_file = VERIFY_FAIL.
- **Post-edit**: `powershell -File .\scripts\ptt-sync.ps1` after every .cs session.

---

*Reviewed by: ptt-ticket-reviewer (Phase 3.5) — 2026-07-07*
