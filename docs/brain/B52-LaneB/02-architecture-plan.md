# B52-LaneB Architecture Plan — knowledge-doc-weak-refs
**Status**: REVIEW_PASS pending  
**Epic**: PTT-COPIER-B52 Lane B  
**Phase**: 1 (Architecture)  
**Author**: ptt-architect  

---

## 1. Overview

This lane closes two deferred work items from prior blocks:

- **DW-B50C-02**: Document the removal of `NinjaTrader.Client.dll` from
  `PropTraderTools.csproj` (done in B50-LaneC to resolve CS0433 Globals ambiguity)
  in `docs/standards/NT8_ADDON_KNOWLEDGE.md` so future engineers understand why
  the DLL is absent and must not be re-added.

- **DW-B50-02**: Replace the strong-reference `List<ComboBox>` field `_atmComboRefs`
  in `TradeCopierPanel.cs` with `List<WeakReference<ComboBox>>`, preventing WPF
  ComboBox controls from being pinned in memory after their logical parent is
  garbage-collected. Updates field declaration, registration in
  `OnFollowerAtmTemplateComboLoaded`, and iteration/pruning in
  `UpdateAtmComboVisibility`.

No new public API surface is introduced. No new xUnit tests are required (UI-thread
helper with no observable side-effects on observable state, covered by existing
integration paths).

---

## 2. DW-B50C-02 — NT8_ADDON_KNOWLEDGE.md Documentation Entry

The following block must be **appended** verbatim to
`docs/standards/NT8_ADDON_KNOWLEDGE.md`:

```
## B52 Discoveries (2026-08-08)

### NinjaTrader.Client.dll Removed from csproj — CS0433 Globals Ambiguity (B50-LaneC)

**Assembly removed**: `NinjaTrader.Client.dll`

**Why removed**: The presence of `NinjaTrader.Client.dll` alongside
`NinjaTrader.Custom.dll` caused CS0433:

  > error CS0433: The type 'Globals' exists in both
  > 'NinjaTrader.Client, Version=8.0.x.x' and
  > 'NinjaTrader.Custom, Version=8.0.x.x'

This ambiguity prevented compilation of `PropTraderTools.csproj`.

**Which assembly provides the same types**: `NinjaTrader.Core.dll` (already
referenced) provides `Account`, `Order`, `Instrument`, and all other types that
`NinjaTrader.Client.dll` exposed. `NinjaTrader.Client.dll` is a legacy namespace-alias
DLL — every type it exposes is duplicated in the core SDK assemblies
(`NinjaTrader.Core.dll` and `NinjaTrader.Custom.dll`).

**Recommendation**: Do NOT add `NinjaTrader.Client.dll` back in future blocks.
All required NT8 types are accessible through `NinjaTrader.Core.dll` and
`NinjaTrader.Custom.dll`.

**Status**: DW-B50C-02 CLOSED
```

Placement: append after the last existing `## BXX` section in the file (i.e., after
the current last top-level heading at the bottom of the document).

---

## 3. DW-B50-02 — WeakReference Change Plan

All three changes are in a single file:
`src/PropTraderTools/TradeCopierPanel.cs`

### 3a. Field Declaration Change

**File**: `src/PropTraderTools/TradeCopierPanel.cs`, lines 199-202

Old:
```csharp
        // B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
        // Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
        private readonly System.Collections.Generic.List<System.Windows.Controls.ComboBox> _atmComboRefs
            = new System.Collections.Generic.List<System.Windows.Controls.ComboBox>();
```

New:
```csharp
        // B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
        // B52: WeakReference<ComboBox> prevents pinning dead WPF controls in memory.
        // Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
        private readonly System.Collections.Generic.List<WeakReference<System.Windows.Controls.ComboBox>> _atmComboRefs
            = new System.Collections.Generic.List<WeakReference<System.Windows.Controls.ComboBox>>();
```

### 3b. OnFollowerAtmTemplateComboLoaded Registration Change

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (within `OnFollowerAtmTemplateComboLoaded`)

The `if (!_atmComboRefs.Contains(cb))` block (which starts at the third conditional
inside the method, after the null guard and idempotency guard) is replaced entirely.

Old block:
```csharp
            if (!_atmComboRefs.Contains(cb))
            {
                _atmComboRefs.Add(cb);                            // B50: track combo for Clone visibility toggle
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
```

New block:
```csharp
            bool alreadyTracked = false;
            foreach (var wr in _atmComboRefs)
                if (wr.TryGetTarget(out var existing) && existing == cb) { alreadyTracked = true; break; }
            if (!alreadyTracked)
            {
                _atmComboRefs.Add(new WeakReference<ComboBox>(cb)); // B52: weak ref -- no memory pin
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
```

No other lines in `OnFollowerAtmTemplateComboLoaded` change.

### 3c. UpdateAtmComboVisibility Iteration Change

**File**: `src/PropTraderTools/TradeCopierPanel.cs`, lines 1479-1489

Old:
```csharp
        // B50: UpdateAtmComboVisibility -- sets Visibility on all tracked per-follower ATM combos.
        // CYC=2: (1) foreach loop body, (2) null guard.
        // JS-021: no lock. UI-thread-only -- called only from Click handlers (UI thread).
        private void UpdateAtmComboVisibility(Visibility v)
        {
            foreach (var cb in _atmComboRefs)   // branch (1)
            {
                if (cb != null)                 // branch (2)
                    cb.Visibility = v;
            }
        }
```

New:
```csharp
        // B52: UpdateAtmComboVisibility -- sets Visibility on live weak-referenced ATM combos.
        // Iterates backwards to prune dead refs in-place without index shifting.
        // CYC=4: (1) for condition, (2) TryGetTarget true, (3) TryGetTarget false (prune), (4) base.
        // JS-021: no lock. UI-thread-only -- called only from Click handlers (UI thread).
        private void UpdateAtmComboVisibility(Visibility v)
        {
            for (int i = _atmComboRefs.Count - 1; i >= 0; i--)   // branch (1)
            {
                if (_atmComboRefs[i].TryGetTarget(out var cb))    // branch (2)
                    cb.Visibility = v;                            // alive: apply
                else
                    _atmComboRefs.RemoveAt(i);                    // branch (3): dead ref -- prune
            }
        }
```

### 3d. CYC Impact Table

> **NT8 compatibility note**: `WeakReference<T>` was introduced in .NET 4.5 and is available in .NET Framework 4.8 — safe for NT8 use without compiler rule violation.


| Method | CYC Before | CYC After | <= 8? |
|---|---|---|---|
| `UpdateAtmComboVisibility` | 2 | 4 | YES |

McCabe derivation for new `UpdateAtmComboVisibility`:  
1 (base) + 1 (for-loop condition) + 1 (TryGetTarget true path) + 1 (TryGetTarget false path) = **4**

---

## 4. No Public API Change Assertion

Both changes are purely internal:

- **DW-B50C-02** is a documentation-only change to a markdown file. Zero C# surface
  is touched.
- **DW-B50-02** modifies a `private readonly` field and two `private` methods. No
  public, internal, or protected API changes. No interface signatures change.
- `UpdateAtmComboVisibility` and `OnFollowerAtmTemplateComboLoaded` are both
  `private`. Their observable behavior to callers is identical: visibility is set on
  alive ComboBox controls; the only new behavior is that dead controls are silently
  pruned from the list (a memory-hygiene improvement with no functional impact).
- No new xUnit `[Fact]` tests are required. The methods are UI-thread-only WPF
  event handlers. The functional path (visibility toggling) is already exercised
  by any integration test that exercises Clone mode. The WeakReference change is a
  GC-hygiene fix, not a behavioral change.

---

## 5. Scan Summary

### Ticket 1 — Docs-Only: NT8_ADDON_KNOWLEDGE.md update (DW-B50C-02)

| Scan | Check | Pass Condition |
|---|---|---|
| SCAN-08 | `grep "NinjaTrader.Client" docs/standards/NT8_ADDON_KNOWLEDGE.md` | At least one match (entry was appended) |

### Ticket 2 — Src Change: WeakReference conversion in TradeCopierPanel.cs (DW-B50-02)

| Scan | Check | Pass Condition |
|---|---|---|
| SCAN-01 | `grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs` | Zero matches (event handlers exempt; none in scope here) |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Zero errors, zero warnings (new) |
| SCAN-06 | CYC audit: `UpdateAtmComboVisibility` branch count | CYC = 4 (<= 8 PASS) |
| SCAN-07 | `powershell -File scripts\verify_links.ps1` | Zero broken links |
