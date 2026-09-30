# BWAVE-DW LaneC — Ticket C-6 Verification Report

**Ticket**: C-6 — B77Tests.cs Opcode and Helper-Scan Fixes
**DW Items**: DW-C39-13, DW-C39-14
**File Verified**: `src/PropTraderTools/TradeCopierPanelB77Tests.cs` (ROOT level)
**Verifier**: ptt-verifier
**Date**: 2026-09-04
**Layer**: 3 (independent re-run — does NOT trust Layer 2 engineer self-report)

---

## SCOPE

Ticket C-6 modifies only `src/PropTraderTools/TradeCopierPanelB77Tests.cs`.
No production `.cs` files touched. NT8 sync and F5 not required.

---

## 1. DW-C39-13: T_B77_TPL_05 Opcode Fix Verification

**Source evidence** (lines 119-156 as read independently):

### 1a. Old opcode 0x72 (ldstr) GONE — CONFIRMED

Independent scan:
```
Select-String -Path src\PropTraderTools\TradeCopierPanelB77Tests.cs -Pattern "0x72"
```
Result: **0 results**. The ldstr opcode literal `0x72` is not present anywhere in the file.

### 1b. New opcode 0x7E (ldsfld) PRESENT — CONFIRMED

Line 130:
```csharp
if (il[i] == 0x7E) // ldsfld
```
The opcode byte literal `0x7E` is present at line 130 of T_B77_TPL_05. Independently verified
by direct file read.

### 1c. Field check uses name-based comparison — CONFIRMED

Lines 137-141:
```csharp
if (
    field != null
    && field.Name == "Empty"
    && field.DeclaringType == typeof(string)
)
```
Name-based field identity: `field.Name == "Empty"` AND `field.DeclaringType == typeof(string)`.
No MetadataToken comparison present.

### 1d. NO MetadataToken comparison (advisory compliance) — CONFIRMED

Independent scan:
```
Select-String -Path src\PropTraderTools\TradeCopierPanelB77Tests.cs -Pattern "MetadataToken"
```
Result: **0 results**. No MetadataToken comparison anywhere in the file.

### 1e. Acceptance Criterion: Would test FAIL if string.Empty replaced with null?

**YES — CONFIRMED.**

If production `GetLeaderAtmTemplateName` returned `null` instead of `string.Empty`:
- The method would NOT emit an `ldsfld System.String::Empty` instruction.
- The scan loop at lines 128-151 searches for `0x7E` (ldsfld) with operand resolving to
  `field.Name == "Empty" && field.DeclaringType == typeof(string)`.
- `return null` compiles to `ldnull` (0x14), not `ldsfld` (0x7E). No match found.
- `foundStringEmpty` remains `false`.
- `Assert.True(foundStringEmpty, ...)` at line 153-156 would FAIL.
- Additionally, line 115: `Assert.Equal(string.Empty, result)` and line 116:
  `Assert.NotNull(result)` would also fail on the null-invoke path (branch 1).
- Test correctly guards against null regression. ✓

**DW-C39-13: CLOSED. All 4 sub-checks PASS.**

---

## 2. DW-C39-14: T_B77_TPL_04 Scan Target Fix Verification

**Source evidence** (lines 74-97 as read independently):

### 2a. Scan target is TryGetAtmNameFromSelector — CONFIRMED

Lines 77-80:
```csharp
var helper = typeof(TradeCopierPanel).GetMethod(
    "TryGetAtmNameFromSelector",
    BindingFlags.NonPublic | BindingFlags.Static
);
```
The method lookup now targets `TryGetAtmNameFromSelector` (the correct helper), NOT
`GetLeaderAtmTemplateName` (the old wrong target).

### 2b. Method lookup is name-based, not token-based — CONFIRMED

The lookup uses `typeof(TradeCopierPanel).GetMethod("TryGetAtmNameFromSelector", ...)` —
a stable string name lookup. No MetadataToken used. `IlContainsCallvirtByName` (lines 164-188)
resolves methods by `resolved.Name == methodName`, not by token comparison.

### 2c. Graceful null-guard present — CONFIRMED

Lines 83-84:
```csharp
if (helper == null)
    return;
```
If `TryGetAtmNameFromSelector` does not exist, the test returns early rather than throwing.

### 2d. Acceptance Criterion: Would test FAIL if get_SelectedAtmStrategy reintroduced?

**YES — CONFIRMED.**

If `TryGetAtmNameFromSelector` were modified to call `get_SelectedAtmStrategy` (regression):
- The IL body of `TryGetAtmNameFromSelector` would contain a `callvirt` (0x6F) instruction
  whose token resolves to a method with `Name == "get_SelectedAtmStrategy"`.
- `IlContainsCallvirtByName(il, module, "get_SelectedAtmStrategy")` (line 94) would return `true`.
- `Assert.False(true, ...)` at line 93-96 would FAIL.
- The regression guard fires correctly. ✓

Note: If `TryGetAtmNameFromSelector` does not exist at runtime, the test returns early
(line 83-84) — this is the "repair assumption unverifiable" path. The ticket and architect plan
both accept this as intentional: the test documents the invariant but does not false-fail when
the helper is absent.

**DW-C39-14: CLOSED. All sub-checks PASS.**

---

## 3. Independent 7-Scan Results (Layer 3)

| Scan | Check | Command | Result | Status |
|------|-------|---------|--------|--------|
| SCAN-01 | No `lock(` in code | `Select-String -Pattern "lock\(" -SimpleMatch` | **0 results** | PASS |
| SCAN-02 | No `async void` in code | `Select-String -Pattern "async void"` | 1 hit — line 9 comment only (`// JS-033: no async void`) — zero in executable code | PASS |
| SCAN-03 | No `return null` in code | `Select-String -Pattern "return null"` | 2 hits — both in comments (lines 9, 163) — zero in executable code | PASS |
| SCAN-04 | No `throw new` in code | `Select-String -Pattern "throw new"` | 1 hit — line 9 comment only — zero in executable code | PASS |
| SCAN-05 | CYC <= 8 | Manual analysis of modified methods | See below | PASS |
| SCAN-06 | ASCII-only | PowerShell byte scan `$b \| Where { $_ -gt 127 }` | **0 non-ASCII bytes** | PASS |
| SCAN-07 | xUnit only | `Select-String -Pattern "using NUnit\|using Microsoft\.VisualStudio"` | **0 results** | PASS |

### SCAN-05: CYC Analysis

**T_B77_TPL_04** (lines 74-97):
- Decision points: 1 null-guard `if (helper == null)` + `Assert.NotNull` + `Assert.True` (il.Length > 0) + `Assert.False` (delegate to helper) = CYC = 2 for the test body itself. Well under 8. ✓

**T_B77_TPL_05** (lines 103-157):
- Decision points: 1 `for` loop + 1 `if (il[i] == 0x7E)` + 1 `try/catch` + 1 `if (field != null && ...)` (counts as 2 for `&&`) = CYC = 5. Under 8. ✓

**IlContainsCallvirtByName** (lines 164-188):
- Per engineer comment at line 162: `CYC = 4: loop + opcode-if + resolve-try + name-if`.
- Independent count: `for` loop (1) + `if (il[i] == 0x6F)` (1) + `try` (1) + `if (resolved != null && resolved.Name == methodName)` (2 for `&&`) = 5 decision points = CYC 5. (The engineer's own annotation says 4; my independent count is 5 due to counting the `&&` short-circuit. Either way, well under 8.) ✓

All methods: **CYC <= 8. PASS.**

### SCAN-02/03/04 — Comment-Only Hits (No Violations)

The hits for `async void`, `return null`, and `throw new` all appear in the compliance header
comment at line 9:
```
// JS-021: no lock. JS-001: no throw new. JS-002: no return null. JS-033: no async void.
```
This is a compliance declaration comment, not executable code. **Zero code-level violations.**

---

## 4. Acceptance Criteria Assessment

| Criterion | Ticket Requirement | Layer 3 Finding | Met? |
|-----------|-------------------|-----------------|------|
| DW-C39-13: opcode changed to 0x7E (ldsfld) | Must use ldsfld not ldstr | Line 130: `if (il[i] == 0x7E) // ldsfld` | YES |
| DW-C39-13: field check name-based | `field.Name == "Empty" && field.DeclaringType == typeof(string)` | Lines 138-140: confirmed | YES |
| DW-C39-13: no MetadataToken | 0 MetadataToken comparisons | 0 results from scan | YES |
| DW-C39-13: test fails on null regression | Would FAIL if return null | Yes — ldsfld not emitted, Assert.True fails | YES |
| DW-C39-14: scans TryGetAtmNameFromSelector | Not GetLeaderAtmTemplateName | Line 78: `"TryGetAtmNameFromSelector"` | YES |
| DW-C39-14: test fails if getter reintroduced | Would FAIL if get_SelectedAtmStrategy present | Yes — Assert.False fires | YES |
| DW-C39-14: method lookup stable (name-based) | Not token-based | String name lookup + name-based IL resolution | YES |

---

## 5. Layer 2 vs Layer 3 Cross-Check

Engineer (Layer 2) reported in `ticket-C6-completion.md`:

| Claim | Layer 2 | Layer 3 | Match? |
|-------|---------|---------|--------|
| SCAN-01 (lock) | 0 results | 0 results | ✓ MATCH |
| SCAN-02 (async void) | 0 in code (1 in comment) | 0 in code (1 in comment line 9) | ✓ MATCH |
| SCAN-03 (return null) | 0 in code (2 in comments) | 0 in code (2 in comments lines 9, 163) | ✓ MATCH |
| SCAN-04 (throw new) | 0 in code (1 in comment) | 0 in code (1 in comment line 9) | ✓ MATCH |
| SCAN-05 (CYC) | IlContainsCallvirtByName CYC=4, tests CYC<=3 | IlContainsCallvirtByName CYC=4 (my count 5 — see note below) | MINOR DISCREPANCY (see note) |
| SCAN-06 (non-ASCII) | 0 non-ASCII bytes | 0 non-ASCII bytes | ✓ MATCH |
| SCAN-07 (xUnit only) | 0 results | 0 results | ✓ MATCH |
| Opcode 0x7E present | YES | YES — line 130 | ✓ MATCH |
| Scan target TryGetAtmNameFromSelector | YES | YES — line 78 | ✓ MATCH |
| No MetadataToken | YES | YES — 0 scan results | ✓ MATCH |

**SCAN-05 Minor Discrepancy Note**:
The engineer reported `IlContainsCallvirtByName` CYC = 4. My independent count is 5 (the `&&`
short-circuit in `if (resolved != null && resolved.Name == methodName)` adds 1 decision point).
Both 4 and 5 are under the CYC <= 8 limit. This is a minor counting difference with NO compliance
impact. Not a violation.

**No material discrepancies.** All 7 scans align between Layer 2 and Layer 3.

---

## 6. DW Item Closure

| DW Item | Status | Evidence |
|---------|--------|----------|
| DW-C39-13 | **CLOSED** | T_B77_TPL_05 line 130: `0x7E` (ldsfld). Name-based field check lines 138-140. No MetadataToken. Test fails on null regression. |
| DW-C39-14 | **CLOSED** | T_B77_TPL_04 line 78: scans `TryGetAtmNameFromSelector`. Name-based `IlContainsCallvirtByName`. Test fails if get_SelectedAtmStrategy reintroduced. Null-guard early return at line 83-84. |

---

## 7. Architecture Compliance

- **JS-021 (No lock)**: 0 lock() in executable code. ✓
- **JS-001 (No throw new)**: 0 throw new in executable code. ✓
- **JS-002 (No return null)**: 0 return null in executable code. ✓
- **JS-033 (No async void)**: 0 async void in executable code. ✓
- **xUnit only**: `using Xunit;` only. No NUnit or MSTest. ✓
- **CYC <= 8**: All methods <= 8. ✓
- **ASCII-only**: 0 non-ASCII bytes. ✓
- **No MetadataToken**: 0 MetadataToken usages. ✓
- **Scope gate**: Only test file modified. No production code changes. ✓

---

## RESULT: VERIFY_PASS

All checks PASS. No violations found. DW-C39-13 and DW-C39-14 are confirmed CLOSED.

*ptt-verifier | BWAVE-DW LaneC | Ticket C-6 | 2026-09-04*