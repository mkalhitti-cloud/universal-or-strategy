# PTT-REPAIRS-04 Direct Edits Log

Session: PTT-REPAIRS-04 REPAIR-LOOP
Mode: DIRECT EDIT (APPROVALMODE: yolo)
Date: 2026-09-06
Files touched: src/PropTraderTools/CopyEngine.cs

---

## STEP 0 — PIPELINE VALIDATION

**PTT-REPAIRS-03-POST 05-final-review.md:** PIPELINE_COMPLETE confirmed at line 231.
All gates: VERIFY_PASS x2, BUILD_PASS x2. No violations. Proceeding.

---

## STEP 2 — BUG-C / BUG-D VALIDATION

**VALIDATION_PASS**

| Bug | Evidence from docs/output.md | Status |
|-----|------------------------------|--------|
| BUG-C (gate5 false block) | Lines 14-18: `Sell x8 MES SEP26` dispatched to 3 followers at Accepted. Lines 18-22: same order re-fires at Working -> `gate5 exit: instrKey=MES SEP26|Sell` (same-orderId, correct dedup block). New orders on same instrKey subsequently dispatch correctly (e.g. lines 36-38 Buy MGC DEC26, lines 96-98 Sell MGC DEC26 after cancels). | FIXED |
| BUG-D (empty-name Limit block) | No `gate0.5 exit: name= act=Sell state=Accepted type=Limit` entries anywhere in log. All Limit entries with name=Entry dispatch normally. | FIXED |

---

## STEP 3 — BUG-E ROOT CAUSE

**ROOT_CAUSE_CONFIRMED**

### Evidence

| Log lines | Event | State change |
|-----------|-------|--------------|
| 36-38 | MGC Buy dispatched to Sim102/103/104 | `_lastLeaderDirection["MGC DEC26"] = Buy` (DispatchCopy line 2549, unconditional) |
| 39-44 | Same order re-fires at Accepted/Working | gate5 blocks (correct dedup, same orderId) |
| 45-52 | CancelPending -> CancelSubmitted | EvictDedup NOT called (not Cancelled state yet) |
| 65-68 | Sell attempt (new order, Accepted): `skip reversal entry: Sim102/103/104 MGC DEC26 cur=Sell last=Buy flat=True` | `ShouldSkipForReversalGuard`: hasLastDirection=true, cur=Sell, last=Buy, followerIsFlat=true -> `IsReversalToFlatFollower(Sell,Buy,true)` = true -> ALL followers skipped, dispatched=0. BUT line 2549 still writes `_lastLeaderDirection["MGC DEC26"] = Sell` (unconditional). |
| 95-98 | Sell attempt 2 (Accepted): dispatched to Sim102/103/104 | last=Sell, cur=Sell -> `IsReversalToFlatFollower(Sell,Sell,true)` = false (same direction) -> NOT reversal -> dispatches. |

**Pattern repeats:** Log lines 186-188 (Buy after cancelled Sells, cur=Buy last=Sell flat=True skipped), lines 208-210 and 426-428 confirm systemic recurrence every direction-change-from-cancel cycle.

### Root Cause Narrative

`EvictDedup` Cancelled branch (pre-fix, lines 5856-5873) cleared:
- `_entryDispatchedOrders[orderId]`
- `_liveEntryInstruments[cancelledInstrKey]` (value-guarded)
- `_entryInstrKeyByOrderId[orderId]`

But did NOT clear `_lastLeaderDirection[instrName]`.

`DispatchCopy` line 2549 writes `_lastLeaderDirection[instr.FullName] = currentAction` unconditionally
AFTER the follower loop -- even when `dispatched == 0`.

Sequence:
1. Buy dispatches -> `_lastLeaderDirection["MGC DEC26"] = Buy`
2. Buy cancels -> `_lastLeaderDirection["MGC DEC26"]` remains = Buy (NOT cleared by EvictDedup)
3. User is flat (no position). User places Sell entry.
4. `ShouldSkipForReversalGuard`: hasLastDirection=true, cur=Sell, last=Buy, flat=True
   -> `IsReversalToFlatFollower(Sell, Buy, true)` = (Sell != Buy) && true = true
   -> ALL followers skipped -> dispatched=0
   -> BUT line 2549 writes `_lastLeaderDirection["MGC DEC26"] = Sell`
5. User places Sell again.
   last=Sell, cur=Sell -> `IsReversalToFlatFollower(Sell, Sell, true)` = false -> dispatches.

User experience: "must click twice, copies on third attempt."

### Code Confirmation

`IsReversalToFlatFollower` at line 5997-6004:
```csharp
return currentAction != lastAction && followerIsFlat;
```
Simple: any direction change while follower is flat returns true.

`_lastLeaderDirection` NOT cleared in EvictDedup Cancelled branch confirmed at lines 5856-5877
(pre-fix: only `_entryDispatchedOrders`, `_entryInstrKeyByOrderId`, `_liveEntryInstruments` touched).

`TryClearLeaderDirectionOnFlat` (the ONLY other clear path) only fires when a real position goes flat.
A cancelled entry that was never filled produces no position -> no flat event -> `TryClearLeaderDirectionOnFlat` never fires.

---

## STEP 4 — BUG-F DIAGNOSIS

**DIAGNOSIS ONLY — no fix in this session**

### Symptom
User must re-click Clone on MGC panel after trading MES -- MGC uses MES ATM instead of the MGC ATM selected when MGC was cloned.

### Storage Location

| Field | File | Line | Type | Per-instrument? |
|-------|------|------|------|-----------------|
| `_cloneAtmCache` | CopyEngine.cs | 145 | `volatile string` | NO -- single shared scalar |
| `_cloneAtmObject` | CopyEngine.cs | 150 | `volatile NinjaTrader.NinjaScript.AtmStrategy` | NO -- single shared scalar |
| `SetCloneAtmObjectCache` | CopyEngine.cs | 729 | writes `_cloneAtmObject` | NO -- overwrites shared field |
| `SetCloneAtmCache` | CopyEngine.cs | 720 | writes `_cloneAtmCache` | NO -- overwrites shared field |
| `OnCloneModeClick` | TradeCopierPanel.cs | 1848 | reads from `_currentChart` | reads single panel chart |

### Root Cause

`OnCloneModeClick` (TradeCopierPanel.cs line 1848) reads `_currentChart` which is a single
panel-level reference. When the user:
1. Clones MGC chart -> `_cloneAtmObject` = MGC ATM, `_cloneAtmCache` = "MGC-ATM-Name"
2. Opens/trades MES chart -> `_currentChart` is updated to the MES chart window
3. User clicks Clone on MGC panel again (to reset) -> `_currentChart` is still the MES chart
   -> `ct.AtmStrategy` = MES ATM -> `_cloneAtmObject` overwritten with MES ATM

**Result:** `GetCloneAtmMode()` (CopyEngine line 738) returns MES ATM for all instruments.

### Fix Design (deferred)

Replace scalar `_cloneAtmObject` and `_cloneAtmCache` with per-instrument dictionaries:
```
ConcurrentDictionary<string, AtmStrategy> _cloneAtmObjectByInstr
ConcurrentDictionary<string, string>      _cloneAtmCacheByInstr
```
`OnCloneModeClick` would key by `instr.FullName` extracted from the chart's instrument.
`GetCloneAtmMode()` would accept an instrument key param.
`DispatchCopy` would pass `instr.FullName` when calling `GetCloneAtmMode`.

**This fix is deferred to docs/brain/PTT-REPAIRS-04/deferred.md and requires a separate session.**

---

## STEP 5 — BUG-E FIX APPLIED

**Timestamp:** 2026-09-06 (PTT-REPAIRS-04 session)
**File:** src/PropTraderTools/CopyEngine.cs
**Method:** EvictDedup
**Lines changed:** 5866-5877 (BEFORE), 5866-5880 (AFTER -- 3 lines added inside Cancelled block)

### Before

```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

### After

```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
    // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
    // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
    var pipeIdx = cancelledInstrKey.IndexOf('|');
    if (pipeIdx > 0)
        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
}
```

### Rationale

When an entry order is dispatched then cancelled without filling, `_lastLeaderDirection` holds
the direction of the cancelled entry indefinitely. The `TryClearLeaderDirectionOnFlat` path
never fires because no position was created. `DispatchCopy` line 2549 writes `lastDirection`
unconditionally (even when dispatched==0), causing the second attempt (direction change from flat)
to write the new direction while dispatching nothing. Only the THIRD attempt dispatches.

Fix: extract instrument name from `cancelledInstrKey` (format: "MGC DEC26|Buy") using `IndexOf('|')`,
then `TryRemove` from `_lastLeaderDirection`. Lock-free, allocation-minimal, ConcurrentDictionary-safe.

### Jane Street Compliance (Direct Edit)

| Rule | Check | Status |
|------|-------|--------|
| JS-021 no lock() | No lock() introduced | PASS |
| JS-001 no throw | TryRemove/IndexOf/Substring all no-throw on non-null string | PASS |
| JS-013 CYC <= 8 | EvictDedup CYC before=6, after=7 (pipeIdx > 0 adds +1). Within limit. | PASS |
| JS-042 ASCII-only | Comment and code are ASCII only | PASS |
| JS-025 ConcurrentDictionary | TryRemove is lock-free atomic op | PASS |
| NT8 no lock | No lock() | PASS |
| No DateTime.Now | Not present | PASS |
| No new thread | Not present | PASS |

---

## STEP 6 — 7-SCAN RESULTS

| # | Check | Command | Result |
|---|-------|---------|--------|
| 1 | lock() scan | `Select-String -Pattern "\\block\\s*("`  (non-comment lines) | PASS: 0 matches |
| 2 | Unicode/non-ASCII | `Select-String -Pattern '[^\x00-\x7F]'` | PASS: 0 matches |
| 3 | CYC spot-check EvictDedup | Manual branch count | PASS: CYC=7 (was 6, +1 for pipeIdx guard). Within JS-013 limit of 8. |
| 4 | [Fact] check | N/A -- no test added in Phase A (BUG-E test added in Phase B TICKET-1) | N/A |
| 5 | Build (CopyEngine errors) | `dotnet build Linting.csproj /nologo 2>&1 \| Select-String "CopyEngine"` | PASS: 0 CopyEngine errors (pre-existing V12_002 errors only) |
| 6 | Hard-link sync | `New-Item -ItemType HardLink` for all 7 files | PASS: 7/7 linked |
| 7 | Hard-link count | `fsutil hardlink list CopyEngine.cs \| Measure-Object` | PASS: 2 (repo + NT8 dir) |

**All 7 scans PASS.**

---

## DEFERRED ITEMS

See docs/brain/PTT-REPAIRS-04/deferred.md for BUG-F full diagnosis.

---

## BUG-F FIX: Clone ATM per-instrument storage

**Timestamp:** 2026-09-06 (PTT-REPAIRS-04 session, second pass)
**BUG-E confirmed fixed:** Zero `[PTT-COPY-GUARD] skip reversal entry` lines in post-fix log.
**Files touched:** src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/TradeCopierPanel.cs

### Root Cause (confirmed)

`_cloneAtmObject` (CopyEngine.cs line 150, pre-fix) and `_cloneAtmCache` (line 145, pre-fix) were
single shared volatile scalars. `OnCloneModeClick` (TradeCopierPanel.cs line 1848) wrote to both
using `_currentChart` as the ATM source -- a single panel-level chart reference.

Scenario:
1. User clones MGC panel -> `_cloneAtmObject = MGC_ATM`, `_cloneAtmCache = "MGC-Template"`
2. User switches to / trades MES chart -> MES panel's `_currentChart` becomes active
3. (No clone click on MES, but if MES panel is the active panel when *any* clone-related event fires,
   or user accidentally clicks Clone on MES) -> `_cloneAtmObject = MES_ATM` overwrites global
4. MGC next dispatch: `GetCloneAtmMode()` returns MES ATM -> MGC orders armed with MES ATM brackets

### Fix

**Replaced two scalar fields with per-instrument ConcurrentDictionaries:**

CopyEngine.cs:
- `volatile string _cloneAtmCache` -> `ConcurrentDictionary<string, string> _cloneAtmCacheByInstr`
- `volatile AtmStrategy _cloneAtmObject` -> `ConcurrentDictionary<string, AtmStrategy> _cloneAtmObjectByInstr`

**Updated signatures:**
- `SetCloneAtmCache(string instrFullName, string value)` -- CYC=1
- `SetCloneAtmObjectCache(string instrFullName, AtmStrategy atmObj)` -- CYC=2 (null branch: remove vs set)
- `GetCloneAtmMode(string instrFullName)` -- CYC=2, unchanged logic, keyed lookup
- `ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)` -- CYC=2, passes instrFullName
- `DispatchToFollower` line ~2672: passes `order.Instrument.FullName`
- `ReplaceFollowerCopyOnAtmCancel` line ~4404: passes `cancelledOrder.Instrument.FullName`

TradeCopierPanel.cs:
- `OnCloneModeClick`: adds `string instrKey = _instrument?.FullName ?? string.Empty;`
  passes `instrKey` to both `SetCloneAtmObjectCache` and `SetCloneAtmCache`

### Key invariant

Each panel instance has its own `_instrument` set by `SetInstrument()` (called by NT8 add-on init).
`_instrument.FullName` is stable for the panel's lifetime.
Each Clone click now stores/reads ATM keyed by the panel's own instrument, not a shared global.

### CYC check

| Method | Before | After | Within limit? |
|--------|--------|-------|---------------|
| `SetCloneAtmObjectCache` | 1 | 2 (+1 null branch) | PASS (<=8) |
| `GetCloneAtmMode` | 2 | 2 (same logic) | PASS |
| `ResolveAtmMode` | 2 | 2 (same logic) | PASS |
| `OnCloneModeClick` | 2 | 2 (instrKey var add, no new branch) | PASS |

### Jane Street Compliance

| Rule | Check | Status |
|------|-------|--------|
| JS-021 no lock() | ConcurrentDictionary indexer + TryGetValue + TryRemove all lock-free | PASS |
| JS-001 no throw | All paths: no throw on null/empty instrFullName (empty string is valid key) | PASS |
| JS-013 CYC <= 8 | All methods at or below prior CYC, max +1 for null branch | PASS |
| JS-042 ASCII-only | All new strings and comments ASCII only | PASS |
| JS-023 volatile | Removed two volatile fields; ConcurrentDictionary is already thread-safe without volatile | PASS |
| JS-002 no null return | GetCloneAtmMode returns Inherit as fallback (never null) | PASS |

### 7-Scan Results (BUG-F)

| # | Check | Result |
|---|-------|--------|
| 1 | lock() CopyEngine + TradeCopierPanel | PASS: 0 matches each |
| 2 | non-ASCII CopyEngine + TradeCopierPanel | PASS: 0 matches each |
| 3 | CYC spot-check | PASS: max CYC change +1 (SetCloneAtmObjectCache 1->2) |
| 4 | [Fact] | N/A -- no test added in Phase A (test added in Phase B) |
| 5 | Build (CopyEngine + TradeCopierPanel errors) | PASS: 0 errors in either file |
| 6 | Hard-link sync | PASS: 7/7 files, inode=2 each |
| 7 | All callers updated | PASS: no old-signature callers remain (grep confirmed) |

**All 7 scans PASS.**
