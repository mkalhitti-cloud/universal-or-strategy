# B52-LaneB Tickets — knowledge-doc-weak-refs
**Epic**: PTT-COPIER-B52 Lane B
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Source plan**: docs/brain/B52-LaneB/02-architecture-plan.md (REVIEW_PASS)

---

## T1 — Add NT8_ADDON_KNOWLEDGE.md entry for NinjaTrader.Client.dll removal

**Spec req closed**: DW-B50C-02
**File modified**: `docs/standards/NT8_ADDON_KNOWLEDGE.md`
**Type**: Docs-only — zero .cs files touched
**Completion artifact**: `docs/brain/B52-LaneB/ticket-1-completion.md`

---

### Implementation

Append the following block **verbatim** to the end of
`docs/standards/NT8_ADDON_KNOWLEDGE.md` (after the current last `## BXX` section):

```
## B52 Discoveries (2026-08-08)

### NinjaTrader.Client.dll Removed from csproj — CS0433 Globals Ambiguity (B50-LaneC)

**Block removed**: PTT-COPIER-B50-LaneC
**Status**: DW-B50C-02 CLOSED

**What happened**: `NinjaTrader.Client.dll` was referenced in `PropTraderTools.csproj` to
provide NT8 client-layer types. Removing it resolved a CS0433 build error:

```
CS0433: The type 'Globals' exists in both 'NinjaTrader.Client, Version=...' and
        'NinjaTrader.Custom, Version=...'
```

**Root cause**: `NinjaTrader.Client.dll` is a legacy namespace alias DLL. Every type it
exposes is also present in `NinjaTrader.Core.dll` and/or `NinjaTrader.Custom.dll`. When
`NinjaTrader.Custom.dll` was added to the csproj (B42, for `AtmStrategyCreate` and the
`Indicator`/`Strategy` base classes), the `Globals` type ambiguity became a hard build error
in the Linting project.

**Replacement assembly**: All `Account`, `Order`, `Instrument`, `Position`, `OrderType`,
`OrderEntry`, `TimeInForce`, and `Globals` types are fully available through
`NinjaTrader.Core.dll` (already referenced). No functionality was lost.

**Rule**: Do NOT add `NinjaTrader.Client.dll` back to `PropTraderTools.csproj`. It
re-introduces the CS0433 Globals ambiguity with `NinjaTrader.Custom.dll`. The three
current NT8 DLL references are sufficient:
- `NinjaTrader.Core.dll`   — Account, Order, Instrument, Position, OrderType, Globals, etc.
- `NinjaTrader.Gui.dll`    — Chart, ChartTrader, ChartControl, AddOnBase, NTWindow, etc.
- `NinjaTrader.Custom.dll` — Strategy, Indicator, AtmStrategyCreate, Calculate enum, etc.

**Scan to verify this entry exists after the ticket is applied**:
```
grep -n "NinjaTrader.Client" docs/standards/NT8_ADDON_KNOWLEDGE.md
```
Expected: at least 4 hits (the entry lines referencing "NinjaTrader.Client.dll").
```

---

### 7-Scan Checklist (T1)

> Scans 1-7 are not applicable — no `.cs` files are touched.
> Only SCAN-08 (docs-content verification) applies.

| ID | Command | Expected Result | PASS? |
|----|---------|-----------------|-------|
| SCAN-08 | `grep -n "NinjaTrader.Client" docs/standards/NT8_ADDON_KNOWLEDGE.md` | >= 1 hit (entry appended) | [ ] |

**PASS criteria**: SCAN-08 returns at least one match containing the string
`NinjaTrader.Client.dll`.

---

## T2 — Replace _atmComboRefs hard-refs with WeakReference<ComboBox> in TradeCopierPanel.cs

**Spec req closed**: DW-B50-02
**File modified**: `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace:
`c:/WSGTA/universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs`)
**Type**: Surgical src edit — three hunks + build-tag update
**Completion artifact**: `docs/brain/B52-LaneB/ticket-2-completion.md`

---

### Implementation

Apply the following three edits in order. Do not change any other lines.

---

#### EDIT 1 — Field declaration (approx. lines 199-202)

**Find this exact block:**
```csharp
        // B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
        // Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
        private readonly System.Collections.Generic.List<System.Windows.Controls.ComboBox> _atmComboRefs
            = new System.Collections.Generic.List<System.Windows.Controls.ComboBox>();
```

**Replace with:**
```csharp
        // B50: Tracks per-follower ATM ComboBox refs for Clone mode visibility toggle.
        // B52: WeakReference<ComboBox> prevents detached combo accumulation on panel rebuild.
        // Populated in OnFollowerAtmTemplateComboLoaded. UI-thread-only -- no volatile.
        private readonly System.Collections.Generic.List<WeakReference<System.Windows.Controls.ComboBox>> _atmComboRefs
            = new System.Collections.Generic.List<WeakReference<System.Windows.Controls.ComboBox>>();
```

---

#### EDIT 2 — OnFollowerAtmTemplateComboLoaded registration block (approx. lines 1974-1979)

**Find this exact block:**
```csharp
            if (!_atmComboRefs.Contains(cb))
            {
                _atmComboRefs.Add(cb);                            // B50: track combo for Clone visibility toggle
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
```

**Replace with:**
```csharp
            bool alreadyTracked = false;
            foreach (var wr in _atmComboRefs)
                if (wr.TryGetTarget(out var existing) && existing == cb) { alreadyTracked = true; break; }
            if (!alreadyTracked)
            {
                _atmComboRefs.Add(new WeakReference<ComboBox>(cb)); // B52: WeakReference prevents detached accumulation
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
```

---

#### EDIT 3 — UpdateAtmComboVisibility method body (approx. lines 1479-1489)

**Find this exact block:**
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

**Replace with:**
```csharp
        // B52: UpdateAtmComboVisibility -- sets Visibility on all tracked per-follower ATM combos.
        // B52: WeakReference<ComboBox> prunes dead refs in the same pass (prune-on-iterate pattern).
        // CYC=4: (1) for-loop body, (2) TryGetTarget true (apply), (3) TryGetTarget false (prune), (4) base.
        // JS-021: no lock. UI-thread-only -- called only from Click handlers (UI thread).
        private void UpdateAtmComboVisibility(Visibility v)
        {
            for (int i = _atmComboRefs.Count - 1; i >= 0; i--)   // branch (1)
            {
                if (_atmComboRefs[i].TryGetTarget(out var cb))    // branch (2)
                    cb.Visibility = v;
                else
                    _atmComboRefs.RemoveAt(i);                    // branch (3): prune dead ref
            }
        }
```

---

#### EDIT 4 — Build tag update

**Find** the current `PttBuild.Tag` assignment line (set by B52-LaneA).
It will contain the text `PTT-COPIER B52`.

**Replace** the tag string with:
```csharp
PttBuild.Tag = "PTT-COPIER B52 | knowledge-doc-weak-refs | 2026-08-08";
```

---

### CYC Impact

| Method | CYC Before | CYC After | <= 8? |
|--------|-----------|-----------|-------|
| `UpdateAtmComboVisibility` | 2 | 4 | YES |

McCabe derivation (new):
1 (base) + 1 (for-loop condition) + 1 (TryGetTarget true) + 1 (TryGetTarget false/prune) = **4**

`WeakReference<T>` is available in .NET 4.8 (NT8 host). No NT8 compiler rule
violations — verified against `docs/standards/NT8_COMPILER_RULES.md`.

---

### No New xUnit Tests Required

`UpdateAtmComboVisibility` and `OnFollowerAtmTemplateComboLoaded` are `private`,
UI-thread-only WPF event handlers. The WeakReference change is a GC-hygiene fix
with identical observable behavior to callers (alive combos still receive the
visibility update). No public or internal API surface changed.

---

### 7-Scan Checklist (T2)

| ID | Command | Expected Result | PASS? |
|----|---------|-----------------|-------|
| SCAN-01 | `grep -r "lock(" c:/WSGTA/universal-or-strategy/src/ --include="*.cs"` | 0 hits | [ ] |
| SCAN-02 | `grep -rn "async void " c:/WSGTA/universal-or-strategy/src/ --include="*.cs"` | 0 non-event-handler hits | [ ] |
| SCAN-05 | `dotnet build c:/WSGTA/universal-or-strategy/src/PropTraderTools/PropTraderTools.csproj` | 0 errors | [ ] |
| SCAN-06 | Branch count audit on `UpdateAtmComboVisibility` | CYC = 4 (<= 8) | [ ] |
| SCAN-07 | `powershell -File c:/WSGTA/universal-or-strategy/scripts/verify_links.ps1` | DESYNC=0 MISSING=0 | [ ] |

**PASS criteria**: All five scans pass with the stated expected results before the
completion artifact is written.

---

## Summary

| Ticket | Spec Req | File(s) | Type | Scans |
|--------|----------|---------|------|-------|
| T1 | DW-B50C-02 | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | Docs append | SCAN-08 |
| T2 | DW-B50-02 | `src/PropTraderTools/TradeCopierPanel.cs` | 3 surgical edits + tag | SCAN-01,02,05,06,07 |
