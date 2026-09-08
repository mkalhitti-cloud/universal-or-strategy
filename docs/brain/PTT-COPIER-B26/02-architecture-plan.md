# PTT-COPIER-B26 Architecture Plan

**Defect**: NullReferenceException crash on Ctrl+Shift+B — `_beBufferBox` never assigned  
**Block**: PTT-COPIER-B26, Single Pipeline  
**Status**: REVIEW_PASS (Cycle 1)  
**Author**: ptt-architect  
**Date**: 2026-07-07  
**Prev block**: PTT-COPIER-B25  

---

## 1. Problem Statement

`DispatchShortcut(Key.B)` at line 3063 reads `_beBufferBox.Text` (line 3065). The field
`_beBufferBox` (`private TextBox`, line 202) is declared but **never assigned**. Its value is
always `null`. Every Ctrl+Shift+B keypress throws a `NullReferenceException` at runtime.

Root cause: `_beBufferBox` is a stale field remnant from a removed `BuildBeArmRow` row. The
changelog at line 42 records: *"Removed _beArmBtn/_beArmBufferBox; removed BuildBeArmRow."*
When the TextBox was removed from the UI, the `DispatchShortcut` usage at line 3065 was not
updated to use the replacement field: `_beBuffer` (`private int`, line 245), which is always
maintained by `OnBeUp` / `OnBeDown` button handlers.

The other shortcut cases are correct:
- `Key.T`: reads `_trimBuffer` (plain int, line 243) — no parse, no TextBox
- `Key.F`: reads `_flattenBuffer` (plain int, line 244) — no parse, no TextBox
- `Key.C`: no buffer field needed

`Key.B` must follow the same pattern as Key.T and Key.F: read the plain int `_beBuffer` directly.

---

## 2. Component List

| Component | File | Kind |
|-----------|------|------|
| Dead field removal | `TradeCopierPanel.cs` line 202 | Delete `private TextBox _beBufferBox;` |
| `DispatchShortcut` Key.B case body | `TradeCopierPanel.cs` lines 3063–3067 | Replace parse scaffolding with `_beBuffer` |
| Stale comment | `TradeCopierPanel.cs` line 3041 | Update to remove `_beBufferBox.Text` reference |
| New [Fact] | `tests/CopyEngineTests.cs` | `DispatchShortcut_KeyB_CallsBreakEvenWithBeBuffer` |

No changes to `CopyEngine.cs`. `BreakEven(Account, Instrument, int)` is an existing method
introduced in B24 with the correct signature; no overload changes are needed.

---

## 3. Lane-Split Gate (Mandatory)

**Q1. Same method or within 50 lines?**  
The two edits are in the same file (`TradeCopierPanel.cs`). The field declaration (line 202) and
the broken case arm (lines 3063–3067) are ~2,861 lines apart. However, they are parts of one
atomic fix for one defect: removing the dead field and removing its only usage. They cannot be
separated — the field deletion requires its last usage to be removed first.

**Q2. Fix B design depends on Fix A?**  
Fix B (replace Key.B case body) is the primary crash fix and can stand alone if Fix A were
deferred. Fix A (delete `_beBufferBox` field) is the dead-code cleanup and depends on Fix B
having already removed the last usage. They are coupled as one atomic commit.

**Q3. Each fix standalone value if other blocked?**  
Fix B alone stops the crash entirely. Fix A alone does not stop the crash. The combined fix is
required to both stop the crash and eliminate the dead field. Both must land in the same ticket.

**Q4. Each fix independent SIM verification path?**  
Both share the single verification path: press Ctrl+Shift+B, confirm no exception, confirm
`BreakEven` fires with the expected buffer value.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## 4. Defect Detail with Line Numbers

| Location | Content | Status |
|----------|---------|--------|
| `TradeCopierPanel.cs:202` | `private TextBox _beBufferBox;` | DEAD — never assigned, must delete |
| `TradeCopierPanel.cs:245` | `private int _beBuffer = 1;` | CORRECT replacement, already exists |
| `TradeCopierPanel.cs:3041` | Comment: `"BE path reads _beBufferBox.Text for buffer ticks"` | STALE — must update |
| `TradeCopierPanel.cs:3063` | `case Key.B:` | In scope |
| `TradeCopierPanel.cs:3064` | `int buf = 2;` | DELETE — scaffolding for stale TextBox parse |
| `TradeCopierPanel.cs:3065` | `int.TryParse(_beBufferBox.Text, out buf);` | DELETE — crashes: `_beBufferBox` is null |
| `TradeCopierPanel.cs:3066` | `_engine.BreakEven(_leaderAccount, _instrument, buf);` | CHANGE — use `_beBuffer` instead of `buf` |

---

## 5. Fix: Exact Source Changes

### 5.1 Delete dead field (TradeCopierPanel.cs line 202)

```csharp
// REMOVE this line entirely:
private TextBox _beBufferBox;
```

The field has no assignments anywhere in the file. Removing it eliminates the dead declaration.
No other references to `_beBufferBox` exist after the Key.B case is fixed in §5.2.

### 5.2 Replace Key.B case body (TradeCopierPanel.cs lines 3063–3067)

**Before (lines 3063–3067 — crashes at runtime):**
```csharp
case Key.B:
    int buf = 2;
    int.TryParse(_beBufferBox.Text, out buf);   // NullReferenceException: _beBufferBox == null
    _engine.BreakEven(_leaderAccount, _instrument, buf);
    break;
```

**After:**
```csharp
case Key.B:
    _engine.BreakEven(_leaderAccount, _instrument, _beBuffer);
    break;
```

- `int buf = 2;` — scaffolding for the old TextBox parse — DELETE
- `int.TryParse(_beBufferBox.Text, out buf);` — null crash site — DELETE
- `_engine.BreakEven(...)` — keep; change third argument from `buf` to `_beBuffer`

`_beBuffer` is a plain int field (`private int _beBuffer = 1;`, line 245). It is written by
`OnBeUp()` and `OnBeDown()` (both UI-thread button click handlers). Reading it in `DispatchShortcut`
is safe: `DispatchShortcut` runs on the WPF UI thread (called from `OnChartKeyDown`, a
`chart.PreviewKeyDown` handler — confirmed by the comment at line 3020–3021).

### 5.3 Update stale comment (TradeCopierPanel.cs line 3041)

**Before (line 3041):**
```csharp
// BE path reads _beBufferBox.Text for buffer ticks (UI-thread-safe; PreviewKeyDown is on UI thread).
```

**After:**
```csharp
// BE path reads _beBuffer (int, UI-thread-only) for buffer ticks.
```

---

## 6. Method Signatures (No New or Changed Signatures)

No method signatures change. `DispatchShortcut(Key key)` remains:
```csharp
private void DispatchShortcut(Key key)
```

`BreakEven(Account, Instrument, int)` in `CopyEngine` is called as before — signature unchanged
(established in B24).

---

## 7. Threading Model

`DispatchShortcut` is called exclusively from `OnChartKeyDown`, which is wired as a
`chart.PreviewKeyDown` handler (WPF UI thread). No `Dispatcher.InvokeAsync` is needed.

`_beBuffer` is a plain int (UI-thread-only) — consistent with `_trimBuffer` and `_flattenBuffer`
at lines 243–244. The comment at line 242 confirms:
> "plain int; UI-thread-only; no volatile per NT8-003"

No `ConcurrentQueue`, no `Dispatcher.InvokeAsync`, no `lock()`. Single-threaded read/write on the
WPF UI thread. Threading model is unchanged from before the fix.

---

## 8. CYC Budget

| Method | CYC Before | CYC After | Status | Notes |
|--------|-----------|-----------|--------|-------|
| `DispatchShortcut` | 5 | 5 | ✅ | 4 case arms + base 1; Key.B arm body simplified but arm count unchanged |

No branches are added or removed. `int.TryParse(...)` is a method call, not a branch. Removing
it does not change CYC. The switch case count (4) and the base (1) are unchanged. CYC = 5.

---

## 9. NT8 Compiler Rules Applied

| Rule | Status | Note |
|------|--------|------|
| NT8-001 (`init;` BANNED) | PASS | No `init` properties in change set |
| NT8-002 (`abstract/sealed record` BANNED) | PASS | No records |
| NT8-003 (`volatile double` BANNED) | PASS | No new volatile declarations |
| NT8-004 (`ImmutableDictionary` BANNED) | PASS | Not applicable |
| NT8-017 (volatile bool/int for cross-thread state) | PASS | `_beBuffer` is UI-thread-only plain int; no volatile needed |
| NT8-018 (`lock()` BANNED) | PASS | No lock anywhere in change set |
| NT8-043 (null-conditional event unsubscription WATCH) | PASS | `DispatchShortcut` performs no event operations |

**Key NT8 API facts (embedded per architect mandate — not directly used in this fix):**
- `AtmStrategyChangeStopTarget()` — StrategyBase-only, NOT AddOnBase
- `AtmStrategyCreate()` — StrategyBase-only, NOT AddOnBase
- `Account.Change()` — AddOnBase-available; silent no-op on ATM-owned brackets
- Correct AddOn bracket-change pattern: `Account.Cancel()` + `Account.CreateOrder()` + `Submit()`

---

## 10. Jane Street Rules Applied

| Rule | Status | Note |
|------|--------|------|
| JS-021 (lock BANNED) | PASS | No lock in change set |
| JS-033 (async void BANNED) | PASS | `DispatchShortcut` is `private void`, not async |
| JS-001 (throw in hot paths BANNED) | PASS | Fix removes a crash path; introduces no throws |
| JS-002 (return null BANNED) | PASS | No return null in change set |
| ASCII-only identifiers | PASS | `_beBuffer`, `BreakEven`, `DispatchShortcut` — all ASCII |
| No `DateTime.Now` | PASS | Not applicable |
| No hex color literals | PASS | Not applicable |
| No FontFamily references | PASS | Not applicable |

---

## 11. Test Requirement

**New test count**: 128 → 129 (one new [Fact])

### 11.1 New [Fact]: `DispatchShortcut_KeyB_CallsBreakEvenWithBeBuffer`

**File**: `tests/CopyEngineTests.cs` (or appropriate Panel test file)  
**Asserts**: When `DispatchShortcut(Key.B)` is invoked and `_beBuffer` is set to a known value
(e.g. 3), `CopyEngine.BreakEven` is called with that exact int value (not with a hardcoded `2`,
not with an exception).

**Implementation note for engineer**: `DispatchShortcut` is private. Use:
1. Reflection to set `_beBuffer` field to 3 on the panel instance under test, AND
2. A spy/mock on `ICopyEngine.BreakEven` (or reflection-based call capture) to observe the
   argument, OR
3. If WPF instantiation of `TradeCopierPanel` is not feasible in xUnit, document the test as a
   compile-time assertion: confirm that `_beBufferBox` is removed (no such field in the source)
   and `_beBuffer` is referenced in Key.B case — verified by the build succeeding with no
   `CS0103` for `_beBufferBox`.

Minimum acceptance: the test [Fact] exercises `Key.B` dispatch without throwing and confirms
`BreakEven` receives the `_beBuffer` value (not a hardcoded fallback of 2).

---

## 12. Scan Checklist Preview (7 Scans — All Must Reach Zero)

| Scan | Check | Expected Result |
|------|-------|-----------------|
| SCAN-01 | No `lock(` in any changed method | ZERO — no lock in DispatchShortcut or anywhere in change set |
| SCAN-02 | No `async void` in any changed method | ZERO — DispatchShortcut is `private void` |
| SCAN-03 | No `return null` in any changed method | ZERO — no return null |
| SCAN-04 | No `throw new XxxException` in hot paths | ZERO — no throws; fix removes a crash, not introduces one |
| SCAN-05 | No `DateTime.Now` in any changed method | ZERO — not applicable |
| SCAN-06 | No hex color literals, no FontFamily references | ZERO — not applicable |
| SCAN-07 | No null-conditional event unsubscription (`?.Event -=`) | ZERO — DispatchShortcut has no event operations |

---

## 13. Write Set

```
src/PropTraderTools/TradeCopierPanel.cs   -- 3 edits (field delete, case body replace, comment update)
tests/CopyEngineTests.cs                  -- 1 new [Fact] test
```

No changes to:
- `src/PropTraderTools/CopyEngine.cs` — BreakEven signature established in B24, no change needed
- `src/PropTraderTools/TradeCopierAddOn.cs` — not in scope

**Baseline**: 128 [Fact] tests.  
**Final count after B26**: 129 [Fact] tests (one added).

---

## 14. Deferred Items — Status in B26

Items carried forward from B24 and B25 that are **NOT addressed in B26**:

| ID | Item | Priority | Status in B26 |
|----|------|----------|---------------|
| DW-B24-01 | NT8-043 formal rule entry: confirm null-conditional unsubscription crash. NT8-043 added as P0 in B23; runtime crash confirmation outstanding. SCAN-07 = 0 in B26 change set. | P2 | OPEN — carry forward |
| DW-B24-02 | Manual E2E runtime verification: press B on a solo account in a live NT8 session. B26 is the fix that *enables* this verification. Recommend executing DW-B24-02 immediately after B26 merges. | P1 | OPEN — carry forward |
| DW-B24-03 | Skip-duplicate guard [Fact] for `if (acc == leader) continue` at CopyEngine.cs:~1195. Not in B26 scope. | P2 | OPEN — carry forward |
| DW-B25-01 | Companion plain-ref field race (`_pendingBeAccount` etc. singleton refs). Not in B26 scope. | P3 | OPEN — carry forward |

B26 does not address any of the above. B26 scope is strictly the `_beBufferBox` NullReferenceException.

---

*ptt-architect · PTT-COPIER-B26 · 2026-07-07 (Cycle 1)*
