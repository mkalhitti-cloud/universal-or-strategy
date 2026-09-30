# B119 Final Review -- DW-B128 Direction-Change Guard

**Reviewer**: ptt-plan-reviewer  
**Block**: B119  
**Defect**: DW-B128  
**Review date**: 2026-08-27  
**Phase**: 5 -- Final Cross-File Coherence Review  
**Source files read independently**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B119Tests.cs`  
**Artifacts read**: 02-architecture-plan.md, 02-plan-review.md, 04-tickets.md, 04-ticket-review.md, ticket-1-completion.md, ticket-1-verification.md, docs/brain/B107/06-deferred-backlog.md

---

## Review Result: FINAL_PASS

---

## A. Spec Requirements Satisfied

| Requirement | Status | Evidence |
|-------------|--------|----------|
| DW-B128: Reversal entry to flat followers is blocked | PASS | `if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))` at CopyEngine.cs L1855; `continue` at L1866 skips the follower |
| First-entry copy unaffected (no `_lastLeaderDirection` key) | PASS | `TryGetValue` returns `false` when key absent; `hasLastDirection=false` causes `&&` to short-circuit; guard cannot fire; copy proceeds normally |
| Same-direction copy unaffected | PASS | `IsReversalToFlatFollower(Buy, Buy, flat)` -- `currentAction != lastAction` is `false` -- helper returns `false`; guard does not fire |
| Reversal copy to non-flat followers unaffected | PASS | `followerIsFlat = IsFlat(FindPosition(acc, instr))` = `false` when follower has open position; `&& followerIsFlat` is `false`; helper returns `false`; copy proceeds |

**Section A verdict: PASS**

---

## B. Cross-File Coherence

| Item | Status | Evidence |
|------|--------|----------|
| `_lastLeaderDirection` field is in CopyEngine.cs (not a separate file) | PASS | Grep confirmed: CopyEngine.cs:308 `private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection` |
| `IsReversalToFlatFollower` is in CopyEngine.cs (not a separate file) | PASS | Grep confirmed: CopyEngine.cs:3347 `internal static bool IsReversalToFlatFollower(` |
| No circular dependency introduced | PASS | Helper is a pure static method with 3 value-type parameters; no instance references; no new imports |
| `B119Tests.cs` is a new file (not overwriting existing) | PASS | Git status shows `?? src/PropTraderTools/Tests/B119Tests.cs` (untracked new file) |
| No changes to other .cs files (scope contained) | PASS | Git status shows only `CopyEngine.cs` as modified (`M`) among .cs files in scope; no other .cs files touched |

**Section B verdict: PASS**

---

## C. All 7 Scans Clean

Independent scans run by ptt-verifier (Layer 3 -- not copied from engineer self-report):

| Scan | Description | Result | PASS/FAIL |
|------|-------------|--------|-----------|
| SCAN 1 | `lock()` audit -- `Select-String -Pattern "lock\s*\("` | 8 hits, ALL in comment annotations ("no lock()"); 0 in executable code | PASS |
| SCAN 2 | `async void` audit | 0 results; no new async methods introduced | PASS |
| SCAN 3 | `return null` audit | 7 pre-existing sites (L1532, L2057, L2103, L3320, L3326, L3401, L4216); 0 new sites in B119 code | PASS |
| SCAN 4 | `throw` audit -- `Select-String -Pattern "\bthrow\b"` | ~40 hits, ALL in "no throw" comment annotations; 0 actual throw statements in new code | PASS |
| SCAN 5 | ASCII audit -- `[regex]::Matches([IO.File]::ReadAllText("CopyEngine.cs"), '[^\x00-\x7F]').Count` | 0 non-ASCII characters | PASS |
| SCAN 6 | CYC audit (manual count, `complexity_audit.py` not present) | DispatchCopy=8 (branch-merge -1 + reversal guard +1 = net 0); IsReversalToFlatFollower=2 | PASS |
| SCAN 7 | Build audit -- `dotnet build PropTraderTools.csproj` | 83 pre-existing errors in CopyEngineTests.cs / TradeCopierPanel.cs / Globals ambiguity L4093; 0 errors referencing B119 identifiers; confirmed by targeted grep | PASS (V12.23 No Scope Creep: pre-existing errors exempt) |

**Section C verdict: PASS**

---

## D. JS Rule Final Compliance

| Rule | Requirement | Evidence | Result |
|------|-------------|----------|--------|
| JS-021 | No `lock()` anywhere | `_lastLeaderDirection` uses `ConcurrentDictionary<string, OrderAction>`; `TryGetValue` (L1833) and indexer-set (L1904) are both atomic; SCAN 1 = 0 actual lock() | PASS |
| JS-001 | No `throw` in hot path | `IsReversalToFlatFollower` body is a single `return` expression (`return currentAction != lastAction && followerIsFlat;`); no throw path anywhere in the change; SCAN 4 = 0 actual throw | PASS |
| JS-002 | No `return null` for missing values | `TryGetValue` with `out` param is the correct pattern; no new `return null` sites; SCAN 3 confirmed 0 new sites | PASS |
| JS-033 | No `async void` | No new async methods introduced; SCAN 2 = 0 | PASS |
| CYC <= 8 | All modified methods | DispatchCopy=8 maintained via branch-merge (L1827+L1832 merged to compound `||`, slot freed for reversal guard); IsReversalToFlatFollower=2 (one `&&` in single `return`) | PASS |
| ASCII-only | No Unicode in strings or identifiers | Log string `[PTT-COPY-GUARD] skip reversal entry: ... follower flat` is 7-bit ASCII; all new identifiers (`_lastLeaderDirection`, `IsReversalToFlatFollower`, `hasLastDirection`, `followerIsFlat`, `currentAction`, `lastAction`, `instr`) are ASCII-only; SCAN 5 = 0 | PASS |
| PTT- prefix on CreateOrder | All order names prefixed | Not applicable -- no `CreateOrder` calls in this change | N/A |
| No FontFamily/hex colors/DateTime.Now | UI and NT8 rules | Not applicable -- no UI code, no DateTime usage | N/A |

**Section D verdict: PASS**

---

## E. Test Completeness

| Item | Status | Evidence |
|------|--------|----------|
| B119Tests.cs exists with at minimum 6 pure unit tests | PASS | File present; grep confirms 11 `[Fact]` methods (11 > 6 minimum) |
| xUnit framework only | PASS | `using Xunit;` at L10; no NUnit, no MSTest imports |
| All 4 direction combinations covered | PASS | A1 (Buy,Buy,flat=T -> false); A2 (Sell,Sell,flat=T -> false); A3 (Sell,Buy,flat=T -> true); A4 (Buy,Sell,flat=T -> true) |
| First-entry test present | PASS | A6 (`T_IsReversalToFlatFollower_NoLastDirection_NotFired`) covers the first-dispatch invariant at unit level; B1 (`T_DirDict_AbsentKey_TryGetValue_ReturnsFalse`) covers absent-key at dictionary level |
| Integration tests listed | PASS | Parts B1-B3 exercise the `ConcurrentDictionary<string, OrderAction>` contract directly (true `DispatchCopy` integration infeasible without NT8 runtime; acknowledged in 04-ticket-review.md Section E as architecturally sound substitute) |
| C-series BuyToCover/SellShort variants | PASS | C1 (`T_IsReversalToFlatFollower_BuyToCoverToSellShort_Flat_ReturnsTrue`) and C2 (`T_IsReversalToFlatFollower_SellShortToBuyToCover_Flat_ReturnsTrue`) present |

**Section E verdict: PASS**

---

## F. Guard Correctness (behavioral)

Verified against actual `CopyEngine.cs` source (lines 1826-1905 read directly):

| Item | Status | Code Reference |
|------|--------|----------------|
| Guard fires ONLY when BOTH conditions true: direction changed AND follower flat | PASS | L1855: `if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))` -- `hasLastDirection` gates on prior direction existing; helper body `currentAction != lastAction && followerIsFlat` requires BOTH conditions simultaneously |
| Guard check is per-follower (inside the loop) | PASS | Guard at L1855 is inside `foreach (var acc in rule.FollowerAccounts)` at L1839; `followerIsFlat = IsFlat(FindPosition(acc, instr))` at L1854 is re-evaluated per `acc`; each follower evaluated independently |
| `continue` used (not `return`) | PASS | L1866: `continue;` -- skips only the current follower iteration; does not exit `DispatchCopy`; other followers in the same dispatch are unaffected |
| Dictionary update is AFTER the foreach (not inside) | PASS | L1900: `}` closes the foreach. L1902-1904: `// B119: DW-B128 -- record direction dispatched...` followed by `_lastLeaderDirection[instr.FullName] = currentAction;` -- write is after the loop; all followers in the current dispatch see the same `lastAction`; partial-update scenario is impossible |

**Section F verdict: PASS**

---

## G. No Scope Creep (V12.23)

| Item | Status | Evidence |
|------|--------|----------|
| Only CopyEngine.cs and B119Tests.cs touched | PASS | Git status: `M src/PropTraderTools/CopyEngine.cs` (modified); `?? src/PropTraderTools/Tests/B119Tests.cs` (new); no other .cs files in the change set |
| No unrelated bug fixes bundled | PASS | ticket-1-completion.md explicitly categorizes 83 pre-existing build errors as exempt under V12.23; zero B119-introduced errors; no side-fixes applied to CopyEngineTests.cs, TradeCopierPanel.cs, or CopyEngine.cs:4093 |
| No formatting changes to unrelated code | PASS | Only 3 insertion regions in CopyEngine.cs: (1) field ~L305, (2) DispatchCopy L1829-1904, (3) helper L3342-3353; no whitespace mutations outside those regions |

**Section G verdict: PASS**

---

## Section K -- Deferred Work

Items not addressed in B119 that require future blocks.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B128 | Reversal Entry Dispatched to Flat Followers | P0 | B119 | CLOSED |
| B119-DEFER-01 | F5 NinjaTrader 8 Compilation Gate -- Director must press F5 in NT8 after ptt-sync-and-verify.ps1 completes with 0 MISMATCH | P0 | Director immediate | OPEN |
| B119-DEFER-02 | SIM Gate: Reversal Guard Behavioral Verification -- live NT8 SIM session required to confirm `[PTT-COPY-GUARD]` log lines fire on reversal+flat scenario and copy proceeds on first-entry, same-direction, and non-flat-follower scenarios | P1 | Director after B119-DEFER-01 | OPEN |
| DW-B107 | MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers (stale OCO pairs included from prior session) | P2 | B108 (next pipeline) | OPEN (carry-forward) |
| B107-DEFER-01 | F5 NT8 Compilation Gate (B107 changes) | P0 | Director | OPEN (carry-forward) |
| B107-DEFER-02 | Combo C Live Re-Test (BE-ALL then QX-ALL, stale partial-fill residue) | P1 | Director SIM gate | OPEN (carry-forward) |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low | B43 or first T3-confirmed block | OPEN (carry-forward) |
| DW-B42-02 | Live NT8 F5 verification for DW-B42 directions | High | Next F5 session | OPEN (carry-forward) |
| DW-B42-03 | IsPttQxTarget range extension for future T4/T5 target slots | Conditional | Block adding 4th+ slot | OPEN (carry-forward) |
| DW-PTT-BE-FIX-01 | Lazy re-resolve for null followers (Option A, DW-B85) | Medium | Next PTT productionisation block | OPEN (carry-forward) |
| DW-PTT-BE-FIX-02 | SIM gate Path B 3-cycle runtime verification (QX-ALL then BE-ALL) | High | Director SIM session | OPEN (carry-forward) |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (CopyEngineTests.cs 83 errors + CS0433 Globals) | High | Dedicated remediation block | OPEN (carry-forward) |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate (DW-B89 changes) | P0 | Director | OPEN (carry-forward) |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal (entry -> BE-ALL, 3 cycles) | High | Director after DEFERRED-01 | OPEN (carry-forward) |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position) | High | Director after DEFERRED-01 | OPEN (carry-forward) |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | High | Director after DEFERRED-01 | OPEN (carry-forward) |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle (BE-ALL immediately) | High | Director after DEFERRED-01 | OPEN (carry-forward) |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML after SIM gate passes | Medium | After all DW-B89 SIM paths green | OPEN (carry-forward) |

---

## Decision

**FINAL_PASS**

All 7 checklist sections (A through G) return PASS. Zero JS rule violations. All spec requirements for DW-B128 are satisfied. CopyEngine.cs and B119Tests.cs form a complete, coherent, and compliant implementation:

- `_lastLeaderDirection` (ConcurrentDictionary, L308) is thread-safe with no `lock()` -- JS-021 compliant.
- `IsReversalToFlatFollower` (internal static, L3347) has CYC=2, no throw, pure function -- JS-001 compliant.
- `DispatchCopy` modified with per-follower guard inside the loop, dictionary updated after the loop, `continue` (not `return`) used -- behavioral invariants AC1-AC7 all verified.
- 11 xUnit-only [Fact] tests cover all 4 direction combos, first-entry, not-flat, BuyToCover/SellShort variants, and dictionary invariants.
- Build SCAN 7 zero B119 errors; 83 pre-existing errors are V12.23 exempt.
- Section K and 06-deferred-backlog.md are both present as required by the FINAL_PASS gate.
