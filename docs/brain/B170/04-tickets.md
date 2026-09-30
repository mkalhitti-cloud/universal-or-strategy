# B170 Tickets — DW-B170-01 FOLLOWER-STOP-LOST-ON-TGT-DRAG

Status: TICKETS_COMPLETE
Source plan: docs/brain/B170/02-architecture-plan.md (REVIEW_PASS)

---

## TICKET B170-1 — Extend OrderState predicate in FindLeaderStopPrice

**Spec requirement:** DW-B170-01
**File:** `src/PropTraderTools/CopyEngine.cs`
**Phase:** src change

---

### Method signatures

Only one method is touched. Signature is unchanged (no parameter or return-type modification):

```csharp
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
```

No other methods are modified.

---

### Change A — Predicate edit (line 4423)

**BEFORE (exact, lines 4421–4424):**
```csharp
                if (
                    order.Name == targetName // (4)
                    && order.OrderState == OrderState.Working
                ) // (5)
```

**AFTER (exact, lines 4421–4424):**
```csharp
                if (
                    order.Name == targetName // (4)
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
                ) // (5)
```

One logical line modified. No lines inserted or deleted. Net line-count delta: 0.

---

### Change B — CYC comment update (line 4410)

**BEFORE (exact, line 4410):**
```
        // CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.
```

**AFTER (exact, line 4410):**
```
        // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
```

One comment line modified. No lines inserted or deleted. Net line-count delta: 0.

---

### Post-edit step

```
powershell -File .\scripts\ptt-sync.ps1
```

Must exit 0. Required to re-synchronize NinjaTrader hard links after every `.cs` save.

---

### 7-Scan Checklist (engineer contract — ALL must pass before marking complete)

| # | Scan | Required Result |
|---|------|----------------|
| SCAN-01 | `grep "OrderState.Working" src/PropTraderTools/CopyEngine.cs` restricted to the `FindLeaderStopPrice` region (lines 4412–4428) | 1 or more matches present |
| SCAN-02 | `grep "OrderState.Accepted" src/PropTraderTools/CopyEngine.cs` restricted to the `FindLeaderStopPrice` region (lines 4412–4428) | Exactly 1 match present |
| SCAN-03 | `grep -r "lock(" src/` | 0 new matches introduced by this change |
| SCAN-04 | Count McCabe branches in `FindLeaderStopPrice` post-change: null account (1) + zero index (2) + foreach (3) + name match (4) + Working state (5) + Accepted state `\|\|` (6) | CYC = 6 |
| SCAN-05 | Inspect all changed lines for Unicode characters, curly/smart quotes, emoji | ASCII-only (7-bit); 0 violations |
| SCAN-06 | `powershell -File .\scripts\ptt-sync.ps1` | Exit code 0 |
| SCAN-07 | Review full diff: only `FindLeaderStopPrice` region (lines 4410 and 4421–4424) modified | No other method bodies or signatures changed |

---

### Acceptance criteria

- No `lock()` added anywhere
- CYC delta confirmed: 5 → 6
- Both `OrderState.Working` AND `OrderState.Accepted` present in predicate (regression-safe)
- CYC comment updated to CYC=6 with DW-B170-01 reference
- ASCII-only in all changed lines
- `ptt-sync.ps1` exits 0

---

## TICKET B170-2 — Add DwB170Tests class to CopyEngineTests.cs

**Spec requirement:** DW-B170-01
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Phase:** test change

---

### Insertion point

Insert the new `DwB170Tests` class **after line 10257** (the closing `}` of `DwB168_02Tests`) and **before line 10259** (the namespace closing `}`). The current line 10258 is blank; new class starts at line 10258.

No existing lines are removed or modified. All other test classes remain intact.

---

### Test class template (engineer must adapt to actual seam pattern)

```csharp
public class DwB170Tests
{
    // DW-B170-01: FindLeaderStopPrice must return stop price for Accepted-state stops
    [Fact]
    public void FindLeaderStopPrice_AcceptedStop_ReturnsPrice()
    {
        // Arrange: stub/fake account whose Orders collection contains one order:
        //   Name = "Stop1", OrderState = OrderState.Accepted, StopPrice = 4500.25
        // Act:
        double result = CopyEngine.FindLeaderStopPriceTestable(/* stubAccount */, bracketIndex: 1);
        // Assert:
        Assert.Equal(4500.25, result, precision: 2);
    }

    [Fact]
    public void FindLeaderStopPrice_WorkingStop_StillReturnsPrice()
    {
        // Arrange: stub/fake account whose Orders collection contains one order:
        //   Name = "Stop1", OrderState = OrderState.Working, StopPrice = 4500.25
        // Act:
        double result = CopyEngine.FindLeaderStopPriceTestable(/* stubAccount */, bracketIndex: 1);
        // Assert:
        Assert.Equal(4500.25, result, precision: 2);
    }
}
```

**CRITICAL NOTE FOR ENGINEER:** The above is a structural template only. Before writing the final `.cs`:

1. Read the `DwB168_02Tests` class body (lines approximately 10200–10257) and all adjacent test helpers.
2. Identify the exact type used to stub/fake an `Account` with a controllable `Orders` collection (e.g., a `FakeAccount` helper, a Moq mock, or a builder pattern already established in the test file).
3. Match that exact pattern in `DwB170Tests`. Do NOT invent new helper types.
4. Use the seam `CopyEngine.FindLeaderStopPriceTestable(Account? leaderAccount, int bracketIndex)` at line 4835. No NT8 runtime types in the test body.

---

### Seam reference

**Seam:** `CopyEngine.FindLeaderStopPriceTestable(Account? leaderAccount, int bracketIndex)`
**Location:** `src/PropTraderTools/CopyEngine.cs`, line 4835
**Access:** `internal static` — accessible from test project without NT8 runtime.
No new seam is required. No seam modification is required.

---

### ptt-sync.ps1

**Not required** for test-only file changes. `ptt-sync.ps1` synchronizes NinjaTrader hard links for `.cs` files in `src/PropTraderTools/`; test files are not hard-linked to the NT8 AddOn directory. State this explicitly when closing the ticket.

---

### 7-Scan Checklist (engineer contract — ALL must pass before marking complete)

| # | Scan | Required Result |
|---|------|----------------|
| SCAN-01 | `grep "FindLeaderStopPrice_AcceptedStop_ReturnsPrice" src/PropTraderTools/CopyEngineTests.cs` | 1 match present |
| SCAN-02 | `grep "FindLeaderStopPrice_WorkingStop_StillReturnsPrice" src/PropTraderTools/CopyEngineTests.cs` | 1 match present (regression guard) |
| SCAN-03 | `grep "FindLeaderStopPriceTestable" src/PropTraderTools/CopyEngineTests.cs` restricted to `DwB170Tests` | 2 matches (one per test) — no NT8 runtime calls in test body |
| SCAN-04 | Inspect test body for NT8 runtime references (e.g. `NinjaTrader.Cbi.Account`, `Instrument`, `Session`) | 0 NT8 runtime references |
| SCAN-05 | Diff of `CopyEngineTests.cs`: confirm no whitespace mutations, line-ending changes, or indentation edits outside the insertion site | 0 mutations outside insert site |
| SCAN-06 | `grep -c "^\s*\[Fact\]" src/PropTraderTools/CopyEngineTests.cs` pre- vs post-change | Count increases by exactly 2 |
| SCAN-07 | Confirm `ptt-sync.ps1` was NOT run for this ticket (test-only change) | Not required; state explicitly in completion note |

---

### Acceptance criteria

- `FindLeaderStopPrice_AcceptedStop_ReturnsPrice` — PASS (previously failing case: Accepted stop returns price)
- `FindLeaderStopPrice_WorkingStop_StillReturnsPrice` — PASS (regression guard: Working stop still returns price)
- 0 tests SKIP, 0 tests FAIL
- Uses `FindLeaderStopPriceTestable` seam exclusively (no NT8 runtime in test body)
- No existing tests removed or renamed
- No whitespace mutations outside insertion site
- `ptt-sync.ps1` not required (state explicitly)

---

## Summary

| Ticket | File | Change type | Net line delta | ptt-sync required |
|--------|------|-------------|---------------|-------------------|
| B170-1 | `src/PropTraderTools/CopyEngine.cs` | Predicate + comment edit | 0 | YES |
| B170-2 | `src/PropTraderTools/CopyEngineTests.cs` | Insert `DwB170Tests` class | +~20 | NO |
