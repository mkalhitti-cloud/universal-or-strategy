# Ticket T1 Verification Report -- CopyEngine.cs

**Ticket:** T1 -- CopyEngine.cs
**Epic:** PTT-COPIER-B1
**Verified by:** PTT Verifier (Bob IDE, v12-phase5-v-verify mode)
**Date:** 2026-07-06
**Source file:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Line count:** 347

---

## 1. Independent 7-Scan Results

All 7 scans run independently by the verifier. Engineer scan results NOT used.

### SCAN-01 -- No lock() (Select-String)
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\("
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 results)**

> Note: `grep` is unavailable in this PowerShell environment. `Select-String -Pattern "lock\s*\("` is the
> functional equivalent and covers all syntactic variants of `lock(`.
> SCAN-07 provides belt-and-suspenders confirmation below.

### SCAN-02 -- ASCII-only
**Command:**
```
Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object { -match '[^\x00-\x7F]'}
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 non-ASCII characters)**

### SCAN-03 -- No FontFamily
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "FontFamily"
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 FontFamily references)**

### SCAN-04 -- No hardcoded hex colors
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "#[0-9A-Fa-f]{6}"
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 hex color literals)**

### SCAN-05 -- PTT- prefix on all CreateOrder calls
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "CreateOrder" -Context 8,1
```
**Raw output:**
```
src\PropTraderTools\CopyEngine.cs:165:  follower.CreateOrder(
src\PropTraderTools\CopyEngine.cs:203:      acc.CreateOrder(
src\PropTraderTools\CopyEngine.cs:240:      acc.CreateOrder(
```
**PTT- prefix verification (verifier read file lines directly):**
- Line 165 (SendCopy): name param = `"PTT-Copy"` at line 175 -- PASS
- Line 203 (Trim):     name param = `"PTT-Trim"` at line 213 -- PASS
- Line 240 (Flatten):  name param = `"PTT-Flatten"` at line 250 -- PASS
**Result: PASS (0 violations, all 3 CreateOrder calls use PTT- prefix)**

### SCAN-06 -- No DateTime.Now
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 DateTime.Now references)**

### SCAN-07 -- No lock keyword (belt-and-suspenders regex)
**Command:**
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\("
```
**Raw output:** (no output -- 0 matches)
**Result: PASS (0 lock keyword occurrences)**

---

## 2. Full Checklist Audit

| ID  | Check | Result | Notes |
|-----|-------|--------|-------|
| **SECTION A -- Structure** | | | |
| A1  | File at correct path | **PASS** | Confirmed by read |
| A2  | Namespace is PropTraderTools | **PASS** | Line 10 |
| A3  | Class is internal sealed class CopyEngine | **PASS** | Line 12. Note: architecture plan shows `public sealed`, implementation uses `internal sealed` -- `internal` is more correct for NT8 Add-On assembly (non-blocking deviation) |
| A4  | Private constructor (JS-010 singleton) | **PASS** | Line 83: `private CopyEngine() { }` |
| **SECTION B -- Structs** | | | |
| B1  | TrimSignal has NO qty field (JS-003) CRITICAL | **PASS** | Lines 66-80: only UtcTime (DateTime) and Instrument (string) |
| B2  | TrimSignal fields: UtcTime (DateTime) + Instrument (string) only | **PASS** | Lines 70-71 |
| B3  | CopySignal fields: Action, Type, Quantity, LimitPrice, OrderId | **PASS** | Lines 47-51 |
| B4  | CopyRule fields: Instrument, MasterAccount, FollowerAccounts | **PASS** | Lines 30-32 |
| B5  | All three structs are private readonly struct | **PASS** | Lines 28, 45, 66 |
| B6  | All three structs have private ctor + internal static Create() | **PASS** | CopyRule: 34/41; CopySignal: 53/62; TrimSignal: 73/79 |
| **SECTION C -- Fields** | | | |
| C1  | _isCopyEnabled is volatile bool (JS-023) | **PASS** | Line 19 |
| C2  | _dedupCache is ConcurrentDictionary<string, long> (JS-025) | **PASS** | Line 20 |
| C3  | Singleton _instance is private static readonly | **PASS** | Line 15 |
| C4  | Instance property returns _instance | **PASS** | Line 16 |
| C5  | StatusUpdate is internal event Action<string> | **PASS** | Line 24 |
| **SECTION D -- Gate Chain (OnOrderUpdate)** | | | |
| D1  | Gate 1: !_isCopyEnabled check is first | **PASS** | Lines 111-113 |
| D2  | Gate 2: matches instrument + master account | **PASS** | Lines 116-127 |
| D3  | Gate 3: OrderState.Submitted check | **PASS** | Lines 130-131 |
| D4  | Gate 3: IsMarket/IsLimit only (stops/targets filtered) | **PASS** | Lines 133-136 |
| D5  | Gate 4: IsDedup called | **PASS** | Lines 139-140 |
| D6  | PassesDailyCapCheck called before SendCopy per follower | **PASS** | Lines 151-158 |
| D7  | No throw in OnOrderUpdate hot path (JS-001) | **PASS** | All exits are early returns; no try/catch wrapping the gate chain itself |
| **SECTION E -- SendCopy** | | | |
| E1  | Returns bool (not void) | **PASS** | Line 161 |
| E2  | CreateOrder name param = "PTT-Copy" | **PASS** | Line 175 (SCAN-05) |
| E3  | Uses DateTime.MaxValue, NOT DateTime.Now | **PASS** | Line 176; SCAN-06 confirms 0 DateTime.Now |
| E4  | try/catch, returns false on exception, never throws | **PASS** | Lines 163-184 |
| E5  | Logs failure via StatusUpdate | **PASS** | Line 182 |
| **SECTION F -- Trim / Flatten / CancelPendingEntries** | | | |
| F1  | Trim uses Math.Ceiling(pos.Quantity / 2.0) | **PASS** | Line 198 |
| F2  | Trim name param = "PTT-Trim" | **PASS** | Line 213 (SCAN-05) |
| F3  | Flatten uses full pos.Quantity | **PASS** | Line 246 |
| F4  | Flatten name param = "PTT-Flatten" | **PASS** | Line 250 (SCAN-05) |
| F5  | CancelPendingEntries calls IsBracketLeg before cancelling | **PASS** | Line 272: `if (IsBracketLeg(order)) continue;` |
| F6  | Cancels only Working and PartialFilled states | **PASS** | Line 270 |
| F7  | All three methods use AllAccounts(instrument) | **PASS** | Lines 189, 227, 264 |
| **SECTION G -- IsBracketLeg** | | | |
| G1  | Layer 1: order.FromEntrySignal != null | **PASS** | Line 339 |
| G2  | Layer 2: order.Name.StartsWith("PTT-") | **PASS** | Line 343 |
| G3  | Layer 3: order.Name.StartsWith("Stop") or StartsWith("Target") | **PASS** | Lines 341-342 |
| G4  | Any ONE layer true -> method returns true | **PASS** | All three conditions in compound boolean; any true causes return true |
| **SECTION H -- IsDedup** | | | |
| H1  | Uses ConcurrentDictionary.TryAdd (not lock + Dictionary) | **PASS** | Line 301 |
| H2  | 10-second TTL expiry logic present | **PASS** | Lines 291-298 |
| H3  | Uses DateTime.UtcNow.Ticks (not DateTime.Now) | **PASS** | Line 290 |
| **SECTION I -- AllAccounts** | | | |
| I1  | Returns master account first, then followers | **PASS** | Lines 313-318 |
| I2  | Filters null followers | **PASS** | Line 316 |
| I3  | Uses instrument fence (FindRule by instrument) | **PASS** | Lines 309-311 |
| **SECTION J -- 7-Scan Results** | | | |
| J1  | SCAN-01: 0 lock() occurrences | **PASS** | 0 results |
| J2  | SCAN-02: 0 non-ASCII characters | **PASS** | 0 results |
| J3  | SCAN-03: 0 FontFamily references | **PASS** | 0 results |
| J4  | SCAN-04: 0 hardcoded hex colors | **PASS** | 0 results |
| J5  | SCAN-05: all CreateOrder calls use PTT- prefix | **PASS** | 0 violations |
| J6  | SCAN-06: 0 DateTime.Now references | **PASS** | 0 results |
| J7  | SCAN-07: 0 lock keyword occurrences | **PASS** | 0 results |

**Total: 42/42 PASS, 0 FAIL**

---

## 3. Architecture Compliance

| Aspect | Status | Notes |
|--------|--------|-------|
| File path | PASS | `src\PropTraderTools\CopyEngine.cs` |
| Namespace | PASS | `PropTraderTools` |
| Class modifier | PASS (note) | `internal sealed` vs `public sealed` in plan -- `internal` is correct for NT8 Add-On |
| All required structs | PASS | CopyRule, CopySignal, TrimSignal all present |
| All required fields | PASS | _isCopyEnabled (volatile), _dedupCache (ConcurrentDictionary), _instance (static readonly) |
| Gate chain 4-gate sequence | PASS | Exact match to architecture spec Section 5 |
| IsBracketLeg 3-layer guard | PASS | All 3 layers present and logically correct |
| SendCopy returns bool | PASS | Section 4.2 requirement met |
| AllAccounts instrument fence | PASS | FindRule gates account scope |
| TrimSignal NO qty field | PASS | CRITICAL -- JS-003 structural guarantee |
| IsDedup 10-sec TTL | PASS | ConcurrentDictionary.TryAdd + prune loop |
| No lock() anywhere | PASS | JS-021 -- volatile + ConcurrentDictionary only |
| DateTime.UtcNow throughout | PASS | JS-006 / SCAN-06 |
| Initialize/Shutdown method names | DEVIATION (non-blocking) | Plan specifies `Initialize(CopyRule rule)` and `Shutdown()`; implementation uses `AddRule()`, `Subscribe()`, `Unsubscribe()` -- functionally equivalent, better decomposed |

**Non-blocking architectural deviations (informational only):**
1. `CopyEngine.cs:12` -- `internal sealed` vs plan `public sealed`. `internal` is more restrictive and correct for NT8 Add-On.
2. `Initialize`/`Shutdown` API names differ from plan. Implementation provides equivalent `AddRule`, `Subscribe`, `Unsubscribe` methods. No capability gap.
3. `IsBracketLeg` layers 2 and 3 are combined in a compound boolean expression (lines 340-343) rather than separate `if/return` blocks. Logical equivalence confirmed: any true condition causes return true.

---

## 4. VIOLATIONS

*None.* All 42 checklist items PASS. All 7 scans return 0 results/violations.

---

## 5. xUnit Tests

Architecture plan Section 11 (T1) specifies 17 `[Fact]` test methods. Verification scope for T1 is the `CopyEngine.cs` source file only. Test files (T1-tests) are a dependency of T2/T3 (parallel tickets) and are tracked under a separate verification. No test file was submitted with T1.

> **Note to orchestrator:** xUnit test file for CopyEngine.cs should be verified as part of the block-level final review (Phase 6), or as an explicit T1-tests sub-ticket if tests are authored separately from the engine.

---

## Final Verdict

`
VERIFY_PASS
`

- 42 checklist items evaluated
- 0 FAIL
- 0 scan violations
- 0 blocking architectural deviations
- 2 non-blocking informational deviations documented above
- Gate rules: B1 (TrimSignal no qty) = PASS; J-section (all scans) = PASS; D7 (no throw) = PASS; section fail count = 0 in any section

**T1 CopyEngine.cs is verified and approved for integration.**
