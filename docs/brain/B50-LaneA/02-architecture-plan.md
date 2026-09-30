# B50-LaneA Architecture Plan — Clone Mode
## PTT-COPIER-B50 / Lane A

**Block**: PTT-COPIER-B50
**Lane**: A
**Label**: clone-mode
**Status**: REVIEW_PENDING
**Date**: 2026-08-08

---

## 1. Feature Summary

Implement `CopyMode.Clone` (value=2), a third copy mode that:

1. Auto-inherits the leader's live ATM template at fill time (no user ATM dropdown selection required).
2. Syncs leader bracket drags to all follower brackets (same `HandleBracketChange` path as Signal mode).
3. Is selected via a new **Clone** radio button appended to the Mode row in `BuildModeRow`.

---

## 2. JS Rule Pre-Check (Thought 2)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in any new code path | PASS — all new fields use `volatile` or are UI-thread-only |
| JS-001 | No `throw` in hot paths | PASS — new dispatch path delegates to `SendCopy` try/catch |
| JS-002 | No `return null` | PASS — `GetCloneAtmMode` returns `Inherit` or `Named`, never null |
| JS-033 | No `async void` (non-event-handler) | PASS — `OnCloneModeClick` is a `RoutedEventHandler`, synchronous |
| JS-010 | No public constructors without smart constructor | PASS — no new public types |
| DateTime | No `DateTime.Now` | PASS — no new DateTime usage |
| ASCII | No Unicode in identifiers or strings | PASS — all strings ASCII |
| PTT- prefix | All `CreateOrder` signal names start "PTT-" | PASS — Clone uses same `SendCopy` path ("PTT-Copy") |

---

## 3. NT8 Compiler Rule Pre-Check (Thought 2 + 7)

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | PASS — no new auto-properties with `init` |
| NT8-002 | No `abstract record` / `sealed record` | PASS — no new record types |
| NT8-003 | No `volatile double` or `volatile float` | PASS — `_cloneAtmCache` is `volatile string` (reference type, SAFE) |
| NT8-004 | No `ImmutableDictionary` | PASS — no immutable collections |
| NT8-054 | Test files in `Tests\` subfolder | PASS — `B50Tests.cs` at `Tests\B50Tests.cs` |

---

## 4. Component List (Files in Scope)

| File | Action | Change Count |
|------|--------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY | 6 changes |
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFY | 7 changes |
| `src/PropTraderTools/Features/PttFollowerStrategy.cs` | NO CHANGE | 0 — `FillSignalEventArgs.AtmTemplateName` already handles Named ATM |
| `src/PropTraderTools/Tests/B50Tests.cs` | CREATE | new file, 5 tests |
| `src/PropTraderTools/PropTraderTools.csproj` | MODIFY | 1 new `<Compile>` entry |

---

## 5. Class Names and Method Signatures

### 5.1 CopyEngine.cs

```csharp
// Enum extension — CopyEngine.cs line ~87
internal enum CopyMode { Signal = 0, Mirror = 1, Clone = 2 }

// New volatile field — after line 108
private volatile string _cloneAtmCache = string.Empty;

// New public method — after GetCopyMode (~line 345)
// CYC=1. Sets _cloneAtmCache. JS-002: null-coalesces to string.Empty.
internal void SetCloneAtmCache(string template)

// New private method — extracted helper for DispatchCopy mode resolution. CYC=2.
// Returns Named or Inherit; never null. Replaces GetAtmMode in DispatchCopy loop.
private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName)

// New internal method — internal for testability (mirrors ShouldMirrorClose pattern). CYC=2.
// Returns Named(_cloneAtmCache) or Inherit + warning log if cache is empty.
internal FollowerAtmMode GetCloneAtmMode()

// Modified method — DispatchCopy inner loop (1-line change only)
// OLD: var mode = GetAtmMode(rule, acc.Name);
// NEW: var mode = ResolveAtmMode(rule, acc.Name);
// CYC remains 8 (at limit). No new branches in DispatchCopy body.
private void DispatchCopy(Order order, CopyRule rule)
```

### 5.2 TradeCopierPanel.cs

```csharp
// New field — after line 196 (_mirrorModeBtn)
private RadioButton _cloneModeBtn = null;

// New field — near combo-related fields (after existing ATM combo refs section)
private readonly List<ComboBox> _atmComboRefs = new List<ComboBox>();

// Modified method — BuildModeRow adds _cloneModeBtn. CYC stays 1.
private void BuildModeRow(StackPanel root)

// Modified method — OnSignalModeClick adds UpdateAtmComboVisibility(Visible). CYC=1.
private void OnSignalModeClick(object sender, RoutedEventArgs e)

// Modified method — OnMirrorModeClick adds UpdateAtmComboVisibility(Visible). CYC=1.
private void OnMirrorModeClick(object sender, RoutedEventArgs e)

// New method — Clone mode click handler. CYC=1. JS-033: event handler (exempt).
private void OnCloneModeClick(object sender, RoutedEventArgs e)

// New method — Iterates _atmComboRefs, sets Visibility. CYC=2. UI-thread-only.
private void UpdateAtmComboVisibility(Visibility v)

// Modified method — OnFollowerAtmTemplateComboLoaded adds 1 line to track cb in _atmComboRefs.
// CYC stays 4.
private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
```

---

## 6. Data Flow (Thought 5)

### Scenario A — User selects Clone mode (UI thread)

```
OnCloneModeClick (UI thread)
  ├─ CopyEngine.Instance.SetCopyMode(CopyMode.Clone)
  │    └─ _copyModeValue = 2  (volatile int write — cross-thread visible)
  ├─ GetLeaderAtmTemplateName(_currentChart)
  │    └─ Reads ChartTrader visual tree ComboBox[2]
  │    └─ Returns string.Empty on null/exception (never throws)
  ├─ CopyEngine.Instance.SetCloneAtmCache(tpl)
  │    └─ _cloneAtmCache = tpl ?? string.Empty  (volatile string write)
  └─ UpdateAtmComboVisibility(Visibility.Collapsed)
       └─ foreach cb in _atmComboRefs: cb.Visibility = Collapsed
```

### Scenario B — Leader fill → Clone ATM dispatch (NT8 background thread)

```
acc.OrderUpdate (background thread)
  └─ CopyEngine.OnOrderUpdate
       ├─ TryFirePositionState (unconditional)
       ├─ Gate 1: _isCopyEnabled
       ├─ Gate 2: matchedRule found
       ├─ Gate 2.5: matchedRule.Enabled
       ├─ Mirror check: _copyModeValue == Mirror → FALSE for Clone (skipped)
       ├─ Gate B: IsWorkingBracket → FALSE for new Submitted fill (skipped)
       └─ DispatchCopy(order, rule)
            └─ per-follower loop:
                 └─ ResolveAtmMode(rule, acc.Name)
                      └─ _copyModeValue == Clone → TRUE
                           └─ GetCloneAtmMode()
                                ├─ _cloneAtmCache empty → Inherit + StatusUpdate warning
                                └─ _cloneAtmCache non-empty → Named(cache)
                 └─ SendCopy(acc, instr, signal, Named("MES $200 SL5"))
                      └─ PttBus.RaiseFillSignal(AtmTemplateName="MES $200 SL5")
                           └─ PttFollowerStrategy.OnFillSignal → CallAtmStrategyCreate
```

### Scenario C — Leader bracket drag → Clone bracket sync (NT8 background thread)

```
acc.OrderUpdate (background thread) — Working state order
  └─ CopyEngine.OnOrderUpdate
       ├─ Gates 1/2/2.5 pass
       ├─ Mirror check: _copyModeValue == Mirror → FALSE (skipped)
       ├─ Gate B: IsWorkingBracket → TRUE
       │    └─ HandleBracketChange(order, rule)
       │         └─ SyncFollowerBracket for each follower
       │              └─ Follower brackets updated (stop or target)
       └─ return  (DispatchCopy NOT called for bracket events)
```

**Key finding**: `HandleBracketChange` is called via Gate B for **all modes** unconditionally.
Clone bracket sync is already handled by the existing Gate B path. No change needed to `OnOrderUpdate`.

---

## 7. Threading Model (Thought 4)

| Field | Written by | Read by | Mechanism |
|-------|-----------|---------|-----------|
| `_copyModeValue` | UI thread (`OnCloneModeClick`) | NT8 background thread (`OnOrderUpdate`) | `volatile int` |
| `_cloneAtmCache` | UI thread (`SetCloneAtmCache`) | NT8 background thread (`GetCloneAtmMode`) | `volatile string` (reference type — CLR volatile SAFE) |
| `_cloneModeBtn` | UI thread (constructor) | UI thread only | UI-thread-only, no volatile |
| `_atmComboRefs` | UI thread (`OnFollowerAtmTemplateComboLoaded`) | UI thread (`UpdateAtmComboVisibility`) | UI-thread-only, `List<ComboBox>` |

No `Dispatcher.InvokeAsync` required for new code paths:
- All UI ops remain on the UI thread (event handlers).
- All `volatile` reads/writes from the background thread use value types (`int`) or reference types (`string`) — both safe in .NET 4.8 x64.
- No `lock()` anywhere.

---

## 8. CYC Analysis per Modified Method (Thought 3)

| Method | File | CYC | Status |
|--------|------|-----|--------|
| `SetCloneAtmCache` | CopyEngine.cs | 1 | NEW — PASS |
| `GetCloneAtmMode` | CopyEngine.cs | 2 | NEW — PASS |
| `ResolveAtmMode` | CopyEngine.cs | 2 | NEW — PASS |
| `DispatchCopy` | CopyEngine.cs | 8 | MODIFIED (1-line change) — AT LIMIT, PASS |
| `BuildModeRow` | TradeCopierPanel.cs | 1 | MODIFIED (add Clone btn) — PASS |
| `OnSignalModeClick` | TradeCopierPanel.cs | 1 | MODIFIED (+1 call) — PASS |
| `OnMirrorModeClick` | TradeCopierPanel.cs | 1 | MODIFIED (+1 call) — PASS |
| `OnCloneModeClick` | TradeCopierPanel.cs | 1 | NEW — PASS |
| `UpdateAtmComboVisibility` | TradeCopierPanel.cs | 2 | NEW — PASS |
| `OnFollowerAtmTemplateComboLoaded` | TradeCopierPanel.cs | 4 | MODIFIED (+1 line) — PASS |

All methods ≤ 8. PASS.

---

## 9. NT8 API Usage

| API | Source | Notes |
|-----|--------|-------|
| `volatile string` | .NET CLR | Reference type — CLR volatile is safe. NT8-003 only bans `volatile double/float`. |
| `List<ComboBox>` | System.Collections.Generic | Standard .NET, safe in NT8. |
| `RadioButton` | WPF | Standard WPF control, used throughout existing panel. |
| `Visibility.Collapsed` | WPF | Standard WPF enum, safe. |
| `GetLeaderAtmTemplateName(Chart)` | TradeCopierPanel.cs | Already proven working (B43). Returns `string.Empty` on any failure. |
| `FollowerAtmMode.Named(string)` | CopyEngine.cs | Existing discriminated union class. |
| `FollowerAtmMode.Inherit()` | CopyEngine.cs | Existing discriminated union class. |
| `FillSignalEventArgs.AtmTemplateName` | PttContracts.cs | Existing field, populated via `SendCopy` / `PttBus.RaiseFillSignal`. |

---

## 10. File Split Validation (Thought 6)

**CopyEngine.cs** — pure logic singleton. No UI, no WPF. All new code is logic-only:
- Enum extension, volatile field, 3 new methods, 1-line change in `DispatchCopy`.

**TradeCopierPanel.cs** — pure UI. No new engine logic:
- RadioButton field, `List<ComboBox>` field, UI event handlers, visibility helper.

**PttFollowerStrategy.cs** — NO CHANGES.
- `FillSignalEventArgs.AtmTemplateName` is already used by `CallAtmStrategyCreate`.
- Clone mode injects `Named(_cloneAtmCache)` → `SendCopy` → `PttBus.RaiseFillSignal(atmTemplate)` → `FillSignalEventArgs.AtmTemplateName` → `CallAtmStrategyCreate`.
- Zero cross-contamination.

**Tests/B50Tests.cs** — test-only, no production imports beyond `PropTraderTools`.

---

## 11. PttBuild.Tag Update

```csharp
// CopyEngine.cs line 41
internal const string Tag = "PTT-COPIER B50 | clone-mode+be-color+test-fix | 2026-08-08";
```

---

## 12. Deferred Items Opened by B50

| ID | Priority | Description |
|----|----------|-------------|
| DW-B50-01 | P1 | Live F5 verification: Clone mode ATM cache fills correctly from leader's ChartTrader ComboBox in real NT8 session. Depends on DW-B43-02 visual-tree index accuracy. |
| DW-B50-02 | P2 | `_atmComboRefs` list retains references to detached ComboBox controls if followers list is rebuilt. Not harmful but adds GC pressure. Future cleanup: weak references or list clear on panel teardown. |

---

## 13. Status

REVIEW_PENDING — awaiting ptt-plan-reviewer pass.
