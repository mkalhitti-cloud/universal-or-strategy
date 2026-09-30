# PTT-COPIER-B2 Architecture Plan

**Status:** PLAN_COMPLETE
**Spec:** specs/002-trade-copier-spec.html
**Date:** 2026-07-06
**Predecessor:** PTT-COPIER-B1 (verified, accepted deviations D1/D2/D3 preserved)

---

## 1. Overview

B2 is a surgical defect-fix block. All five defects were found in post-B1 review.
No new features are added. No B1 accepted deviations are re-opened.
Gate chain internals (OnOrderUpdate, SendCopy, Trim, Flatten, CancelPendingEntries,
IsBracketLeg, IsDedup) are **unchanged**.

### Scope

| Ticket | File | Changes |
|--------|------|---------|
| T1 | `CopyEngine.cs` | `_rules` ConcurrentBag + string-based `AddRule` overload |
| T2 | `TradeCopierWindow.cs` | Subscribe lifecycle, row Apply wiring, bare catch, border brush |
| T3 | `TradeCopierPanel.cs` | Rule wiring (field promotion, Apply button), button IsEnabled |
| T4 | `specs/002-trade-copier-spec.html` | 10 SD item surgical updates for B2 features |

### Wave Workspace

`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## 2. Defect Fix Table

| ID | Priority | File | Line (before) | Symptom | Fix | JS Rule Satisfied |
|----|----------|------|---------------|---------|-----|-------------------|
| D1 | P0 | `TradeCopierWindow.cs` | 27 (OnInitialize), 33 (OnDestroyed) | `Subscribe()`/`Unsubscribe()` never called; engine never hears order events | Add `_engine.Subscribe()` in OnInitialize after StatusUpdate hook; add `_engine.Unsubscribe()` in OnDestroyed after StatusUpdate unhook | JS-021 (no lock), lifecycle correctness |
| D2a | P0 | `CopyEngine.cs` | 21 (`_rules` field) | `List<CopyRule>` is not thread-safe; UI thread writes while NT thread iterates | Replace with `ConcurrentBag<CopyRule>` | JS-021, JS-025 (lock-free) |
| D2b | P0 | `CopyEngine.cs` | 93-96 (`AddRule`) | `CopyRule` is private — no external caller can construct one; `AddRule(CopyRule)` is unreachable from UI | Add `internal void AddRule(string instrument, Account master, Account[] followers)` overload that constructs `CopyRule` internally | JS-003 (CopyRule privacy preserved), JS-010 |
| D2c | P0 | `TradeCopierPanel.cs` | 55, 65 (local combos), 57-68 (string population), BuildUI | ComboBoxes populated with Name strings; no Apply handler; rule list stays empty forever | Promote combos to fields; set `ItemsSource = Account.All`; add Apply button and `OnApplyRule()` | JS-001 (no throw in handler), JS-021 |
| D2d | P0 | `TradeCopierWindow.cs` | 137-143 (followerCb), BuildRuleRow | followerCb has no `ItemsSource`; no Apply button; rule list stays empty forever | Add `ItemsSource = Account.All` to followerCb; add column 7 Apply button; add `OnRowApply()` | JS-001, JS-021 |
| D3 | P1 | `TradeCopierPanel.cs` | 89, 94, 99 | `IsEnabled = false` makes Trim/Flatten/Cancel unreachable; cannot test live | Change to `IsEnabled = true` | N/A (correctness only) |
| D4 | P1 | `TradeCopierWindow.cs` | 241 | Bare `catch {}` swallows all exception types; JS-001 violation | Change to `catch (Exception) { return null; }` | JS-001 (typed catch) |
| D5 | P1 | `TradeCopierWindow.cs` | 63, 87 | `"BorderBrush"` resource key is wrong; Panel already uses `"NTBrushes.BorderBrush"` correctly | Change both sep1/sep2 references to `"NTBrushes.BorderBrush"` | SCAN-04 (NTBrushes.* only) |

---

## 3. Thread Model Diagram

```
WPF UI THREAD
-------------
TradeCopierWindow.OnInitialize()
  |-- _engine = CopyEngine.Instance
  |-- _engine.StatusUpdate += OnStatusUpdate
  |-- _engine.Subscribe()                          [<-- B2 NEW]
  |      |-- Account.All.OrderUpdate += OnOrderUpdate
  |-- BuildUI()

Button Click: "Apply Rule" (Panel)
  |-- OnApplyRule()
  |      |-- reads _leaderCombo.SelectedItem as Account
  |      |-- reads _followersCombo.SelectedItem as Account
  |      |-- _engine.AddRule(instrument.FullName, leader, followers)  [<-- B2 NEW]
  |             |-- CopyRule.Create(...)    [stays private to CopyEngine]
  |             |-- _rules.Add(rule)        [ConcurrentBag.Add -- lock-free]

Button Click: "Apply" (Window row)
  |-- OnRowApply()
  |      |-- reads btn.Tag as object[] { instrName, leaderCb, followerCb }
  |      |-- _engine.AddRule(instrName, leader, followers)            [<-- B2 NEW]
  |             |-- CopyRule.Create(...)
  |             |-- _rules.Add(rule)

Button Click: SetEnabled / Trim / Flatten / CancelPendingEntries
  |-- synchronous calls into CopyEngine (unchanged from B1)

TradeCopierWindow.OnDestroyed()
  |-- _engine.StatusUpdate -= OnStatusUpdate
  |-- _engine.Unsubscribe()                        [<-- B2 NEW]
         |-- Account.All.OrderUpdate -= OnOrderUpdate


NT STRATEGY THREAD
------------------
Account.All.OrderUpdate fires
  |-- CopyEngine.OnOrderUpdate(sender, e)   [UNCHANGED from B1]
  |      Gate 1: if (!_isCopyEnabled) return
  |      Gate 2: foreach rule in _rules          [ConcurrentBag.GetEnumerator -- snapshot-safe]
  |                find matching instrument + master
  |      Gate 3: OrderState.Submitted, Market/Limit only
  |      Gate 4: IsDedup(orderId)
  |      --> CopySignal.Create(...)
  |      --> foreach follower: SendCopy(acc, instrument, in signal)
  |                                        [unchanged, "PTT-Copy"]
  |-- StatusUpdate?.Invoke(msg)             [fires on NT thread]


CROSS-THREAD BOUNDARY
---------------------
StatusUpdate event: NT thread --> WPF UI thread
  TradeCopierWindow.OnStatusUpdate(line)
    |-- Dispatcher.InvokeAsync(() => { _logPanel.Children.Insert(0, tb); })
  TradeCopierPanel.OnStatusUpdate(line)
    |-- Dispatcher.InvokeAsync(() => { _statusText.Text = line; })

                    [Dispatcher.InvokeAsync: NO CHANGES in B2]

SUBSCRIBE OWNERSHIP (B2 CONSTRAINT):
  TradeCopierWindow: OWNS Subscribe() and Unsubscribe()
  TradeCopierPanel:  MUST NOT call Subscribe() or Unsubscribe()
```

### Key Invariants

1. `_engine.Subscribe()` is called exactly once per Window instance lifetime.
2. `_engine.Unsubscribe()` is called exactly once when Window is destroyed.
3. All `AddRule()` calls originate on the WPF UI thread (button clicks).
4. All `OnOrderUpdate` iterations of `_rules` occur on the NT strategy thread.
5. `ConcurrentBag` makes concurrent Add + GetEnumerator safe without any `lock()`.

---

## 4. `_rules` Collection Trade-off Analysis

### Chosen: `ConcurrentBag<CopyRule>`

```csharp
private readonly System.Collections.Concurrent.ConcurrentBag<CopyRule> _rules
    = new System.Collections.Concurrent.ConcurrentBag<CopyRule>();
```

### Alternatives Considered

| Collection | Verdict | Reason Rejected |
|------------|---------|-----------------|
| `List<CopyRule>` | **REJECTED** | Not thread-safe for concurrent Add + foreach. Data race between UI thread (Add) and NT strategy thread (foreach). B1 defect. |
| `ConcurrentBag<CopyRule>` | **CHOSEN** | Thread-safe Add + snapshot-safe GetEnumerator. Zero lock(). Rules are few (1-10 instruments). O(n) iteration is fine. |
| `ConcurrentDictionary<string, CopyRule>` | Rejected | Overkill. Key-based lookup has no benefit here — Gate 2 already does linear scan by instrument name. Adds unnecessary key-management complexity. |
| `ImmutableList<CopyRule>` with `Interlocked.CompareExchange` | Rejected | Requires atomic pointer swap on every Add — correct but overly complex for write-once-at-startup semantics. ConcurrentBag is simpler and sufficient. |

### Why ConcurrentBag is the Right Choice Here

**Access pattern:**
- **Writes:** UI thread only, at rule-wiring time (before active trading starts, typically).
- **Reads:** NT strategy thread, on every `OnOrderUpdate` call (hot path Gate 2).
- **Concurrent write+read:** Theoretically possible (user clicks Apply while trading live).

**ConcurrentBag guarantees:**
- `Add(item)` is thread-safe: uses thread-local storage + steal-other-thread fallback, no visible `lock()`.
- `GetEnumerator()` is thread-safe: returns a snapshot of items at time of enumeration.
- A concurrent `Add()` during `foreach` either appears in the snapshot or does not — both are safe outcomes (the new rule fires on the next order event at worst).

**JS-021 compliance:** No `lock()` statement in this file. PASS.
**JS-025 compliance:** Lock-free data structure for concurrent state. PASS.

---

## 5. Rule Wiring Diagram

```
PANEL SURFACE                       WINDOW SURFACE
-----------------                   -----------------
  _leaderCombo                         leaderCb
  (ItemsSource = Account.All)          (ItemsSource = Account.All)    [FIXED D2d]
       |                                    |
  _followersCombo                      followerCb
  (ItemsSource = Account.All)          (ItemsSource = Account.All)    [FIXED D2d]
       |                                    |
  [Apply Rule]                         [Apply]  col 7                 [NEW D2c/D2d]
  applyBtn.Click                       applyBtn.Click
       |                                    |
  OnApplyRule()                        OnRowApply()
       |                                    |
       |-- validate leader/follower/    |-- read tag = {instrName,
       |   instrument not null              leaderCb, followerCb}
       |                                |-- validate not null
       |                                |
       +--------------------+-----------+
                            |
                    _engine.AddRule(
                        string instrument,
                        Account master,
                        Account[] followers)       [NEW OVERLOAD -- CopyEngine.cs]
                            |
                    CopyRule.Create(               [PRIVATE struct -- stays private]
                        instrument,
                        master,
                        followers)
                            |
                    _rules.Add(rule)               [ConcurrentBag -- thread-safe]
                            |
                    rule is now live:
                    OnOrderUpdate Gate 2 will
                    match on next order event


EXISTING OVERLOAD (unchanged, internal-only):
    internal void AddRule(CopyRule rule)
        _rules.Add(rule)               [called only from within CopyEngine itself]
```

---

## 6. Gate Chain Confirmation

**The gate chain in `CopyEngine.OnOrderUpdate` is UNCHANGED in B2.**

Only the collection type changes (`List<CopyRule>` → `ConcurrentBag<CopyRule>`).
The foreach iteration pattern at Gate 2 is identical:

```csharp
// Gate 2: find matching rule -- instrument AND master account must match
CopyRule? matchedRule = null;
foreach (var rule in _rules)                     // _rules is now ConcurrentBag
{
    if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)
    {
        matchedRule = rule;
        break;
    }
}
```

All of the following are UNCHANGED:
- Gate 1: `!_isCopyEnabled` volatile bool read
- Gate 2: instrument + master account match
- Gate 3: `OrderState.Submitted`, `OrderType.Market` or `OrderType.Limit`
- Gate 4: `IsDedup(orderId)` via `_dedupCache` ConcurrentDictionary
- `CopySignal.Create(...)` dispatch
- `PassesDailyCapCheck(acc)` stub (Block 2 returns `true`)
- `SendCopy(acc, instrument, in signal)` — "PTT-Copy" CreateOrder
- `Trim()` — "PTT-Trim" CreateOrder
- `Flatten()` — "PTT-Flatten" CreateOrder
- `CancelPendingEntries()` + `IsBracketLeg()` 3-layer guard
- `IsDedup()` 10-second TTL pruning
- `AllAccounts()` instrument fence
- `FindRule()` linear scan

**B2 gate chain delta: zero lines changed.**

---

## 7. B1 Deviation Preservation

| Deviation | B1 Accepted Decision | B2 Status |
|-----------|---------------------|-----------|
| **D1** | `TradeCopierPanel` is `public sealed class` (NTWindow subclass — `sealed` acceptable for ChartTrader row extension) | **UNCHANGED.** B2 does not touch the class declaration. Still `public sealed class TradeCopierPanel : NTWindow`. |
| **D2** | API naming: `AddRule` / `Subscribe` / `Unsubscribe` accepted in place of B1 plan's `Initialize` / `Shutdown` | **UNCHANGED.** B2 adds a new `AddRule(string, Account, Account[])` overload but does not rename or remove any existing method. Subscribe/Unsubscribe are now correctly wired (fixing D1 defect) without changing names. |
| **D3** | `CopyEngine` is `internal sealed` (correct visibility for NT8 Add-On assembly; `public` would expose singleton to unintended callers) | **UNCHANGED.** B2 does not touch the class declaration. Still `internal sealed class CopyEngine`. |

---

## 8. 7-Scan + B2-Scan Compliance

### Standard 7-Scan

| Scan | Pattern | B2 Change | Guarantee |
|------|---------|-----------|-----------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | `ConcurrentBag.Add()` and `ConcurrentBag.GetEnumerator()` use no visible `lock()`. All new handlers (OnApplyRule, OnRowApply) have zero lock statements. | **0 results** |
| SCAN-02 | Non-ASCII characters | All new string literals ("Apply Rule", "Apply", "No instrument -- open a chart first.", "Select leader and follower accounts.", "Rule applied: ") are 7-bit ASCII. All new identifiers are ASCII. | **0 results** |
| SCAN-03 | `FontFamily` | No `FontFamily` property in any new code. All controls inherit NT WPF theme. | **0 results** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` hex colors | All color references use `SetResourceReference` with `NTBrushes.*` keys. D5 fix changes `"BorderBrush"` → `"NTBrushes.BorderBrush"` (removes the non-conforming key). | **0 results** |
| SCAN-05 | `CreateOrder` name not starting with `"PTT-"` | No new `CreateOrder` calls in B2. Existing "PTT-Copy", "PTT-Trim", "PTT-Flatten" are unchanged. | **0 violations** |
| SCAN-06 | `DateTime\.Now[^U]` | No timestamps in any new code. All existing `DateTime.UtcNow` usages unchanged. | **0 results** |
| SCAN-07 | `\block\s*\(` (belt-and-suspenders lock scan) | Same guarantee as SCAN-01. No `lock()` syntax anywhere. | **0 results** |

### B2-Specific Scans

| Scan | What to Verify | Expected Result |
|------|---------------|-----------------|
| B2-SCAN-01 | `grep "Account.All.OrderUpdate" CopyEngine.cs` | Exactly 2 hits: `+= OnOrderUpdate` in `Subscribe()` and `-= OnOrderUpdate` in `Unsubscribe()`. No additional subscriptions. |
| B2-SCAN-02 | `grep "_engine.Subscribe\|_engine.Unsubscribe" TradeCopierPanel.cs` | **0 results** — Panel must never call these. |
| B2-SCAN-03 | `grep "_engine.Subscribe\|_engine.Unsubscribe" TradeCopierWindow.cs` | Exactly 2 results: one in `OnInitialize()` and one in `OnDestroyed()`. |
| B2-SCAN-04 | `grep "new List<CopyRule>" CopyEngine.cs` | **0 results** — must be `ConcurrentBag` only. |
| B2-SCAN-05 | `grep "ConcurrentBag" CopyEngine.cs` | Exactly 1 result: the `_rules` field declaration. |
| B2-SCAN-06 | `grep "catch {" TradeCopierWindow.cs` | **0 results** — bare catch must be gone (D4 fix). |
| B2-SCAN-07 | `grep '"BorderBrush"' TradeCopierWindow.cs` (plain key without NTBrushes. prefix) | **0 results** — D5 fix applied. |
| B2-SCAN-08 | `grep "IsEnabled = false" TradeCopierPanel.cs` | **0 results** in button declarations — D3 fix applied (trimBtn, flattenBtn, cancelBtn all `IsEnabled = true`). |

---

## 9. Method Signatures — New in B2

### 9.1 CopyEngine.cs

```csharp
// NEW OVERLOAD -- callable from external UI surfaces
// (existing AddRule(CopyRule rule) stays, used internally only)
internal void AddRule(string instrument, Account master, Account[] followers)
{
    _rules.Add(CopyRule.Create(instrument, master, followers));
}
```

CYC: 1. No branches. No lock(). No throw.

### 9.2 TradeCopierPanel.cs

**Promoted fields (were local variables in BuildUI):**
```csharp
private ComboBox _leaderCombo;
private ComboBox _followersCombo;
```

**BuildUI() delta (additions only — existing code unchanged):**
```csharp
// Changed: ItemsSource = Account.All instead of foreach adding Name strings
_leaderCombo = new ComboBox();
_leaderCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
_leaderCombo.ItemsSource = Account.All;

_followersCombo = new ComboBox();
_followersCombo.SetResourceReference(Control.StyleProperty, "AccountComboBoxStyle");
_followersCombo.ItemsSource = Account.All;

// NEW: Apply button (after the two combos, before separator)
var applyBtn = new Button { Content = "Apply Rule" };
applyBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
applyBtn.Click += OnApplyRule;
root.Children.Add(applyBtn);
```

**New handler:**
```csharp
private void OnApplyRule(object sender, RoutedEventArgs e)
{
    var leader   = _leaderCombo.SelectedItem as Account;
    var follower = _followersCombo.SelectedItem as Account;
    if (leader == null || follower == null)
    {
        if (_statusText != null)
            _statusText.Text = _instrument == null
                ? "No instrument -- open a chart first."
                : "Select leader and follower accounts.";
        return;
    }
    if (_instrument == null)
    {
        if (_statusText != null)
            _statusText.Text = "No instrument -- open a chart first.";
        return;
    }
    _engine.AddRule(_instrument.FullName, leader, new[] { follower });
    if (_statusText != null)
        _statusText.Text = "Rule applied: " + _instrument.Name;
}
```

CYC: 6 (1 base + 5 decision points). Within CYC <= 8.

### 9.3 TradeCopierWindow.cs

**BuildRuleRow() delta (column 7 addition):**
```csharp
// followerCb: add missing ItemsSource (D2d fix)
followerCb.ItemsSource = Account.All;

// Column 7: Apply button
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
var applyBtn = new Button
{
    Content = "Apply",
    Tag = new object[] { instrumentName, leaderCb, followerCb },
    Margin = new Thickness(2)
};
applyBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
applyBtn.Click += OnRowApply;
Grid.SetColumn(applyBtn, 7);
grid.Children.Add(applyBtn);
```

**New handler:**
```csharp
private void OnRowApply(object sender, RoutedEventArgs e)
{
    var btn = sender as Button;
    if (btn?.Tag is not object[] tag) return;
    var instrName  = tag[0] as string;
    var leaderCb   = tag[1] as ComboBox;
    var followerCb = tag[2] as ComboBox;
    var leader     = leaderCb?.SelectedItem as Account;
    var follower   = followerCb?.SelectedItem as Account;
    if (leader == null || follower == null || instrName == null) return;
    _engine.AddRule(instrName, leader, new[] { follower });
}
```

CYC: 3 (1 base + 2 decision points). Within CYC <= 8.

---

## 10. Ticket Decomposition

### T1: CopyEngine.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Defects addressed:** D2a (ConcurrentBag), D2b (string-based AddRule overload)
**Lines changed:** 2 (field declaration line 21; insert new overload after line 96)

**Changes:**
1. Line 21: `private readonly List<CopyRule> _rules = new List<CopyRule>();`
   → `private readonly System.Collections.Concurrent.ConcurrentBag<CopyRule> _rules = new System.Collections.Concurrent.ConcurrentBag<CopyRule>();`
2. After line 96 (after existing `AddRule(CopyRule rule)` method): insert new `AddRule(string, Account, Account[])` overload.

**Method signatures to implement:**
- `internal void AddRule(string instrument, Account master, Account[] followers)` — CYC 1

**xUnit tests to write:**
- `[Fact] AddRule_StringOverload_AddsRuleToCollection()`
- `[Fact] AddRule_StringOverload_RuleIsFoundByGate2()`
- `[Fact] Rules_ConcurrentBag_ThreadSafeAddAndEnumerate()` — verifies no exception when Add and foreach run concurrently

**7-scan checklist:**
- SCAN-01: `grep "lock(" CopyEngine.cs` — 0 results
- SCAN-02: non-ASCII — 0 results
- SCAN-03: FontFamily — 0 results (N/A, no UI)
- SCAN-04: hex color — 0 results (N/A, no UI)
- SCAN-05: CreateOrder names — "PTT-Copy", "PTT-Trim", "PTT-Flatten" unchanged
- SCAN-06: DateTime.Now — 0 results
- SCAN-07: `\block\s*\(` — 0 results
- B2-SCAN-04: `grep "new List<CopyRule>"` — 0 results
- B2-SCAN-05: `grep "ConcurrentBag"` — exactly 1 result

---

### T2: TradeCopierWindow.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
**Defects addressed:** D1 (Subscribe lifecycle), D2d (row Apply wiring + followerCb ItemsSource), D4 (bare catch), D5 (border brush)
**Lines changed:** ~20 lines (7 individual line changes + new method + BuildRuleRow additions)

**Changes:**
1. `OnInitialize()` line 27: after `_engine.StatusUpdate += OnStatusUpdate;` add `_engine.Subscribe();`
2. `OnDestroyed()` line 33: after `_engine.StatusUpdate -= OnStatusUpdate;` add `_engine.Unsubscribe();`
3. `BuildRuleRow()` line 137-143: add `ItemsSource = Account.All` to `followerCb`
4. `BuildRuleRow()`: add 8th column definition + Apply button + `applyBtn.Click += OnRowApply`
5. Line 63: `"BorderBrush"` → `"NTBrushes.BorderBrush"`
6. Line 87: `"BorderBrush"` → `"NTBrushes.BorderBrush"`
7. Line 241: `catch {` → `catch (Exception) {`
8. New method: `private void OnRowApply(object sender, RoutedEventArgs e)` — CYC 3

**Method signatures to implement:**
- `private void OnRowApply(object sender, RoutedEventArgs e)` — CYC 3

**xUnit tests to write:**
- `[Fact] OnInitialize_CallsSubscribeOnEngine()`
- `[Fact] OnDestroyed_CallsUnsubscribeOnEngine()`
- `[Fact] OnRowApply_NullLeader_DoesNotCallAddRule()`
- `[Fact] OnRowApply_NullFollower_DoesNotCallAddRule()`
- `[Fact] OnRowApply_ValidSelections_CallsAddRule()`
- `[Fact] FindInstrument_ExceptionThrown_ReturnsCatchedException()` — verifies typed catch

**7-scan checklist:**
- SCAN-01: `grep "lock(" TradeCopierWindow.cs` — 0 results
- SCAN-02: non-ASCII — 0 results
- SCAN-03: FontFamily — 0 results
- SCAN-04: hex color — 0 results
- SCAN-05: no CreateOrder in this file — N/A
- SCAN-06: DateTime.Now — 0 results (DateTime.UtcNow used in OnStatusUpdate only)
- SCAN-07: `\block\s*\(` — 0 results
- B2-SCAN-03: `grep "_engine.Subscribe\|_engine.Unsubscribe"` — exactly 2 results
- B2-SCAN-06: `grep "catch {"` — 0 results
- B2-SCAN-07: `grep '"BorderBrush"'` (plain key) — 0 results

---

### T3: TradeCopierPanel.cs

**File:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
**Defects addressed:** D2c (rule wiring: field promotion + Apply button), D3 (IsEnabled = true)
**Lines changed:** ~20 lines

**Changes:**
1. Add class-level fields: `private ComboBox _leaderCombo;` and `private ComboBox _followersCombo;`
2. `BuildUI()`: replace local `leaderCombo` with `_leaderCombo` field; change population from foreach/Items.Add to `ItemsSource = Account.All`
3. `BuildUI()`: replace local `followersCombo` with `_followersCombo` field; change population to `ItemsSource = Account.All`
4. `BuildUI()`: insert Apply button + `applyBtn.Click += OnApplyRule` (after followersPanel is added to accountGrid, before separator)
5. Line 89: `IsEnabled = false` → `IsEnabled = true` (trimBtn)
6. Line 94: `IsEnabled = false` → `IsEnabled = true` (flattenBtn)
7. Line 99: `IsEnabled = false` → `IsEnabled = true` (cancelBtn)
8. New method: `private void OnApplyRule(object sender, RoutedEventArgs e)` — CYC 6

**Method signatures to implement:**
- `private void OnApplyRule(object sender, RoutedEventArgs e)` — CYC 6

**xUnit tests to write:**
- `[Fact] OnApplyRule_NullLeader_ShowsStatusText()`
- `[Fact] OnApplyRule_NullInstrument_ShowsStatusText()`
- `[Fact] OnApplyRule_ValidSelections_CallsAddRule()`
- `[Fact] TrimButton_IsEnabled_True()`
- `[Fact] FlattenButton_IsEnabled_True()`
- `[Fact] CancelButton_IsEnabled_True()`

**7-scan checklist:**
- SCAN-01: `grep "lock(" TradeCopierPanel.cs` — 0 results
- SCAN-02: non-ASCII — 0 results
- SCAN-03: FontFamily — 0 results
- SCAN-04: hex color — 0 results
- SCAN-05: no CreateOrder in this file — N/A
- SCAN-06: DateTime.Now — 0 results
- SCAN-07: `\block\s*\(` — 0 results
- B2-SCAN-02: `grep "_engine.Subscribe\|_engine.Unsubscribe" TradeCopierPanel.cs` — 0 results
- B2-SCAN-08: `grep "IsEnabled = false" TradeCopierPanel.cs` (button declarations) — 0 results

---

### T4: specs/002-trade-copier-spec.html

**File:** `specs/002-trade-copier-spec.html`
**Scope:** Surgical SD item updates reflecting B2 completed state. No structural redesign.

**10 SD items to update:**

| # | SD Item | Before | After |
|---|---------|--------|-------|
| 1 | `_rules` collection type | `List<CopyRule>` | `ConcurrentBag<CopyRule>` |
| 2 | `AddRule` API | single overload (CopyRule) | two overloads: internal (CopyRule) + internal (string, Account, Account[]) |
| 3 | Subscribe lifecycle | Window must call Subscribe/Unsubscribe (B2 fix) | confirmed: Window OnInitialize/OnDestroyed own the lifecycle |
| 4 | Panel ComboBox population | iterates Account.All, adds Name strings | `ItemsSource = Account.All` (Account object binding) |
| 5 | Panel rule apply | no Apply button (B1 gap) | "Apply Rule" button + `OnApplyRule()` handler |
| 6 | Window follower ComboBox | no ItemsSource (B1 gap) | `ItemsSource = Account.All` |
| 7 | Window row Apply | no per-row Apply button (B1 gap) | column 7 "Apply" button + `OnRowApply()` handler |
| 8 | Panel action buttons | `IsEnabled = false` (B1 defect) | `IsEnabled = true`; engine logs "flat skip" if no position |
| 9 | Bare catch in FindInstrument | `catch {}` | `catch (Exception) {}` per JS-001 |
| 10 | Border brush resource key | `"BorderBrush"` (wrong) | `"NTBrushes.BorderBrush"` (correct NT8 resource key) |

**No test required for T4** (HTML spec is documentation, not compiled code).

---

## 11. CYC Budget Summary

| Method | File | CYC | Limit |
|--------|------|-----|-------|
| `AddRule(string, Account, Account[])` | CopyEngine.cs | 1 | 8 |
| `OnApplyRule()` | TradeCopierPanel.cs | 6 | 8 |
| `OnRowApply()` | TradeCopierWindow.cs | 3 | 8 |
| All gate chain methods (unchanged) | CopyEngine.cs | 1–7 | 8 |

All methods within Jane Street strict standard (CYC <= 8). PASS.

---

## 12. Concurrency Contract Summary

| Concern | Mechanism | Lock? |
|---------|-----------|-------|
| `_isCopyEnabled` reads/writes | `volatile bool` | No |
| `_dedupCache` reads/writes | `ConcurrentDictionary<string, long>` | No |
| `_rules` reads/writes | `ConcurrentBag<CopyRule>` | No |
| `StatusUpdate` event firing | `?.Invoke` (atomic delegate read) | No |
| UI thread → WPF controls | All button handlers on UI thread natively | No |
| NT thread → UI controls | `Dispatcher.InvokeAsync` in OnStatusUpdate | No |
| `Account.All.OrderUpdate` subscribe/unsubscribe | NT event infrastructure is thread-safe | No |

**Zero `lock()` statements. Full B2 compliance.**
