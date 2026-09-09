# Ticket 2 Verification: BUG-D empty-name Limit entry orders blocked at gate0.5 (DW-LB-FL-01-V7)

**Epic:** PTT-REPAIRS-03-POST
**Ticket:** TICKET-2
**Verifier:** ptt-verifier (Phase 4b)
**Date:** 2026-09-06
**Engineer completion status:** BUILD_PASS (confirmed at ticket-2-completion.md line 159)

---

## Phase Gate Pre-Check

`ticket-2-completion.md` line 159 states **BUILD_PASS**.
All pre-checks confirmed in Ph4a report: JS-021/001/002/003, CYC limits, NT8 constraints,
test coverage, scan checklist, file routing, line range accuracy.

---

## VERIFY STEP 1 — SOURCE (independent reads)

### 1a. IsExitSignalName — CopyEngine.cs lines 2360–2379

**Verification: PASS**

Line 2362 (exact quote):
```
            if (name == null)
```
Null guard present. Returns `false` on null — first branch confirmed.

No `name.Length == 0` branch anywhere in lines 2362–2379. Scanned entire method body independently. ABSENT confirmed.

CYC manual count (independent):
- base: 1
- `if (name == null)`: +1
- `if (name.StartsWith("PTT-", StringComparison.Ordinal))`: +1
- `if (IsNativeCloseOrFlattenSignal(name))`: +1
- `if (name.StartsWith("Rev", StringComparison.Ordinal))`: +1
- `if (name.StartsWith("Exit", StringComparison.Ordinal))`: +1
- `if (IsAtmTargetSignalName(name))`: +1
**CYC = 7** ? (matches spec and Ph4a report)

### 1b. IsExitSignalNameOrAnonClose — CopyEngine.cs lines 2439–2444

**Verification: PASS**

Full method body (independent read, lines 2439–2444):
```csharp
        internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)
        {
            if (name != null && name.Length == 0)
                return orderType != OrderType.Limit; // (1)+(2): empty-name Limit=allow, others=block
            return IsExitSignalName(name); // (3): named order -- delegate to normal check
        }
```

- Signature: `internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)` ?
- Line 2441: `if (name != null && name.Length == 0)` ?
- Line 2442: `return orderType != OrderType.Limit;` ?
- Line 2443: `return IsExitSignalName(name);` ?
- No lock(), no throw, returns bool, ASCII-only ?

CYC manual count (independent):
- base: 1
- `if (name != null` — first condition: +1
- `&& name.Length == 0` — second condition (&&): +1
**CYC = 3** ?

### 1c. DispatchCopy gate0.5 — CopyEngine.cs line 2454

**Verification: PASS**

Line 2454 (exact quote):
```
            if (IsExitSignalNameOrAnonClose(order.Name, order.OrderType))
```

Confirmed: calls `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)`.
NOT `IsExitSignalName(order.Name)`. ?

CYC manual count for DispatchCopy (lines 2450–2550):
- base: 1
- gate0.5 `if (IsExitSignalNameOrAnonClose(...))`: +1
- gate3 `if (!IsDispatchTriggerState(...))`: +1
- gate4 `if (!IsDispatchableOrderType(...))`: +1
- gate5 `if (IsLiveEntryBlocked_Check(...))`: +1
- `foreach (var acc in rule.FollowerAccounts)`: +1
- `if (ShouldSkipFollower(...))`: +1
- `if (dispatched > 0)`: +1
**CYC = 8** ? (at spec limit, not exceeded)

---

## VERIFY STEP 2 — TESTS (independent reads)

### 2a. T_B59_07 — CopyEngineTests.cs lines 3125–3132

**Verification: PASS**

`[Fact]` at line 3125 ?

Line 3131 (exact quote):
```
            Assert.False(CopyEngine.IsExitSignalName(""));
```
?

### 2b. T_B59_AnonClose_01 through _06 — CopyEngineTests.cs lines 3141–3182

**Verification: PASS — all 6 present with correct assertions**

| Test | [Fact] Line | Assert Line (exact quote) |
|------|-------------|---------------------------|
| AnonClose_01 | 3141 | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Limit));` (line 3145) |
| AnonClose_02 | 3148 | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Market));` (line 3152) |
| AnonClose_03 | 3155 | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.StopMarket));` (line 3159) |
| AnonClose_04 | 3162 | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("PTT-Copy", OrderType.Limit));` (line 3166) AND `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("PTT-Copy", OrderType.Market));` (line 3167) |
| AnonClose_05 | 3170 | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("Entry", OrderType.Limit));` (line 3174) |
| AnonClose_06 | 3177 | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose(null, OrderType.Market));` (line 3181) |

---

## VERIFY STEP 3 — INDEPENDENT 7-SCAN

### SCAN-01: lock() detection

**Command (independent):**
```
Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs | Where-Object { $_.Line -notmatch "//" } | Select-Object LineNumber, Line
```
**Output:** *(no output — 0 matches)*
**Result: PASS**

### SCAN-02: Non-ASCII characters

**Command (independent):**
```
Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | Select-Object -First 5
```
**Output:** *(no output — 0 matches)*
**Result: PASS**

### SCAN-03: CYC counts (independent manual count from source)

| Method | My Manual Count | Spec | Result |
|--------|----------------|------|--------|
| `IsExitSignalName` | 7 | 7 | PASS |
| `IsExitSignalNameOrAnonClose` | 3 | 3 | PASS |
| `DispatchCopy` | 8 | <=8 | PASS |

**Result: PASS — all CYC counts within JS-013 limit (<=8)**

### SCAN-04: [Fact] annotations (independent grep)

**Command (independent):**
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "\[Fact\]" | Where-Object { $_.LineNumber -ge 3124 -and $_.LineNumber -le 3183 }
```
**Output:**
```
3125  [Fact]
3141  [Fact]
3148  [Fact]
3155  [Fact]
3162  [Fact]
3170  [Fact]
3177  [Fact]
```
**Result: PASS — 7 [Fact] attributes confirmed**

### SCAN-05: dotnet build errors

**Command (independent):**
```
dotnet build Linting.csproj 2>&1 | Out-File build_out_verify.txt
Get-Content build_out_verify.txt | Select-String -Pattern "CopyEngine"
```
**Output:** *(no output — 0 CopyEngine matches)*

Secondary check:
```
Get-Content build_out_verify.txt | Select-String -Pattern "error" | Where-Object { $_ -notmatch "V12_002" -and $_ -notmatch "^\d+ Error" }
```
**Output:** *(no output)*

All 307 build errors are pre-existing V12_002 infrastructure stub errors. None reference CopyEngine.cs or CopyEngineTests.cs.
**Result: PASS**

### SCAN-06: N/A

Ticket 2 is a verification/assertion-only ticket. No new production logic added. No new DateTime usage.

### SCAN-07: N/A

Ticket 2 is a verification/assertion-only ticket. No new block patterns. No deploy-sync required.

---

## DNA Rule Check (Jane Street — all VERIFY_FAIL conditions)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() | SCAN-01: 0 matches in non-comment lines | PASS |
| JS-023/025 Monitor/Mutex/Semaphore | None present in scope | PASS |
| JS-001 throw in gate/dispatch | No throw in IsExitSignalName, IsExitSignalNameOrAnonClose, DispatchCopy | PASS |
| JS-002 return null | All methods return bool, no null return possible | PASS |
| JS-003 magic string | No mode/state discrimination via magic string | PASS |
| JS-008/009 mutable struct | No new struct definitions | PASS |
| JS-009 SolidColorBrush unfrosen | No UI brushes in scope | PASS |
| JS-010 non-private constructor | IsExitSignalNameOrAnonClose is static, no constructor | PASS |
| NT8: async/await forbidden | Not present | PASS |
| NT8: sealed on TradeCopierWindow | Not applicable to this ticket | PASS |
| NT8: FontFamily= | Not present | PASS |
| NT8: #RRGGBB hex | Not present | PASS |
| NT8: CreateOrder without PTT- | Not present | PASS |
| NT8: DateTime.Now | Not present | PASS |
| ASCII-only | SCAN-02: 0 non-ASCII chars | PASS |
| CYC <= 8 | SCAN-03: 7, 3, 8 | PASS |

---

## Ph4a vs Ph4b Comparison (Discrepancy Check)

| Item | Ph4a Report | Ph4b (My) Finding | Match? |
|------|-------------|-------------------|--------|
| Line 2362 content | `if (name == null)` | `if (name == null)` | ? |
| Empty-name branch absent | ABSENT | ABSENT | ? |
| IsExitSignalNameOrAnonClose body | As quoted | Exact match | ? |
| Line 2454 call | `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` | Exact match | ? |
| T_B59_07 line 3131 | `Assert.False(CopyEngine.IsExitSignalName(""))` | Exact match | ? |
| AnonClose_01–06 asserts | As table in Ph4a | All 6 exact match | ? |
| SCAN-01 lock() | 0 matches | 0 matches | ? |
| SCAN-02 non-ASCII | 0 matches | 0 matches | ? |
| SCAN-03 CYC | 7/3/8 | 7/3/8 | ? |
| SCAN-04 [Fact] | 7 confirmed | 7 confirmed | ? |
| SCAN-05 build | 0 CopyEngine errors | 0 CopyEngine errors | ? |

**No discrepancies found between Ph4a self-report and Ph4b independent verification.**

---

## Acceptance Criteria

| AC | Description | Result |
|----|-------------|--------|
| AC-1 | `IsExitSignalName` has no `name.Length == 0` branch | PASS |
| AC-2 | `IsExitSignalName` first branch is null guard returning false | PASS |
| AC-3 | `IsExitSignalNameOrAnonClose` line 2441: `if (name != null && name.Length == 0) return orderType != OrderType.Limit;` | PASS |
| AC-4 | `DispatchCopy` line 2454 calls `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` | PASS |
| AC-5 | `T_B59_07` line 3131: `Assert.False(CopyEngine.IsExitSignalName(""))` | PASS |
| AC-6 | All 6 `T_B59_AnonClose_*` tests present with correct assertions | PASS |
| AC-7 | All 7 scans zero / within limits | PASS |

---

## Blocking Items

**None.**

---

## Final Gate

**VERIFY_PASS**

*ptt-verifier · PTT-REPAIRS-03-POST · ticket-2-verification.md · 2026-09-06*
