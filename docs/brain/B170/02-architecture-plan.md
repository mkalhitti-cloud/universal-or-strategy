# B170 Architecture Plan — DW-B170-01 FOLLOWER-STOP-LOST-ON-TGT-DRAG

Status: PLAN_COMPLETE

---

## 1. Root Cause

`FindLeaderStopPrice` returns 0.0 because its predicate only accepts `OrderState.Working`,
but ATM bracket stop orders are in `OrderState.Accepted` while a position is open; the fix
is to add `|| order.OrderState == OrderState.Accepted` to the predicate.

---

## 2. Exact Line Numbers — src/PropTraderTools/CopyEngine.cs

| Item | Line |
|------|------|
| `FindLeaderStopPrice` method definition | 4412 |
| Predicate: `order.OrderState == OrderState.Working` | 4423 |
| `CreateFollowerReplacementStop` guard: `if (stopPrice <= 0.0)` | 4445 |
| `ExecutePhaseCStopReplacement` call to `FindLeaderStopPrice` | 4510 |
| `FindLeaderStopPriceTestable` seam definition | 4835 |

---

## 3. LANE-SPLIT GATE RESULT

**SINGLE-PIPELINE**

Q1. Same method or within 50 lines? — N/A (only one fix in this block).
Q2. Fix B design depends on Fix A final design? — N/A.
Q3. Each fix has standalone value if the other is blocked? — N/A.
Q4. Each fix has an independent SIM verification path? — N/A.

There is exactly one logical change: extend the `OrderState` predicate in
`FindLeaderStopPrice`. No lane split is warranted.

---

## 4. Changed Predicate — Verbatim Before/After

**File:** `src/PropTraderTools/CopyEngine.cs`, lines 4421–4424

### BEFORE (line 4423):
```csharp
                if (
                    order.Name == targetName // (4)
                    && order.OrderState == OrderState.Working
                ) // (5)
```

### AFTER (line 4423, single-line change — `||` branch added):
```csharp
                if (
                    order.Name == targetName // (4)
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
                ) // (5)
```

No other lines in `FindLeaderStopPrice` are modified.

---

## 5. CYC Audit — FindLeaderStopPrice

| State | McCabe Branches | CYC |
|-------|----------------|-----|
| Pre-change | null account (1) + zero index (2) + foreach (3) + name match (4) + Working state (5) | **5** |
| Post-change | same 5 + Accepted state `\|\|` (6) | **6** |

CYC 6 is within the JS-001 ceiling of 8. No further extraction required.

**Comment block update required (line 4410):**

```
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match;
//        (5) state==Working; (6) state==Accepted.
```

---

## 6. Confirmation — No Other Methods Touched

Only `FindLeaderStopPrice` predicate and its CYC comment are modified.

- `ExecutePhaseCStopReplacement` — **no change** (delegates unchanged).
- `CreateFollowerReplacementStop` — **no change** (guard `stopPrice <= 0` is correct; once price is non-zero the guard passes).
- `FindLeaderStopPriceTestable` — **no change** (seam already delegates to `FindLeaderStopPrice`; no new seam needed).
- All other methods — **untouched**.

---

## 7. Files Touched and Line-Count Delta

| File | Change | Delta |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Predicate line 4423: expand `OrderState` check (+1 char on existing line); CYC comment line 4410: update text (0 net lines — same line count, content changed). | **0 net lines** (no insertions, no deletions; one line modified, one comment line modified) |
| `src/PropTraderTools/CopyEngineTests.cs` | New test class `DwB170Tests` with one `[Fact]` | **+~20 lines** |

---

## 8. Test Specification

### [Fact] name: `FindLeaderStopPrice_AcceptedStop_ReturnsPrice`

**Seam used:** `CopyEngine.FindLeaderStopPriceTestable(Account? leaderAccount, int bracketIndex)`
(existing seam at line 4835 — no new seam needed).

**Test class:** `DwB170Tests`

**Insertion point:** After the closing `}` of `DwB168_02Tests` (line 10257),
before the closing `}` of the namespace (line 10259).
New class starts at line 10258.

**What the test asserts:**

1. Arrange: Create a mock/stub `Account` whose `Orders` collection contains one order with
   - `Name = "Stop1"`
   - `OrderState = OrderState.Accepted`  ← the previously failing case
   - `StopPrice = 4500.25`
2. Act: Call `CopyEngine.FindLeaderStopPriceTestable(account, bracketIndex: 1)`.
3. Assert: Return value equals `4500.25` (was `0.0` before fix).

**Companion regression test:** `FindLeaderStopPrice_WorkingStop_StillReturnsPrice`

1. Arrange: same structure but `OrderState = OrderState.Working` (existing passing case preserved).
2. Assert: return value equals `4500.25`.

**Test class skeleton (pseudo-code for engineer — do not write .cs here):**

```
public class DwB170Tests
{
    [Fact]
    public void FindLeaderStopPrice_AcceptedStop_ReturnsPrice()
    {
        // arrange: stub account with Stop1, Accepted, StopPrice=4500.25
        // act:     CopyEngine.FindLeaderStopPriceTestable(account, 1)
        // assert:  result == 4500.25
    }

    [Fact]
    public void FindLeaderStopPrice_WorkingStop_StillReturnsPrice()
    {
        // arrange: stub account with Stop1, Working, StopPrice=4500.25
        // act:     CopyEngine.FindLeaderStopPriceTestable(account, 1)
        // assert:  result == 4500.25
    }
}
```

---

## 9. Constraint Checklist

| Constraint | Status |
|-----------|--------|
| No `lock()` anywhere | PASS — `FindLeaderStopPrice` uses `leaderAccount.Orders.ToList()` (NT8 Orders collection thread-safe per JS-021); no lock added |
| ASCII-only in all changed lines | PASS — no Unicode, emoji, or curly quotes in predicate or comment update |
| `PTT-` prefix on CreateOrder calls | N/A — no new CreateOrder in this fix |
| `DateTime.UtcNow` (no `DateTime.Now`) | N/A — no date/time usage in changed code |
| No FontFamily, no hardcoded hex colors | N/A |
| `ptt-sync.ps1` required step | REQUIRED — run `powershell -File .\scripts\ptt-sync.ps1` after every `.cs` file save to re-synchronize NinjaTrader hard links |
| CYC <= 8 post-change | PASS — CYC=6 |
| No new method signatures (single-line fix) | PASS |

---

## 10. Required Post-Implementation Steps

1. Edit `src/PropTraderTools/CopyEngine.cs` line 4423 (predicate) and line 4410 (CYC comment).
2. Add `DwB170Tests` class to `src/PropTraderTools/CopyEngineTests.cs` after line 10257.
3. Run `powershell -File .\scripts\ptt-sync.ps1` (hard-link sync).
4. Run `powershell -File .\scripts\test_stress.ps1` (stress/unit test suite).
5. SIM verify: drag a target in NinjaTrader with an open ATM position; confirm follower stop is NOT lost.
