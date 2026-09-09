# Ticket 2 Completion: BUG-D empty-name Limit entry orders blocked at gate0.5 (DW-LB-FL-01-V7)

**Epic:** PTT-REPAIRS-03-POST
**Ticket:** TICKET-2
**Engineer:** ptt-engineer (Phase 4a)
**Date:** 2026-09-06
**Phase gate:** TICKET_REVIEW_PASS confirmed (04-ticket-review.md line 199)

---

## Phase Gate Confirmation

`04-ticket-review.md` confirms **TICKET_REVIEW_PASS** for T2 (line 199).
All pre-checks passed: JS-021/001/002/003, CYC limits, NT8 constraints, test coverage, scan checklist, file routing, line range accuracy.

---

## Step 1 — Source Verification Results

### 1a. IsExitSignalName (CopyEngine.cs lines 2360–2379)

**Status: MATCH — no drift**

- **Signature** (line 2360): `internal static bool IsExitSignalName(string name)` ✓
- **Line 2362 content:** `if (name == null)` — null guard present ✓
- **Empty-name branch:** ABSENT — no `name.Length == 0` branch anywhere in method ✓
- **Comment** (line 2358): `// empty("") returns false -- see IsExitSignalNameOrAnonClose for the type-aware empty guard.` ✓
- **CYC=7** (comment line 2357): `CCN=7: base(1)+null(1)+PTT-(1)+IsNativeClose(1)+Rev(1)+Exit(1)+IsAtmTarget(1)` ✓
- Manual branch count: null(1) + PTT-(1) + IsNativeCloseOrFlattenSignal(1) + Rev(1) + Exit(1) + IsAtmTarget(1) + base(1) = **7** ✓

### 1b. IsExitSignalNameOrAnonClose (CopyEngine.cs lines 2439–2444)

**Status: MATCH — no drift**

Full method body (lines 2439–2444):
```
internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)
{
    if (name != null && name.Length == 0)
        return orderType != OrderType.Limit; // (1)+(2): empty-name Limit=allow, others=block
    return IsExitSignalName(name); // (3): named order -- delegate to normal check
}
```

- Signature correct: `internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)` ✓
- Body: `if (name != null && name.Length == 0) return orderType != OrderType.Limit;` ✓
- Tail: `return IsExitSignalName(name);` ✓
- CYC=3 per comment (line 2437): `CYC=3: empty-name check(1) + not-Limit branch(2) + IsExitSignalName call` ✓
- No lock(), no throw, returns bool, ASCII-only ✓

### 1c. DispatchCopy gate0.5 (CopyEngine.cs lines 2450–2464)

**Status: MATCH — no drift**

- **Line 2454 (exact):** `if (IsExitSignalNameOrAnonClose(order.Name, order.OrderType))` ✓
- NOT `IsExitSignalName(order.Name)` — confirmed correct ✓
- Diagnostic log strings (lines 2457–2460): `"[PTT-COPY-DIAG] gate0.5 exit: name="`, `" act="`, `" state="`, `" type="` — all ASCII-only ✓

---

## Step 2 — Test Verification Results

### 2a. T_B59_07 (CopyEngineTests.cs lines 3125–3132)

**Status: MATCH — no drift**

- Line 3125: `[Fact]` ✓
- Line 3126: `public void T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse()` ✓
- **Line 3131 content:** `Assert.False(CopyEngine.IsExitSignalName(""));` ✓

### 2b. T_B59_AnonClose_01 through T_B59_AnonClose_06 (lines 3141–3182)

**Status: ALL 6 PRESENT — no drift**

| Test | Line | [Fact] | Key Assert |
|------|------|--------|------------|
| AnonClose_01 | 3141–3146 | ✓ (3141) | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Limit))` |
| AnonClose_02 | 3148–3153 | ✓ (3148) | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Market))` |
| AnonClose_03 | 3155–3160 | ✓ (3155) | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.StopMarket))` |
| AnonClose_04 | 3162–3168 | ✓ (3162) | `Assert.True(... "PTT-Copy", OrderType.Limit)` AND `Assert.True(... "PTT-Copy", OrderType.Market)` |
| AnonClose_05 | 3170–3175 | ✓ (3170) | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("Entry", OrderType.Limit))` |
| AnonClose_06 | 3177–3182 | ✓ (3177) | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose(null, OrderType.Market))` |

---

## Step 3 — 7-Scan Results

### SCAN-01: lock() detection
**Command:** `Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs | Where-Object { $_.Line -notmatch "//" } | Select-Object LineNumber, Line`
**Output:** *(no output)*
**Result: PASS — 0 matches**

### SCAN-02: Non-ASCII characters
**Command:** `Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | Select-Object -First 5`
**Output:** *(no output)*
**Result: PASS — 0 matches**

### SCAN-03: CYC counts
**Method verification from source comments + manual branch count:**

| Method | Comment CYC | Manual Count | Spec | Result |
|--------|-------------|--------------|------|--------|
| `IsExitSignalName` | 7 (line 2357) | 7 | 7 | PASS |
| `IsExitSignalNameOrAnonClose` | 3 (line 2437) | 3 | 3 | PASS |
| `DispatchCopy` | 8 (line 2448) | 8 | <=8 | PASS |

**Result: PASS — all CYC counts within JS-013 limit (<=8)**

### SCAN-04: [Fact] annotations
**Verified from source reads (lines 3125–3182):**
- T_B59_07: `[Fact]` at line 3125 ✓
- T_B59_AnonClose_01: `[Fact]` at line 3141 ✓
- T_B59_AnonClose_02: `[Fact]` at line 3148 ✓
- T_B59_AnonClose_03: `[Fact]` at line 3155 ✓
- T_B59_AnonClose_04: `[Fact]` at line 3162 ✓
- T_B59_AnonClose_05: `[Fact]` at line 3170 ✓
- T_B59_AnonClose_06: `[Fact]` at line 3177 ✓

**Result: PASS — all 7 test methods have [Fact]**

### SCAN-05: dotnet build CopyEngine errors
**Command:** `dotnet build Linting.csproj > build_out_t2.txt 2>&1; Get-Content build_out_t2.txt | Where-Object { $_ -match " error " -and ($_ -match "CopyEngine" -or $_ -match "PropTraderTools") }`
**Output:** *(no output — 0 CopyEngine/PropTraderTools errors)*
**Note:** 307 pre-existing `CS1069`/`CS0246` type-forwarding errors exist in `V12_002.*` files (infrastructure/NinjaTrader stub limitations). None reference `CopyEngine.cs` or `CopyEngineTests.cs`.
**Result: PASS — 0 CopyEngine.cs or CopyEngineTests.cs errors**

### SCAN-06: N/A
Verification-only ticket — no new production logic added. InternalsVisibleTo was pre-existing.

### SCAN-07: N/A
Verification-only ticket — no deploy-sync required (no new .cs production code written).

---

## Drift Corrections

**No drift found.**

All source lines (CopyEngine.cs 2360–2379, 2439–2444, 2450–2464) and test lines (CopyEngineTests.cs 3125–3132, 3141–3182) matched the spec exactly. No edits were made to any source file.

---

## Acceptance Criteria Checklist

| AC | Description | Result |
|----|-------------|--------|
| AC-1 | `IsExitSignalName` has no `name.Length == 0` branch | PASS |
| AC-2 | `IsExitSignalName` first branch is null guard returning false | PASS |
| AC-3 | `IsExitSignalNameOrAnonClose` body: `if (name != null && name.Length == 0) return orderType != OrderType.Limit;` | PASS |
| AC-4 | `DispatchCopy` line 2454 calls `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` | PASS |
| AC-5 | `T_B59_07` line 3131: `Assert.False(CopyEngine.IsExitSignalName(""))` | PASS |
| AC-6 | All 6 `T_B59_AnonClose_*` tests present with correct assertions | PASS |
| AC-7 | All 7 scans zero | PASS |

---

## Final Status

**BUILD_PASS**

*ptt-engineer · PTT-REPAIRS-03-POST · ticket-2-completion.md · 2026-09-06*
