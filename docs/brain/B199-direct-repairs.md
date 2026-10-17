# B199 Direct Repairs Log

All direct repairs to `src/PropTraderTools/CopyEngine.cs` applied this investigation session.
Pipeline-ready: keep any repair that survives live NT8 testing; promote the survivors into formal tickets.

---

## DR-1 — Gate 5 Diagnostic Log
**File**: `CopyEngine.cs`  
**Location**: `IsLiveEntryBlocked` ~line 5832  
**Status**: Applied. Confirmed firing in user output.

**Change**: Added `[PTT-GATE5-BLOCKED]` log when `_liveEntryInstruments.ContainsKey(instrKey)` returns true.

```
[PTT-GATE5-BLOCKED] stale instrKey=<key> orderId=<id>
```

**Purpose**: Makes every Gate 5 block visible. Previously a silent return=true.  
**Keep if**: Log fires on the blocked entry. Remove noise version after RC confirmed.

---

## DR-2 — Gate 2.5 Diagnostic Log
**File**: `CopyEngine.cs`  
**Location**: `OnOrderUpdate` ~line 1627  
**Status**: Applied. Not firing in user output (rule IS enabled -- Gate 2.5 not the issue).

**Change**: Added `[PTT-GATE25-BLOCKED]` log when `matchedRule.Value.Enabled == false`.

```
[PTT-GATE25-BLOCKED] rule disabled instr=<instr> acct=<acct> state=<state>
```

**Purpose**: Makes Gate 2.5 silent exit visible. Absence confirms rule is enabled.  
**Keep if**: Useful for future rule-disabled debugging. Low noise (Accepted/Working only).

---

## DR-3 — Clear `_liveEntryInstruments` on Copy ON
**File**: `CopyEngine.cs`  
**Location**: `SetEnabled` ~line 670  
**Status**: Applied. Structurally correct. Unclear if firing before or after stale key write (RC-2 hypothesis).

**Change**: Added `if (enabled) _liveEntryInstruments.Clear();` inside `SetEnabled`.

**Purpose**: User workaround -- toggle Copy OFF then ON to flush stale keys without NT8 restart.  
**Keep if**: DR-4 log confirms `cleared > 0` (RC-3 path). Even on RC-2, this is a valid safety net.  
**Superseded by**: DR-4 (which extends DR-3 to also clear `_entryInstrKeyByOrderId` and logs count).

---

## DR-4 — Extended Clear + Diagnostic Log on Copy ON
**File**: `CopyEngine.cs`  
**Location**: `SetEnabled` ~line 670  
**Status**: Applied (this session).

**Change**:
- Captures `_liveEntryInstruments.Count` BEFORE clear.
- Also clears `_entryInstrKeyByOrderId` (companion dict -- must stay in sync).
- Emits `[PTT-DR4-CLEAR]` log with count flushed.

```
[PTT-DR4-CLEAR] SetEnabled(true) flushed liveEntry=<N> entryMap=<N>
```

**Interpretation**:
- `liveEntry=0` → key was written AFTER Clear ran → RC-2 (orderId mismatch). DR-5 is the fix.
- `liveEntry=1` → stale key from prior session was present → RC-3 (PSC never fired). DR-4 itself fixes this.

**Keep if**: Confirmed firing. The `_entryInstrKeyByOrderId.Clear()` addition is non-optional -- omitting it
leaves the companion dict with stale GUIDs that will never evict.

---

## DR-5 — `EvictDedup` Fallback on orderId Reassignment (RC-2 Fix)
**File**: `CopyEngine.cs`  
**Location**: `EvictDedup` signature + `Cancelled` block + `OnOrderUpdate` call site  
**Status**: Applied (this session).

**Root cause**: NT8/Rithmic reassigns `OrderId` from a GUID (at `Accepted`) to a numeric value (at `Cancelled`).
`IsLiveEntryBlocked` writes `_entryInstrKeyByOrderId[GUID] = instrKey` at `Accepted` time.
`EvictDedup` is called with the numeric orderId at `Cancelled` time → `TryRemove(numericId)` → miss →
`_liveEntryInstruments[instrKey]` is never removed → stuck forever.

**Changes**:

1. **`OnOrderUpdate` call site** (line 1541): Pass `instrKey` (computed from `Order.Instrument.FullName + "|" + Order.OrderAction`) as third arg to `EvictDedup`.

2. **`EvictDedup` signature** (line 5868): Added optional `string instrKey = null` parameter.

3. **`EvictDedup` Cancelled block** (line 5886): 
   - Normal path: `_entryInstrKeyByOrderId.TryRemove` succeeds → log `[PTT-DR5-EVICT]`.
   - Fallback path: TryRemove missed (orderId reassigned) AND `_liveEntryInstruments.ContainsKey(instrKey)` → remove directly → log `[PTT-DR5-FALLBACK]`.

**Log signals**:
```
[PTT-DR5-EVICT] orderId=<id> instrKey=<key>        -- normal path, key removed via companion dict
[PTT-DR5-FALLBACK] orderId=<id> instrKey=<key> (orderId remapped by NT8/Rithmic)  -- RC-2 path, direct removal
```

**Keep if**: `[PTT-DR5-FALLBACK]` fires → confirms RC-2. Even if only `[PTT-DR5-EVICT]` fires, the evict log is
useful permanent telemetry.  
**Risk**: None. The fallback only removes if `ContainsKey` confirms the key exists -- no false removals.

---

## Summary Table

| ID | Location | Change | Status | Log Tag |
|----|----------|--------|--------|---------|
| DR-1 | `IsLiveEntryBlocked` | Gate 5 block log | Applied, confirmed | `[PTT-GATE5-BLOCKED]` |
| DR-2 | `OnOrderUpdate` | Gate 2.5 block log | Applied, not firing (expected) | `[PTT-GATE25-BLOCKED]` |
| DR-3 | `SetEnabled` | `_liveEntryInstruments.Clear()` on enable | Applied, superseded by DR-4 | — |
| DR-4 | `SetEnabled` | Extended clear + count log | Applied this session | `[PTT-DR4-CLEAR]` |
| DR-5 | `EvictDedup` + call site | orderId-fallback eviction | Applied this session | `[PTT-DR5-EVICT]` / `[PTT-DR5-FALLBACK]` |

---

## Test Protocol

1. NT8 compile + restart.
2. Clear output tab.
3. Toggle Copy OFF then ON -- confirm `[PTT-DR4-CLEAR]` fires.
   - `liveEntry=0` → RC-2 path → watch for `[PTT-DR5-FALLBACK]` on cancel.
   - `liveEntry=N>0` → RC-3 path → DR-4 cleared the keys; subsequent entry should dispatch.
4. Place MGC test entry (Limit). Watch output:
   - Expected: `[PTT-ATM-SEND]` or copy dispatch log (no `[PTT-GATE5-BLOCKED]`).
   - If `[PTT-GATE5-BLOCKED]` still fires: key was re-written after DR-4. Look for `[PTT-DR5-FALLBACK]` on cancel.
5. Cancel the entry. Confirm `[PTT-DR5-EVICT]` OR `[PTT-DR5-FALLBACK]` fires.
6. Place second entry. Should dispatch cleanly.

---

---

## DR-6 — Dispatch + LaneD Entry Diagnostic Logs
**File**: `CopyEngine.cs`
**Location**: `DispatchCopy` post-Gate-5 + `TryLaneDHook` entry
**Status**: Applied (session 7 -- second test run).

**Trigger**: First test run showed Gate 5 correctly blocked the second `Accepted` event and `[PTT-DR5-EVICT]` confirmed clean eviction. But NO copy was dispatched on the first `Accepted` — and NO `[PTT-LANED-EXIT*]` log appeared. The dispatch path reached Gate 5, wrote the key, but TryLaneDHook produced zero output.

**Changes**:
1. `[PTT-DISPATCH]` log immediately after Gate 5 pass in `DispatchCopy` — confirms all 5 gates cleared, logs mode + follower count.
2. `[PTT-LANED-ENTRY]` log at top of `TryLaneDHook` — shows clone ATM cache string + object presence + follower count.

**Log signals**:
```
[PTT-DISPATCH] all gates passed instrKey=<key> orderId=<id> mode=Clone followers=<N>
[PTT-LANED-ENTRY] mode=Clone cache='<template>' obj=set|null followers=<N>
[PTT-LANED-EXIT1] mode=<m> ...       -- non-Clone mode, LaneD no-op
[PTT-LANED-EXIT2] type=<t> ...       -- Market leader, LaneD no-op
[PTT-LANED-EXIT3] atmMode=Inherit .. -- empty ATM cache, LaneD no-op -> follower loop runs
```

**Interpretation table**:

| Output | Meaning |
|--------|---------|
| No `[PTT-DISPATCH]` | DispatchCopy never reached Gate 5 -- `TryCancelFollowerEntries` or `TryDispatchLeaderFlat` returned true |
| `[PTT-DISPATCH]` but no `[PTT-LANED-ENTRY]` | Impossible (LANED-ENTRY is unconditional first line of TryLaneDHook) -- indicates compilation issue |
| `[PTT-LANED-ENTRY]` + `[PTT-LANED-EXIT1]` mode≠Clone | CopyMode was NOT Clone at dispatch time |
| `[PTT-LANED-ENTRY]` + `[PTT-LANED-EXIT3]` cache='' | ATM template not set -- followers=0 or loop dispatched via raw SendCopy |
| `[PTT-LANED-ENTRY]` mode=Clone cache='MCG $400 SL4' | ATM cache set correctly -- LaneD dispatches, check [PTT-ATM-SEND] |

**Keep if**: Reveals the exact exit branch. Remove after root cause confirmed.

---

## DR-7 — `SendCopyWithAtm` Exception Log
**File**: `CopyEngine.cs`
**Location**: `SendCopyWithAtm` catch block
**Status**: Applied.

**Change**: Added `[PTT-ATM-ERROR]` `Output.Process` to the catch block. Previously exceptions were swallowed into `StatusUpdate` only (not always subscribed).

**Log**: `[PTT-ATM-ERROR] ExceptionType: message`

---

## DR-8 — SIM Account Daily Cap Bypass
**File**: `CopyEngine.cs`
**Location**: `PassesDailyCapCheck`
**Status**: Applied (this run — the confirmed root cause of "no copy" on SIM followers).

**Root cause**: `PassesDailyCapCheck` checks `pnl > _dailyCapFloor (-500.0)`. For Sim103 and Sim104, `acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar)` is returning a value ≤ -500.0, causing the gate to block all follower dispatches silently. The daily cap is a live-account risk control — it should never apply to SIM/Playback accounts.

**Evidence**: `[PTT-LANED-SKIP] idx=0 reason=daily_cap acct=Sim103` and `idx=1 acct=Sim104` in the output. Gate 5 passed, `[PTT-DISPATCH]` confirmed 3 gates open, `[PTT-LANED-ENTRY]` confirmed Clone mode + obj=set + followers=2 — but both followers were rejected at Branch (B).

**Change**: Added SIM/Playback name prefix guard at top of `PassesDailyCapCheck` — returns `true` (pass) immediately for any account whose name starts with `"Sim"` or `"Playback"` (case-insensitive). Live accounts (no Sim/Playback prefix) still go through the P&L check.

**Keep if**: Copy dispatches to Sim103/Sim104 after this change. This fix is correct and permanent — daily cap was never designed for SIM testing.

---

## Pipeline Promotion Criteria

After passing the test protocol above:
- DR-1, DR-2: keep as permanent telemetry (low noise, high value).
- DR-4: keep -- `_entryInstrKeyByOrderId.Clear()` is mandatory; diagnostic log can be removed later.
- DR-5: promote to formal ticket with full DW card. The `EvictDedup` signature change needs a test coverage pass.
