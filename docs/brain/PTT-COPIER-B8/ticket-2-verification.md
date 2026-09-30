# PTT-COPIER-B8 -- Ticket T2 Verification Report
**Ticket**: T2 -- FollowerAtmMode Behavioral Wiring (DW-B7-03)
**Verifier**: PTT Verifier (Phase 5.V)
**Date**: 2026-07-08
**Input files read directly**:
- c:/WSGTA/universal-or-strategy/src/PropTraderTools/CopyEngine.cs (1040 lines)
- c:/WSGTA/universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs (543 lines)
- c:/WSGTA/universal-or-strategy/src/PropTraderTools/TradeCopierWindow.cs (559 lines)
- c:/WSGTA/universal-or-strategy/src/PropTraderTools/CopyEngineTests.cs (464 lines)
- docs/brain/PTT-COPIER-B8/04-tickets.md (T2 requirements)
- docs/brain/PTT-COPIER-B8/02-architecture-plan.md

---

## Section 1: Implementation Completeness (T2 Section C)

### CopyEngine.cs -- T2 Methods

| Method | Required Signature | Status | File:Line | Notes |
|--------|-------------------|--------|-----------|-------|
| `SendCopy` modified | `private bool SendCopy(Account, Instrument, in CopySignal, FollowerAtmMode)` | PRESENT | CopyEngine.cs:512 | Correct signature; mode dispatch implemented |
| `GetAtmMode` | `private static FollowerAtmMode GetAtmMode(CopyRule, string)` | PRESENT | CopyEngine.cs:556 | Returns Inherit if not found; never null |
| `ParseAtmModeName` | `internal static FollowerAtmMode ParseAtmModeName(string)` | PRESENT | CopyEngine.cs:566 | internal static; correct visibility |
| `AtmModeToString` | `internal static string AtmModeToString(FollowerAtmMode)` | PRESENT | CopyEngine.cs:579 | internal static; correct visibility |
| `SetAtmMode` | `internal void SetAtmMode(string, string, FollowerAtmMode)` | PRESENT | CopyEngine.cs:591 | ConcurrentBag rebuild; no lock |
| `DispatchCopy` passes mode | Mode retrieved via GetAtmMode; passed to SendCopy | PRESENT | CopyEngine.cs:340-341 | Index-tracking loop; mode per follower |
| `RuleToDto` uses AtmModeToString | `atmNames[i] = AtmModeToString(GetAtmMode(rule, accName))` | PRESENT | CopyEngine.cs:905 | Correct serialization per follower |
| `DtoToRule` parses FollowerAtmModeNames | `ParseAtmModeName(dto.FollowerAtmModeNames[i])` | PRESENT | CopyEngine.cs:958 | Null-safe; B6/B7 backward compat |
| `CopyRuleDto.FollowerAtmModeNames` | `public string[] FollowerAtmModeNames` | PRESENT | CopyEngine.cs:868 | Default `new string[0]` |
| `FollowerAtmMode` sealed hierarchy | `Inherit / Market / Named(string)` | PRESENT | CopyEngine.cs:34-40 | Sealed records; private base ctor |

### TradeCopierPanel.cs -- T2 Items

| Item | Required | Status | File:Line | Notes |
|------|----------|--------|-----------|-------|
| `FollowerItem.AtmModeName` | `public string AtmModeName { get; set; } = "Inherit"` | PRESENT | TradeCopierPanel.cs:103 | Default "Inherit" |
| ATM ComboBox in row | Width=80; Loaded+SelectionChanged handlers wired | PRESENT | TradeCopierPanel.cs:358-364 | Both handlers wired correctly |
| `OnFollowerAtmComboLoaded` | Populates items {"Inherit","Market","Named"}; SelectedIndex=0 | PRESENT | TradeCopierPanel.cs:398-404 | Correct items and default |
| `OnFollowerAtmModeChanged` | Sets `item.AtmModeName` on selection change | PRESENT | TradeCopierPanel.cs:409-418 | Correct null guards |
| `OnApplyRule` updated | Collects atmNames per follower; builds ImmutableDictionary | PRESENT | TradeCopierPanel.cs:476-520 | Calls 5-arg AddRule correctly |
| `ParseAtmModeNameLocal` | `private static FollowerAtmMode ParseAtmModeNameLocal(string)` | PRESENT | TradeCopierPanel.cs:524-533 | Mirrors engine method; self-contained |

### TradeCopierWindow.cs -- T2 Items

| Item | Required | Status | File:Line | Notes |
|------|----------|--------|-----------|-------|
| `BuildRuleRow` Col 9 ATM ComboBox | ATM ComboBox (Inherit/Market) added | PRESENT | TradeCopierWindow.cs:321-328 | Present visually |
| `BuildDynamicRuleRow` Col 9 ATM ComboBox | ATM ComboBox added; ref stored in `atmCbDyn` | PRESENT | TradeCopierWindow.cs:409-435 | `atmCbDyn` reference stored; placed at Col 9 |
| `OnRowApply` reads `tag[3]` | Reads ATM ComboBox; builds atmMap | PARTIAL | TradeCopierWindow.cs:520-527 | Dynamic rows: WORKS (4-element tag). Static row: BROKEN. |
| `ParseAtmModeNameWindow` | `private static FollowerAtmMode ParseAtmModeNameWindow(string)` | MISSING | TradeCopierWindow.cs (absent) | Uses `CopyEngine.ParseAtmModeName` directly instead |
| `using System.Collections.Immutable` | Added | PRESENT | TradeCopierWindow.cs:12 | `using System.Collections.Immutable;` on line 12 |

---

## Section 2: CRITICAL -- PTT- Prefix Check (signalName)

| Branch | Expected signalName | Actual signalName | ATM template | Status |
|--------|--------------------|--------------------|--------------|--------|
| Inherit mode | "PTT-Copy" | "PTT-Copy" (line 516) | null (line 525-527) | PASS |
| Market mode | "PTT-Copy" | "PTT-Copy" (line 516, unchanged) | null (line 527) | PASS |
| Named mode | "PTT-Copy" | "PTT-Copy" (line 516, unchanged) | `named.TemplateName` (line 526) | PASS |

**signalName is ALWAYS "PTT-Copy" for ALL modes.**
ATM template name is passed as the final `atmTemplate` parameter of `Account.CreateOrder`, NOT as signal name.
Line 516: `string signalName = "PTT-Copy";` -- never overwritten in any branch.
Line 525-527: `string atmTemplate = mode is FollowerAtmMode.Named named ? named.TemplateName : null;`
Line 541: `signalName,` -- always passes "PTT-Copy"
Line 543: `atmTemplate` -- passes template name or null as last CreateOrder param

**Fix 3 from Ticket Review: CORRECTLY IMPLEMENTED.**

---

## Section 3: JS Rule Scans (Independent -- Run by Verifier)

### SCAN-01: `lock(`

Command: `Select-String -Path "*.cs" -Pattern "^\s*lock\s*\("`
Result: **ZERO matches** -- all "lock" occurrences are in comments only (CopyEngine.cs:208, 589).
VERDICT: **PASS**

### SCAN-02: Non-ASCII Characters

Command: `Get-Content *.cs | Where-Object {$_ -match '[^\x00-\x7F]'}`
Result: **1 match** -- CopyEngine.cs:866: `// B8 T2 (pre-declared here for single DTO edit pass per plan §3.1):`
Finding: `§` character (section sign, U+00A7) in a **comment only**.
No non-ASCII in any executable code path.
The project standard bans Unicode in C# string literals (AGENTS.md §2: "ASCII-Only Compliance: NEVER use Unicode, emoji, or curly quotes in C# string literals").
The hit is in a comment -- not a string literal.
VERDICT: **PASS** (comment-only, no executable code impact)

### SCAN-03: `FontFamily=` on WPF elements

Command: `Select-String -Path "*.cs" -Pattern "FontFamily"`
Result: **ZERO matches**
VERDICT: **PASS**

### SCAN-04: `#RRGGBB` hex color strings

Command: `Select-String -Path "*.cs" -Pattern "#[0-9A-Fa-f]{6}"`
Result: **8 matches** -- ALL in comments only (`// green #22c55e`, `// red #ef4444`, etc.)
No hex color literals in executable code. All brushes use `MakeBrush(r,g,b)` / `MakeWinBrush(r,g,b)` with integer RGB components.
VERDICT: **PASS** (comment-only)

### SCAN-05: `DateTime.Now` (non-UTC)

Command: `Select-String -Path "*.cs" -Pattern "DateTime\.Now[^U]"`
Result: **ZERO matches**
VERDICT: **PASS**

### SCAN-06: `async void`

Command: `Select-String -Path "*.cs" -Pattern "async void"`
Result: **ZERO matches**
VERDICT: **PASS**

### SCAN-07: `throw new` in SendCopy / DispatchCopy

Command: `Select-String -Path "*.cs" -Pattern "throw new"`
Result: **ZERO matches**
VERDICT: **PASS**

### SCAN Extra: `new Dictionary<` (mutable collections)

Command: `Select-String -Path "*.cs" -Pattern "new Dictionary<"`
Result: **ZERO matches**
VERDICT: **PASS**

---

## Section 4: NT8 Constraint Check

| Constraint | Status | Evidence |
|-----------|--------|---------|
| signalName for CreateOrder = "PTT-Copy" (always) | PASS | CopyEngine.cs:516 -- `string signalName = "PTT-Copy";` never overwritten |
| ATM template for Named passed as last CreateOrder param, NOT signal name | PASS | CopyEngine.cs:525-543 -- `atmTemplate` as 12th parameter |
| `TradeCopierWindow` NOT sealed | PASS | TradeCopierWindow.cs:20 -- `public class TradeCopierWindow : Window` (no sealed) |
| No async/await in new methods | PASS | SCAN-06 zero matches; all T2 handlers are synchronous void |
| No `Account.All` outside Loaded handler | PASS | BuildDynamicRuleRow:359,366 binds Account.All but is called from `OnAddRule` which fires on WPF UI thread after Loaded -- pre-existing pattern |

---

## Section 5: CYC Check (Verifier-Counted)

Methodology: CYC = 1 (base) + decision points (if/else if/ternary/for/foreach/while/case/&&/||)

| Method | File:Line | Decisions | CYC | Limit | Status |
|--------|-----------|-----------|-----|-------|--------|
| `SendCopy` | CopyEngine.cs:512-552 | if Market(1) + ternary Named(2) + try/catch(3) | 4 | <=5 | PASS |
| `GetAtmMode` | CopyEngine.cs:556-562 | if TryGetValue(1) | 2 | <=2 | PASS |
| `ParseAtmModeName` | CopyEngine.cs:566-575 | if empty(1)+if Market(2)+if Named:(3) | 4 | <=8 | PASS |
| `AtmModeToString` | CopyEngine.cs:579-586 | if Market(1)+if Named(2) | 3 | <=8 | PASS |
| `SetAtmMode` | CopyEngine.cs:591-606 | foreach(1)+if instrument!=(2) | 3 | <=8 | PASS |
| `DispatchCopy` (T1+T2 combined) | CopyEngine.cs:302-344 | Submitted(1)+type(2)+dedup(3)+foreach(4)+nullAcc(5)+dailyCap(6)+idx++ | 8 | <=8 | PASS |
| `OnFollowerAtmComboLoaded` | TradeCopierPanel.cs:398-404 | if cb null(1) | 2 | <=8 | PASS |
| `OnFollowerAtmModeChanged` | TradeCopierPanel.cs:409-418 | if cb null(1)+if item null(2)+if selected null(3) | 4 | <=8 | PASS |
| `ParseAtmModeNameLocal` | TradeCopierPanel.cs:524-533 | if empty(1)+if Market(2)+if Named:(3) | 4 | <=8 | PASS |
| `OnRowApply` (Window) | TradeCopierWindow.cs:505-534 | if tag null(1)+if name empty(2)+if followerLb(3)+if leader/followers(4)+if tag.Length>3(5) | 6 | <=8 | PASS |
| `OnApplyRule` (Panel) | TradeCopierPanel.cs:476-520 | if leader null(1)+if instr null(2)+if followers 0(3)+for followers(4)+foreach items(5) | 6 | <=8 | PASS |

All methods within CYC <=8 (Jane Street strict standard). PASS.

---

## Section 6: Test Regression

| Check | Required | Actual | Status |
|-------|----------|--------|--------|
| [Fact] count in CopyEngineTests.cs | 27 (T3 adds more) | 27 | PASS |
| Any existing test modified | None allowed | Zero modifications to existing tests (lines 23-463 verbatim from B7) | PASS |
| 3-arg `AddRule` preserved | Required by T2 ticket | CopyEngine.cs:190-193 -- UNCHANGED | PASS |

---

## Section 7: DW-B7-03 Satisfaction

| Requirement | Status | Evidence |
|-------------|--------|---------|
| Inherit: order placed with original signal type and quantity | PASS | SendCopy:514-516 -- orderType=signal.Type, limitPrice=signal.LimitPrice; no modification in Inherit path |
| Market: order forced to OrderType.Market | PASS | SendCopy:518-522 -- orderType=OrderType.Market; limitPrice=0 |
| Named: order placed with atmTemplate parameter | PASS | SendCopy:525-543 -- atmTemplate=named.TemplateName passed as last param of CreateOrder |
| Per-follower ATM ComboBox in Panel | PASS | TradeCopierPanel.cs:355-364 -- atmFactory wired with Loaded+SelectionChanged |
| Per-rule ATM ComboBox in Window (dynamic rows) | PASS | TradeCopierWindow.cs:409-435 -- atmCbDyn present; wired via 4-element tag |
| Per-rule ATM ComboBox in Window (static MES row) | FAIL | TradeCopierWindow.cs:302 -- applyBtn.Tag is 3-element; atmCb NOT wired to tag |
| Serialization round-trip (AtmModeToString + ParseAtmModeName) | PASS | CopyEngine.cs:566-586 -- Inherit/Market/Named:XXX all round-trip |
| Backward compat: B6/B7 XML with no FollowerAtmModeNames | PASS | CopyEngine.cs:951-960 -- null check before loop; defaults to Inherit |

---

## Section 8: Defects Found

### DEFECT-T2-001 (VERIFY_FAIL -- Functional)
**File**: TradeCopierWindow.cs  
**Line**: 302  
**Severity**: FUNCTIONAL BUG  
**Description**: In `BuildRuleRow()` (the static pre-built "MES" row), `applyBtn.Tag` is set to a
3-element array `{ instrumentName, leaderCb, followerLb }`. The ATM ComboBox (`atmCb`) created at
line 323 is **NOT included** in the tag. `OnRowApply` guards with `if (tag.Length > 3 && ...)` --
this guard is always false for static rows, so the ATM selection is silently ignored and all followers
always receive `Inherit` regardless of UI selection.  
**Required fix**: Change line 302 to:
```
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb };
```

### DEFECT-T2-002 (Advisory -- Spec Deviation)
**File**: TradeCopierWindow.cs  
**Line**: 524  
**Severity**: SPEC DEVIATION (functional equivalent used)  
**Description**: Ticket T2 section C specifies `private static FollowerAtmMode ParseAtmModeNameWindow(string name)`
as a local helper on TradeCopierWindow. The actual implementation calls `CopyEngine.ParseAtmModeName(atmSel)`
directly (line 524). While functionally identical, the private local helper is absent.
The architecture plan intent was to keep each file self-contained (Panel uses ParseAtmModeNameLocal;
Window should use ParseAtmModeNameWindow). This breaks the isolation principle but is not a runtime defect.

### DEFECT-T2-003 (Advisory -- Non-ASCII in comment)
**File**: CopyEngine.cs  
**Line**: 866  
**Severity**: MINOR (comment only)  
**Description**: `§` character (U+00A7) in comment text. Not in executable code.
Project AGENTS.md §2 prohibits Unicode in C# string literals -- this is a comment, not a literal.
Recommend replacing with `S` or `sec` in comment.

---

## Section 9: Summary Verdict

| Check | Result |
|-------|--------|
| SCAN-01: lock() | PASS |
| SCAN-02: non-ASCII | PASS (comment only) |
| SCAN-03: FontFamily | PASS |
| SCAN-04: hex #RRGGBB | PASS (comment only) |
| SCAN-05: DateTime.Now | PASS |
| SCAN-06: async void | PASS |
| SCAN-07: throw new in dispatch | PASS |
| PTT- prefix invariant | PASS |
| Named mode uses atmTemplate param (not signalName) | PASS |
| TradeCopierWindow not sealed | PASS |
| SendCopy signature | PASS |
| GetAtmMode signature | PASS |
| ParseAtmModeName signature | PASS |
| AtmModeToString signature | PASS |
| SetAtmMode signature | PASS |
| DispatchCopy passes mode | PASS |
| RuleToDto AtmModeToString | PASS |
| DtoToRule ParseAtmModeName | PASS |
| Panel ATM ComboBox | PASS |
| Window dynamic row ATM ComboBox | PASS |
| Window static row ATM ComboBox wired to tag | FAIL -- DEFECT-T2-001 |
| ParseAtmModeNameWindow local helper | ADVISORY -- DEFECT-T2-002 |
| CYC <= 8 all methods | PASS |
| [Fact] count = 27 | PASS |
| DW-B7-03 Inherit behavior | PASS |
| DW-B7-03 Market behavior | PASS |
| DW-B7-03 Named behavior (atmTemplate param) | PASS |
| Serialization round-trip | PASS |
| Backward compat | PASS |

**OVERALL VERDICT: VERIFY_FAIL**

**Blocking violation**: DEFECT-T2-001 -- TradeCopierWindow.cs:302
The static rule row ATM ComboBox is visually present but functionally inoperative because `atmCb`
is not included in `applyBtn.Tag`. The `OnRowApply` guard `if (tag.Length > 3 ...)` is always false
for static rows. Every rule applied from the pre-built "MES" row ignores ATM mode selection.

**Repair required**: One-line fix at TradeCopierWindow.cs:302:
```csharp
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb };
```

DEFECT-T2-002 (advisory): Missing `ParseAtmModeNameWindow` local helper.
Functionally equivalent but violates the isolation principle stated in the ticket.
Recommend adding the private static helper and changing line 524 to use it.
---

## Retry Verification (Cycle 2) -- 2026-07-08

**Retry Reason**: DEFECT-T2-001 fixed -- `applyBtn.Tag` in `BuildRuleRow()` now includes `atmCb` as `tag[3]`.

### Fix Verification: BuildRuleRow() Static Row

| Check | Expected | Actual | Line | Status |
|-------|----------|--------|------|--------|
| `atmCb` created BEFORE `applyBtn` | YES | YES -- `atmCb` declared at line 302, `applyBtn` declared at line 311 | 302/311 | PASS |
| `applyBtn.Tag` is 4-element array | `{ instrumentName, leaderCb, followerLb, atmCb }` | `new object[] { instrumentName, leaderCb, followerLb, atmCb }` | 312 | PASS |
| `atmCb` is `tag[3]` | YES | YES -- 4th element | 312 | PASS |
| Comment documents intent | "created BEFORE applyBtn so tag can reference it" | Present at line 300 | 300 | PASS |

**Evidence** (TradeCopierWindow.cs:300-315):
```
// B8 T2: Col 9 -- ATM mode ComboBox -- created BEFORE applyBtn so tag can reference it.
var atmCb = new ComboBox { Width = 80, Margin = new Thickness(2) };   // line 302
atmCb.Items.Add("Inherit");
atmCb.Items.Add("Market");
atmCb.SelectedIndex = 0;
Grid.SetColumn(atmCb, 9);
grid.Children.Add(atmCb);
// Col 7: Apply -- tag[3] = atmCb
var applyBtn = new Button { Content = "Apply", Margin = new Thickness(2) };   // line 311
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb };  // line 312
```

DEFECT-T2-001 is **RESOLVED**.

---

### Fix Verification: BuildDynamicRuleRow() Dynamic Row

| Check | Expected | Actual | Line | Status |
|-------|----------|--------|------|--------|
| `atmCbDyn` in `applyBtn.Tag` as `tag[3]` | YES | `new object[] { instrTextBox, leaderCb, followerLb, atmCbDyn }` | 415 | PASS |
| `atmCbDyn` created before `applyBtn` | YES | `atmCbDyn` declared at line 410, `applyBtn` at line 414 | 410/414 | PASS |
| `atmCbDyn` added to grid at Col 9 | YES | `Grid.SetColumn(atmCbDyn, 9); grid.Children.Add(atmCbDyn)` | 435-436 | PASS |

UNCHANGED from cycle 1 -- still correct.

---

### Fix Verification: OnRowApply() ATM Read Path

| Check | Expected | Actual | Line | Status |
|-------|----------|--------|------|--------|
| Guard `tag.Length > 3` | YES | `if (tag.Length > 3 && tag[3] is ComboBox atmCb && atmCb.SelectedItem is string atmSel)` | 523 | PASS |
| Static row satisfies guard | YES -- tag is now 4-element | `tag.Length == 4 > 3` -- TRUE | 312 + 523 | PASS |
| Dynamic row satisfies guard | YES -- tag is 4-element | `tag.Length == 4 > 3` -- TRUE | 415 + 523 | PASS |
| ATM mode applied to all followers | `foreach (var acc in followers) atmMap = atmMap.SetItem(...)` | Present at lines 526-527 | 526-527 | PASS |

---

### Section 2 Re-check: signalName Invariant

`signalName` is hardcoded `"PTT-Copy"` at `CopyEngine.cs:516`, never overwritten in any branch.
`atmTemplate` (Named mode) is passed as the 12th argument to `CreateOrder` at `CopyEngine.cs:543`.
All `CreateOrder` calls use `"PTT-Copy"` as the signal name argument.

UNCHANGED -- still PASS.

---

### Section 3 Re-check: 7 Mandatory Scans (Re-run by Verifier)

All scans re-executed against `c:/WSGTA/universal-or-strategy/src/PropTraderTools/`

| Scan | Pattern | Result | Verdict |
|------|---------|--------|---------|
| SCAN-01 | `lock\s*\(` (code-only, not comments) | ZERO matches | PASS |
| SCAN-02 | Non-ASCII characters | 1 match: CopyEngine.cs:866 `§` in comment only (unchanged from cycle 1) | PASS |
| SCAN-03 | `FontFamily` | ZERO matches | PASS |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | 8 matches -- ALL in comments only (color annotations on `MakeBrush`/`MakeWinBrush` lines) | PASS |
| SCAN-05 | `DateTime\.Now[^U]` | ZERO matches | PASS |
| SCAN-06 | `async void` | ZERO matches | PASS |
| SCAN-07 | `throw new` | ZERO matches | PASS |

No new scan violations introduced by the fix.

---

### [Fact] Count Re-check

`(Select-String -Path "CopyEngineTests.cs" -Pattern "\[Fact\]").Count` --> **27**

PASS -- no regression.

---

### CYC Re-check: OnRowApply

`OnRowApply` now executes the `if (tag.Length > 3 ...)` branch for static rows (was dead code before the fix).
No structural change to the method body -- decision point count unchanged.

| Method | Decisions | CYC | Limit | Status |
|--------|-----------|-----|-------|--------|
| `OnRowApply` | tag null(1) + name empty(2) + followerLb(3) + leader/followers(4) + tag.Length>3(5) | 6 | <=8 | PASS |

---

### Advisory Status: DEFECT-T2-002

`ParseAtmModeNameWindow` private helper still absent -- `CopyEngine.ParseAtmModeName` called directly at `TradeCopierWindow.cs:525`.
This is a spec deviation (advisory only, no runtime impact). Remains open as advisory.

---

### Retry Summary Verdict

| Check | Cycle 1 | Cycle 2 |
|-------|---------|---------|
| DEFECT-T2-001: static row atmCb in tag | FAIL | **PASS -- RESOLVED** |
| DEFECT-T2-002: ParseAtmModeNameWindow helper | ADVISORY | ADVISORY (unchanged) |
| All 7 SCANS | PASS | PASS |
| signalName="PTT-Copy" invariant | PASS | PASS |
| [Fact] count = 27 | PASS | PASS |
| CYC <= 8 all methods | PASS | PASS |
| DW-B7-03 static row ATM wiring | FAIL | **PASS** |

**OVERALL RETRY VERDICT: VERIFY_PASS**

The sole blocking defect (DEFECT-T2-001) is resolved.
`atmCb` is created at `TradeCopierWindow.cs:302` before `applyBtn` at line 311.
`applyBtn.Tag` at line 312 is `new object[] { instrumentName, leaderCb, followerLb, atmCb }`.
`OnRowApply` at line 523 reads `tag[3]` for both static and dynamic rows correctly.
No new violations introduced. All 7 mandatory scans pass. All DNA rules satisfied.