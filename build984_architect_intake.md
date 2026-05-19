# ARCHITECT INTAKE -- Build-984-SourceHardening -- 2026-05-05

## MISSION METADATA

- **BUILD_TAG**: `1111.004-v28.0-pr75-repairs` (new tag will be `1111.005-v28.0-b984`)
- **MISSION**: Build-984 Source Hardening
- **BRANCH**: `build-984-source-hardening` (branch off from PR #76 baseline)
- **REPO**: <https://github.com/mkalhitti-cloud/universal-or-strategy>
- **PLAN FILE**: `docs/brain/implementation_plan.md` (OVERWRITE with new plan)

---

## CONTEXT: WHY THIS MISSION EXISTS

Phase 4 (Build-983) extracted `ProcessOnStateChange` (432 lines, Complexity 91) into 5 discrete
handlers in `src/V12_002.Lifecycle.cs`. The extraction was pure (zero logic mutation, Path A).

During the Phase 4 P4 Arena adversarial audit (Codex 5.3, 2026-05-04), **12 pre-existing source
defects** (F-01 to F-12) were identified **within the code that was extracted verbatim** into the
5 handlers. Per the extraction-purity mandate, these were triaged as DEFERRED -- to be addressed
by the next mission (Build-984) as surgical source hardening, NOT as part of the extraction.

**Phase 4 is now confirmed complete.** The 5 handlers exist live:

```text
OnStateChangeSetDefaults  -> line 93
OnStateChangeConfigure    -> line 220
OnStateChangeDataLoaded   -> line 302
OnStateChangeRealtime     -> line 404
OnStateChangeTerminated   -> line 451
```

**Your mission**: Verify the defect evidence, design the structural repairs, and write a complete
surgical implementation plan for the ENGINEER to execute.

---

## FORENSIC EVIDENCE (P2 Package)

All defects are located in `src/V12_002.Lifecycle.cs`.

### F-01 -- Struct Layout Invariant: Hard Crash on Configure

**Severity**: MEDIUM | **Handler**: `OnStateChangeConfigure` | **Lines**: 260-269

```csharp
int _slotSize = Marshal.SizeOf(typeof(FleetDispatchSlot));
int _shadowOffset = Marshal.OffsetOf(typeof(FleetDispatchSlot), "Shadow").ToInt32();
if (_slotSize != 64 || _shadowOffset != 56)
{
    throw new InvalidOperationException(string.Format(
        "FleetDispatchSlot layout invariant violated: size={0}, shadowOffset={1}; expected size=64, offset=56",
        _slotSize, _shadowOffset));
}
```

**Proof**: Throws `InvalidOperationException` during `State.Configure` -- strategy unloadable.
No logging before throw, no recovery path, no graceful degradation.

---

### F-02 -- BarsArray Index Access Without Guard

**Severity**: HIGH | **Handler**: `OnStateChangeDataLoaded` | **Line**: 345

```csharp
atrIndicator = this.ATR(BarsArray[1], RMAATRPeriod);
```

**Proof**: `BarsArray[1]` valid only if `AddDataSeries` ran in Configure. If Configure threw
(e.g. F-01) or `AddDataSeries` was skipped, `BarsArray.Count == 1` and this throws
`IndexOutOfRangeException`.

---

### F-03 -- AddDataSeries Ordering Concern

**Severity**: LOW | **Handler**: `OnStateChangeConfigure` | **Lines**: 294-297

```csharp
AddDataSeries(BarsPeriodType.Minute, 5);  // placed LAST in Configure
AddDataSeries(BarsPeriodType.Minute, 10);
AddDataSeries(BarsPeriodType.Minute, 15);
```

**Proof**: NT8 requires AddDataSeries as early as possible in Configure. Placed after throwing
code (F-01), so if F-01 fires, secondary series are never registered.

---

### F-04 -- Silent Target Count Override

**Severity**: LOW | **Handler**: `OnStateChangeDataLoaded` | **Lines**: 327-342

```csharp
ConfiguredTargetCount = activeTargetCount; // silent mutation in backward-compat path
```

**Proof**: No Print() before mutation. User has no visibility when loading a pre-V12 template.
Violates observability mandate.

---

### F-05 to F-12 -- TBD (To Be Catalogued by Architect)

The Arena auditor identified 8 additional pre-existing defects in the same file.
Session log was truncated before the full triage table was captured.

**Architect task**: Independently read `V12_002.Lifecycle.cs` and catalogue F-05 to F-12.
Focus areas:

1. `OnStateChangeRealtime` (lines 404-449): `_isTerminating` guards, ChartControl null path
2. `OnStateChangeTerminated` (lines 451-534): Teardown ordering vs `_isTerminating = true`,
   `CancelAllV12GtcOrders(force: false)` placement
3. `OnStateChangeDataLoaded` (lines 302-402): Sticky state, ExecuteRiskLogicAudit placement
4. `OnConnectionStatusUpdate` (lines 536-570): Double-Enqueue pattern, error handling
5. `OnMarketData` (lines 574-594): Missed guards

---

## CONSTRAINTS

1. No `lock(stateLock)` -- BANNED. Use FSM/Actor Enqueue or atomic primitives.
2. ASCII-only in C# string literals. No Unicode, emoji, curly quotes.
3. Zero new `src/` files. All repairs in `V12_002.Lifecycle.cs` only.
4. Zero logic mutations. Harden error handling, add guards, reorder calls, add telemetry.
5. Photon hot path is untouchable (MMIO/SPSCRing/PhotonPool) except graceful fallback for F-01.

---

## DELIVERABLES

1. Verify F-01 to F-04 against live source.
2. Identify and catalogue F-05 to F-12.
3. Overwrite `docs/brain/implementation_plan.md` with full surgical plan.
4. End with Director's Handoff Block (single markdown code block, paste-ready for Codex).

---

*Packaged by: Antigravity (P1 Orchestrator) | Protocol: /architect_intake V14 Alpha | 2026-05-05*
