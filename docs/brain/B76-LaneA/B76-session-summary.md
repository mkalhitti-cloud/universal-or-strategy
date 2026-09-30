# B76-LaneA -- Direct-Engineer Session Summary
# Date: 2026-08-18
# Authorization: Director (live-trading session, pre-pipeline)

---

## What we started with

One reported symptom: **followers inverting to Short after ATM BE stop filled them flat.**
Root cause investigation uncovered 3 distinct bugs.

---

## Bug #1 -- HOTFIX-B76-FLATTEN-RACE-01 (P1 -- VERIFIED PASS)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount`

**Bug**: ATM BE stop fills a follower flat. `acc.Positions` is stale in the same `OnOrderUpdate`
cycle (NT8 position lag, NT8_FULL_REFERENCE.md line 1721). `FlattenOneAccount` read `FindPosition`
before `CancelAllAccountOrders` -- saw qty=1 (stale) -- submitted PTT-Flatten Sell Market --
account inverted to 1 Short.

**Fix**: Added `posAfterCancel = FindPosition(acc, instr)` AFTER `CancelAllAccountOrders`.
If `posAfterCancel` is null or qty=0 -- emit "flat-race skip" and return.
Otherwise use `posAfterCancel` for action ternary and CreateOrder qty.

**Test result**: PASS -- confirmed live 12:48 PM session: exactly 1 PTT-Flatten Filled,
zero Cancelled duplicates, zero Short inversions.

---

## Bug #2 -- HOTFIX-B76-ATM-TPL-CLASSNAME (P2 -- VERIFIED PASS)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `GetLeaderAtmTemplateName`

**Bug**: `ct.AtmStrategy.Name` returns `"AtmStrategy"` (the NT8 class name) when no template
is actively staged in ChartTrader. The primary path returned this class name as if it were a
real template name. Confirmed by live log: `[PTT-CLONE] SetCloneAtmCache: 'AtmStrategy' (empty=False)`.
Clone mode received a fake template name -- follower got wrong ATM mode.

**Fix**: Added class-name guard: `if (n.Length > 0 && n != "AtmStrategy") return n;`
When name equals "AtmStrategy", fall through to Fallback-1 (AtmStrategySelector) and
Fallback-2 (ComboBox index walk) to get the real staged template name.

**Test result**: PASS -- confirmed live: `[PTT-CLONE] SetCloneAtmCache: 'AtmStrategy' (empty=False)`
no longer appears; correct template name is captured.

---

## Bug #3 -- HOTFIX-B76-POSSTATE-DEDUP-01 (P1 -- VERIFIED PASS)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryFirePositionState` + new field `_lastHasPos`

**Bug**: `PositionStateChanged` fired N times per position event where N = number of
Filled/PartFilled orders that pass Gate 2 per trade (entry fill + bracket fills + target fills
+ Close fill = 8+ orders). With 2 chart windows open: 16 False per close, 16 True per entry.
Root cause: `TryFirePositionState` invoked `PositionStateChanged` on every qualifying fill
regardless of whether `hasPos` had actually changed.

**Fix**: Added `_lastHasPos ConcurrentDictionary<string, int[]>` keyed by instrument FullName.
`int[]` box gives a stable heap ref for `Interlocked.Exchange`. Sentinel value 2 = unknown
(fires unconditionally on first fill). Values 0=False, 1=True. Only the first thread to write
a new value wins -- all subsequent threads with the same value return immediately.

**Test result**: VERIFIED PASS -- 2026-08-18.
- 1 chart: 1 log line per transition (1 engine fire x 1 panel) ✅
- 2 charts: 2 log lines per transition (1 engine fire x 2 panels) ✅

---

## Pipeline status

All 3 hotfixes are logged in `docs/brain/NO-PIPELINE-REPAIRS.md`.
All 3 are APPLIED + SYNCED to NT8.

| Hotfix ID | File | Status |
|-----------|------|--------|
| HOTFIX-B76-FLATTEN-RACE-01 | CopyEngine.cs | APPLIED -- awaiting pipeline |
| HOTFIX-B76-ATM-TPL-CLASSNAME | TradeCopierPanel.cs | APPLIED -- awaiting pipeline |
| HOTFIX-B76-POSSTATE-DEDUP-01 | CopyEngine.cs | VERIFIED PASS -- awaiting pipeline |

**Pipeline block**: B76-LaneA
**Orchestrator prompt**: `docs/brain/B76-LaneA/ptt-orchestrator-prompt.md`
**Note**: ptt-orchestrator-prompt.md was written before Bug #2 (ATM class-name) and Bug #3
(posstate dedup) were identified. Ph4a engineer must execute all 3 tickets, not just the
original 2. See NO-PIPELINE-REPAIRS.md entries for the additional diffs.

---

## What to run next

```
Start ptt-orchestrator mode.
Paste: docs/brain/B76-LaneA/ptt-orchestrator-prompt.md
Note: 3 hotfixes are now in scope (not 2). The additional fixes are:
  - HOTFIX-B76-ATM-TPL-CLASSNAME (TradeCopierPanel.cs GetLeaderAtmTemplateName)
  - HOTFIX-B76-POSSTATE-DEDUP-01 (CopyEngine.cs TryFirePositionState + _lastHasPos field)
Both are already applied in src/. Ph4a engineer writes tests and verifies.
```
