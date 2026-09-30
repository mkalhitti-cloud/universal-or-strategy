# DW-B73-B-01/02 -- Architecture Plan

**Pipeline ID**: DW-B73-B-01 + DW-B73-B-02 (combined)
**Brain dir**: `docs/brain/DW-B73-B-01/`
**Phase**: 1 (Architecture)
**Author**: ptt-architect
**Date**: 2026-08-21
**Source baseline**: HEAD d15709be, 295 [Fact]
**File in scope**: `src/PropTraderTools/TradeCopierPanel.cs` ONLY

---

## A. Problem Statements

### DW-B73-B-01 -- RaiseBeAllDisarmed self-notification redundancy

**Location**: `UpdateButtonColors` at `TradeCopierPanel.cs:583-588`

**Code as-is**:
```csharp
if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
{
    if (_leaderAccount != null)
        CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
    UpdateBeAllVisuals(BeState.Idle);                      // L587: paints THIS panel
    CopyEngine.Instance.RaiseBeAllDisarmed();              // L588: fires GlobalBeAllDisarmed
}
```

`RaiseBeAllDisarmed()` fires `GlobalBeAllDisarmed` on all subscribers, including this panel's
own `OnGlobalBeAllDisarmed` handler:

```csharp
private void OnGlobalBeAllDisarmed()
{
    Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle));  // L946
}
```

**Result**: This panel calls `UpdateBeAllVisuals(BeState.Idle)` TWICE -- once synchronously at
L587, and once via `Dispatcher.InvokeAsync` at L946 after the event fires. The second call is
redundant. Other panels receive the event exactly once and paint themselves once (correct).

**Consequence**: On every flat event with armed BE slots, one `Dispatcher.InvokeAsync` allocation
and one redundant WPF property assignment triple (`BorderBrush`, `Foreground`, `Background`) fires
on the calling panel. With `BrushTeal` inline allocations also present (DW-B73-B-02 unfixed),
this creates 2 + 2 = 4 wasted `SolidColorBrush` allocations per flat event from this panel alone.

**Chosen fix** (architecturally simplest, zero semantic change):

Remove the local `UpdateBeAllVisuals(BeState.Idle)` call at L587.
Rely solely on `RaiseBeAllDisarmed()` -> `OnGlobalBeAllDisarmed` -> `Dispatcher.InvokeAsync`
for ALL panels uniformly (including the calling panel). The calling panel's update becomes
async (queued on its own Dispatcher at `Background` priority) instead of synchronous, but:

- We are already on the UI thread when `UpdateButtonColors` fires (via `TryFirePositionState`
  -> `Dispatcher.InvokeAsync`)
- `Dispatcher.InvokeAsync` at `Background` priority runs before the next render pass
- No visible difference to the user
- All panels (including caller) take the same code path -- uniform behavior

**Net change**: remove 1 line (`UpdateBeAllVisuals(BeState.Idle)` at L587).
CYC of `UpdateButtonColors`: unchanged (the `if` block at L583 still has 1 branch -- removing
the body line does not remove the branch; CYC stays the same).

**Why not "unsubscribe-raise-resubscribe"**: That pattern is fragile, harder to verify, and
adds 2 lines of subscription churn. Removing the redundant direct call is strictly simpler.

**Why not guard in `OnGlobalBeAllDisarmed`**: That would add a state check to a CYC=1 method
and requires a local shadow variable. Overkill for a cosmetic redundancy.

---

### DW-B73-B-02 -- UpdateBeAllVisuals inline MakeBrush allocations

**Location**: `TradeCopierPanel.cs:957-958` (hot call) + 4 construction sites

**Code as-is** -- `UpdateBeAllVisuals` Idle branch:
```csharp
_globalBeBtn2.BorderBrush = MakeBrush(13, 148, 136);   // L957: new SolidColorBrush + Freeze()
_globalBeBtn2.Foreground  = MakeBrush(13, 148, 136);   // L958: new SolidColorBrush + Freeze()
```

`MakeBrush` always allocates:
```csharp
private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
{
    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
    brush.Freeze();
    return brush;
}
```

Every call to `UpdateBeAllVisuals(BeState.Idle)` allocates 2 `SolidColorBrush` objects.
`UpdateBeAllVisuals` fires on every flat event, every BE-ALL disarm, and every position close.

The teal color `(13, 148, 136)` = `#0d9488` (Tailwind `teal-600`) appears at **10 call sites**
across 5 button construction statements. Unlike `BrushActive`, `BrushCaution`, etc., it has
no `static readonly` cache field.

**All 10 inline `MakeBrush(13, 148, 136)` sites**:
```
L957   UpdateBeAllVisuals  -- _globalBeBtn2.BorderBrush (HOT PATH -- fires on every disarm/flat)
L958   UpdateBeAllVisuals  -- _globalBeBtn2.Foreground  (HOT PATH)
L1049  BuildBufferedButtonsRow -- _beBtn2.BorderBrush       (construction-time only)
L1050  BuildBufferedButtonsRow -- _beBtn2.Foreground        (construction-time only)
L1078  BuildBufferedButtonsRow -- _globalBeBtn2.BorderBrush (construction-time only)
L1079  BuildBufferedButtonsRow -- _globalBeBtn2.Foreground  (construction-time only)
L1111  BuildBufferedButtonsRow -- _quickBtn.BorderBrush     (construction-time only)
L1112  BuildBufferedButtonsRow -- _quickBtn.Foreground      (construction-time only)
L1140  BuildBufferedButtonsRow -- _quickAllBtn.BorderBrush  (construction-time only)
L1141  BuildBufferedButtonsRow -- _quickAllBtn.Foreground   (construction-time only)
```

The hot-path allocations are at L957-958. The construction-time allocations at L1049-1141 are
called only once per panel lifetime -- they are low-urgency but should be fixed for consistency
with the `static readonly` pattern already used for all other semantic colors.

**Chosen fix**:

Add one `static readonly` field after the existing brush block (L279):

```csharp
// DW-B73-B-02: teal border/foreground for BE/Quick buttons -- cached per JS-008
private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136);  // teal-600 #0d9488
```

Replace all 10 inline `MakeBrush(13, 148, 136)` calls with `BrushTeal`.

**Net change**: +1 field declaration, 10 inline `MakeBrush(...)` calls replaced with field ref.
CYC of `UpdateBeAllVisuals`: unchanged (2 branches, stays CYC=2).
CYC of `BuildBufferedButtonsRow`: unchanged (CYC=1, no branches touched).

---

## B. Ticket Split

**Ticket 1**: DW-B73-B-01 -- Remove redundant `UpdateBeAllVisuals` call in `UpdateButtonColors`
**Ticket 2**: DW-B73-B-02 -- Add `BrushTeal` static field + replace 10 inline `MakeBrush` calls

The tickets are independent: T1 touches only L587, T2 touches L279-area + L957-958 + L1049-1141.
They can share one pipeline run with sequential execution (T1 then T2).

---

## C. Test Plan

### Ticket 1 tests (DW-B73-B-01)

**T_DW_B73_B01_01** -- `RaiseBeAllDisarmed_NoException_WhenCalled`
Verify `CopyEngine.Instance.RaiseBeAllDisarmed()` executes without exception when no subscribers.
(Structural -- confirms the event path is intact after T1 edit.)

**T_DW_B73_B01_02** -- `GlobalBeAllDisarmed_EventExists_AndIsSubscribable`
Verify `CopyEngine` exposes `GlobalBeAllDisarmed` as a subscribable `Action` event.
(Structural -- confirms event member not accidentally removed.)

**T_DW_B73_B01_03** -- `RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce`
Subscribe a counter lambda to `GlobalBeAllDisarmed`, call `RaiseBeAllDisarmed()` once,
assert counter == 1. Confirms single-fire behavior.

### Ticket 2 tests (DW-B73-B-02)

**T_DW_B73_B02_01** -- `BrushTeal_IsNotNull`
`TradeCopierPanel.BrushTeal` (via reflection on the static field) is not null.
(Structural -- confirms field was added.)

**T_DW_B73_B02_02** -- `BrushTeal_IsFrozen`
`TradeCopierPanel.BrushTeal.IsFrozen == true`.
(JS-008 compliance -- confirms MakeBrush freeze is preserved.)

**T_DW_B73_B02_03** -- `BrushTeal_Color_MatchesTeal600`
`BrushTeal.Color.R == 13 && .G == 148 && .B == 136`.
(Regression guard -- confirms the correct color was cached.)

Total new [Fact]: **6** (3 per ticket).
Expected count after pipeline: 295 + 6 = **301** [Fact].

*Note*: B73Tests.cs already has 33 [Fact] covering `GlobalBeAllDisarmed` structural checks
(T_BEALL_ARM_02, T_DISARM_SYNC_01/02, T_DISARM_CROSS_01/02). The 3 new T1 tests complement
these without duplicating them. Tests T_DW_B73_B02_01/02/03 are new structural guards.

---

## D. CYC Budget

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `UpdateButtonColors` | 8 | 8 | 0 (1 line removed, no branch change) |
| `UpdateBeAllVisuals` | 2 | 2 | 0 |
| `BuildBufferedButtonsRow` | 1 | 1 | 0 |

No method exceeds CYC=8. No new methods introduced.

---

## E. JS-DNA Compliance

| Rule | Check |
|------|-------|
| JS-021 no `lock()` | No lock added |
| JS-001 no `throw new` | None added |
| JS-002 no `return null` | None added |
| JS-008 Freeze() brushes | `BrushTeal` uses `MakeBrush` which calls `.Freeze()` |
| ASCII-only | All identifiers and literals ASCII |
| CYC <= 8 | All methods at or below limit (see table above) |

---

## F. Scope Constraints

- **ONE file only**: `TradeCopierPanel.cs`
- **No changes to**: `CopyEngine.cs`, `PttBreakEven.cs`, `CopyEngineTests.cs`, or any test file
  except the new `[Fact]` additions in the appropriate test class (B73Tests.cs or a new file)
- **No refactors** beyond the two described fixes
- **No whitespace mutations** outside the 11 changed lines

---

## G. Expected Diff Summary

```
TradeCopierPanel.cs changes:
  ~ L279+1   ADD: private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136);
  - L587     REMOVE: UpdateBeAllVisuals(BeState.Idle);
  ~ L957     CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L958     CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1049    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1050    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1078    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1079    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1111    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1112    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1140    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal
  ~ L1141    CHANGE: MakeBrush(13, 148, 136) -> BrushTeal

Test file changes (new [Fact] in B73Tests.cs or DW-B73-B-Tests.cs):
  + T_DW_B73_B01_01  RaiseBeAllDisarmed_NoException_WhenCalled
  + T_DW_B73_B01_02  GlobalBeAllDisarmed_EventExists_AndIsSubscribable
  + T_DW_B73_B01_03  RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce
  + T_DW_B73_B02_01  BrushTeal_IsNotNull
  + T_DW_B73_B02_02  BrushTeal_IsFrozen
  + T_DW_B73_B02_03  BrushTeal_Color_MatchesTeal600
```

**Total changed lines in src/**: 12 (1 deletion + 1 insertion + 10 substitutions)
**Total new [Fact]**: 6
**Total lines changed including tests**: ~12 src + ~60 test = ~72 lines

---

## H. Risk Assessment

**DW-B73-B-01**: Low. The removed `UpdateBeAllVisuals(BeState.Idle)` call is redundant by
construction -- the identical call fires via `OnGlobalBeAllDisarmed` on the same panel within
the same Dispatcher frame. The only risk is if `GlobalBeAllDisarmed` has zero subscribers at
the moment of the raise (panel not yet attached). That cannot happen here: the event is
subscribed in `OnSessionAttached` (L624) and raised only from `UpdateButtonColors` which runs
only after `OnSessionAttached`. If the panel is attached, it has its own handler subscribed.

**DW-B73-B-02**: Zero risk. `BrushTeal` produces an identical frozen brush to the inline calls.
The construction-site replacements (L1049-1141) run once at panel init -- no behavioral change.
The hot-path replacements (L957-958) eliminate allocation -- no behavioral change.

---

## I. Pipeline Entry Point

Phase 1 (this document) is complete. Pipeline proceeds to:

**Phase 2**: `ptt-plan-reviewer` reads this document and produces `02-plan-review.md`.
Gate: `REVIEW_PASS` required before proceeding to Phase 3.
