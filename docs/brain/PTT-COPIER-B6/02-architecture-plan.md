# PTT-COPIER-B6 Architecture Plan
**Status:** REVIEW_PASS
**Block:** B6 (final backlog resolution block)
**Architect:** PTT Architect
**Produced:** 2026-07-06
**Based on:** 8 sequential thoughts (all `nextThoughtNeeded = false`)

---

## A. Executive Summary

Block 6 is the **backlog-closure block**. It has no new user-visible features beyond
what the B5 deferred ledger specifies. The two OPEN items from B5 are both addressed
in this block:

| Item | Decision | Rationale |
|------|----------|-----------|
| DW-B5-03 Rule persistence across sessions | **ADDRESS IN B6** | Last meaningful UX gap — rules are wiped on every NT restart without this. Bounded, safe, lifecycle-only. |
| DW-B5-04 Spec HTML update for B3/B4/B5 | **ADDRESS IN B6** | B6 is the final (or near-final) block; spec should be current. Zero code risk. |

**New scope beyond backlog:** None. B6 is purely backlog-driven.

**Ticket count:** 4 (T1: engine persistence logic, T2: window lifecycle hooks,
T3: xUnit tests, T4: spec HTML doc update)

**Additive-only mandate:** Every change appends new code. Zero deletions.
Zero modifications to existing logic paths.

---

## B. Architect Thought 1 — Backlog Disposition

### DW-B5-03: Rule Persistence Across Sessions

**Decision: ADDRESS IN B6.**

**Justification:**
Without persistence, every NinjaTrader restart clears the user's configured copy rules.
The user must re-enter source account, follower accounts, lot ratio, tick offset, stop
buffer, and enabled state each session. This is the most significant remaining UX gap in
the add-on. It directly degrades the value proposition of a production-ready trade copier.

The implementation is bounded:
- Serialize on `TradeCopierWindow.OnDestroyed()` (NT shutdown path, main thread)
- Deserialize on `TradeCopierWindow.OnInitialize()` (NT startup path, main thread)
- No concurrency concern at lifecycle boundaries
- No lock() required
- IO is synchronous (small XML file, acceptable at startup/shutdown)
- Zero .csproj changes (uses `System.Xml.Serialization.XmlSerializer`, built into
  .NET Framework 4.8 which NT8 targets)
- CYC of all new methods stays <= 8

P3 priority is correct (it is not blocking existing functionality), but addressing it
now avoids carrying the item indefinitely. B6 closes the ledger cleanly.

### DW-B5-04: Spec HTML Update for B3/B4/B5 Changes

**Decision: ADDRESS IN B6.**

**Justification:**
B6 is the last planned block. The spec (`specs/002-trade-copier-spec.html`) has not been
updated since before B3. It is missing documentation for:
- BreakEven button (B3/B4)
- Shift+B keyboard shortcut (B5)
- ListBox/ScrollViewer follower multi-select (B5)
- Stop buffer field (B5)

Leaving the spec permanently out of sync with the implementation creates a maintenance
hazard for any future engineer who reads the spec as ground truth. This is a documentation-
only change with zero code risk. Ticket T4 covers it. No Wave workspace files are touched.

### New Scope for B6 Beyond Backlog Items

**None.** B6 scope is exactly the two deferred items plus their test coverage. No features
are added beyond DW-B5-03 and DW-B5-04.

---

## C. Scope for B6

| # | Feature | Files Changed | Ticket |
|---|---------|--------------|--------|
| 1 | Rule persistence — serialize CopyRule list on shutdown | CopyEngine.cs, TradeCopierWindow.cs | T1, T2 |
| 2 | Rule persistence — deserialize CopyRule list on startup | CopyEngine.cs, TradeCopierWindow.cs | T1, T2 |
| 3 | xUnit tests for persistence | CopyEngineTests.cs | T3 |
| 4 | Spec HTML update (B3+B4+B5 features documented) | specs/002-trade-copier-spec.html | T4 |

**TradeCopierPanel.cs: ZERO changes.** Persistence lifecycle belongs exclusively to
TradeCopierWindow (the NT Add-On host window). TradeCopierPanel is a view-only control
instantiated inside TradeCopierWindow; it has no NT lifecycle methods of its own.

---

## D. File-Level Change Plan

### D.1 CopyEngine.cs — Wave workspace `src/PropTraderTools/CopyEngine.cs`

**Current:** 424 lines (B4-complete, zero changes in B5)
**Expected after B6:** ~478 lines (+54 lines additive)

Additive additions only (no deletions, no modifications to existing code):

| Addition | Description |
|----------|-------------|
| `private sealed class CopyRuleDto` | Serialization DTO. Public get/set properties, parameterless ctor, `[Serializable]`. Mirrors the fields of `CopyRule`. |
| `private sealed class CopyRulesContainer` | XmlSerializer root element wrapper. Contains `List<CopyRuleDto> Rules`. |
| `private static string GetPersistencePath()` | Computes full XML file path under `NinjaTrader.Core.Globals.UserDataDir`. |
| `private static CopyRuleDto RuleToDto(CopyRule rule)` | Converts domain `CopyRule` struct to serialization DTO. |
| `private static CopyRule DtoToRule(CopyRuleDto dto)` | Converts DTO back to domain `CopyRule` struct via existing `CopyRule.Create()` factory. |
| `public void SaveRules()` | Snapshots `_rules.ToArray()`, converts to DTOs, serializes via `XmlSerializer`, writes file. |
| `public void LoadRules()` | If file exists: reads XML, deserializes, calls `_rules.Add()` for each rule. |

**Important constraint on `_rules` field:**
`LoadRules()` populates the bag by calling `_rules.Add(item)` for each deserialized rule,
not by reassigning the field. This preserves compatibility with a `readonly` field
declaration (if present in B1) without modifying existing declarations. `ConcurrentBag<T>`
has no `Clear()` — `OnInitialize` is called once at startup when the bag is empty, so
iterative Add is safe.

**Serialization library choice: `System.Xml.Serialization.XmlSerializer`**
Rationale: Built into .NET Framework 4.8 (NT8 runtime). Zero new NuGet packages. Zero
`.csproj` changes. `System.Text.Json` is NOT included in .NET Framework 4.8 without a
NuGet package. Newtonsoft.Json, while available in NT8's distribution, requires an
explicit assembly reference in the project file. `XmlSerializer` is the zero-dependency
path.

File path: `NinjaTrader.Core.Globals.UserDataDir + "PropTraderTools\copy_rules.xml"`

### D.2 TradeCopierWindow.cs — Wave workspace `src/PropTraderTools/TradeCopierWindow.cs`

**Current:** 462 lines (B5-complete)
**Expected after B6:** ~470 lines (+8 lines additive)

| Lifecycle method | Addition |
|-----------------|----------|
| `OnInitialize()` | Append `CopyEngine.Instance.LoadRules();` then call existing rule-refresh UI helper (name to be confirmed by reading B5 source). |
| `OnDestroyed()` | Prepend `CopyEngine.Instance.SaveRules();` as first statement (before any UI cleanup). |

No new UI controls. No new event handlers. No new Dispatcher.InvokeAsync calls.

### D.3 CopyEngineTests.cs — Wave workspace `src/PropTraderTools.Tests/CopyEngineTests.cs`

**Current:** 264 lines, 19 `[Fact]` tests (B5-complete)
**Expected after B6:** ~310 lines, 22 `[Fact]` tests (+3 new facts, +46 lines additive)

3 new test methods (all xUnit `[Fact]`):

| Test | Description |
|------|-------------|
| `SaveRules_WritesXmlFile_WhenRulesExist()` | Arrange: add 1 rule to engine; call SaveRules(path). Assert: file exists and deserializes back with correct field values. |
| `LoadRules_PopulatesRules_WhenFileExists()` | Arrange: write a valid XML file with 2 rules to temp path; call LoadRules(path). Assert: engine has 2 rules with correct values. |
| `LoadRules_DoesNotThrow_WhenFileAbsent()` | Arrange: non-existent path. Assert: LoadRules returns without exception, engine has 0 rules. |

Note on testability: `SaveRules()` and `LoadRules()` will accept an **optional `string path`
parameter** (defaulting to `GetPersistencePath()`) so tests can inject a temp directory
path without mocking the static `Globals.UserDataDir`. This is a standard seam for I/O
testability. The default parameter approach keeps the public API clean while making tests
self-contained.

### D.4 specs/002-trade-copier-spec.html — Director workspace

**Ticket T4 only. No Wave workspace files involved.**

Sections to add/update:
1. BreakEven button (B3): description of the UI control and behavior
2. Shift+B keyboard shortcut (B4/B5): document the `KeyBinding` and `OnWindowBreakEven` handler
3. Follower multi-select (B5): document `ListBox` + `ScrollViewer` replacing single-select `ComboBox`
4. Stop buffer field (B5): document the buffer tick-count field and its role in `StopBuffer` copy logic
5. Correct any reference to "JSON" in the B6 phase-detail section to read "XML (copy_rules.xml)" — the plan implements `XmlSerializer`, not JSON

---

## E. Method Signatures for All New / Modified Methods

All new methods are in `src/PropTraderTools/CopyEngine.cs` unless noted.

```csharp
// ── Persistence path ──────────────────────────────────────────────────────────

/// <summary>
/// Returns the full path to the persistence XML file.
/// Caller is responsible for ensuring the directory exists before writing.
/// CYC = 1
/// </summary>
private static string GetPersistencePath(
    string? overridePath = null)

// ── DTO classes (nested inside CopyEngine) ───────────────────────────────────

[Serializable]
private sealed class CopyRuleDto
{
    public string SourceAccountName { get; set; }   // = string.Empty
    public string[] FollowerAccountNames { get; set; }  // = Array.Empty<string>()
    public double LotRatio { get; set; }
    public int TickOffset { get; set; }
    public int StopBuffer { get; set; }
    public bool IsEnabled { get; set; }
}

[Serializable]
private sealed class CopyRulesContainer
{
    public List<CopyRuleDto> Rules { get; set; }    // = new List<CopyRuleDto>()
}

// ── Conversion helpers ────────────────────────────────────────────────────────

/// <summary>
/// Converts a CopyRule domain value to a serialization DTO.
/// Engineer: verify field names match actual CopyRule properties from B1 source.
/// CYC = 1
/// </summary>
private static CopyRuleDto RuleToDto(CopyRule rule)

/// <summary>
/// Converts a deserialized DTO back to a CopyRule domain value.
/// Calls the existing CopyRule.Create() factory (JS-010).
/// CYC = 1
/// </summary>
private static CopyRule DtoToRule(CopyRuleDto dto)

// ── Public persistence API ────────────────────────────────────────────────────

/// <summary>
/// Serializes the current rule set to an XML file.
/// Called from TradeCopierWindow.OnDestroyed() on the NT main thread.
/// Swallows IOException to prevent NT shutdown crash on I/O failure.
/// CYC = 2 (try/catch = 1 branch)
/// </summary>
public void SaveRules(string? overridePath = null)

/// <summary>
/// Deserializes rules from an XML file and adds them to _rules via ConcurrentBag.Add().
/// Called from TradeCopierWindow.OnInitialize() on the NT main thread.
/// No-op if the file does not exist.
/// CYC = 3 (File.Exists guard + try/catch + foreach)
/// </summary>
public void LoadRules(string? overridePath = null)
```

**TradeCopierWindow.cs modifications (additive, existing method signatures unchanged):**

```csharp
// OnInitialize() — append these lines (exact insertion point after existing init logic):
CopyEngine.Instance.LoadRules();
// [call existing rule-list UI refresh method — engineer confirms name from B5 source]

// OnDestroyed() — prepend this line before existing cleanup:
CopyEngine.Instance.SaveRules();
```

**CopyEngineTests.cs new test signatures:**

```csharp
[Fact]
public void SaveRules_WritesXmlFile_WhenRulesExist()

[Fact]
public void LoadRules_PopulatesRules_WhenFileExists()

[Fact]
public void LoadRules_DoesNotThrow_WhenFileAbsent()
```

---

## F. xUnit Test Plan

### Test Environment Setup

Tests use a temp directory (`Path.GetTempPath() + Guid.NewGuid().ToString()`) to avoid
touching `NinjaTrader.Core.Globals.UserDataDir` in the test environment.
The `overridePath` parameter on `SaveRules()` and `LoadRules()` injects this seam.
Temp directory is created in test Arrange and deleted in test Dispose/finally.

### Test Coverage Matrix

| Test ID | Method Under Test | Scenario | Assert |
|---------|-------------------|----------|--------|
| T3-01 | `SaveRules()` | 1 rule in engine → save | XML file exists at path; file non-empty; re-deserialize yields correct `SourceAccountName` |
| T3-02 | `LoadRules()` | Valid XML with 2 rules → load | `CopyEngine.Instance` has 2 rules (via existing `GetRules()` or `_rules.Count`); field values match |
| T3-03 | `LoadRules()` | File does not exist | No exception thrown; rule count unchanged (0) |

### Test Isolation

- Each test uses a fresh `CopyEngine.Instance` with rules cleared via the existing
  test-only Reset pathway (if one exists from B1–B5) OR each test writes/reads its own
  temp file with no engine state contamination.
- Engineer must verify whether `CopyEngine` exposes a `ClearRules()` or Reset mechanism
  for test setup. If not, T3-02 must be sequenced after a clean state assertion.

### xUnit Framework Compliance (per V12 mandate)

- Framework: `[Fact]` attributes only. Never `[Test]`, never NUnit, never MSTest.
- Assertions: `Assert.True`, `Assert.Equal`, `Assert.NotNull`. No FluentAssertions unless
  already in use in B5 tests.
- Total tests after B6: **22 `[Fact]` methods** (19 existing + 3 new)

---

## G. 7-Scan Checklist

All 7 scans must return **0 results** when run in the Wave workspace on
`src/PropTraderTools/` after B6 implementation.

| Scan ID | Pattern | B6 New Code — Verdict |
|---------|---------|----------------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 — no lock() in any new method |
| SCAN-02 | Non-ASCII chars in .cs files | 0 — all identifiers, strings, XML element names are ASCII |
| SCAN-03 | `Select-String -Pattern "FontFamily"` | 0 — no new UI controls added |
| SCAN-04 | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | 0 — no hardcoded hex colors |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 — no new `CreateOrder` calls |
| SCAN-06 | `Select-String -Pattern "DateTime\.Now[^U]"` | 0 — no `DateTime.Now`; no timestamps used in persistence |
| SCAN-07 | `Select-String -Pattern "\block\s*\("` | 0 — belt-and-suspenders lock check; same as SCAN-01 |

**Note on SCAN-07:** The protocol description states "sealed class TradeCopierWindow → 0"
as the check content, but the PTT_WORKSPACE_PROTOCOL.md scan table defines SCAN-07 as
the `\block\s*\(` regex (belt-and-suspenders lock check). The architecture brief's SCAN-07
label "sealed class TradeCopierWindow → 0" is interpreted as: the class declaration must
NOT contain the `sealed` keyword — which is already satisfied by B1 and is unchanged in B6.
Both interpretations pass.

---

## H. Ticket Breakdown

### T1 — CopyEngine Persistence Logic
**File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Type:** Source code, additive-only
**Methods to add:**
- `private sealed class CopyRuleDto` (nested class)
- `private sealed class CopyRulesContainer` (nested class)
- `private static string GetPersistencePath(string? overridePath = null)`
- `private static CopyRuleDto RuleToDto(CopyRule rule)`
- `private static CopyRule DtoToRule(CopyRuleDto dto)`
- `public void SaveRules(string? overridePath = null)`
- `public void LoadRules(string? overridePath = null)`

**Pre-work:** Engineer reads existing `CopyRule` struct field/property names from B1 source
to correctly populate `CopyRuleDto`. If `CopyRule.Create()` factory exists, confirm its
signature. If `_rules` is `readonly`, confirm `ConcurrentBag.Add()` iterative approach works.

**Scans:** SCAN-01 through SCAN-07 must pass on CopyEngine.cs after change.

**DoD:**
- `CopyEngine.Instance.SaveRules()` writes a valid XML file
- `CopyEngine.Instance.LoadRules()` reads the file and populates _rules
- No lock() anywhere in new code
- CYC of each new method <= 8

---

### T2 — TradeCopierWindow Lifecycle Hooks
**File:** `src/PropTraderTools/TradeCopierWindow.cs` (Wave workspace)
**Type:** Source code, additive-only (~8 lines)
**Changes:**
- `OnInitialize()`: append `CopyEngine.Instance.LoadRules();` + UI rule list refresh call
- `OnDestroyed()`: prepend `CopyEngine.Instance.SaveRules();`

**Pre-work:** Engineer reads `OnInitialize()` and `OnDestroyed()` bodies from B5 source to
confirm insertion points and identify the existing rule-list UI refresh method name.

**Dependency:** T1 must be complete before T2 (requires `LoadRules()`/`SaveRules()` to exist).

**Scans:** SCAN-01 through SCAN-07 must pass on TradeCopierWindow.cs after change.

**DoD:**
- `OnInitialize()` calls `LoadRules()` after engine setup, before returning
- `OnDestroyed()` calls `SaveRules()` before any cleanup
- Existing CYC of both methods remains <= 8 after additions
- No async/await introduced

---

### T3 — xUnit Persistence Tests
**File:** `src/PropTraderTools.Tests/CopyEngineTests.cs` (Wave workspace)
**Type:** Test code, additive-only
**Tests to add:**
1. `SaveRules_WritesXmlFile_WhenRulesExist()` — `[Fact]`
2. `LoadRules_PopulatesRules_WhenFileExists()` — `[Fact]`
3. `LoadRules_DoesNotThrow_WhenFileAbsent()` — `[Fact]`

**Dependency:** T1 must be complete.

**DoD:**
- All 3 new `[Fact]` tests pass (`dotnet test`)
- All 19 existing tests still pass (no regressions)
- Total test count = 22
- No NUnit or MSTest references added

---

### T4 — Spec HTML Update (Documentation Only)
**File:** `specs/002-trade-copier-spec.html` (Director workspace)
**Type:** Documentation, no code
**Sections to update:**
1. Add Break-Even button section (B3 feature): UI placement, activation logic
2. Add Shift+B keyboard shortcut section (B5 feature): KeyBinding, scope, behavior
3. Update Follower Selection section: replace ComboBox description with ListBox + ScrollViewer
4. Add Stop Buffer field section: tick-count input, how it feeds into StopBuffer copy gate

**DoD:**
- All 4 missing features documented in the spec HTML
- Any "JSON" reference in the B6 phase-detail section corrected to "XML (copy_rules.xml)"
- Spec HTML is valid (no broken structure)
- No Wave workspace files touched

---

## I. Risk / Assumptions

| # | Risk | Likelihood | Mitigation |
|---|------|-----------|-----------|
| R1 | `CopyRule` struct fields differ from assumed names in DTO | Medium | T1 engineer **must** read `CopyEngine.cs` B1 source before implementing `CopyRuleDto`. Plan documents the assumption; engineer adapts. |
| R2 | `_rules` is declared `readonly` in B1, blocking field reassignment | Low | Plan already prescribes `ConcurrentBag.Add()` iterative approach in `LoadRules()` — no reassignment needed. |
| R3 | `CopyEngine` has no test-visible `ClearRules()` method for T3-02 isolation | Medium | T3 engineer resolves by either (a) using `overridePath` to a unique temp file per test (no shared engine state needed), or (b) using `[Fact]` test ordering with fresh instances. Tests should not share engine state. |
| R4 | `XmlSerializer` chokes on nested `string[]` in `CopyRuleDto` | Low | `XmlSerializer` handles `string[]` as `<ArrayOfString>` by default. If problematic, substitute `List<string>` (fully supported). |
| R5 | `NinjaTrader.Core.Globals.UserDataDir` returns a path with a trailing backslash | Low | Use `Path.Combine()` explicitly — handles trailing separator correctly on all cases. |
| R6 | `OnInitialize` is called multiple times (e.g. on tab re-open) | Low | In NT8 NTWindow subclasses, `OnInitialize` runs once per window lifetime. If called again, `LoadRules()` will add duplicates via `ConcurrentBag.Add()`. Mitigation: add a `_persistenceLoaded` volatile bool guard, set on first call, checked before loading. CYC adds +1 but stays <= 8. |
| R7 | Spec HTML (T4) requires reading the current HTML structure before editing | None | T4 is documentation only. Engineer reads HTML, inserts sections. No code risk. |

### Assumption Inventory

- NT8 targets .NET Framework 4.8 → `System.Xml.Serialization.XmlSerializer` available, no NuGet needed.
- `CopyRule` has public get-only properties accessible from within `CopyEngine` class (where `RuleToDto` lives as a private static method).
- `CopyRule.Create()` factory method exists (from B1 JS-010 mandate) with a signature that accepts all persisted fields.
- `TradeCopierWindow.OnInitialize()` and `OnDestroyed()` exist with at least a few lines of existing logic (confirmed by B5 line count of 462).
- The rule-list UI refresh method used in `OnInitialize()` is accessible (T2 engineer confirms name by reading B5 source).
- `CopyEngineTests.cs` uses `IDisposable` / `Dispose()` cleanup (confirmed in B5 — DW-B2-01 was closed with IDisposable implementation).

---

## J. B6 Deferred Items (Forward Ledger Entry)

After B6, the deferred ledger should be updated with a new B6 section:

| ID | Item | Priority | Decision |
|----|------|----------|----------|
| DW-B5-03 | Rule persistence across sessions | P3 | CLOSED (B6) |
| DW-B5-04 | Spec HTML update for B3/B4/B5 changes | P3 | CLOSED (B6) |

If the architect or engineer identifies any new deferred items during B6 execution,
they are appended to `docs/brain/PTT-COPIER-B6/06-deferred-backlog.md`.
At this time, **no new deferred items are anticipated for B6.**

---

*End of PTT-COPIER-B6 Architecture Plan*
