# Ticket 6 (B165-VERIFY) — Completion Report

**Epic:** B161-B166  
**Ticket:** 6 — B165-VERIFY  
**Type:** VERIFICATION ONLY (no src modifications made)  
**File verified:** `src/PropTraderTools/CopyEngine.cs`  
**Methods verified:** `IsFollowerPositionBlocked` (~L3182–L3241), `IsBeSlotNonTerminal` (~L9382–L9386)

---

## 1. Signal 5 Block — Exact Lines Found in src

Lines **L3222–L3230** of `src/PropTraderTools/CopyEngine.cs`:

```csharp
if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return true;
}
```

**PRESENT** — matches ticket specification exactly. ✅

---

## 2. IsBeSlotNonTerminal Helper — Confirmation

Lines **L9381–L9386** of `src/PropTraderTools/CopyEngine.cs`:

```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeSlotNonTerminal(string accName)
{
    // CYC=1
    return _pendingFollowerBeSlots.ContainsKey(accName);
}
```

**PRESENT** — pre-existing helper confirmed at expected location. ✅

---

## 3. Signal Ordering Confirmation

| Signal | Guard | Line | Notes |
|--------|-------|------|-------|
| Guard-null | `if (acc == null)` | L3184 | Null guard for acc |
| Guard-null | `if (instr == null)` | L3185 | Null guard for instr |
| Signal 1 | `HasOpenPosition` | L3186 | B159 — normal case |
| Signal 2 | `HasActivePttBeExits` | L3195 | pre-fill race |
| Signal 3 | `HasSettlingBeTargets` | L3204 | OCO-settle race |
| **Signal 4** | `HasActivePttBeTargets` | **L3213** | post-Stop-fill race (B164) |
| **Signal 5** | `IsBeSlotNonTerminal` | **L3222** | BE settle window **(B165) ← THIS TICKET** |
| **Signal 6** | `IsBeRetryArmedFlat` | **L3231** | post-fallback-BE window (B166) |

Signal 5 is **after Signal 4 and before Signal 6**. ✅ Order 4 → 5 → 6 confirmed.

---

## 4. Full IsFollowerPositionBlocked — Branch-by-Branch CYC Count

Method span: L3182–L3241

| # | Branch construct | Line | +1 |
|---|-----------------|------|----|
| Base | method entry | — | 1 |
| 1 | `if (acc == null)` | 3184 | +1 |
| 2 | `if (instr == null)` | 3185 | +1 |
| 3 | `if (HasOpenPosition(...))` Signal 1 | 3186 | +1 |
| 4 | `if (HasActivePttBeExits(...))` Signal 2 | 3195 | +1 |
| 5 | `if (HasSettlingBeTargets(...))` Signal 3 | 3204 | +1 |
| 6 | `if (HasActivePttBeTargets(...))` Signal 4 | 3213 | +1 |
| 7 | `if (IsBeSlotNonTerminal(...))` Signal 5 | 3222 | +1 |
| 8 | `if (IsBeRetryArmedFlat(...))` Signal 6 | 3231 | +1 |

**CYC = 9**

> **VIOLATION-01:** CYC=9 exceeds JS-CYC budget of 8 by 1.  
> This is a **known, documented violation** introduced cumulatively as each Signal was added across B159–B165.  
> A deferred P1 fix is planned (split method into two sub-guards, e.g. `IsPositionRaceBlocked` + `IsBeWindowBlocked`, each CYC ≤ 5).  
> No action required in this ticket. Document only.

---

## 5. 7-Scan Checklist

| Scan | Rule | Result | Detail |
|------|------|--------|--------|
| SCAN-01 | JS-001 no-throw | ✅ PASS | No `throw` in `IsFollowerPositionBlocked`; all exits are `return true` / `return false` |
| SCAN-02 | JS-002 no-null-return | ✅ PASS | Return type `bool`; no null value returned anywhere in method |
| SCAN-03 | JS-021 no-lock | ✅ PASS | No `lock()` statement present in method |
| SCAN-04 | JS-042 ASCII-only | ✅ PASS | String `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` is 100% ASCII; no Unicode characters |
| SCAN-05 | JS-CYC | ⚠️ VIOLATION-01 | **CYC=9** (budget=8). Exceeds limit by 1. Known cumulative violation. Deferred P1 fix planned (method split). No immediate action required. |
| SCAN-06 | NT8 API | ✅ PASS | `acc.Name` is `string` property of `Account`; correct AddOnBase usage. No StrategyBase-only API called. |
| SCAN-07 | Signal continuity | ✅ PASS | Signal 5 (`IsBeSlotNonTerminal`, L3222) is after Signal 4 (`HasActivePttBeTargets`, L3213) and before Signal 6 (`IsBeRetryArmedFlat`, L3231) |

---

## 6. Summary

- **Signal 5 block:** PRESENT at L3222–L3230, matches specification exactly
- **IsBeSlotNonTerminal helper:** PRESENT at L9382–L9386, CYC=1 (clean)
- **Signal ordering:** 4 → 5 → 6 confirmed correct
- **CYC:** 9 (VIOLATION-01 — documented, deferred P1 fix planned)
- **All other scans:** PASS

---

## Final Status: TICKET_COMPLETE

All required elements verified present in src. Signal 5 block matches specification. VIOLATION-01 (CYC=9) documented per pipeline protocol. No src modifications made. No discrepancies found.
