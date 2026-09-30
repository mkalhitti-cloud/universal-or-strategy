# DW-B73-B-01/02 -- Tickets

**Pipeline ID**: DW-B73-B-01 + DW-B73-B-02 (combined)
**Brain dir**: `docs/brain/DW-B73-B-01/`
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Date**: 2026-08-21
**Source plan**: `docs/brain/DW-B73-B-01/02-architecture-plan.md` (REVIEW_PASS)
**Rules gate**: PASS (JS-021, JS-001, JS-002, JS-008, JS-033, ASCII-only, CYC<=8)
**Baseline**: HEAD d15709be, 295 [Fact]
**Expected [Fact] after pipeline**: 295 + 6 = **301**

---

## Ticket 1 -- DW-B73-B-01: Remove redundant UpdateBeAllVisuals in UpdateButtonColors

### Spec requirement IDs

- **DW-B73-B-01**: RaiseBeAllDisarmed redundant self-notification

### Problem statement

`UpdateButtonColors` calls `UpdateBeAllVisuals(BeState.Idle)` directly at L587, then
immediately calls `CopyEngine.Instance.RaiseBeAllDisarmed()` at L588. The raise fires
`GlobalBeAllDisarmed`, which causes this same panel's `OnGlobalBeAllDisarmed` handler to
invoke `UpdateBeAllVisuals(BeState.Idle)` a second time via `Dispatcher.InvokeAsync`. The
direct call at L587 is therefore redundant -- every panel (including the caller) already
receives the event-driven paint. Removing L587 makes all panels take the same uniform code
path.

### Exact method(s) touched

| File | Method | Lines |
|------|--------|-------|
| `src/PropTraderTools/TradeCopierPanel.cs` | `UpdateButtonColors` | L583-588 (remove L587) |

No other methods, files, or test files are modified by this ticket.

### Method signatures (unchanged)

```csharp
private void UpdateButtonColors()
```

The method signature does not change. Only one line inside its body is removed.

### Precise edit instructions

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**Before** (L583-588 inclusive):
```csharp
if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
{
    if (_leaderAccount != null)
        CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
    UpdateBeAllVisuals(BeState.Idle);                      // <-- REMOVE THIS LINE
    CopyEngine.Instance.RaiseBeAllDisarmed();
}
```

**After** (L583-588 with L587 removed):
```csharp
if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
{
    if (_leaderAccount != null)
        CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
    CopyEngine.Instance.RaiseBeAllDisarmed();
}
```

**Change summary**: Remove exactly 1 line -- the `UpdateBeAllVisuals(BeState.Idle);` call
at approximately L587 (the line immediately before `CopyEngine.Instance.RaiseBeAllDisarmed()`
in the `if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())` block).

No whitespace mutations. No other lines touched. No brace changes.

### JS rule constraints

| Rule ID | Category | Severity | Constraint |
|---------|----------|----------|------------|
| JS-021 | Concurrency | P0 | No `lock()` introduced. Edit removes one line; introduces nothing. CONFIRM: grep shows 0 new `lock(` occurrences after edit. |
| JS-001 | Type Safety | P0 | No `throw new XxxException` introduced. CONFIRM: no exception-throwing code added. |
| JS-002 | Type Safety | P0 | No `return null` introduced. CONFIRM: method returns `void`; no null return possible. |
| JS-033 | Concurrency | P0 | No `async void` introduced. CONFIRM: method is `private void`, not async. |
| ASCII-only | DNA | P0 | All identifiers ASCII. Edit removes only an ASCII identifier call site; no new identifiers. |
| CYC <= 8 | DNA | P1 | `UpdateButtonColors` CYC stays at 8 after edit. Removing a call from an `if` body does not remove the `if` branch. CONFIRM: `python scripts/complexity_audit.py` reports <= 8 for `UpdateButtonColors`. |

### NT8 constraints

- **No NT8 API additions**: Edit only removes a method call; no NT8 API invoked in new code.
- **Dispatcher.InvokeAsync remains intact**: The `OnGlobalBeAllDisarmed` handler at L944-946
  continues to call `Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(BeState.Idle))` unchanged.
  This is the correct and now-sole path for the calling panel to receive the paint update.
- **No lifecycle methods touched**: `OnSessionAttached`, `OnSessionDetached`, and related NT8
  lifecycle overrides are not modified.

### xUnit [Fact] names required

Add these 3 [Fact] methods to `tests/PropTraderTools.Tests/B73Tests.cs`
(or create `tests/PropTraderTools.Tests/DW-B73-B-Tests.cs` if B73Tests.cs does not exist or
cannot be extended cleanly -- see plan Section C note).

```
T_DW_B73_B01_01  RaiseBeAllDisarmed_NoException_WhenCalled
T_DW_B73_B01_02  GlobalBeAllDisarmed_EventExists_AndIsSubscribable
T_DW_B73_B01_03  RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce
```

**T_DW_B73_B01_01** -- `RaiseBeAllDisarmed_NoException_WhenCalled`

Assert that `CopyEngine.Instance.RaiseBeAllDisarmed()` executes without throwing when called
with zero subscribers. Structural guard: confirms the event path is intact after the T1 edit
and that the line removal did not accidentally break the raise site.

```csharp
[Fact]
public void RaiseBeAllDisarmed_NoException_WhenCalled()
{
    // Act + Assert: no exception thrown when no subscribers attached
    var exception = Record.Exception(() => CopyEngine.Instance.RaiseBeAllDisarmed());
    Assert.Null(exception);
}
```

**T_DW_B73_B01_02** -- `GlobalBeAllDisarmed_EventExists_AndIsSubscribable`

Assert that `CopyEngine` exposes `GlobalBeAllDisarmed` as a subscribable `Action` event/field.
Structural guard: confirms the event member was not accidentally removed.

```csharp
[Fact]
public void GlobalBeAllDisarmed_EventExists_AndIsSubscribable()
{
    // Arrange
    var subscribed = false;
    CopyEngine.Instance.GlobalBeAllDisarmed += () => subscribed = true;

    // Act
    CopyEngine.Instance.RaiseBeAllDisarmed();

    // Assert
    Assert.True(subscribed);

    // Cleanup
    CopyEngine.Instance.GlobalBeAllDisarmed -= () => { };
}
```

**T_DW_B73_B01_03** -- `RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce`

Subscribe a counter lambda to `GlobalBeAllDisarmed`, call `RaiseBeAllDisarmed()` once, assert
counter == 1. Behavioral guard: confirms single-fire behavior -- the remove of L587 must not
cause a regression where the event fires zero or multiple times.

```csharp
[Fact]
public void RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce()
{
    // Arrange
    var fireCount = 0;
    Action handler = () => fireCount++;
    CopyEngine.Instance.GlobalBeAllDisarmed += handler;

    // Act
    CopyEngine.Instance.RaiseBeAllDisarmed();

    // Assert
    Assert.Equal(1, fireCount);

    // Cleanup
    CopyEngine.Instance.GlobalBeAllDisarmed -= handler;
}
```

### 7-scan checklist (engineer contract)

The engineer MUST run all 7 scans to zero before BUILD_PASS:

- [ ] **Scan 1: lock() grep** -- `grep -r "lock(" src/ --include="*.cs"` -- must return **0 matches** (no new `lock()` introduced by this ticket)
- [ ] **Scan 2: async void grep** -- `grep -rn "async void " src/ --include="*.cs"` -- must return **0 matches** for non-event-handler occurrences in new/modified code
- [ ] **Scan 3: return null grep** -- `grep -rn "return null;" src/ --include="*.cs"` -- must return **0 matches** in new or modified code added by this ticket
- [ ] **Scan 4: CYC audit** -- `python scripts/complexity_audit.py` -- `UpdateButtonColors` must report **CYC <= 8** after the 1-line removal
- [ ] **Scan 5: ASCII-only** -- `grep -P "[\x80-\xFF]" src/PropTraderTools/TradeCopierPanel.cs` -- must return **0 matches** (no Unicode introduced)
- [ ] **Scan 6: Build** -- `dotnet build` -- must complete with **0 errors, 0 warnings** in new/modified code
- [ ] **Scan 7: Test** -- `dotnet test` -- all prior [Fact] pass; new [Fact] count = **298** (295 + 3 new T1 tests); all 3 T_DW_B73_B01_XX pass

### Acceptance criteria

- [ ] `UpdateBeAllVisuals(BeState.Idle);` at approximately L587 is removed -- the line no longer appears in the `if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())` block
- [ ] `CopyEngine.Instance.RaiseBeAllDisarmed();` at approximately L588 is still present and unchanged
- [ ] `OnGlobalBeAllDisarmed` handler at L944-946 is unchanged
- [ ] `dotnet build` passes with 0 errors in `TradeCopierPanel.cs`
- [ ] `T_DW_B73_B01_01`, `T_DW_B73_B01_02`, `T_DW_B73_B01_03` all pass (`dotnet test`)
- [ ] Total [Fact] count = 298 (295 baseline + 3 new)
- [ ] All 7 scans report clean (0 violations)

---

## Ticket 2 -- DW-B73-B-02: Add BrushTeal static field + replace 10 inline MakeBrush calls

### Spec requirement IDs

- **DW-B73-B-02**: UpdateBeAllVisuals inline MakeBrush allocations (teal color not cached)

### Problem statement

The teal color `(13, 148, 136)` = `#0d9488` (Tailwind teal-600) is called inline via
`MakeBrush(13, 148, 136)` at 10 sites across `UpdateBeAllVisuals` and `BuildBufferedButtonsRow`.
`MakeBrush` allocates a new `SolidColorBrush` and calls `.Freeze()` on every invocation.
The 2 hot-path sites (L957-958 inside `UpdateBeAllVisuals`) fire on every flat event, every
BE-ALL disarm, and every position close, causing 2 wasted heap allocations per call. Unlike
every other semantic color in the panel (`BrushActive`, `BrushDanger`, `BrushCaution`,
`BrushInactive`, `BrushPurple`, `BrushConnected`), teal has no `static readonly` cache field.
Fix: add one `BrushTeal` static field and replace all 10 inline call sites.

### Exact method(s) touched

| File | Method / Location | Lines |
|------|-------------------|-------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Field block (class level) | ~L279 (add 2 lines after existing brush block) |
| `src/PropTraderTools/TradeCopierPanel.cs` | `UpdateBeAllVisuals` | L957, L958 (replace 2 inline calls) |
| `src/PropTraderTools/TradeCopierPanel.cs` | `BuildBufferedButtonsRow` | L1049, L1050, L1078, L1079, L1111, L1112, L1140, L1141 (replace 8 inline calls) |

**INFO-1 (from plan review)**: The spec originally identified 6 call sites but had TBD markers
at L1111 and L1140. The correct total is **10 call sites** (confirmed in architecture plan
Section A). All 10 must be replaced. No partial replacement is acceptable.

### Method signatures (unchanged)

```csharp
private static void UpdateBeAllVisuals(BeState state)
private void BuildBufferedButtonsRow(StackPanel panel, string label)
```

Neither method signature changes. Only the `MakeBrush(13, 148, 136)` call sites within each
method body are substituted with `BrushTeal`.

### Precise edit instructions

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

#### Edit 1 -- Add BrushTeal field (~L279, after existing brush block)

Find the last line of the existing brush block. The existing pattern ends near L276-279 with
fields like `BrushActive`, `BrushDanger`, `BrushCaution`, `BrushInactive`. Add a blank line
and the new field immediately after:

```csharp
// DW-B73-B-02: teal border/foreground for BE/Quick buttons -- cached per JS-008
private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136);  // teal-600 #0d9488
```

The comment and field declaration are two new lines inserted AFTER the last line of the
existing brush block (after the line containing `BrushInactive` or whichever is last). Do not
alter surrounding lines. No whitespace mutations elsewhere.

**Reference**: Existing pattern in same class (follow exactly):
```csharp
private static readonly SolidColorBrush BrushActive   = MakeBrush(34, 197, 94);
private static readonly SolidColorBrush BrushDanger   = MakeBrush(239, 68, 68);
private static readonly SolidColorBrush BrushCaution  = MakeBrush(234, 179, 8);
private static readonly SolidColorBrush BrushInactive = MakeBrush(100, 116, 139);
// <-- INSERT HERE (blank line + new field)
// DW-B73-B-02: teal border/foreground for BE/Quick buttons -- cached per JS-008
private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136);  // teal-600 #0d9488
```

#### Edit 2 -- Replace 10 inline MakeBrush(13, 148, 136) calls with BrushTeal

Replace each occurrence of `MakeBrush(13, 148, 136)` with `BrushTeal`. All 10 occurrences
are listed below with their approximate line numbers and context. The ptt-engineer MUST verify
the exact line number by reading the file before making each substitution.

| Site | Approx. Line | Method | Property assigned |
|------|-------------|--------|-------------------|
| 1 | L957 | `UpdateBeAllVisuals` | `_globalBeBtn2.BorderBrush` |
| 2 | L958 | `UpdateBeAllVisuals` | `_globalBeBtn2.Foreground` |
| 3 | L1049 | `BuildBufferedButtonsRow` | `_beBtn2.BorderBrush` |
| 4 | L1050 | `BuildBufferedButtonsRow` | `_beBtn2.Foreground` |
| 5 | L1078 | `BuildBufferedButtonsRow` | `_globalBeBtn2.BorderBrush` |
| 6 | L1079 | `BuildBufferedButtonsRow` | `_globalBeBtn2.Foreground` |
| 7 | L1111 | `BuildBufferedButtonsRow` | `_quickBtn.BorderBrush` |
| 8 | L1112 | `BuildBufferedButtonsRow` | `_quickBtn.Foreground` |
| 9 | L1140 | `BuildBufferedButtonsRow` | `_quickAllBtn.BorderBrush` |
| 10 | L1141 | `BuildBufferedButtonsRow` | `_quickAllBtn.Foreground` |

**Before** (representative example, each of the 10 sites has this pattern):
```csharp
_globalBeBtn2.BorderBrush = MakeBrush(13, 148, 136);
_globalBeBtn2.Foreground  = MakeBrush(13, 148, 136);
```

**After**:
```csharp
_globalBeBtn2.BorderBrush = BrushTeal;
_globalBeBtn2.Foreground  = BrushTeal;
```

**Verification**: After all 10 replacements, the string `MakeBrush(13, 148, 136)` must NOT
appear anywhere in `TradeCopierPanel.cs`. Run:
```
grep -n "MakeBrush(13, 148, 136)" src/PropTraderTools/TradeCopierPanel.cs
```
This must return **0 matches**. If any matches remain, the replacement is incomplete.

### JS rule constraints

| Rule ID | Category | Severity | Constraint |
|---------|----------|----------|------------|
| JS-021 | Concurrency | P0 | No `lock()` introduced. New field and 10 substitutions involve no locking. CONFIRM: grep shows 0 new `lock(` in modified file. |
| JS-001 | Type Safety | P0 | No `throw new XxxException` introduced. Field init and substitutions add no exception code. |
| JS-002 | Type Safety | P0 | No `return null`. `BrushTeal` is initialized to `MakeBrush(13, 148, 136)` -- a non-null frozen brush. The field cannot be null at runtime (static initializer runs before first access). CONFIRM: `T_DW_B73_B02_01` (`BrushTeal_IsNotNull`) must pass. |
| **JS-008** | Type Safety | **P1** | **Frozen brushes / readonly struct**: `BrushTeal` MUST be frozen. `MakeBrush` calls `brush.Freeze()` before returning, so the field is frozen by construction. CONFIRM: `T_DW_B73_B02_02` (`BrushTeal_IsFrozen`) must pass AND assert `BrushTeal.IsFrozen == true`. |
| JS-033 | Concurrency | P0 | No `async void` introduced. No new async code. |
| ASCII-only | DNA | P0 | All new identifiers ASCII: `BrushTeal`. Comment text is ASCII. No Unicode in string literals. CONFIRM: Scan 5. |
| CYC <= 8 | DNA | P1 | `UpdateBeAllVisuals` CYC stays at 2 after 2 substitutions (no branch changes). `BuildBufferedButtonsRow` CYC stays at 1 after 8 substitutions (no branch changes). CONFIRM: Scan 4. |

### NT8 constraints

- **SolidColorBrush must be Frozen**: NT8 UI requires `SolidColorBrush` instances used as
  dependency property values to be frozen for thread safety. `BrushTeal` is frozen by
  `MakeBrush` (which calls `.Freeze()` before returning). Verified by `T_DW_B73_B02_02`.
- **No hex color strings**: Numeric RGB `(13, 148, 136)` is used -- no `#RRGGBB` string
  literals introduced. This matches the existing pattern in the file.
- **No FontFamily**: No `FontFamily` objects are introduced.
- **static readonly is appropriate**: Static readonly fields for `SolidColorBrush` are the
  established pattern in this class. `MakeBrush` is a `private static` factory. The field
  `BrushTeal` follows the identical pattern as `BrushActive`, `BrushDanger`, etc.
- **No Dispatcher.InvokeAsync required for field init**: `static readonly` fields are
  initialized at class load time on the CLR thread. The field is only READ from the UI thread
  (in `UpdateBeAllVisuals` which runs via `Dispatcher.InvokeAsync`). No threading concern.

### xUnit [Fact] names required

Add these 3 [Fact] methods to `tests/PropTraderTools.Tests/B73Tests.cs`
(or the same test file used for Ticket 1 tests -- both tickets' tests belong in the same file).

```
T_DW_B73_B02_01  BrushTeal_IsNotNull
T_DW_B73_B02_02  BrushTeal_IsFrozen
T_DW_B73_B02_03  BrushTeal_Color_MatchesTeal600
```

**Access pattern**: `BrushTeal` is `private static readonly`. All 3 tests access it via
reflection:

```csharp
private static SolidColorBrush GetBrushTeal()
{
    var field = typeof(TradeCopierPanel)
        .GetField("BrushTeal", BindingFlags.NonPublic | BindingFlags.Static);
    return (SolidColorBrush)field!.GetValue(null)!;
}
```

**T_DW_B73_B02_01** -- `BrushTeal_IsNotNull`

Structural guard: confirms the `BrushTeal` field was added and is not null.

```csharp
[Fact]
public void BrushTeal_IsNotNull()
{
    // Act
    var brush = GetBrushTeal();

    // Assert
    Assert.NotNull(brush);
}
```

**T_DW_B73_B02_02** -- `BrushTeal_IsFrozen`

JS-008 compliance guard: confirms `MakeBrush` freeze is preserved on the cached field.

```csharp
[Fact]
public void BrushTeal_IsFrozen()
{
    // Act
    var brush = GetBrushTeal();

    // Assert
    Assert.True(brush.IsFrozen);
}
```

**T_DW_B73_B02_03** -- `BrushTeal_Color_MatchesTeal600`

Regression guard: confirms the correct teal-600 color is cached (R==13, G==148, B==136).

```csharp
[Fact]
public void BrushTeal_Color_MatchesTeal600()
{
    // Act
    var brush = GetBrushTeal();
    var color = brush.Color;

    // Assert
    Assert.Equal(13,  color.R);
    Assert.Equal(148, color.G);
    Assert.Equal(136, color.B);
}
```

### 7-scan checklist (engineer contract)

The engineer MUST run all 7 scans to zero before BUILD_PASS:

- [ ] **Scan 1: lock() grep** -- `grep -r "lock(" src/ --include="*.cs"` -- must return **0 matches** (no new `lock()` introduced by this ticket)
- [ ] **Scan 2: async void grep** -- `grep -rn "async void " src/ --include="*.cs"` -- must return **0 matches** for non-event-handler occurrences in new/modified code
- [ ] **Scan 3: return null grep** -- `grep -rn "return null;" src/ --include="*.cs"` -- must return **0 matches** in new or modified code added by this ticket
- [ ] **Scan 4: CYC audit** -- `python scripts/complexity_audit.py` -- `UpdateBeAllVisuals` must report **CYC <= 8** (expect 2); `BuildBufferedButtonsRow` must report **CYC <= 8** (expect 1)
- [ ] **Scan 5: ASCII-only** -- `grep -P "[\x80-\xFF]" src/PropTraderTools/TradeCopierPanel.cs` -- must return **0 matches** (no Unicode introduced)
- [ ] **Scan 6: Build** -- `dotnet build` -- must complete with **0 errors, 0 warnings** in new/modified code
- [ ] **Scan 7: Test** -- `dotnet test` -- all prior [Fact] pass; new [Fact] count = **301** (298 after T1 + 3 new T2 tests = 301 total from baseline 295); all 3 T_DW_B73_B02_XX pass; zero regressions in existing tests

Additional verification grep (not a formal scan, but engineer MUST run):
```
grep -n "MakeBrush(13, 148, 136)" src/PropTraderTools/TradeCopierPanel.cs
```
Must return **0 matches**. Any remaining inline `MakeBrush(13, 148, 136)` = incomplete ticket.

### Acceptance criteria

- [ ] `private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136);` is present in the class-level field block in `TradeCopierPanel.cs`, immediately after the last existing `BrushXxx` field
- [ ] The comment `// DW-B73-B-02: teal border/foreground for BE/Quick buttons -- cached per JS-008` is present on the line before `BrushTeal`
- [ ] All 10 occurrences of `MakeBrush(13, 148, 136)` in `TradeCopierPanel.cs` are replaced with `BrushTeal` (grep returns 0 matches for `MakeBrush(13, 148, 136)`)
- [ ] `BrushTeal.IsFrozen == true` (verified by `T_DW_B73_B02_02`)
- [ ] `BrushTeal.Color == Color.FromRgb(13, 148, 136)` (verified by `T_DW_B73_B02_03`)
- [ ] `dotnet build` passes with 0 errors in `TradeCopierPanel.cs`
- [ ] `T_DW_B73_B02_01`, `T_DW_B73_B02_02`, `T_DW_B73_B02_03` all pass (`dotnet test`)
- [ ] Total [Fact] count = **301** (295 baseline + 3 T1 tests + 3 T2 tests)
- [ ] All 7 scans report clean (0 violations)

---

## Pipeline summary

| Ticket | File(s) touched | Lines changed | New [Fact] | [Fact] running total |
|--------|----------------|---------------|------------|---------------------|
| T1 (DW-B73-B-01) | `TradeCopierPanel.cs`, test file | 1 removal | 3 | 295 + 3 = 298 |
| T2 (DW-B73-B-02) | `TradeCopierPanel.cs`, test file | 1 insert + 10 substitutions | 3 | 298 + 3 = 301 |
| **Total** | `TradeCopierPanel.cs` only in `src/` | **12** | **6** | **301** |

Execution order: T1 first, T2 second (sequential). T1 and T2 touch zero overlapping lines
and could be run in parallel, but sequential is simpler to verify.

**INFO-1** (absorbed from plan review): B-02 has 10 call sites (not 6 as in original spec).
All 10 are listed in Ticket 2 above. No partial replacement is acceptable.

**INFO-2** (absorbed from plan review): Total [Fact] after pipeline = **301** (not 298).
The commit message NNN field must be confirmed by ptt-verifier after implementation.

---

## Commit message (template)

```
fix(ptt): DW-B73-B-01+02 BeAllDisarmed self-notify + BrushTeal cache [NNN tests]
```

Where `NNN` = actual [Fact] count confirmed by ptt-verifier (expected: 301).
