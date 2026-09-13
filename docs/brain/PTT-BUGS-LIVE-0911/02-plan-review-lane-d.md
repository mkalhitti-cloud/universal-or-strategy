# Plan Review — PTT-BUGS-LIVE-0911 Lane D
Date: 2026-09-11
Reviewer: ptt-plan-reviewer

---

## Verdict: REVIEW_PASS

No violations found. All 22 checks pass. Plan is approved to proceed to Phase 3 (ticket generation).

---

## LANE-SPLIT GATE Compliance

**Result: PASS**

Plan declares **SINGLE-PIPELINE** at the top of the document and in Section 2.

| Gate Question | Answer | Compliance |
|---|---|---|
| Q1. Same method or within 50 lines? | YES | Correct trigger for SINGLE-PIPELINE |
| Q2. Fix B design depends on Fix A final design? | N/A | Single workstream — correct |
| Q3. Each fix has standalone value if the other is blocked? | N/A | Single workstream — correct |
| Q4. Each fix has an independent SIM verification path? | N/A | Single workstream — correct |

Gate result is stated explicitly. Q1=YES with Q2/Q3/Q4=N/A correctly yields SINGLE-PIPELINE. No lane split was required or used.

---

## Checklist Results

### P0 Constraint Compliance

**1. DW-PTT0911C-02 — CYC Budget: PASS**

- `DispatchCopy` CYC=8, explicitly unchanged. Zero branches added at call site. Evidence: Section 7, Section 1 ("zero branches added").
- `TryLaneDHook` (after implementation): Section 7 counts 5 explicit branches. Recount from Section 6 pseudocode including the `foreach` loop decision yields CYC=7. Either way, 7 ≤ 8. WITHIN BUDGET.
- `TryLaneDDispatchFollower` (new): 3 branches (A, B, C) → CYC=4 ≤ 8. WITHIN BUDGET.
- `DispatchToFollower` (modified): Section 7 states CYC=3 (annotation: "Prior ~2 + 1 new"). Arithmetic note: 1 (base) + 2 (prior branches) + 1 (new skip gate) = CYC=4 is the correct count; the table shows "3" which is a notation error. At CYC=4 the method remains well within the ≤8 budget. Not a rule violation.
- All new conditional logic lives inside `TryLaneDHook`/`TryLaneDDispatchFollower`, not at the `DispatchCopy` call site. Evidence: Section 4 dispatch tree, Section 15 method signatures.

**2. DW-PTT0911C-03 — Thread Safety: PASS**

- `_laneDDispatched` declared as `ConcurrentDictionary<string, byte>`. No `lock()` anywhere in new code. Evidence: Sections 5, 8, 15.
- Write path uses `TryAdd` (atomic). Read path uses `ContainsKey` (lock-free O(1)). Evidence: Section 5.
- No WPF UI writes inside `TryLaneDHook` or `TryLaneDDispatchFollower` — `Dispatcher.InvokeAsync` not required in these methods. Status events raised from `SendCopyWithAtm` (existing, unchanged). Evidence: Section 8.
- `SetLiveEntryDispatched` already called on NT8 account background thread in `DispatchCopy` — thread-safe by existing design. Evidence: Section 8.

**3. DW-PTT0911C-04 — StartAtmStrategy Form: PASS**

- Fully-qualified static form used: `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order)`. Evidence: Section 10.
- Order name `"Entry"` specified (not "PTT-Copy") for ATM bracket arming. Evidence: Sections 1, 10, 15.
- `AtmStrategyCreate()` is absent from the plan. Evidence: exhaustive search of all 16 sections + Section K.

**4. DW-PTT0911C-05 — ATM Null Guard: PASS**

- Plan explicitly states `namedMode.AtmObject == null` for Clone mode (string-only Named ctor path). Evidence: Section 6 pseudocode comment, Section 10.
- Null guard `if (namedMode.AtmObject != null)` → object overload; else → string overload confirmed present in `SendCopyWithAtm` at lines 5145–5151. Evidence: Section 10.
- `StartAtmStrategy(null, order)` never called — string overload is the fallback when AtmObject is null. Evidence: Section 10.

### Jane Street Rules

**5. JS-021 — No lock(): PASS**

- Zero `lock()` in new or modified code. `ConcurrentDictionary` + `volatile` reads only. Evidence: Sections 5, 8, 15. SCAN-01 listed in Section 16.

**6. JS-001 — No throw in dispatch/gate/event handler methods: PASS**

- `TryLaneDHook`: no throw; all error paths return silently (early-return branches 1–3) or return false from helper. Evidence: Section 6 pseudocode.
- `TryLaneDDispatchFollower`: no throw; returns bool (false on null/cap failure, ok on dispatch). Evidence: Sections 6, 15.
- `SendCopyWithAtm`: existing try/catch, no re-throw. Evidence: Section 12 ("JS-001: catch in SendCopyWithAtm, no throw").
- SCAN-05 listed in Section 16.

**7. JS-023 — No off-thread UI update without Dispatcher.InvokeAsync: PASS**

- `TryLaneDHook` runs on NT8 account background thread and contains zero WPF UI writes. Evidence: Section 8.
- StatusUpdate events raised from `SendCopyWithAtm` are an existing pattern, unchanged. Evidence: Section 8.

**8. ASCII-only: PASS**

- All string literals and comments in new code use ASCII characters only. Dedup key separator `"#"` and instrument key separator `"|"` are both ASCII. Evidence: Section 5 field comment, Sections 6, 15. SCAN-04 listed in Section 16.

**9. No async/await in NT8 lifecycle/event handlers: PASS**

- `TryLaneDHook` and `TryLaneDDispatchFollower` are declared `private void` and `private bool` respectively. No async/await in any new or modified method. Evidence: Section 15 method signatures.

### Architecture Correctness

**10. Double-dispatch prevention — concrete mechanism, thread-safe: PASS**

- Mechanism: `_laneDDispatched` ConcurrentDictionary keyed by `orderId + "#" + accName`. Written by `TryLaneDDispatchFollower` via `TryAdd` (atomic) only when `SendCopyWithAtm` returns true. Read by `DispatchToFollower` via `ContainsKey` (lock-free) before mode dispatch. Evidence: Sections 4, 5, 15.
- Thread-safety: ConcurrentDictionary guarantees atomicity of both `TryAdd` and `ContainsKey` with no external synchronization required. Evidence: Section 8.
- Dedup commit: `SetLiveEntryDispatched` called in `TryLaneDHook` immediately after the fan-out loop when `laneDCount > 0` — prevents a second `OnOrderUpdate` for the same fill from passing Gate 5. Evidence: Sections 5, 6.

**11. Non-Clone mode safety — proven by exhaustive case analysis: PASS**

- Section 9 provides a case-by-case proof for every non-Clone mode:
  - Signal/Mirror: TryLaneDHook branch 1 fires → immediate no-op; skip gate always false → full existing path runs unchanged.
  - Named per-rule (Signal/Mirror): TryLaneDHook no-op; DispatchToFollower Named branch unchanged.
  - Market followers: TryLaneDHook no-op (not Clone OR Market order guard); `else SendCopy(...)` unchanged.
  - Inherit: `GetCloneAtmMode()` returns Inherit when cache empty; TryLaneDHook branch 3 fires → no-op; `else SendCopy(...)` unchanged.
- Proof is exhaustive, not just asserted. Evidence: Section 9.

**12. SendCopyWithAtm callability from TryLaneDHook: PASS**

- Both methods are `private` instance methods of `CopyEngine` — directly callable. Evidence: Section 10.
- Parameter availability table (Section 10) confirms all 4 required parameters are present at the TryLaneDHook call site: `acc` from `rule.FollowerAccounts`, `instrument` from `order.Instrument`, `scaledSig` constructed from `baseSignal`+`baseQty`+multiplier, `namedMode` from `GetCloneAtmMode()`. Evidence: Section 10.

**13. Follower loop integrity — skip gate in DispatchToFollower: PASS**

- Section 4 shows the skip gate added as first action in `DispatchToFollower` after `var mode = ResolveAtmMode(...)`:
  ```csharp
  string laneD_key = baseSignal.OrderId + "#" + acc.Name;
  if (_laneDDispatched.ContainsKey(laneD_key)) return;
  ```
- Skip gate does NOT modify the `dispatched` counter, preserving the loop's dedup commit logic for non-ATM followers. Evidence: Sections 4, 5, 15.

**14. Order name "Entry" for ATM arming: PASS**

- Section 10: `follower.CreateOrder(..., "Entry", ...)` confirmed as the ATM arming form.
- Section 1 "What the user sees": "follower accounts receive an `Entry` limit order immediately followed by `StartAtmStrategy(templateName, order)`."
- Non-ATM path retains `"PTT-Copy"` order name. Evidence: Sections 1, 3, 10, 15.

### Completeness

**15. Test strategy — three required assertions: PASS**

- T-LD-01: Clone+Named follower receives ATM dispatch (`_laneDDispatched` key present after hook). Evidence: Section 13.
- T-LD-02: Signal mode (non-ATM, non-Clone) follower → LaneD no-op (`_laneDDispatched.Count == 0`). Evidence: Section 13.
- T-LD-04: Double-dispatch prevention — `DispatchToFollower` skips when LaneD key pre-populated. Evidence: Section 13.
- Additional tests T-LD-03 (Market order guard), T-LD-05 (field type), T-LD-06 (helper method existence) increase coverage beyond minimum. Evidence: Section 13.

**16. Lane E wiring point named and documented: PASS**

- Section 11 names `TryLaneEHook` as the Lane E hook, specifies call site in `OnFollowerPositionUpdate` after `_followerSlateReady[acc.Name]` is set to true, and documents the re-arm ATM logic requirement.
- Section K DW-LD-03 records the deferred item. Evidence: Sections 11, K.

**17. Section K — Deferred Work: PASS**

- Section K present. Contains:
  - DW-LD-01: Market-order ATM entry (P2, Future lane)
  - DW-LD-02: `_laneDDispatched` cleanup (P3, Future)
  - DW-LD-03: Lane E re-arm (P1, Lane E)
  - DW-PTT0911C-01 through DW-PTT0911C-05: all CLOSED by this lane.
- Prior OPEN items (B24–B26, Lane A/B/C) explicitly noted as unchanged/not addressed.

**18. CYC analysis — decision points for all new/modified methods: PASS**

- Section 7 table covers all 4 affected methods with branch enumeration and budget status. Individual branch labels (1–5 for hook, A–C for helper) are provided in the pseudocode. Evidence: Sections 6, 7.
- Note: DispatchToFollower CYC in the table is stated as 3 when the correct sum is 4 (see Check 1 note above). At CYC=4 the method is within the ≤8 budget — this is a notation error in the plan, not a compliance failure.

**19. Spec requirement traceability: PASS**

- Section 12 maps all DW-PTT0911C-01–05 constraints, all JS rules, and the BUG spec requirement to plan sections.
- Lane D spec requirements R-LD-01 through R-LD-06 are addressed:

| Requirement | Evidence | Status |
|---|---|---|
| R-LD-01: TryLaneDHook body implemented | Sections 1, 4, 6 | ✓ |
| R-LD-02: NT8-owned ATM brackets at entry | Sections 1, 4, 10 | ✓ |
| R-LD-03: No double-dispatch | Sections 4, 5, 15 | ✓ |
| R-LD-04: Non-Clone unaffected | Section 9 | ✓ |
| R-LD-05: DispatchCopy CYC ≤8 | Section 7 | ✓ |
| R-LD-06: Lane E wiring point documented | Sections 11, K | ✓ |

### NT8 API Compliance

**20. AtmStrategyCreate banned — absent from plan: PASS**

- `AtmStrategyCreate()` does not appear in any section of the plan. Evidence: exhaustive review.

**21. AtmStrategyChangeStopTarget banned — absent from plan: PASS**

- `AtmStrategyChangeStopTarget()` does not appear in any section of the plan. Evidence: exhaustive review.

**22. StartAtmStrategy static form only: PASS**

- Plan uses fully-qualified static form: `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` throughout. No instance method invocation. Evidence: Section 10.

---

## Violations

None.

---

## Non-Blocking Observations (Informational Only — Not Violations)

These items do not trigger REVIEW_FAIL. They are noted for the engineer's awareness.

**OBS-1 — DispatchToFollower CYC notation error (Section 7)**: The CYC column shows "3" for DispatchToFollower. Correct count from the plan's own description ("Prior: ~2 + 1 new") is CYC=4 (1 base + 3 branches). The budget is ≤8; the actual value of 4 is compliant. Engineer should use CYC=4 in the final verification table.

**OBS-2 — `_laneDDispatched` growth is session-unbounded**: Section K DW-LD-02 correctly defers this. The plan's sizing estimate (~2.4 KB/session for 4 followers × 10 dispatches/day × 60 bytes/key) is acceptable. No action required for Lane D.

**OBS-3 — T-LD-01 is Skip-annotated**: `[Fact(Skip = "NT8-runtime: SendCopyWithAtm requires Account.CreateOrder + StartAtmStrategy NT8 host")]`. This is architecturally correct (NT8 runtime absent in xUnit host). The reflection-based tests (T-LD-02 through T-LD-06) provide structural coverage without the Skip annotation. Consistent with the project's established testing pattern. No action required.

---

## Recommendation

**Approved to proceed to Phase 3 (ticket generation).**

All 22 checklist items pass. Zero rule violations. Zero Jane Street DNA violations. The plan is architecturally sound, thread-safe, and fully traceable to all Lane D spec requirements (R-LD-01 through R-LD-06) and all Lane C P0 constraints (DW-PTT0911C-01 through DW-PTT0911C-05).

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane D · 2026-09-11*
