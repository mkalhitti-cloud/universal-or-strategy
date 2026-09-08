# ticket-5-verification.md
# Epic: PR-122  |  Ticket: 5
# Verifier: PTT Verifier (ptt-phase5-v-verify)
# Date: 2026-08-27

## TICKET: 5
## FINDINGS VERIFIED: C12, C13

---

### C12 PASS/FAIL: PASS

**Finding**: SubmitQxOcoPair was always recomputing the target price from t1Ticks*(i+1) with no
forced-price branch.

**Fix verified**: Price resolution is now delegated to `ResolveTNPrice` (extracted helper, line 158
in SubmitQxOcoPair). That helper contains the forced-price guard at line 197:

```
Line 197: if (targets != null && i < targets.Count && targets[i].Price > 0.0)
Line 198:     return targets[i].Price; // use forced price directly
```

When `targets[i].Price > 0.0` the forced price is returned immediately. Only when that guard is
false does the fallback tick-computation path execute (lines 199-201). The forced-price branch
exists and is correct.

**Evidence — lines 197-201 of PttQuickExit.cs**:
```csharp
if (targets != null && i < targets.Count && targets[i].Price > 0.0)
    return targets[i].Price; // use forced price directly
int tNTicks = t1Ticks * (i + 1);
double rawTN = isLong ? entryPx + tNTicks * tick : entryPx - tNTicks * tick;
return Math.Round(rawTN / tick) * tick;
```

---

### C13 PASS/FAIL: PASS

**Finding**: `IsFlatOrMissing` used `p.Instrument == instr` (reference equality), which fails when
NT8 supplies distinct Instrument instances for the same contract.

**Fix verified**: The foreach in `IsFlatOrMissing` now uses FullName comparison at line 221:

```
Line 221: if (p.Instrument?.FullName == instr?.FullName)
```

Reference comparison `p.Instrument == instr` is NOT present. FullName string comparison is correct.

**Evidence — lines 220-225 of PttQuickExit.cs**:
```csharp
foreach (Position p in leader.Positions)
    if (p.Instrument?.FullName == instr?.FullName)
    {
        pos = p;
        break;
    }
```

---

### BUILD: 0 errors

Tested against `C:\WSGTA\ptt-features\Linting.csproj` (the only .csproj found in the worktree).
Result: `Build succeeded. 0 Error(s)`

Note: `src\PropTraderTools\PropTraderTools.csproj` does not exist in this worktree — PropTraderTools
source is compiled as part of `Linting.csproj`. Build is clean.

---

### LOCK SCAN: 0 matches

```
Select-String -Path PttQuickExit.cs -Pattern "lock\("
```
Result: no output (zero matches). PASS.

---

### THROW SCAN: 0 matches in executable code

```
Select-String -Path PttQuickExit.cs -Pattern "throw "
```
Raw result: 3 matches — all are in `///` XML doc-comment lines (lines 38, 296, 349), not in
executable code. Zero `throw` statements in actual C# code paths.

Confirmed with second scan filtering doc-comment lines:
```
Select-String ... | Where-Object { $_.Line -notmatch "^\s*///" }
```
Result: no output. PASS.

---

### DNA RULES SPOT-CHECK (additional)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | grep lock( | PASS — 0 hits |
| JS-001 (no throw) | grep throw in code | PASS — 0 hits in executable lines |
| JS-002 (no return null) | ResolveTNPrice returns double; IsFlatOrMissing returns bool | PASS |
| NT8-014 (PTT- prefix) | stopName = "PTT-QX-Stop*", targetName = "PTT-QX-T*" | PASS |
| NT8-013 (DateTime.MaxValue) | CreateOrder arg11 = DateTime.MaxValue in both Submit methods | PASS |
| NT8-049 (Limit/StopMarket arg order) | Limit: arg6=tNPrice, arg7=0; StopMarket: arg6=0, arg7=snapshotStop | PASS |
| ASCII-only | No non-ASCII characters observed in source | PASS |

---

## STATUS: VERIFY_PASS