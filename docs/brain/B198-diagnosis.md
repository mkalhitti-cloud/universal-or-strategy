# B198 Pre-Pipeline Diagnosis
**Session**: 2026-10-13 | **Author**: copier-spec (diagnosis-only mode)

---

## STEP 1 — MEGACOMMIT AUDIT RESULT

Commands run:
```
git log --all --oneline --grep="SimStop" -- src/PropTraderTools/CopyEngine.cs   → 0 commits
git log --all --oneline --grep="Sim[Ss]top" -- src/PropTraderTools/CopyEngine.cs → 0 commits
git show 878a5b33~1:CopyEngine.cs | grep SimStop                               → 0 matches
git diff 878a5b33~1 878a5b33 -- CopyEngine.cs | grep -i "SimStop"              → 0 matches
grep -r "SimStop" src/PropTraderTools/CopyEngine.cs                            → 0 matches
```

**VERDICT: SimStop support NEVER existed in any commit.**
DW-B198-SIMSTP-01 classification: **NEW FIX** (not a restore ticket).

---

## STEP 2 — B197 DEFERRED BACKLOG CHECK

File: docs/brain/B197/06-deferred-backlog.md — READ. Key findings:
- DW-B194-04 listed as open item BUT with note "closed by this field" (line 381 in source).
- DW-B195-01 (fo=NULL drain path) NOT in B197 backlog as an explicit item — carried implicitly.
- 15 open items total. P0 count = 0. P1 = 5.

---

## STEP 3 — SIMSTP SOURCE VERIFICATION

All 5 predicates confirmed in CopyEngine.cs with exact lines. Zero SimStop mentions anywhere.

| Predicate | Line | Current logic | Missing |
|-----------|------|---------------|---------|
| `IsAtmBracketName` | 858 | `StartsWith("Stop") && digit[4]` + `StartsWith("Target") && digit[6]` | "SimStop" prefix |
| `IsNativeExitName` | 2564 | "Close", "Flatten", StartsWith("Rev"), StartsWith("Exit") | "SimStop" prefix |
| `IsBracketLeg` | 5880 | `StartsWith("Stop")` \|\| `StartsWith("Target")` | "SimStop" prefix |
| `IsBracketLegStatic` | 5861 | `StartsWith("Stop")` \|\| `StartsWith("Target")` \|\| `StartsWith("PTT-")` \|\| `EndsWith("STP")` | "SimStop" prefix |
| `IsAtmSTPOrder` | 3044 | `EndsWith("STP")`, `StartsWith("Stop")`, `StartsWith("Target")`, `StartsWith("PTT-STP-Drag-")`, `StartsWith("PTT-TGT-Drag-")` | "SimStop" prefix |
| `IsStopLeg` | 5848 | `FromEntrySignal != null` \|\| `StartsWith("Stop")` \|\| `EndsWith("STP")` | "SimStop" prefix |

---

## STEP 4 — DW-B194-04 STATUS CORRECTION

**DW-B194-04 is ALREADY IMPLEMENTED in the current source.**

Evidence:
- `_flattenFilledTicks` field: CopyEngine.cs line 382 (comment explicitly: "DW-B194-04: closed by this field")
- Debounce guard in `FlattenOneAccount`: line 5363 — `if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L) return;`
- Timestamp write on submit: line 5408 — `_flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, ...)`

The 30+ PTT-Flatten fires in output.md are caused by the **SimStop root cause** (T2):
- SimStop1/SimStop2 fire → DispatchCopy copies them as Sell → followers oversold → SHORT →
  NakedPositionDetector triggers repeatedly as each order-update callback fires during the short lifecycle.
- With T2 (IsNativeExitName SimStop fix), `DispatchCopy` is bypassed entirely for SimStop fills,
  followers flatten cleanly via FlattenOneAccount once, and the loop does not occur.

**CONCLUSION: TICKET 1 (PTT-Flatten debounce) IS NOT NEEDED. DW-B194-04 is already done.**
Remove Ticket 1 from B198 scope. The PTT-Flatten loop will be solved by T2 (SimStop fix).

---

## STEP 5 — QTY BUG INVESTIGATION (DW-B198-QTY-01)

QTY trace path confirmed:
1. `DispatchCopy` (line 2627): `baseSignal.Quantity = order.Quantity` (leader Entry qty)
2. `TryLaneDHook` (line 2635): passes `baseQty = baseSignal.Quantity`
3. `TryLaneDDispatchFollower` (line 2753): `scaledSig.Quantity = baseQty * mult`
4. `GetMultiplier` (line 2995): returns 1 if no multiplier configured → `scaledSig.Quantity = baseQty`
5. `SendCopyWithAtm` (line 5014): `signal.Quantity` passed to `CreateOrder`
6. `StartAtmStrategy(namedMode.AtmObject, order)` or `StartAtmStrategy(namedMode.TemplateName, order)` called

If leader qty=10 and mult=1, then `scaledSig.Quantity=10` is passed to `CreateOrder`.

**OPEN QUESTION**: Does NT8's `StartAtmStrategy(qty=10, "MGC $400 SL4")` create position qty=10
or does it use the template's per-group qty (5+5=10) independently of the passed order qty?

The NT8_FULL_REFERENCE.md and NT8_ADDON_KNOWLEDGE.md do NOT document this behavior.
The screenshot evidence (followers=Long 5 when leader=Long 10) is empirical.

**HYPOTHESIS H2 (most likely)**: NT8's `StartAtmStrategy` ignores the `entryOrder.Quantity` field
and uses the ATM template's own per-group quantity (5 contracts per group). So passing qty=10
with a 2-group template where each group is 5 = NT8 creates TWO groups of 5 each = 10 total,
but the entry ORDER itself fills for 5 (the first group only, since NT8 internal qty for the
entry order is taken from the template, not from CreateOrder qty).

**Alternate H3**: `StartAtmStrategy` respects the passed order qty=10 but the SIM broker
only fills half (5) due to partial fill behavior on MGC micro futures.

**RESOLUTION PATH**: Architect must add ONE log line before `StartAtmStrategy` call:
```csharp
StatusUpdate?.Invoke(acc.Name + ": ATM-entry-qty=" + order.Quantity + " tmpl=" + namedMode.TemplateName);
```
Run SIM, confirm what qty the order has when StartAtmStrategy is called. If qty=5 arrives
here, the reduction happened upstream (in CreateOrder NT8 internal behavior). If qty=10
arrives, NT8 is reducing it after StartAtmStrategy. Either way the log pinpoints the site.

**DESIGN CONSTRAINT**: If NT8 ignores the qty argument and uses template qty, then
CreateOrder(qty=10) is a no-op on quantity — NT8 will create qty=5 (template's per-group qty).
Fix would be: PTT must not pass qty=10; instead it should match the template's per-group qty
which requires reading the ATM template XML or accepting a UI config parameter.
This is non-trivial. Architect must propose options after log evidence is gathered.

---

## STEP 6 — DW-B195-01 (fo=NULL) CONFIRMATION

output.md line 95-96 confirm fo=NULL for Sim102 and Sim103 during SyncFollowerBracket calls.
This is DW-B195-01 (SubmitDrainedEntry → Clone mode → fo=NULL on drain path). Still open.

---

## STEP 6a — ADDITIONAL HARDCODINGS (Director: any N brackets)

Director confirmed 2026-10-13: fix must work for any number of ATM bracket groups, not only 2.
Investigation found TWO additional hardcoding categories beyond the 6 predicates.

### TryParseStopSuffix (line 3377-3387) — BROKEN FOR SimStop AND N > 3

- Line 3382: `stopName.Substring(4)` — always slices at position 4 ("Stop" prefix length).
  "SimStop1".Substring(4) = "top1" → int.TryParse("top1") = false → SimStop drag silently broken.
- Line 3383: `n > 3` cap — Stop4, Stop5... silently rejected → >3 groups drag broken.
- Called by ParseLegSuffix → TryTrackDragInFlight + TryRemoveDragSuffix (drag in-flight tracking).
- Called directly in SyncFollowerBracket (line 3115) and ResubmitCollateralLegs (line 3316).
- Fix: detect "SimStop" prefix (len 7) vs "Stop" prefix (len 4). Remove n > 3 cap.
- CYC: 3 → 5. Stays ≤8.

### CaptureOtherLegTargetPrices (line 3348-3371) — CAPPED AT 3 LEGS

- Line 3350: `new double[3]` — array size hardcoded.
- Line 3355: `for (int i = 1; i <= 3; i++)` — loop cap hardcoded.
- Fix: `const int MaxBracketLegs = 9` (NT8 ATM max). Replace both. CYC: 0 change.

### FindLeaderCollateralOrder (line 3405-3420) — BUILDS "Stop" + suffix ONLY

- Lines 3409-3410: builds "Stop"+suffix and "Target"+suffix. Never tries "SimStop"+suffix.
- With SimStop ATM: leader orders named "SimStop1", lookup for "Stop1" finds nothing.
- Fix: add "SimStop"+suffix as third candidate. CYC: +1. Must stay ≤8.

### CaptureLinkedTargetPrice (called line 3117) — PENDING

- Called with leaderOrder.Name (e.g. "SimStop1"). May rely on TryParseStopSuffix (auto-fixed)
  or have its own StartsWith("Stop") hardcoding. Architect must read and confirm.

---

## REVISED B198 TICKET SCOPE

| # | Ticket | Priority | DW Card | Note |
|---|--------|----------|---------|------|
| REMOVED | PTT-Flatten debounce | — | DW-B194-04 | Already implemented. Remove. |
| T1 | SimStop N-general bracket support | P0 | DW-B198-SIMSTP-01 | Part A: 6 predicates. Part B: TryParseStopSuffix, CaptureOtherLegTargetPrices, FindLeaderCollateralOrder. |
| T2 | QTY bug investigation + log + fix | P0 | DW-B198-QTY-01 | Architect must log first, then fix. |
| T3 | fo=NULL drain path (SubmitDrainedEntry) | P1 | DW-B195-01 | Full spec unchanged from prior sessions. |
| T4 | ATM dir path startup fix | P2 | DW-B198-ATMDIR-01 | Correct dir lookup. |

**Ticket 1 (was PTT-Flatten debounce) removed — work already done.**
**T1 expanded: SimStop N-general (any bracket count). QTY is T2. fo=NULL is T3. ATM dir is T4.**
