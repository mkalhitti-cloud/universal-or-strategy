# PTT-REPAIRS-02 Ticket 1 — Independent Verification Report
**Verifier**: ptt-verifier (Phase 4b)
**Ticket**: PTT-REPAIRS-02-T1
**Date**: 2026-09-07
**Verdict**: VERIFY_PASS

---

## 1. Production Fix Verification

### 1a. EvictDedup Location (line 5740)
CONFIRMED: internal void EvictDedup(string orderId, OrderState state) found at line 5740.

### 1b. Filled Case Block
CONFIRMED: Lines 5762-5769 contain the correct fix:
`csharp
if (state == OrderState.Filled)
{
    // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
    // Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
    // MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
`
- TryRemove on _entryInstrKeyByOrderId capturing out var filledInstrKey — PRESENT (line 5767)
- TryRemove on _liveEntryInstruments using the captured key illedInstrKey — PRESENT (line 5768)
- Pattern mirrors Cancelled branch — CONFIRMED

### 1c. Old "Do NOT remove" Comment
CONFIRMED GONE: Select-String -Pattern "Do NOT remove" → 0 matches. The old comments:
- "DW-B142-MGC-02: clean up companion map (lazy)."
- "Do NOT remove _liveEntryInstruments key -- trade is live."
- "PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup."
- _entryInstrKeyByOrderId.TryRemove(orderId, out _);
...are all gone.

### 1d. Cancelled Branch (lines 5758-5759)
CONFIRMED UNTOUCHED:
`
5758: if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
5759:     _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
`
Identical to design. Not modified.

### 1e. _entryDispatchedOrders Guard (line 5755)
CONFIRMED UNTOUCHED:
`
5755: _entryDispatchedOrders.TryRemove(orderId, out _);
`
Present and unchanged in Cancelled block.

### 1f. CYC=6 Header Comment
CONFIRMED at line 5738:
// PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5).

---

## 2. Root Cause Fix Correctness

Manual trace of alternating-dispatch scenario:
- **First dispatch**: IsLiveEntryBlocked(instrKey, orderId1, price) → _liveEntryInstruments.TryAdd(instrKey) succeeds → returns false (not blocked) → followers dispatched.
- **Fill event**: EvictDedup(orderId1, Filled) → _entryInstrKeyByOrderId.TryRemove(orderId1, out filledInstrKey) → _liveEntryInstruments.TryRemove(filledInstrKey) → instrKey **cleared**.
- **Second dispatch**: IsLiveEntryBlocked(instrKey, orderId2, price) → _liveEntryInstruments.ContainsKey(instrKey) → **false** (cleared by fix) → Gate 5 check (a) passes → followers dispatched.

**MATCHES** the design in plan Section 3 and ticket Section D/G. Defect corrected.

---

## 3. MGC Guard Integrity Verification

### 3a. _entryDispatchedOrders Primary Guard
CONFIRMED: _entryDispatchedOrders is still the primary double-dispatch guard. IsEntryDispatched(orderId) at line 5695-5697 and Gate 5 check (c) in IsLiveEntryBlocked line 5716 are UNCHANGED. Once dispatched, same orderId is blocked at check (c) regardless of instrKey state.

### 3b. Cancelled Path TryRemove
CONFIRMED UNCHANGED: Lines 5758-5759 still clear _liveEntryInstruments on Cancelled. Path reads:
  1. _entryDispatchedOrders.TryRemove(orderId) (line 5755)
  2. if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)) (line 5758)
  3. _liveEntryInstruments.TryRemove(cancelledInstrKey) (line 5759)

### 3c. Dual Guard Intact
CONFIRMED: The fix adds a Filled-path TryRemove that mirrors the Cancelled path. It does NOT remove _entryDispatchedOrders from either path. Both guards remain independently operational. MGC cancel+resubmit scenario uses the Cancelled path (UNCHANGED). The Filled path releasing instrKey only affects subsequent entries with a NEW orderId — correctly guarded by _entryDispatchedOrders being checked via IsEntryDispatched.

---

## 4. Independent CYC Verification

### 4a-4c. EvictDedup (lines 5740-5771) — Independent Count

| # | Location | Branch | Type |
|---|----------|--------|------|
| BASE | — | implicit start | +1 |
| 1 | L5742-5747 | if (state != Filled && state != Cancelled && state != Rejected) | compound if |
| 2 | L5751 | if (state == OrderState.Cancelled) | if |
| 3 | L5758 | if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)) | if |
| 4 | L5762 | if (state == OrderState.Filled) | if |
| 5 | L5767 | if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)) | if (NEW) |

**Independent CYC = 1 (base) + 5 = 6**
Budget ≤ 7: **PASS**

### 4d-4e. IsLiveEntryBlocked (lines 5710-5721) — Independent Count

| # | Location | Branch | Type |
|---|----------|--------|------|
| BASE | — | implicit start | +1 |
| 1 | L5712 | if (_liveEntryInstruments.ContainsKey(instrKey)) | if |
| 2 | L5714 | if (IsDedup(orderId, limitPrice)) | if |
| 3 | L5716 | if (IsEntryDispatched(orderId)) | if |

**Independent CYC = 1 (base) + 3 = 4**
Budget ≤ 5: **PASS**

---

## 5. Test Verification

### 5a. Test Located
CONFIRMED at line 7784:
public void IsLiveEntryBlocked_ClearsOnFill_AllowsReentry()

### 5b. [Fact] Attribute
CONFIRMED at line 7784: [Fact] attribute present immediately before the method.

### 5c. 5-Step Scenario Present
| Step | Code | Assertion | Present |
|------|------|-----------|---------|
| Pre | ClearLiveEntryForInstrument_ForTest("MGC DEC26") | — | CONFIRMED (L7793) |
| 1 | IsLiveEntryBlocked_ForTest(instrKey, orderId1, limitPrice) | Assert.False(blocked1) | CONFIRMED (L7796-7797) |
| 2 | LiveEntryInstrumentsContains_ForTest(instrKey) | Assert.True(...) | CONFIRMED (L7800) |
| 3 | EvictDedup_ForTest(orderId1, OrderState.Filled) | — | CONFIRMED (L7803) |
| 4 | LiveEntryInstrumentsContains_ForTest(instrKey) | Assert.False(...) | CONFIRMED (L7806) |
| 5 | IsLiveEntryBlocked_ForTest(instrKey, orderId2, limitPrice) | Assert.False(blocked2) | CONFIRMED (L7809-7810) |

All 5 steps present. Assertions match plan Section 7 and ticket Section H exactly.

### 5d. InternalsVisibleTo Seam Used Correctly
CONFIRMED: All seam methods exist at CopyEngine.cs lines 4264-4280:
- IsLiveEntryBlocked_ForTest (L4264-4268) — used in steps 1+5
- EvictDedup_ForTest (L4270-4271) — used in step 3
- ClearLiveEntryForInstrument_ForTest (L4273-4274) — used in pre-condition
- LiveEntryInstrumentsContains_ForTest (L4276-4277) — used in steps 2+4
- InternalsVisibleTo("PropTraderTools.Tests") confirmed at line 46 (referenced in L4261 comment)

### 5e. Independent [Fact] Count
Command run: Select-String -Path 'src/PropTraderTools/CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
**Result: 475 [Fact] attributes**

### 5f. Count = Baseline + 1
Engineer reported: 474 before, 475 after (+1).
**My independent count: 475. Delta +1 confirmed.**

Note: Plan stated 300→301, but engineer correctly identified the actual baseline as 474 (the file grew since the plan was written). Delta of +1 is what matters per ticket K. CONFIRMED.

---

## 6. Independent 7-Scan Results

### Scan Comparison Table

| Scan | Engineer (Layer 2) | Verifier Independent (Layer 3) | Match? |
|------|--------------------|-------------------------------|--------|
| SCAN-01: lock( in region 5735-5775 | 0 matches | 0 actual lock( in executable code (all comment-only hits across whole file) | MATCH ✓ |
| SCAN-02: DateTime.Now in region 5735-5775 | 0 matches | 0 (all comment-only hits across whole file, none in modified region) | MATCH ✓ |
| SCAN-03: return null in region 5710-5775 | 0 matches | 0 in region 5710-5775 | MATCH ✓ |
| SCAN-04: async void in region 5710-5775 | 0 matches | 0 (all comment-only hits across whole file) | MATCH ✓ |
| SCAN-05: non-ASCII in region 5735-5775 | 0 matches | 0 matches | MATCH ✓ |
| SCAN-06: ?.* -= in region 5710-5775 | 0 matches | 0 matches | MATCH ✓ |
| SCAN-07: EvictDedup CYC=6, IsLiveEntryBlocked CYC=4 | CYC=6 / CYC=4 | CYC=6 / CYC=4 (independent count) | MATCH ✓ |

**No discrepancies found between engineer Layer 2 report and verifier Layer 3 independent scans.**

#### SCAN-01 Detail (Whole-File Lock Scan)
All 60+ matches for "lock" in CopyEngine.cs are in **comment strings** (e.g., // no lock(), // no lock (JS-021)). Zero lock( keyword used in executable code anywhere in the file. PASS.

#### SCAN-02 Detail (Whole-File DateTime.Now Scan)
All 7 matches for "DateTime.Now" in the file are in **comment strings** (e.g., // No DateTime.Now). Zero actual DateTime.Now in executable code. PASS.

#### SCAN-04 Detail (Whole-File async void Scan)
Both matches are in **comment strings** (e.g., // NOT async void). Zero actual sync void declarations introduced. PASS.

---

## 7. Cross-Check vs Architecture Plan

### 7a. Plan Section 5 (Proposed Code Change) vs Implementation

| Plan Item | Implemented | Match |
|-----------|-------------|-------|
| Remove old comment "DW-B142-MGC-02: clean up companion map (lazy)." | DONE | ✓ |
| Remove old comment "Do NOT remove _liveEntryInstruments key -- trade is live." | DONE | ✓ |
| Remove old comment "PositionStateChanged flat gate is the authoritative cleanup." | DONE | ✓ |
| Remove _entryInstrKeyByOrderId.TryRemove(orderId, out _); (discard key) | DONE | ✓ |
| Add if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)) | DONE (L5767) | ✓ |
| Add _liveEntryInstruments.TryRemove(filledInstrKey, out _); | DONE (L5768) | ✓ |
| Update header comment to CYC=6 at line 5738 | DONE: "PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5)." | ✓ |

### 7b. Plan Section 7/9 (Test Design) vs Implementation

| Plan Item | Implemented | Match |
|-----------|-------------|-------|
| Test name IsLiveEntryBlocked_ClearsOnFill_AllowsReentry | DONE | ✓ |
| [Fact] attribute | DONE | ✓ |
| 5-step scenario per plan Section 7 assertions | DONE (all 5 steps + pre-cond) | ✓ |
| Uses all 4 seams: IsLiveEntryBlocked_ForTest, EvictDedup_ForTest, LiveEntryInstrumentsContains_ForTest, ClearLiveEntryForInstrument_ForTest | DONE | ✓ |
| Appended after T_R6 block before class/namespace closing braces | DONE (L7780-L7811) | ✓ |

### 7c. Deviations from Plan
**NO DEVIATIONS FOUND.** Implementation is an exact match to plan Section 5 and plan Section 9.

Minor note: The plan's header comment example says "filledInstrKey-TryRemove(5)" while the implemented comment says "filledInstrKey-remove(5)". This is a cosmetically equivalent paraphrase — not a functional deviation. **ACCEPTABLE**.

---

## 8. DNA Rule Check Summary

| Rule | Description | Check | Status |
|------|-------------|-------|--------|
| JS-021 | No lock() in source | 0 actual lock( in executable code (whole-file scan confirmed all in comments) | PASS |
| JS-023 | UI mutation via Dispatcher.InvokeAsync | No UI changes in this fix | N/A |
| JS-025 | ConcurrentDictionary.TryRemove lock-free | Both TryRemove calls are ConcurrentDictionary operations — lock-free | PASS |
| JS-001 | No throw in gate methods | EvictDedup uses TryRemove (no throw). No try/catch introduced. | PASS |
| JS-002 | No return null | EvictDedup is void. IsLiveEntryBlocked returns bool. 0 return null in region 5710-5775. | PASS |
| JS-008 | No mutable struct / unfrozen brush | No struct usage introduced | N/A |
| JS-010 | Non-private constructor (singleton) | No constructor changes | N/A |
| NT8: async/await in lifecycle | No async keyword introduced | PASS |
| NT8: Account.All outside Loaded | Not used | N/A |
| NT8: sealed on TradeCopierWindow | Not used | N/A |
| NT8: FontFamily= | Not introduced (SCAN-03 confirms 0 in region) | PASS |
| NT8: #RRGGBB hex color | Not introduced | PASS |
| NT8: CreateOrder without PTT- prefix | Not introduced | PASS |
| NT8: DateTime.Now | 0 in executable code (SCAN-02 confirms) | PASS |
| CYC ≤ 8 | EvictDedup CYC=6 (≤7 budget), IsLiveEntryBlocked CYC=4 (≤5 budget) | PASS |
| ASCII-only | 0 non-ASCII in modified region 5735-5775 (SCAN-05) | PASS |

---

## 9. Overall Verdict

**VERIFY_PASS**

All checklist items confirmed. Summary of findings:

1. **Production Fix**: EvictDedup Filled block correctly mirrors the Cancelled branch. TryRemove on _entryInstrKeyByOrderId captures key; TryRemove on _liveEntryInstruments uses captured key. Old "Do NOT remove" comment is gone. CYC=6 header present. ✓
2. **Root Cause Correctness**: Fix resolves the alternating-dispatch defect. instrKey is released on fill, allowing re-entry for subsequent trades. ✓
3. **MGC Guard Integrity**: _entryDispatchedOrders primary guard untouched at line 5755. Cancelled path untouched at lines 5758-5759. Both guards independently operational. ✓
4. **CYC (Independent)**: EvictDedup=6 (≤7 PASS), IsLiveEntryBlocked=4 (≤5 PASS). ✓
5. **Test**: IsLiveEntryBlocked_ClearsOnFill_AllowsReentry present with [Fact], 5-step scenario, correct seams. ✓
6. **[Fact] Count**: 475 (baseline 474 + 1). Delta matches. ✓
7. **7 Scans**: All 0 violations. Layer 2 vs Layer 3: no discrepancies. ✓
8. **Plan Compliance**: Implementation is exact match to plan Sections 5 and 9. No unauthorized deviations. ✓
9. **DNA Rules**: All applicable rules PASS. No violations. ✓

**No violations found. No discrepancies between engineer Layer 2 and verifier Layer 3. Ticket PTT-REPAIRS-02-T1 is clear for Phase 5.**

---

*ptt-verifier · PTT-REPAIRS-02-T1 · 2026-09-07*
