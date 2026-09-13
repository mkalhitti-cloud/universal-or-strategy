# Ticket Review: PTT-BUGS-LIVE-0911 Lane E
**Reviewer**: ptt-ticket-reviewer (Ph3.5)  
**Ticket file**: `docs/brain/PTT-BUGS-LIVE-0911/04-tickets-lane-e.md`  
**Plan file**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md` (REVIEW_PASS, Cycle 3)  
**Rules file**: `docs/standards/RULES_CATALOG.md`  
**Date**: 2026-09-11  

---

## T1 — Re-arm ATM Bracket After QX/BE Partial Exit (Clone Mode)

---

### TC-1: Traceability

14 spec requirement IDs (LANE-E-REQ-01 through LANE-E-REQ-14) are present. Every item maps to at least one plan section citation (§3–§11, App.A). All seven architecture decisions from the plan are covered:

| Architecture Decision | Requirement IDs | Plan Section | Covered In Ticket |
|---|---|---|---|
| Q1 Trigger condition | REQ-02 | §4, §11.5a | IsLaneEEligible guards A–E (Step 5) |
| Q2 Position quantity | REQ-03 | §5, §11.6 | pos.Quantity in TryLaneEDispatchFollower (Step 7) |
| Q3 ATM template | REQ-03, REQ-14 | §6, §3.4 | GetCloneAtmMode in IsLaneEEligible + SendCopyWithAtm |
| Q4 Dedup design | REQ-04, REQ-05 | §7, §11.1–11.4 | _laneEDispatched field + BuildLaneEKey (Steps 1, 8) |
| Q5 Gate interaction | REQ-06, REQ-07 | §8 | TryLaneEHook if(ok) gate-open (Step 6) |
| Q6 CYC budget | REQ-12 | §9 | CYC stated per method; SCAN-02 |
| Q7 Non-Clone safety | REQ-08 | §10 | IsLaneEEligible check (1) |

No phantom work (all items in ticket map to plan). No missing work (all plan elements mapped to ticket steps).

**Traceability: PASS**

---

### TC-2: Method Signatures

All four new method signatures present and exact (ticket lines 363–368):

| Method | Exact Signature | Present |
|---|---|---|
| `IsLaneEEligible` | `private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)` | ✓ |
| `TryLaneEHook` | `private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)` | ✓ |
| `TryLaneEDispatchFollower` | `private bool TryLaneEDispatchFollower(Account acc, NinjaTrader.Cbi.Instrument instr, NinjaTrader.Cbi.Position pos, FollowerAtmMode.Named namedMode, string dedupKey)` | ✓ |
| `BuildLaneEKey` | `private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)` | ✓ |

Field declaration exact: `private readonly ConcurrentDictionary<string, byte> _laneEDispatched = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);` ✓

`OnFollowerPositionUpdate` modification described with exact anchor (the `TrySetFollowerSlateReady(e.Position.Account);` line, before closing brace) ✓. Both `CancelQxBrackets` modifications anchored to the exact comment string on the preceding line ✓.

**Method Signatures: PASS**

---

### TC-3: JS Pre-Check

| Rule | Ticket Coverage | Result |
|---|---|---|
| JS-021 (no lock) | Every new method's constraint table row states "No lock." SCAN-01 present. Field declared as ConcurrentDictionary. | ✓ |
| JS-001 (no throw) | Every new method: "No throw." REQ-10 explicitly cites JS-001. All paths use early-return or return false. | ✓ |
| JS-002 (no null return) | Bool-returning methods return `false` on all failure paths. `BuildLaneEKey` returns a non-null string (empty-suffix on null instr). `TryLaneEHook` is void. | ✓ |
| JS-023 (UI off-thread) | Constraint table for TryLaneEHook: "No WPF update — StatusUpdate raised inside SendCopyWithAtm (existing pattern)." REQ-11 cites JS-023. | ✓ |
| JS-025 (ConcurrentDictionary) | _laneEDispatched declared as `ConcurrentDictionary<string, byte>` with `StringComparer.Ordinal`. | ✓ |

**JS Pre-Check: PASS**

---

### TC-4: CYC Pre-Check

**FAIL — incorrect CYC stated for `TryLaneEDispatchFollower`.**

The ticket (Step 7, CYC verification comment, line ~325) states:

> "Count: entry(1) + A(1) + B(1) + C(1) = **4**. The `?:` ternary for `action` is counted under branch C's if(ok) block (same decision node grouping per plan §9)."

This is incorrect under JS-013. JS-013 (RULES_CATALOG.md §1) is explicit:

> "Add +1 for each: `if`, `else if`, `case`, `while`, `for`, `foreach`, `catch`, `?:`, `??`, `&&`, `||`."

The `?:` ternary at the `action` assignment (`pos.MarketPosition == … ? OrderAction.Buy : OrderAction.SellShort`) is a standalone decision point. It cannot be subsumed into another `if` branch's count. The ticket itself acknowledges this: "it is still +1 per JS-013" — then immediately contradicts that acknowledgement.

**Correct JS-013 McCabe count for `TryLaneEDispatchFollower`:**

```
method entry                                      +1
(A) if (acc == null)                              +1
(B) if (!PassesDailyCapCheck(acc))                +1
?:  action = ... Long ? Buy : SellShort           +1   ← ternary, JS-013 §1
(C) if (ok)                                       +1
                                         Total =   5
```

Stated CYC in ticket: **4**. Correct CYC: **5**.

This is NOT a budget violation (5 ≤ 8 is within budget). However, the ticket is the Layer 1 engineer contract and anchors the Layer 3 verifier cross-check (SCAN-02). If the ticket states CYC=4 and the verifier independently counts CYC=5, a VERIFY_FAIL is triggered. The stated value in the SCAN-02 required count line must be corrected to 5.

Additionally, SCAN-02 (ticket lines 553–558) states:
> `TryLaneEDispatchFollower  : entry(1) + 3 if-branches = must equal 4`

This must read: `entry(1) + 2 if-branches + 1 ternary + 1 if-branch = must equal 5`.

The plan §9 `TryLaneEDispatchFollower` branch count also omits the ternary (plan line 365: "entry + 3 branches = 4"). The plan has the same defect, but the ticket is the controlling contract for the engineer. The ticket review is the last checkpoint before code is written. Both the ticket body and SCAN-02 must be corrected before this ticket receives TICKET_REVIEW_PASS.

**Citation**: JS-013 (RULES_CATALOG.md §1, counting standard). Violated in: Step 7 CYC verification note + SCAN-02 required value line for `TryLaneEDispatchFollower`.

**CYC Pre-Check: FAIL**

---

### TC-5: NT8 API Constraints

| Constraint | Coverage | Result |
|---|---|---|
| `StartAtmStrategy` STATIC form | Delegated to `SendCopyWithAtm` (unchanged); plan §3.4 shows static form; REQ-14 cites NT8-007 | ✓ |
| Order name "Entry" for ATM | Plan §3.4 hardcodes "Entry"; Step 7 note explicitly addresses NT8-007 | ✓ |
| `AtmStrategyCreate` BANNED | SCAN-05 grep pattern; REQ-14 cites NT8-008 | ✓ |
| `AtmStrategyChangeStopTarget` BANNED | SCAN-05 grep pattern; REQ-14 cites NT8-009 | ✓ |
| `async/await` BANNED in lifecycle/event methods | SCAN-03 explicit; all new methods synchronous void or bool | ✓ |

**NT8 Check: PASS**

---

### TC-6: Completeness

| Modification | Step | Anchor | Clear? |
|---|---|---|---|
| `OnFollowerPositionUpdate` | Step 2 | After `TrySetFollowerSlateReady(e.Position.Account);` before `}` | ✓ |
| `CancelQxBrackets` 2-param | Step 3 | After `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent` | ✓ |
| `CancelQxBrackets` 3-param | Step 4 | After `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)` | ✓ |

Dedup key format explicitly specified as `acc.Name + "#LANE-E#" + instr.FullName` in: Step 1 comment, Field Declarations table, Step 6 method body (`key` variable), SCAN-07(b).

All 3 modifications described. All anchors unambiguous (exact comment strings that are unique within the method).

**Completeness: PASS**

---

### TC-7: Test Coverage

All 10 test names present (T_LANE_E_01 through T_LANE_E_10). Coverage:

| ID | Method Name | Coverage Topic | Skip Annotation |
|---|---|---|---|
| T_LANE_E_01 | `TryLaneEHook_CloneMode_PositionExists_ArmsAtmBracket` | Clone re-arm dispatch | `[Fact(Skip="NT8-runtime:...")]` ✓ |
| T_LANE_E_02 | `TryLaneEHook_NonCloneMode_IsNoOp` | Non-Clone no-op | `[Fact]` (no Skip) ✓ |
| T_LANE_E_03 | `TryLaneEHook_NullPosition_IsNoOp` | Null position guard | `[Fact]` (no Skip) ✓ |
| T_LANE_E_04 | `TryLaneEHook_ZeroQuantity_IsNoOp` | Zero qty guard | `[Fact]` (no Skip) ✓ |
| T_LANE_E_05 | `TryLaneEHook_GateAlreadyOpen_IsNoOp` | Gate already open | `[Fact(Skip="NT8-runtime:...")]` ✓ |
| T_LANE_E_06 | `TryLaneEHook_DoubleArm_DeduplicatedByLaneEDispatched` | Double-arm dedup | `[Fact(Skip="NT8-runtime:...")]` ✓ |
| T_LANE_E_07 | `BuildLaneEKey_ReturnsExpectedKeyFormat` | Key format | `[Fact]` (no Skip) ✓ |
| T_LANE_E_08 | `BuildLaneEKey_NullInstrument_ReturnsKeyWithEmptyInstrSuffix` | Null instrument key | `[Fact]` (no Skip) ✓ |
| T_LANE_E_09 | `CancelQxBrackets_ResetsLaneEDispatchedKey` | CancelQxBrackets resets key | `[Fact(Skip="NT8-runtime:...")]` ✓ |
| T_LANE_E_10 | `IsLaneEEligible_AllGuardsPass_ReturnsTrue` | IsLaneEEligible guards | `[Fact(Skip="NT8-runtime:...")]` ✓ |

Skip/no-Skip pattern: T_LANE_E_02/03/04/07/08 = no Skip ✓; T_LANE_E_01/05/06/09/10 = Skip with NT8-runtime reason ✓.
SCAN-07(g) encodes the same rules ✓.
Test class `#region Lane E Tests` additive-only, zero existing tests modified ✓.

**Test Coverage: PASS**

---

### TC-8: 7-Scan Checklist Presence

All 7 scans present (ticket lines 541–608):

| Scan | Present | Command | Required Result |
|---|---|---|---|
| SCAN-01 | ✓ | `grep -r "lock("` + PowerShell equivalent | 0 matches |
| SCAN-02 | ✓ | Manual McCabe for all 7 new/modified methods | Per-method values listed |
| SCAN-03 | ✓ | `grep -r "\basync\b\|\bawait\b"` | 0 in new/modified executable code |
| SCAN-04 | ✓ | PowerShell byte scan `$_ -gt 127` | 0 |
| SCAN-05 | ✓ | `grep -r "AtmStrategyCreate\|AtmStrategyChangeStopTarget"` | 0 actual calls |
| SCAN-06 | ✓ | `powershell -File .\scripts\build_readiness.ps1` | 0 errors, 0 failures, specific tests pass/skip |
| SCAN-07 | ✓ | Structural items (a)–(g) all present | Confirmed in diff |

SCAN-07 items (a) through (g) all present ✓.

Note: SCAN-02's required value for `TryLaneEDispatchFollower` is stated as 4 — this is incorrect per TC-4 above. The scan checklist itself is fully present (7/7 scans, all structural items); however, the required value for `TryLaneEDispatchFollower` in SCAN-02 is wrong. The checklist is present as required, but SCAN-02's anchor value must be corrected in conjunction with the TC-4 fix.

**Scan Checklist: PASS** (all 7 scans present; the incorrect required value in SCAN-02 is tracked as part of the TC-4 FAIL, not a separate missing-checklist failure)

---

### TC-9: Scope Integrity

Scope Lock table explicitly limits changes to exactly two files ✓.
"Any change not listed above is OUT OF SCOPE" stated explicitly ✓.
Out of Scope section enumerates: `TrySetFollowerSlateReady`, `TryMarkFollowerSlateNotReady`, `SendCopyWithAtm`, `TryLaneDHook`, `TryLaneDDispatchFollower`, `_laneDDispatched`, `_followerSlateReady` field declaration, and all files outside the two listed ✓.
No Lane A/B/C/D re-implementation present ✓.

**Scope Integrity: PASS**

---

### TC-10: Gate Integrity Constraint

REQ-07 explicitly states: "Gate integrity: `TrySetFollowerSlateReady` and `TryMarkFollowerSlateNotReady` must NOT be modified — both methods: zero modification." ✓

Step 6 (`TryLaneEHook`): gate is opened via `_followerSlateReady[acc.Name] = true` inside the `if (ok)` block in `TryLaneEHook` directly — not via `TrySetFollowerSlateReady` ✓.

Pre-Implementation Checklist item 4: "Do not modify `TrySetFollowerSlateReady`, `TryMarkFollowerSlateNotReady`, or `SendCopyWithAtm`." ✓

Out of Scope section reiterates both methods as READ-ONLY ✓.

SCAN-07(e): "TrySetFollowerSlateReady is byte-for-byte unchanged (diff confirms)" ✓.

**Gate Integrity: PASS**

---

## Violation Summary

| Check | Result | Violation |
|---|---|---|
| TC-1 Traceability | PASS | — |
| TC-2 Method Signatures | PASS | — |
| TC-3 JS Pre-Check | PASS | — |
| **TC-4 CYC Pre-Check** | **FAIL** | **`TryLaneEDispatchFollower` CYC stated as 4; correct JS-013 McCabe count is 5 (ternary `?:` at action assignment is +1 per JS-013, not subsumed into branch C). Ticket Step 7 CYC verification note and SCAN-02 required value line both state 4. Must be corrected to 5 in both locations.** |
| TC-5 NT8 Constraints | PASS | — |
| TC-6 Completeness | PASS | — |
| TC-7 Test Coverage | PASS | — |
| TC-8 Scan Checklist | PASS | — |
| TC-9 Scope Integrity | PASS | — |
| TC-10 Gate Integrity | PASS | — |

---

## Required Fixes Before Resubmission

**Fix 1 — Step 7, CYC verification comment** (ticket line ~325):  
Replace:
```
Count: entry(1) + A(1) + B(1) + C(1) = **4** ≤ 8 ✓. The `?:` ternary for `action`
is counted under branch C's if(ok) block (same decision node grouping per plan §9).
```
With:
```
Count: entry(1) + A(1) + B(1) + ?:action(1) + C/if(ok)(1) = **5** ≤ 8 ✓.
The `?:` ternary for `action` is +1 per JS-013 as an independent decision point.
```

**Fix 2 — SCAN-02 required value line for `TryLaneEDispatchFollower`** (ticket line ~554):  
Replace:
```
TryLaneEDispatchFollower  : entry(1) + 3 if-branches = must equal 4
```
With:
```
TryLaneEDispatchFollower  : entry(1) + 2 if-branches + 1 ternary ?: + 1 if-branch = must equal 5
```

No other changes required. After these two corrections, all TC checks pass.

---

## Overall

**TICKET_REVIEW_FAIL**

Single violation: TC-4 CYC pre-check — `TryLaneEDispatchFollower` CYC stated as 4 in both Step 7 verification note and SCAN-02 required value; correct JS-013 count is 5. The ternary `?:` operator is an independent +1 per JS-013 and cannot be collapsed into another branch's count. The stated value is the Layer 1 contract anchor for the verifier's SCAN-02 cross-check; an incorrect anchor guarantees a downstream VERIFY_FAIL.

Required fixes: two targeted corrections (Step 7 CYC note + SCAN-02 required value). No architectural changes needed. All other checks pass.

---

## Cycle 2 Re-Review — 2026-09-11

**Reviewer**: ptt-ticket-reviewer (Ph3.5, Cycle 2)
**Trigger**: Architect resubmission after TC-4 FAIL (Cycle 1). Two targeted fixes applied to `04-tickets-lane-e.md`:
1. Step 7 CYC verification note for `TryLaneEDispatchFollower` corrected to CYC=5.
2. SCAN-02 required value line for `TryLaneEDispatchFollower` corrected to 5.

---

### TC-4 Re-Verification: CYC Pre-Check (Primary Focus)

**Independent McCabe recount of `TryLaneEDispatchFollower` from Cycle 2 ticket pseudo-code (Step 7):**

```
method entry                                                    +1  →  running total: 1
(A) if (acc == null) return false;                              +1  →  running total: 2
(B) if (!PassesDailyCapCheck(acc)) return false;               +1  →  running total: 3
?:  action = pos.MarketPosition == Long ? Buy : SellShort      +1  →  running total: 4   ← JS-013 §1 standalone ternary
(C) if (ok) _laneEDispatched.TryAdd(dedupKey, 0);             +1  →  running total: 5
                                                               ─────────────────────────
                                                    Total =    5
```

**Ticket-stated value (Step 7 CYC note, line ~325)**:
> `entry(1) + A(1) + B(1) + ?:action(1) + C/if(ok)(1) = **5** ≤ 8 ✓`
→ Stated CYC = **5**. Matches independent count ✓

**Ticket-stated value (JS Rule Constraints table, `TryLaneEDispatchFollower` row)**:
> `CYC=5. No throw. No lock. Calls SendCopyWithAtm … ?: ternary action assignment counts +1 per JS-013.`
→ CYC = **5** ✓

**SCAN-02 required value line**:
> `TryLaneEDispatchFollower  : entry(1) + 2 if-branches + 1 ternary ?: + 1 if-branch = must equal 5`
→ Required value = **5** ✓

All three locations agree. My independent count = 5. All match. **TC-4: PASS** (Cycle 2).

---

### Full Re-Review: TC-1 through TC-10

#### TC-1: Traceability
Identical to Cycle 1 (no structural changes). 14 requirement IDs, all seven architecture decisions covered, no phantom or missing work.
**Traceability: PASS**

---

#### TC-2: Method Signatures
Identical to Cycle 1. All four new method signatures present and exact. Field declaration exact. Modification anchors unambiguous.
**Method Signatures: PASS**

---

#### TC-3: JS Pre-Check

| Rule | Coverage | Result |
|---|---|---|
| JS-001 (no throw) | All new methods use early-return or return false on all failure paths | ✓ |
| JS-002 (no null return) | Bool-returning methods return `false`; `BuildLaneEKey` returns non-null string; `TryLaneEHook` is void | ✓ |
| JS-021 (no lock) | All state uses `ConcurrentDictionary`; SCAN-01 present | ✓ |
| JS-023 (no off-thread UI) | `StatusUpdate?.Invoke` stays inside `SendCopyWithAtm` (existing); new methods have no WPF updates | ✓ |
| JS-025 (ConcurrentDictionary) | `_laneEDispatched` declared as `ConcurrentDictionary<string, byte>` with `StringComparer.Ordinal` | ✓ |

**JS Pre-Check: PASS**

---

#### TC-4: CYC Pre-Check

| Method | Stated CYC | Independent Count | Operators Counted | Budget | Result |
|---|---|---|---|---|---|
| `OnFollowerPositionUpdate` (modified) | 3 | 3 (entry+2 if) | — | ≤8 | ✓ |
| `IsLaneEEligible` | 8 | 8 (entry+if+if+||+if+if+||+if) | 2× `||` each +1 | ≤8 | ✓ |
| `TryLaneEHook` | 5 | 5 (entry+3 if, zero `||`/`&&`) | none | ≤8 | ✓ |
| `TryLaneEDispatchFollower` | **5** | **5** (entry+A+B+?:+C) | `?:` +1 per JS-013 | ≤8 | ✓ |
| `BuildLaneEKey` | 2 | 2 (entry+`?:`) | `?:` +1 per JS-013 | ≤8 | ✓ |
| `CancelQxBrackets` 2-param (modified) | 4 | 4 (unchanged; helper call +0) | — | ≤8 | ✓ |
| `CancelQxBrackets` 3-param (modified) | 8 | 8 (unchanged; helper call +0; `??` inside `BuildLaneEKey` not here) | — | ≤8 | ✓ |

No method exceeds CYC=8. No ternary subsumption error remains. JS-013 §1 correctly applied throughout.

**CYC Pre-Check: PASS**

---

#### TC-5: NT8 API Constraints
Identical to Cycle 1 — no changes to NT8 coverage. `StartAtmStrategy` static form delegated to `SendCopyWithAtm`; `AtmStrategyCreate`/`AtmStrategyChangeStopTarget` banned and covered by SCAN-05; no `async/await` in new methods.
**NT8 Check: PASS**

---

#### TC-6: Completeness
Identical to Cycle 1 — all 3 modifications present with unambiguous anchors. Dedup key format consistently specified in 4 locations.
**Completeness: PASS**

---

#### TC-7: Test Coverage
Identical to Cycle 1 — 10 tests T_LANE_E_01 through T_LANE_E_10 present. Skip/no-Skip pattern correct. Additive-only, zero existing tests modified.
**Test Coverage: PASS**

---

#### TC-8: 7-Scan Checklist Presence

All 7 scans present in ticket (lines 541–608). SCAN-02 required value for `TryLaneEDispatchFollower` now correctly states 5. Every scan carries a command, a required result, and (where applicable) the exact expected value.

| Scan | Present | Required Value / Command | Cycle 2 Status |
|---|---|---|---|
| SCAN-01 | ✓ | 0 lock() matches | ✓ |
| SCAN-02 | ✓ | Per-method CYC values — `TryLaneEDispatchFollower` = 5 | ✓ (corrected from 4) |
| SCAN-03 | ✓ | 0 async/await in new/modified code | ✓ |
| SCAN-04 | ✓ | 0 non-ASCII bytes | ✓ |
| SCAN-05 | ✓ | 0 banned NT8 ATM API calls | ✓ |
| SCAN-06 | ✓ | 0 build errors, 0 failures, 5 specific tests pass/skip | ✓ |
| SCAN-07 | ✓ | Structural items (a)–(g) all present | ✓ |

**Scan Checklist: PASS**

---

#### TC-9: Scope Integrity
Identical to Cycle 1. Scope Lock table limits to exactly two files. Out of Scope section complete.
**Scope Integrity: PASS**

---

#### TC-10: Gate Integrity Constraint
Identical to Cycle 1. REQ-07 present. `TrySetFollowerSlateReady` and `TryMarkFollowerSlateNotReady` declared READ-ONLY in three locations. SCAN-07(e) enforces byte-for-byte unchanged check.
**Gate Integrity: PASS**

---

### Cycle 2 Violation Summary

| Check | Cycle 1 Result | Cycle 2 Result | Δ |
|---|---|---|---|
| TC-1 Traceability | PASS | PASS | — |
| TC-2 Method Signatures | PASS | PASS | — |
| TC-3 JS Pre-Check | PASS | PASS | — |
| TC-4 CYC Pre-Check | **FAIL** | **PASS** | ✅ Fixed |
| TC-5 NT8 Constraints | PASS | PASS | — |
| TC-6 Completeness | PASS | PASS | — |
| TC-7 Test Coverage | PASS | PASS | — |
| TC-8 Scan Checklist | PASS | PASS | — |
| TC-9 Scope Integrity | PASS | PASS | — |
| TC-10 Gate Integrity | PASS | PASS | — |

**Zero violations in Cycle 2.**

---

## Overall (Cycle 2)

**TICKET_REVIEW_PASS**

All 10 checks pass. The TC-4 defect (CYC stated as 4 for `TryLaneEDispatchFollower`) has been correctly remediated in both the Step 7 CYC verification note and the SCAN-02 required value line. The JS Rule Constraints table row was also updated to state CYC=5 and explicitly cites the `?:` ternary as +1 per JS-013. My independent recount confirms CYC=5 matches all three stated locations.

The Layer 1 engineer contract (per-ticket 7-scan checklist) is complete and internally consistent. SCAN-02 now anchors the correct CYC=5 value that the Layer 3 verifier will cross-check. Phase 4a (engineer) is authorized to proceed.

**→ TICKET_REVIEW_PASS. Phase 4a unlocked.**
