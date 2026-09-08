# Ticket 6 Verification Report

**TICKET:** 6
**EPIC:** PR-122
**FILE VERIFIED:** C:\WSGTA\ptt-features\src\PropTraderTools\Features\PttBreakEven.cs
**VERIFIER:** PTT Verifier (Phase 4b)
**DATE:** Independent verification — source read directly

---

## FINDINGS VERIFIED: C14, C15

---

### C14 — IsBePriceOk short branch validates against ask

**STATUS: PASS**

**Evidence — IsBePriceOk (lines 138–143):**
```csharp
private static bool IsBePriceOk(bool isLong, double bePrice, double ask, double bid)
{
    if (isLong)
        return ask <= 0.0 || bePrice <= ask; // (1)(2)
    return ask <= 0.0 || bePrice >= ask; // (3)
}
```

**Verdict:** Short branch (line 142) correctly uses `ask`, not `bid`.
The fix `return ask <= 0.0 || bePrice >= ask;` is confirmed present.
Previously would have been `bid`; now correctly uses `ask` for short-side guard.

---

### C15 — FindPositionLocal FullName comparison

**STATUS: PASS**

**Evidence — FindPositionLocal (lines 551–559):**
```csharp
private static Position FindPositionLocal(Account acc, Instrument instr)
{
    if (acc == null || instr == null)
        return null;
    foreach (Position p in acc.Positions)
        if (p.Instrument?.FullName == instr?.FullName)
            return p;
    return null;
}
```

**Verdict:** Line 556 uses `p.Instrument?.FullName == instr?.FullName` (value comparison).
The previously incorrect `p.Instrument == instr` reference comparison is NOT present.

---

## SCAN RESULTS

### BUILD
- **Command:** `dotnet build C:\WSGTA\ptt-features\Linting.csproj`
- **Result:** 0 errors, 0 warnings — BUILD CLEAN
- **Note:** `Testing.csproj` has pre-existing errors (NUnit `[TestCase]`/`[Test]` attributes
  not found — `TestCaseAttribute`/`TestAttribute` CS0246 in `LogicTests.cs`).
  These are pre-existing test framework dependency failures, NOT introduced by Ticket 6,
  and are unrelated to `PttBreakEven.cs`. Production source (Linting.csproj) is 0 errors.

### LOCK SCAN
- **Command:** `Select-String -Path PttBreakEven.cs -Pattern "lock\("`
- **Result:** 0 matches

### THROW SCAN
- **Command:** `Select-String -Path PttBreakEven.cs -Pattern "throw "`
- **Result:** 4 matches — ALL in XML doc comment lines only:
  - Line 63:  `/// JS-001: no throw -- try/catch in helpers.`
  - Line 94:  `/// JS-001: no throw -- try/catch in helpers.`
  - Line 384: `/// JS-001: no throw -- try/catch.`
  - Line 434: `/// JS-001: no throw -- try/catch per order.`
  - **0 matches in executable code.**

---

## DNA RULE SPOT CHECK

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | lock( absent | PASS |
| JS-001 | throw in executable code absent | PASS |
| C14 fix | short branch uses ask | PASS |
| C15 fix | FullName value comparison | PASS |

---

## STATUS: VERIFY_PASS

Both C14 and C15 fixes are correctly implemented.
Production build is clean (0 errors).
No lock( usage. No throw in executable code.