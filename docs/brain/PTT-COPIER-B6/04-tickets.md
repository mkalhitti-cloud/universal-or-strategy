# PTT-COPIER-B6 Tickets
**Block:** B6 (backlog-closure block)
**Status:** TICKETS_COMPLETE
**Architecture Plan:** docs/brain/PTT-COPIER-B6/02-architecture-plan.md (REVIEW_PASS)
**Produced:** 2026-07-06

---

## Ticket T1 — CopyEngine Persistence Logic

**File path (Wave workspace):** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Type:** ADDITIVE CODE

### Scope
Add rule persistence to `CopyEngine.cs` so user-configured copy rules survive NinjaTrader
restarts. All additions are appended to the file; no existing lines are deleted or modified.

Specifically:
- Add two nested serialization DTO classes (`CopyRuleDto`, `CopyRulesContainer`)
- Add `GetPersistencePath()` helper that resolves the XML file path under `UserDataDir`
- Add `RuleToDto()` and `DtoToRule()` conversion helpers
- Add `public void SaveRules(string? overridePath = null)` — serialize `_rules` to XML
- Add `public void LoadRules(string? overridePath = null)` — deserialize XML into `_rules`

**Pre-work (engineer must do before writing a line of code):**
1. Read `CopyEngine.cs` (B5 source) to confirm: `CopyRule` struct field/property names,
   `CopyRule.Create()` factory signature, whether `_rules` is `readonly`, and whether
   a `_persistenceLoaded` guard field already exists.
2. If `CopyRule.Create()` signature differs from the assumed fields below, adapt `DtoToRule()`
   accordingly. Document the actual signature in the completion report.

### Method signatures to implement

```csharp
// ── Nested DTO classes (private, inside CopyEngine class body) ────────────────

[Serializable]
private sealed class CopyRuleDto
{
    public string SourceAccountName { get; set; } = string.Empty;
    public string[] FollowerAccountNames { get; set; } = Array.Empty<string>();
    public double LotRatio { get; set; }
    public int TickOffset { get; set; }
    public int StopBuffer { get; set; }
    public bool IsEnabled { get; set; }
}

[Serializable]
private sealed class CopyRulesContainer
{
    public List<CopyRuleDto> Rules { get; set; } = new List<CopyRuleDto>();
}

// ── Path helper ───────────────────────────────────────────────────────────────

/// <summary>
/// Returns the full path to the persistence XML file.
/// CYC = 1
/// </summary>
private static string GetPersistencePath(string? overridePath = null)

// ── Conversion helpers ────────────────────────────────────────────────────────

/// <summary>
/// Converts a CopyRule domain value to a serialization DTO.
/// Engineer: adapt property names to match actual CopyRule from B1 source.
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
/// No lock() — called only from the NT main thread at shutdown.
/// CYC = 2 (try/catch = 1 branch)
/// </summary>
public void SaveRules(string? overridePath = null)

/// <summary>
/// Deserializes rules from an XML file and adds them to _rules via ConcurrentBag.Add().
/// No-op if the file does not exist.
/// Called from TradeCopierWindow.OnInitialize() on the NT main thread.
/// No lock() — called only once at startup; _rules is ConcurrentBag (thread-safe Add).
/// CYC = 3 (File.Exists guard + try/catch + foreach)
/// </summary>
public void LoadRules(string? overridePath = null)
```

**Implementation notes:**
- `GetPersistencePath`: use `Path.Combine(overridePath ?? NinjaTrader.Core.Globals.UserDataDir, "PropTraderTools", "copy_rules.xml")`.
  Do NOT use string concatenation — use `Path.Combine` to handle trailing separators (Risk R5).
- `SaveRules`: call `Directory.CreateDirectory(Path.GetDirectoryName(path))` before writing
  to ensure the `PropTraderTools` subdirectory exists.
- `LoadRules`: guard with `if (!File.Exists(path)) return;` before deserializing.
- `LoadRules`: use iterative `_rules.Add(DtoToRule(dto))` — never reassign `_rules` field
  (may be `readonly` in B1, Risk R2).
- Add a `private volatile bool _persistenceLoaded;` field. In `LoadRules()`, guard the
  entire body with `if (_persistenceLoaded) return; _persistenceLoaded = true;` to prevent
  duplicate-add if `OnInitialize` is ever called more than once (Risk R6). CYC adds +1,
  still <= 8.
- `XmlSerializer` handles `string[]` as `<ArrayOfString>`. If build errors occur, change
  `FollowerAccountNames` to `List<string>` in `CopyRuleDto` (Risk R4).
- No `async`/`await` anywhere. No `lock()` anywhere. No `DateTime.Now`.

### xUnit tests to write
None in this ticket. Tests are in T3.

### 7-Scan Checklist
| Scan | Pattern | Target |
|------|---------|--------|
| SCAN-01 | `lock(` | 0 |
| SCAN-02 | non-ASCII chars in .cs file | 0 |
| SCAN-03 | `FontFamily` | 0 |
| SCAN-04 | `#RRGGBB` hex color literals | 0 |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 |
| SCAN-06 | `DateTime.Now` | 0 |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 |

### Definition of Done
- `CopyEngine.cs` line count is ~478 (from 424 + ~54 additive lines)
- `SaveRules()` writes a valid, non-empty XML file at the resolved path
- `LoadRules()` reads that file and populates `_rules` via `ConcurrentBag.Add()`
- `LoadRules()` is a no-op when the file does not exist
- No `lock()` in any new method
- CYC of each new method <= 8
- All 7 scans return 0 results on `CopyEngine.cs`
- Build passes: `powershell -File .\scripts\build_readiness.ps1`

---

## Ticket T2 — TradeCopierWindow Lifecycle Hooks

**File path (Wave workspace):** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
**Type:** ADDITIVE CODE

### Scope
Wire the persistence API into the NT window lifecycle. Two method bodies are modified by
appending/prepending exactly one line (or two lines) each. No other lines are touched.

- `OnInitialize()`: append `CopyEngine.Instance.LoadRules();` after all existing init logic,
  then call the existing rule-list UI refresh helper (engineer confirms its name by reading
  the B5 source before editing).
- `OnDestroyed()`: prepend `CopyEngine.Instance.SaveRules();` as the very first statement,
  before any existing cleanup.

**Pre-work (engineer must do before editing):**
1. Read `TradeCopierWindow.cs` (B5 source) — locate `OnInitialize()` and `OnDestroyed()`
   bodies and identify the existing rule-list UI refresh method name (called in `OnInitialize`).
2. Confirm the current CYC of both methods is <= 7 (budget for +1 from the new call in
   `OnInitialize` remains <= 8).
3. Confirm no `async`/`await` is present in either method body (no change to threading model).

**Dependency:** T1 must be COMPLETE before T2 is executed.

### Method signatures to implement
No new method signatures. Existing signatures are unchanged. The modifications are additive
call insertions inside the existing method bodies:

```csharp
// ── Inside OnInitialize() — APPEND after all existing init logic ──────────────
CopyEngine.Instance.LoadRules();
// [call existing rule-list UI refresh method — engineer confirms name from B5 source]

// ── Inside OnDestroyed() — PREPEND before any existing cleanup ───────────────
CopyEngine.Instance.SaveRules();
```

**Implementation notes:**
- Do NOT introduce `async`/`await` — both lifecycle methods must remain synchronous.
- Do NOT add `Dispatcher.InvokeAsync` — both lifecycle methods are already called on the
  NT main thread.
- Do NOT add any new UI controls, event handlers, or fields in this ticket.
- The rule-list UI refresh call in `OnInitialize()` ensures the loaded rules are reflected
  in the UI immediately on startup. If no dedicated refresh helper exists in B5, engineer
  must trigger the equivalent inline repopulation — document in completion report.

### xUnit tests to write
Not applicable. `TradeCopierWindow` is an NT `NTWindow` subclass; lifecycle methods are
NT-host-dependent and not unit-testable in isolation. Persistence behavior is tested via
`CopyEngineTests.cs` (T3).

### 7-Scan Checklist
| Scan | Pattern | Target |
|------|---------|--------|
| SCAN-01 | `lock(` | 0 |
| SCAN-02 | non-ASCII chars in .cs file | 0 |
| SCAN-03 | `FontFamily` | 0 |
| SCAN-04 | `#RRGGBB` hex color literals | 0 |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 |
| SCAN-06 | `DateTime.Now` | 0 |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 |

### Definition of Done
- `TradeCopierWindow.cs` line count is ~470 (from 462 + ~8 additive lines)
- `OnInitialize()` calls `CopyEngine.Instance.LoadRules()` after existing init logic
- `OnDestroyed()` calls `CopyEngine.Instance.SaveRules()` before any cleanup
- CYC of `OnInitialize()` and `OnDestroyed()` both remain <= 8 after additions
- No `async`/`await` introduced
- No `lock()` introduced
- All 7 scans return 0 results on `TradeCopierWindow.cs`
- Build passes: `powershell -File .\scripts\build_readiness.ps1`

---

## Ticket T3 — xUnit Persistence Tests

**File path (Wave workspace):** `c:\WSGTA\universal-or-strategy\src\PropTraderTools.Tests\CopyEngineTests.cs`
**Type:** ADDITIVE CODE

### Scope
Add 3 new `[Fact]` tests to the existing `CopyEngineTests.cs` test file. The file grows
from 264 lines / 19 tests to approximately 310 lines / 22 tests. No existing tests are
deleted or modified.

**Pre-work (engineer must do before editing):**
1. Read `CopyEngineTests.cs` (B5 source) to confirm:
   - The `using` directives already present (no duplicate imports)
   - Whether `CopyEngine` exposes a `ClearRulesForTesting()` or equivalent Reset method
   - Whether the class implements `IDisposable` for cleanup (confirmed from B5 DW-B2-01)
   - The access pattern for asserting rule count (e.g., `GetRules()`, `GetRuleCount()`)
2. Confirm `SaveRules(string? overridePath)` and `LoadRules(string? overridePath)` overloads
   are available after T1 is complete.

**Dependency:** T1 must be COMPLETE before T3 is executed.

### Method signatures to implement

```csharp
[Fact]
public void SaveRules_WritesXmlFile_WhenRulesExist()

[Fact]
public void LoadRules_PopulatesRules_WhenFileExists()

[Fact]
public void LoadRules_DoesNotThrow_WhenFileAbsent()
```

### xUnit tests to write

**T3-01 — `SaveRules_WritesXmlFile_WhenRulesExist`**
- Arrange: create a unique temp directory (`Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`);
  add 1 `CopyRule` to `CopyEngine.Instance` via the existing `AddRule()` / equivalent API;
  record the expected temp file path.
- Act: call `CopyEngine.Instance.SaveRules(tempFilePath)`.
- Assert:
  - `Assert.True(File.Exists(tempFilePath))` — file was created
  - `Assert.True(new FileInfo(tempFilePath).Length > 0)` — file is non-empty
  - Re-deserialize file contents and assert `SourceAccountName` matches the rule that was added
- Cleanup: delete the temp file and directory in `finally`.

**T3-02 — `LoadRules_PopulatesRules_WhenFileExists`**
- Arrange: create a unique temp directory and file path; hand-write a valid XML file with
  2 rules using `XmlSerializer` (or raw XML string); ensure `CopyEngine.Instance` starts
  with 0 rules (use Reset/Clear if available, or use a fresh instance via test isolation).
- Act: call `CopyEngine.Instance.LoadRules(tempFilePath)`.
- Assert:
  - `Assert.Equal(2, CopyEngine.Instance.GetRuleCount())` (or equivalent)
  - Assert field values of the first loaded rule match what was written to the XML
- Cleanup: delete the temp file and directory in `finally`.

**T3-03 — `LoadRules_DoesNotThrow_WhenFileAbsent`**
- Arrange: generate a path to a file that does NOT exist
  (`Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "does_not_exist.xml")`).
- Act + Assert: call `CopyEngine.Instance.LoadRules(nonExistentPath)` inside
  `var ex = Record.Exception(() => CopyEngine.Instance.LoadRules(nonExistentPath));`
  then `Assert.Null(ex)` — no exception thrown.
- Assert: rule count is unchanged (0) after the no-op call.

**Implementation notes:**
- Use `Path.GetTempPath()` + `Guid.NewGuid().ToString()` for every temp path — never
  use a hardcoded path, never use `NinjaTrader.Core.Globals.UserDataDir` in tests.
- If `CopyEngine` is a singleton, the test class must reset its state between tests.
  Use the existing Reset mechanism from B1–B5, or confirm that `overridePath` isolation
  makes shared engine state irrelevant (T3-03 and T3-01 write/read their own files).
- No NUnit attributes (`[Test]`, `[SetUp]`, `[TearDown]`). No MSTest attributes.
  Only xUnit: `[Fact]`, `Assert.*`, and optionally `Record.Exception`.

### 7-Scan Checklist
| Scan | Pattern | Target |
|------|---------|--------|
| SCAN-01 | `lock(` | 0 |
| SCAN-02 | non-ASCII chars in .cs file | 0 |
| SCAN-03 | `FontFamily` | 0 |
| SCAN-04 | `#RRGGBB` hex color literals | 0 |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 |
| SCAN-06 | `DateTime.Now` | 0 |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 |

### Definition of Done
- `CopyEngineTests.cs` line count is ~310 (from 264 + ~46 additive lines)
- All 3 new `[Fact]` tests pass: `dotnet test` exits 0
- All 19 existing tests continue to pass — zero regressions
- Total `[Fact]` count in the file = **22**
- No NUnit or MSTest references introduced
- All temp files cleaned up in test `finally` blocks
- Build passes: `powershell -File .\scripts\build_readiness.ps1`

---

## Ticket T4 — Spec HTML Update (Documentation Only)

**File path (Director workspace):** `specs/002-trade-copier-spec.html`
**Type:** DOC UPDATE

### Scope
Update the trade copier specification HTML to bring it in sync with B3–B6
implementation. No Wave workspace (`src/`) files are touched.

Four feature sections are missing entirely from the current spec; one factual error
(format label) must be corrected.

**Pre-work (engineer must do before editing):**
1. Read `specs/002-trade-copier-spec.html` to understand the existing section structure
   (headings, IDs, table of contents) so new sections integrate consistently.
2. Identify the line that references "JSON" in any B5 or B6 phase-detail description —
   the architecture plan identifies line 1531 as the likely location.

### Method signatures to implement
Not applicable (documentation only).

### Sections to add / correct

**Section 1 — Break-Even Button (B3 feature)**
- Where to insert: after the existing BreakEven/PnL section or at end of UI Controls section
- Content to document:
  - UI placement: "Break Even" button in the control panel
  - Activation logic: clicking the button triggers `OnBreakEvenClicked` handler in
    `TradeCopierWindow`, which calls `CopyEngine.Instance.ApplyBreakEven()` (or equivalent
    method name — engineer confirms from B3/B4 source)
  - Behavior: moves stop loss on all active copied positions to the entry price (break-even)

**Section 2 — Shift+B Keyboard Shortcut (B5 feature)**
- Where to insert: after or within the Break-Even Button section
- Content to document:
  - Keyboard binding: `Shift+B`
  - Registered via NT `KeyBinding` in `TradeCopierWindow.OnInitialize()`
  - Handler: `OnWindowBreakEven` (confirm name from B5 source)
  - Scope: applies only when the TradeCopierWindow has focus
  - Behavior: identical to clicking the Break Even button

**Section 3 — Follower Account Selection — ListBox/ScrollViewer (B5 feature)**
- Where to insert: update/replace the existing Follower Account ComboBox description
- Content to document:
  - Control type changed from single-select `ComboBox` to multi-select `ListBox` wrapped
    in a `ScrollViewer`
  - Selection mode: multiple followers can be selected simultaneously
  - Behavior: all selected follower accounts receive the copied trade when a rule fires

**Section 4 — Stop Buffer Field (B5 feature)**
- Where to insert: after the Follower Account Selection section
- Content to document:
  - Control: integer tick-count input field labeled "Stop Buffer"
  - Purpose: adds a buffer (in ticks) to the copied stop loss distance
  - Effect: when `StopBuffer > 0`, the follower stop loss is placed `StopBuffer` ticks
    further from entry than the source stop loss
  - Default: 0 (no buffer — stop loss is copied exactly)

**Section 5 — B6 Persistence (XML)**
- Where to insert: new section for B6, or within the existing persistence/settings section
- Content to document:
  - Format: `XML` via `System.Xml.Serialization.XmlSerializer` (NOT JSON)
  - File location: `{UserDataDir}\PropTraderTools\copy_rules.xml`
  - Save trigger: `TradeCopierWindow.OnDestroyed()` (NT shutdown)
  - Load trigger: `TradeCopierWindow.OnInitialize()` (NT startup)
  - Fields persisted: `SourceAccountName`, `FollowerAccountNames[]`, `LotRatio`,
    `TickOffset`, `StopBuffer`, `IsEnabled`

**Correction — line ~1531**
- Find: any reference to `"JSON"` or `".json"` in the B5/B6 phase-detail section
- Replace with: `"XML (copy_rules.xml)"`

### xUnit tests to write
Not applicable (documentation only).

### 7-Scan Checklist
| Scan | Pattern | Target |
|------|---------|--------|
| SCAN-01 | `lock(` | 0 |
| SCAN-02 | non-ASCII chars in .cs file | 0 |
| SCAN-03 | `FontFamily` | 0 |
| SCAN-04 | `#RRGGBB` hex color literals | 0 |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 |
| SCAN-06 | `DateTime.Now` | 0 |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 |

*(Scans are N/A for an HTML file; checklist retained for format consistency. The engineer
running T4 does not run these scans on the HTML file.)*

### Definition of Done
- All 4 missing features (Break-Even button, Shift+B shortcut, ListBox/ScrollViewer follower
  select, Stop Buffer field) are documented in the spec HTML
- B6 persistence section documents `XML (copy_rules.xml)` — not JSON
- Any pre-existing `"JSON"` reference in the B6 phase-detail section is corrected
- The HTML file is structurally valid (no unclosed tags, no broken TOC links)
- No Wave workspace (`src/`) files are created, modified, or deleted

---

## Execution Order

```
T1 (CopyEngine persistence logic)
  → T2 (TradeCopierWindow lifecycle hooks)  [depends on T1]
  → T3 (xUnit persistence tests)            [depends on T1]
T4 (Spec HTML update)                       [independent — can run in parallel with T1]
```

## Completion Gate

All four tickets complete when:
1. `dotnet build` exits 0 (no new warnings)
2. `dotnet test` exits 0 — **22 `[Fact]` tests pass**
3. All 7 scans return **0 results** across all modified `.cs` files
4. `powershell -File .\scripts\build_readiness.ps1` passes
5. `powershell -File .\deploy-sync.ps1` executed successfully (hard-link sync)
6. Spec HTML updated and structurally valid

---

*End of PTT-COPIER-B6 Tickets*
