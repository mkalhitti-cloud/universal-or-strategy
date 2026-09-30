# B58-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-10
**Plan**: docs/brain/B58-LaneA/02-architecture-plan.md (Revision 1 — cycle 1 of 2)
**Rules**: docs/standards/jane-street/RULES_CATALOG.md
**Cycle**: Second review (first review issued REVIEW_FAIL with 2 violations: V1, V2)

---

## Review Result: REVIEW_PASS

**Violations**: 0

---

## V1 Resolution Confirmed

**Previous violation**: Plan claimed 4 relay methods were already implemented; grep returned 0
matches; adding `: ICopyEngine` would have produced 4 CS0535 errors.

**Architect action taken**: Added Items 1a–1d — four `public void` method bodies
(`RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`) with full implementation code.

**Reviewer verification**:

1. Live `src/PropTraderTools/CopyEngine.cs` grep for
   `RelayBe|RelayTrim|RelayFlatten|RelayCancel|ICopyEngine` → **0 matches** (methods remain
   absent before B58; confirms they are genuine missing members, not renames).

2. Signature match against `src/PropTraderTools/Core/PttContracts.cs`:

   | Interface (PttContracts.cs) | Plan implementation | Match |
   |-----------------------------|---------------------|-------|
   | `void RelayBe(BeEventArgs e)` (line 82) | `public void RelayBe(BeEventArgs e)` (Item 1a) | ✅ |
   | `void RelayTrim(TrimEventArgs e)` (line 85) | `public void RelayTrim(TrimEventArgs e)` (Item 1b) | ✅ |
   | `void RelayFlatten(FlatEventArgs e)` (line 88) | `public void RelayFlatten(FlatEventArgs e)` (Item 1c) | ✅ |
   | `void RelayCancel(CancelEventArgs e)` (line 91) | `public void RelayCancel(CancelEventArgs e)` (Item 1d) | ✅ |

3. Delegate targets confirmed in live `CopyEngine.cs`:
   - `AllAccounts(Instrument)` at line 1336 ✅
   - `SubmitBeStop(Account, Instrument, double)` at line 381 ✅
   - `Trim(Instrument)` at line 1006 ✅
   - `Flatten(Instrument)` at line 1012 ✅
   - `CancelPendingEntries(Instrument)` at line 1192 ✅

4. CYC: RelayBe=2, RelayTrim=1, RelayFlatten=1, RelayCancel=1 — all ≤ 8 ✅

**V1 status**: RESOLVED.

---

## V2 Resolution Confirmed

**Previous violation**: Plan Section D covered SCAN-01 through SCAN-05 only; SCAN-06 and SCAN-07
were completely absent.

**Architect action taken**: Added SCAN-06 (dotnet build) and SCAN-07 (verify_links.ps1) to
Section D.

**Reviewer verification**:

- SCAN-06 present: states 0 new errors from B58 changes; acknowledges 2 pre-existing
  `AtrSizingEngine.cs` errors as exempt (same precedent as B39/B40/B50/B54); instructs
  engineer to verify against pre-B58 baseline ✅
- SCAN-07 present: instructs engineer to run `powershell -File scripts\verify_links.ps1 -Fix`
  after commit; states expected result DESYNC=0, MISSING=0 ✅

**V2 status**: RESOLVED.

---

## Criteria Results (all 13)

| ID | Criterion | Result | Notes |
|----|-----------|--------|-------|
| R1 | Spec traceability — all 13 items trace to one source artifact | PASS | Items 1/1a-1d → PttContracts.cs ICopyEngine lines 82/85/88/91; Items 2/8/9a/9b → B54; Item 3 → B39; Item 4 → B40; Items 5a-5e → B50; Items 6/7 → derived (public wrapper of private method). Each item has exactly one cited source artifact. |
| R2 | ICopyEngine interface compliance | PASS | V1 resolved. Plan adds Items 1a–1d: four `public void` method bodies with correct signatures (matched against PttContracts.cs lines 82, 85, 88, 91), correct delegate targets (confirmed in live file), CYC ≤ 8 for all four. |
| R3 | IsEnabled backing field name | PASS | Live CopyEngine.cs line 98: `private volatile bool _isCopyEnabled;`. B54-LaneA §A1 confirms `public bool IsEnabled => _isCopyEnabled;`. Field name correct. |
| R4 | GlobalBe type and lazy-init pattern | PASS | B39-LaneA ticket-1-completion.md Implementation Note 1 confirms `PttGlobalBreakEven` type. Plan Risk 2 correctly identifies UI-thread-only access as justification for non-atomic lazy init. |
| R5 | IsPendingSlotsEmpty field name | PASS | Live CopyEngine.cs line 131: `private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots`. B40-LaneA §3 confirms `=> _pendingBeSlots.IsEmpty`. Field name correct. |
| R6 | ATM cache methods and GetAtmMode existence | PASS | `GetAtmMode` confirmed at line 953. `DispatchCopy` call site confirmed at line 699: `var mode = GetAtmMode(rule, acc.Name);`. B50 artifact confirms method signatures and `FollowerAtmMode` return type. |
| R7 | FindPositionPublic / SnapshotTargetsPublic | PASS | `private Position FindPosition(Account acc, Instrument instrument)` confirmed at line 1424. Thin wrapper `=> FindPosition(acc, instrument)` is a valid delegate. SnapshotTargetsPublic returns empty `List<Order>` (not null) as documented. |
| R8 | CopyRulesContainer.CopyEnabled default | PASS | B54-LaneA §A2 confirms `public bool CopyEnabled { get; set; }`. `[Serializable]` already at line 1810 — Risk 5 correctly flags for engineer to verify no duplicate attribute. Default `= false` matches B54 §A2. |
| R9 | SaveRules/LoadRules wiring | PASS | `CopyEnabledChanged` at line 162. Anchor texts match exactly. Statements `container.CopyEnabled = _isCopyEnabled;` and `_isCopyEnabled = container.CopyEnabled; CopyEnabledChanged?.Invoke(_isCopyEnabled);` match B54 §A3/A4 verbatim. |
| R10 | 7-Scan pre-analysis completeness | PASS | V2 resolved. All 7 scans present: SCAN-01 (lock), SCAN-02 (async void), SCAN-03 (return null), SCAN-04 (throw new), SCAN-05 (CYC ≤ 8), SCAN-06 (dotnet build), SCAN-07 (verify_links.ps1). Each scan carries a pre-analysis verdict. |
| R11 | JS Rules compliance | PASS | JS-021 (lock): zero lock() in all 13 new items — SCAN-01 confirms. JS-033 (async void): zero — SCAN-02 confirms. JS-002 (return null): FindPositionPublic delegates pre-existing null contract, not a new site; all other new members are void or return non-null. JS-066/CYC: max CYC across all new/modified code is 3 (SnapshotTargetsPublic) — all ≤ 8. JS-010: no new public constructors. NT8-001: CopyEnabled uses `{ get; set; }` (not `{ get; init; }`). |
| R12 | Single-ticket design | PASS | All 13 items (15 individual code changes) are in `CopyEngine.cs` only. Section B states "Zero changes to any other file." Section G provides a safe insertion order; dependency chain (field before methods; GetCloneAtmMode before ResolveAtmMode) is correctly sequenced. |
| R13 | No scope creep | PASS | Section B explicitly states zero changes to any other file. No TradeCopierPanel.cs, PttContracts.cs, or other file is listed for modification. |

---

## Approval Notes

Both violations from the first review are fully resolved. All 13 criteria pass with no new issues.

**This plan is approved for Phase 3 (ticket generation).**

**Resubmission cycles consumed**: 1 of 2.

---

*ptt-plan-reviewer | Phase 2 (Second Review) | B58-LaneA | 2026-08-10*
