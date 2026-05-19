# Phase 7: IPC Protocol Upgrade -- Implementation Plan (Build 1003)

**Date**: 2026-03-17
**Architect**: Claude (P3) | **Sign-off**: P5 GRANTED
**Engineer**: Codex
**Build Target**: 1003
**Predecessor**: Build 1002 (Phase 6: FSM Promotion & MetadataGuard) -- SIGNED OFF

---

## Phase 6 Closure (For Record)

Build 1002 Phase 6 sign-off confirmed by P5 Architect (Claude):

- C7 stale hydrated FSM auto-termination: LIVE, ghost FSMs closing correctly
- MetadataGuard G3 (IPC) + G4 (REAPER) gating: ACTIVE via deploy-sync.ps1
- No lock(stateLock) in new paths: CONFIRMED
- ASCII enforcement: PASSED deploy gate
- BUILD_TAG "1002": CONFIRMED

---

## Mission Brief

Phase 6 defined `MetadataGuardTimestamp` (G1) but never wired it into the IPC command path.
IPC commands carry NO sender-side UTC timestamp. G3 duplicate protection covers only 2 of ~20
commands (FLATTEN, CANCEL_ALL) using coarse minute-granularity server-side time binning.

Phase 7 closes these three gaps:

1. Introduce optional `ts=<UTC_TICKS>` field in the IPC command protocol (sender side)
2. Wire G1 universally at the strategy-thread parse boundary (pre-routing, all commands)
3. Extend G3 deduplication to all 8 entry commands with sub-minute resolution

**Scope boundary**: Master account unchanged. FSM event G1 path (AccountEvent.TimestampTicks) unchanged. No architectural changes to REAPER, Symmetry, or SIMA.

---

## Diagnosis

| Gap | File | Location | Severity |
|-----|------|----------|----------|
| G1 never called in IPC path | `V12_002.UI.IPC.cs` | `ProcessIpcCommands()` | P0 |
| No sender timestamp in command payload | All IPC senders | Protocol level | P0 |
| G3 covers only FLATTEN + CANCEL_ALL | `V12_002.UI.IPC.Commands.Fleet.cs` | Lines 81, 98 | P1 |
| G3 cmdId uses minute-bin (coarse dedup) | `V12_002.UI.IPC.Commands.Fleet.cs` | Line 80 | P1 |
| BUILD_TAG still "1002" | `src/V12_002.cs` | Line 44 | P0 |

**Existing assets to reuse (no new logic):**

- `MetadataGuardTimestamp(long eventTicks, string context)` -- `V12_002.MetadataGuard.cs` line 17
  - Threshold: 5000ms, fail-open on exception, handles `eventTicks <= 0` (returns true)
  - **DO NOT MODIFY this function**
- `MetadataGuardDuplicate(string cmdId, string context)` -- `V12_002.MetadataGuard.cs`
  - Reused as-is for C3 extension; no changes needed

---

## Change Inventory

| ID | Priority | File | Description |
|----|----------|------|-------------|
| C5 | P0 | `src/V12_002.cs` | `BUILD_TAG = "1003"` |
| C1 | P0 | `src/V12_002.UI.IPC.cs` | Extract `ts=<ticks>` from command parts; call `MetadataGuardCommandTimestamp()` before allowlist check; thread `senderTicks` through `ProcessIpcCommandCore()` |
| C2 | P0 | `src/V12_002.MetadataGuard.cs` | Add `MetadataGuardCommandTimestamp(long senderTicks, string context)` wrapper |
| C3 | P1 | `src/V12_002.UI.IPC.Commands.Fleet.cs` | Add G3 guard to 8 entry commands |
| C4 | P1 | `src/V12_002.UI.IPC.Commands.Fleet.cs` | Receive `senderTicks` param; upgrade cmdId seed to sub-minute when available |

**Total: 4 files, 5 changes.**

---

## Detailed Implementation

### C5 -- BUILD_TAG (src/V12_002.cs, line 44)

```csharp
// BEFORE:
public const string BUILD_TAG = "1002";

// AFTER:
public const string BUILD_TAG = "1003"; // V12.1003: Phase 7 IPC Protocol Upgrade -- UTC Ticks G1 enforcement
```

---

### C2 -- MetadataGuardCommandTimestamp (src/V12_002.MetadataGuard.cs)

Add after the existing `MetadataGuardTimestamp()` function (after line 41):

```csharp
private bool MetadataGuardCommandTimestamp(long senderTicks, string context)
{
    try
    {
        if (senderTicks <= 0)
        {
            Print(string.Format("[IPC-G1] WARN no-ts {0}: pass (fail-open)", context));
            return true;
        }
        return MetadataGuardTimestamp(senderTicks, string.Format("IPC:{0}", context));
    }
    catch { return true; }
}
```

**Invariants:**

- `MetadataGuardTimestamp` is UNCHANGED -- this is a thin wrapper only
- Fail-open when `senderTicks == 0` (backward compat for senders without ts= field)
- Fail-open on exception (catch block returns true)
- ASCII-only strings (no emoji, no curly quotes)

---

### C1 -- G1 Gate + ts= Extraction (src/V12_002.UI.IPC.cs)

**Step 1: Extract senderTicks in ProcessIpcCommands()**

In `ProcessIpcCommands()`, locate the line that splits parts:

```csharp
string[] parts = command.Split('|');
```

Immediately after, before the allowlist check, add:

```csharp
// Build 1003: Extract optional sender UTC ticks for G1 command-age validation
long senderTicks = 0;
for (int i = 1; i < parts.Length; i++)
{
    if (parts[i].StartsWith("ts=", StringComparison.OrdinalIgnoreCase))
    {
        long.TryParse(parts[i].Substring(3), out senderTicks);
        break;
    }
}

// G1: Validate command age. Fail-open when no ts= field present (senderTicks == 0).
if (!MetadataGuardCommandTimestamp(senderTicks, parts[0]))
    continue;
```

**Step 2: Thread senderTicks through ProcessIpcCommandCore()**

Change the signature of `ProcessIpcCommandCore()` to accept `senderTicks`:

```csharp
// BEFORE:
private void ProcessIpcCommandCore(string command, string[] parts)

// AFTER:
private void ProcessIpcCommandCore(string command, string[] parts, long senderTicks)
```

Update all call sites in `ProcessIpcCommands()` to pass `senderTicks`.

---

### C4 + C3 -- senderTicks Threading + G3 Extension (src/V12_002.UI.IPC.Commands.Fleet.cs)

**Step 1: Update TryHandleFleetCommand() signature**

```csharp
// BEFORE:
private bool TryHandleFleetCommand(string command, string[] parts)

// AFTER:
private bool TryHandleFleetCommand(string command, string[] parts, long senderTicks)
```

**Step 2: Upgrade cmdId seed (C4)**

Locate the existing cmdId line (currently line ~80):

```csharp
// BEFORE:
string cmdId = action + "|" + (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute).ToString();

// AFTER:
// Build 1003: Use sender ticks for sub-minute dedup resolution when ts= field provided
string cmdId = senderTicks > 0
    ? action + "|" + senderTicks.ToString()
    : action + "|" + (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute).ToString();
```

**Step 3: Extend G3 to entry commands (C3)**

The existing pattern for FLATTEN (line ~81) is:

```csharp
if (!MetadataGuardDuplicate(cmdId, action)) return true;
```

Add the identical guard line at the top of each of the following command blocks,
immediately before the dispatch call:

- `LONG`
- `SHORT`
- `OR_LONG`
- `OR_SHORT`
- `TREND_MANUAL_LIMIT`
- `RETEST_MANUAL_LIMIT`
- `FFMA_MANUAL_LIMIT`
- `FFMA_MANUAL_MARKET`

**CANCEL_ALL and FLATTEN already have G3 -- do NOT add a second guard to those.**

---

## New IPC Command Protocol (N1)

Sender-side format upgrade (backward-compatible):

```text
// Legacy (still accepted, G1 fail-open):
FLATTEN|
LONG|ES|1.5|2.0

// Build 1003+ (G1 age-validated, G3 sub-minute dedup):
FLATTEN|ts=638765432100000000
LONG|ES|1.5|2.0|ts=638765432100000000
```

**Sender implementation pattern:**

```python
# Python sender example
import time

UTC_EPOCH_TICKS_OFFSET = 621355968000000000  # .NET epoch vs Unix epoch
ticks_per_second = 10_000_000

def utc_ticks():
    return int(time.time() * ticks_per_second) + UTC_EPOCH_TICKS_OFFSET

cmd = f"LONG|ES|1.5|2.0|ts={utc_ticks()}"
```

---

## Safety Invariants (Engineer Must Verify)

1. **Fail-open mandatory**: G1 MUST return true when `senderTicks == 0`. Zero regression for legacy senders.
2. **MetadataGuardTimestamp unchanged**: C2 is additive. The existing function body is not touched.
3. **No G1 on FSM events**: `MetadataGuardFsmEvent()` path is independent -- no double-wrapping.
4. **ASCII-only** in all new `Print()` strings: `[IPC-G1]`, `[IPC-G1] WARN no-ts`, etc.
5. **BUILD_TAG "1003"** committed first, before any other change is deployed.
6. **No lock(stateLock)**: All new code runs on strategy thread inside `ProcessIpcCommands()`.
7. **Fail-open on exception**: All guard functions must have outer `try { ... } catch { return true; }`.
8. **No new files**: C2 is added to the existing `V12_002.MetadataGuard.cs` partial class.

---

## File Manifest

| File | Change | Priority |
|------|--------|----------|
| `src/V12_002.cs` | BUILD_TAG "1002" -> "1003" | P0 |
| `src/V12_002.MetadataGuard.cs` | Add `MetadataGuardCommandTimestamp()` wrapper | P0 |
| `src/V12_002.UI.IPC.cs` | ts= extraction, G1 gate, senderTicks threading | P0 |
| `src/V12_002.UI.IPC.Commands.Fleet.cs` | G3 on 8 entry commands, cmdId upgrade, senderTicks param | P1 |

---

## P4 Self-Audit Checklist

Engineer must verify each item before marking Build 1003 complete:

- [ ] BUILD_TAG == "1003" in V12_002.cs line 44
- [ ] `MetadataGuardTimestamp()` body unchanged (diff check)
- [ ] G1 rejects stale: command with ts= 6 seconds ago -> `[METADATA-G1] STALE IPC:FLATTEN` logged, command not executed
- [ ] G1 passes fresh: command with ts= = UtcNow.Ticks -> executes normally
- [ ] G1 fail-open: command with NO ts= field -> `[IPC-G1] WARN no-ts` logged, command executes normally
- [ ] G3 dedup LONG: same LONG command + same ts= sent twice -> second rejected with duplicate log
- [ ] G3 backward compat: FLATTEN without ts= -> falls back to minute-bin cmdId, no regression
- [ ] No G3 double-gate on FLATTEN or CANCEL_ALL (only one MetadataGuardDuplicate call per command)
- [ ] FSM event G1 path unchanged: MetadataGuardFsmEvent() calls MetadataGuardTimestamp() directly
- [ ] check_ascii.py passes on all 4 modified files
- [ ] No lock(stateLock) in any new code path
