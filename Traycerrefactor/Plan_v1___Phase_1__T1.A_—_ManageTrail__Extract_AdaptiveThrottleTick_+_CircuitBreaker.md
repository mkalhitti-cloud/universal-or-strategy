I have created the following plan after thorough exploration and analysis of the codebase. Follow the below plan verbatim. Trust the files and references. Do not re-verify what's written in the plan. Explore only when absolutely necessary. First implement all the proposed file changes and then I'll review all the changes together at the end.

## Observations

- `ManageTrailingStops` lives in `file:src/V12_002.Trailing.cs` lines 39-451 inside `#region Trailing Stops` of the `public partial class V12_002 : Strategy`.
- Lines 41-78 form a self-contained "tick + throttle + cleanup + circuit-breaker" preamble using only instance state (no method-local data flows out into the foreach below).
- All touched fields are partial-class members declared in `file:src/V12_002.cs`: `tickCountInLastSecond` (line 285), `lastTickCountReset` (286), `adaptiveThrottleMs` (287), `lastStopManagementTime` (269), `circuitBreakerActive` (volatile, 272), and the `circuitBreakerActivatedTime` property (274-282) wrapping `circuitBreakerActivatedTicks` with `Volatile.Read/Write`.
- `CleanupStalePendingReplacements()` is a private partial-class method in `file:src/V12_002.Trailing.StopUpdate.cs` line 38, freely callable from the new helper.
- The two existing `return;` early-exits (line 59 throttle deadline, line 76 circuit-breaker still hot) are the only branches that today bypass the foreach — they translate cleanly into `shouldExit = true; return;` paths.

## Approach

Surgically lift lines 41-78 verbatim into a new `private void ManageTrail_AdaptiveThrottleTick(out bool shouldExit)` placed immediately after the closing brace of `ManageTrailingStops` inside the same `#region Trailing Stops`. The two existing `return;` inside the lifted block become `shouldExit = true; return;`; the rest of the body is moved character-for-character to preserve the read-modify-write order on the six instance fields (Hotspot H10) and to keep the `Print` literal byte-identical (gate C6). The parent's first two executable statements become the helper invocation + early-exit check, then `var positionSnapshot = activePositions.ToArray();`. No new types, no `lock`, no new allocations, no new clock reads.

## Call Flow After Extraction

```mermaid
sequenceDiagram
    participant Parent as ManageTrailingStops
    participant Helper as ManageTrail_AdaptiveThrottleTick
    participant Cleanup as CleanupStalePendingReplacements
    Parent->>Helper: out _shouldExit
    Helper->>Helper: DateTime now = DateTime.Now
    Helper->>Helper: tickCountInLastSecond++; adaptive adjust
    alt throttle deadline NOT met
        Helper-->>Parent: shouldExit = true
    else deadline met
        Helper->>Helper: lastStopManagementTime = now
        Helper->>Cleanup: invoke
        alt circuitBreakerActive AND not yet expired
            Helper-->>Parent: shouldExit = true
        else expired
            Helper->>Helper: reset + Print "V8.30: Circuit breaker RESET..."
            Helper-->>Parent: shouldExit = false
        else not active
            Helper-->>Parent: shouldExit = false
        end
    end
    Parent->>Parent: if (_shouldExit) return
    Parent->>Parent: var positionSnapshot = activePositions.ToArray()
```

## Implementation Instructions

### Step 1 — Author the new helper in `file:src/V12_002.Trailing.cs`

Insert a new private partial-class method right after the closing brace of `ManageTrailingStops` (currently line 451) and before the orphaned comments at lines 453-454. Keep it inside `#region Trailing Stops`. Do not touch the orphaned comments or the `#endregion`.

Helper shape and contents:

- **Signature**: `private void ManageTrail_AdaptiveThrottleTick(out bool shouldExit)` exactly as specified.
- **First statement**: initialize `shouldExit = false;` so all code paths satisfy C# definite-assignment for the `out` parameter without rearranging existing logic.
- **Body** — move lines 41-78 of the current parent verbatim, in the SAME order, with two surgical edits only:
  1. Replace `return;` at the original line 59 (throttle deadline not met) with `shouldExit = true; return;`.
  2. Replace `return;` at the original line 76 (circuit breaker still hot) with `shouldExit = true; return;`.
- Everything else moves byte-identical:
  - `DateTime now = DateTime.Now;` (single clock read — do NOT duplicate).
  - The `tickCountInLastSecond++` + `if ((now - lastTickCountReset).TotalSeconds >= 1) { ... adaptiveThrottleMs Math.Min(500, ...+50)/Math.Max(100, ...-25); tickCountInLastSecond = 0; lastTickCountReset = now; }` block.
  - `if ((now - lastStopManagementTime).TotalMilliseconds < adaptiveThrottleMs)` guard.
  - `lastStopManagementTime = now;`.
  - `CleanupStalePendingReplacements();`.
  - The full `if (circuitBreakerActive) { ... }` block with the verbatim ASCII Print `"V8.30: Circuit breaker RESET - trailing stops resumed"`.
- All `// V8.30:` comments adjacent to the lifted statements move with their statements (preserves grep locality and audit traceability).
- No `lock`, no `new`, no LINQ, no lambda-captured locals, no `string.Concat`, no extra `DateTime.Now` calls. ASCII-only.

### Step 2 — Update the parent `ManageTrailingStops` call site in the same file

In `ManageTrailingStops` (line 39), delete the entire block currently spanning lines 41-78 (everything from `DateTime now = DateTime.Now;` through the closing `}` of the `if (circuitBreakerActive) { ... }` block) and replace it with the exact two-statement preamble specified by the ticket:

```
bool _shouldExit; ManageTrail_AdaptiveThrottleTick(out _shouldExit); if (_shouldExit) return;
```

The next executable statement remains the unchanged `var positionSnapshot = activePositions.ToArray();` (currently line 81). The `// V8.30: Thread-safe snapshot iteration` comment immediately above it stays in the parent.

Do NOT touch:
- The foreach loop body (lines 82-383) — that is T1.B/T1.C scope.
- The post-foreach `if (EnableSIMA)` block (lines 389-447) — T1.D scope.
- The trailing `ShadowEngineCheck();` call at line 450 — must remain the LAST executable statement of `ManageTrailingStops` per Hotspot H3.
- The two orphaned comments at lines 453-454, the `#endregion` at line 455, or class/namespace closers (Karpathy "Surgical Changes").

### Step 3 — Diff hygiene

- Touch ONLY the lines being lifted out of the parent and the inserted helper. No whitespace mutation across other lines (AGENTS.md "WHITESPACE MUTATION BANNED").
- No edits to `file:src/V12_002.Trailing.StopUpdate.cs`, `file:src/V12_002.cs`, or any other file (the writer-side circuit breaker arm-and-Print at `Trailing.StopUpdate.cs` lines 146-152 / 209-215 is intentionally separate from the reader-side reset moved here — leave both writer sites alone).
- Diff size budget: the entire change is ~38 lines moved + ~3 new lines (helper signature + brace + `shouldExit = false;`) + 1 new call line in the parent. Well under the 150 KB cap.

### Step 4 — Verification gates (run in order)

| # | Command | Expected outcome |
|---|---|---|
| 1 | `python scripts/csharp_hotspots.py \| findstr ManageTrail` | New `ManageTrail_AdaptiveThrottleTick` row visible: < 20 CYC, ≤ 50 LOC. Parent `ManageTrailingStops` CYC drops by ~10. |
| 2 | `grep -cn "V8.30: Circuit breaker RESET" src/V12_002.Trailing.cs` | == 1 (single occurrence, now inside the helper). |
| 3 | `grep -cn "lastStopManagementTime" src/V12_002.Trailing.cs` | ≥ 1 (no orphan reads in parent; both the throttle-check read and the timestamp write live in the helper). |
| 4 | `python check_ascii.py src/V12_002.Trailing.cs` | PASS (helper introduces no non-ASCII characters). |
| 5 | `grep -n "lock(" src/V12_002.Trailing.cs` | 0 matches (gate C-Thread2). |
| 6 | `dotnet build .\Linting.csproj` | Zero new warnings, zero new errors (definite-assignment for `out shouldExit` satisfied by the leading `shouldExit = false;`). |
| 7 | `powershell -File .\deploy-sync.ps1` | EXIT 0 (NinjaTrader hard-link sync per AGENTS.md §2). |

### Step 5 — PR

- Branch / PR title: `phase-6-t1a-adaptive-throttle-tick`.
- PR body should explicitly call out: byte-identical Print (gate C6), preserved field touch order (Hotspot H10), single `DateTime.Now` read preserved (P3), `ShadowEngineCheck()` still last call of parent (H3 — untouched in this ticket), zero new heap allocations (P1), zero new `lock` (C-Thread2), diff < 150 KB.
- Out of scope (call out for reviewer): T1.B/C/D foreach extractions, fleet-symmetry-sync extraction, writer-side circuit breaker arm-and-Print sites in `file:src/V12_002.Trailing.StopUpdate.cs`.