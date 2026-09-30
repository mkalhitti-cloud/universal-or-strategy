# Ticket 5 (B164-VERIFY) — Completion Report

**Epic:** B161-B166  
**Ticket:** 5 — B164-VERIFY  
**Engineer role:** ptt-engineer (VERIFICATION ONLY — no src modifications)  
**Date:** 2026-09-14  
**Status:** TICKET_COMPLETE

---

## 1. HasActivePttBeTargets Helper

**Location:** `src/PropTraderTools/CopyEngine.cs` L3147–L3158

```
L3147:  internal bool HasActivePttBeTargets(Account acc, Instrument instr)
L3148:  {
L3149:      if (acc == null)   return false;    // (2)
L3150:      if (instr == null) return false;    // (3)
L3151:      foreach (var o in acc.Orders.ToList())   // (4) snapshot live collection, no lock
L3152:      {
L3153:          if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue;   // (5) (6)
L3154:          if (o.Instrument?.FullName != instr.FullName) continue;   // (7)
L3155:          if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
L3156:      }
L3157:      return false;
L3158:  }
```

**Match against ticket spec:** PASS — all 7 lines match exactly (guards, foreach snapshot, prefix check, null-safe instrument, state predicate, return false).

---

## 2. Signal 4 Block in IsFollowerPositionBlocked

**Location:** `src/PropTraderTools/CopyEngine.cs` L3213–L3221

```
L3213:  if (HasActivePttBeTargets(acc, instr))  // (7) Signal 4 -- post-Stop-fill race (B164)
L3214:  {
L3215:      NinjaTrader.Code.Output.Process(
L3216:          "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
L3217:              + acc.Name + "\n" + instr.FullName,
L3218:          NinjaTrader.NinjaScript.PrintTo.OutputTab1
L3219:      );
L3220:      return true;
L3221:  }
```

**Match against ticket spec:** PASS — condition, log tag, account+instrument concatenation, PrintTo, and return true all match.

---

## 3. Signal Ordering Confirmation

| Step | Signal # | Method | Line | Comment tag |
|------|----------|--------|------|-------------|
| (2)  | guard    | `if (acc == null)`     | L3184 | acc-null guard |
| (3)  | guard    | `if (instr == null)`   | L3185 | instr-null guard |
| (4)  | Signal 1 | `HasOpenPosition`      | L3186 | has-position |
| (5)  | Signal 2 | `HasActivePttBeExits`  | L3195 | active-be-exits |
| **(6)**  | **Signal 3** | **`HasSettlingBeTargets`** | **L3204** | settling-be-targets |
| **(7)**  | **Signal 4** | **`HasActivePttBeTargets`** | **L3213** | active-be-targets (B164) |
| **(8)**  | **Signal 5** | **`IsBeSlotNonTerminal`** | **L3222** | be-settle-pending |
| (9)  | Signal 6 | `IsBeRetryArmedFlat`   | L3231 | be-retry-armed-flat (B166) |

**Signal 3 → Signal 4 → Signal 5 ordering: CONFIRMED CORRECT.**  
Signal 4 is at L3213, after Signal 3 at L3204 and before Signal 5 at L3222.

---

## 4. CYC Counts

### HasActivePttBeTargets (L3147–L3158)

| Branch | +CYC |
|--------|------|
| Base   | 1    |
| `if (acc == null)` | +1 = 2 |
| `if (instr == null)` | +1 = 3 |
| `foreach` | +1 = 4 |
| `if (o.Name == null \|\| ...)` — two conditions via `\|\|` | +2 = 6 |
| `if (o.Instrument?.FullName != ...)` | +1 = 7 |
| `if (IsPttBeStopActiveState(...))` | +1 = **8** |

**CYC = 8. Within spec budget of ≤ 8. PASS.**

### IsFollowerPositionBlocked (L3182–L3241)

| Branch | +CYC |
|--------|------|
| Base   | 1    |
| `if (acc == null)` | +1 = 2 |
| `if (instr == null)` | +1 = 3 |
| Signal 1 `if (HasOpenPosition)` | +1 = 4 |
| Signal 2 `if (HasActivePttBeExits)` | +1 = 5 |
| Signal 3 `if (HasSettlingBeTargets)` | +1 = 6 |
| Signal 4 `if (HasActivePttBeTargets)` | +1 = 7 |
| Signal 5 `if (IsBeSlotNonTerminal)` | +1 = 8 |
| Signal 6 `if (IsBeRetryArmedFlat)` | +1 = **9** |

**CYC = 9.**  
Inline comment at L3178 says `CYC=8` — this was accurate at B164 write time. B165 (Signal 5) and B166 (Signal 6) each added +1 branch, raising actual CYC to 9. This matches ticket VIOLATION-01 note: "actual may be 9 after B165+B166 added more — report actual count."  
**Stale comment is pre-existing; actual CYC=9 is expected and documented.**

---

## 5. 7-Scan Checklist

| Scan | Rule | Check | Result |
|------|------|-------|--------|
| SCAN-01 | JS-001 no-throw | `Select-String "throw"` L3147–L3241 → only comment line, zero actual throw statements | **PASS** |
| SCAN-02 | JS-002 no-null-return | `return null` L3147–L3241 → zero hits; both methods return `bool` covering all paths | **PASS** |
| SCAN-03 | JS-021 no-lock | `Select-String "lock\s*\("` L3147–L3241 → zero hits | **PASS** |
| SCAN-04 | JS-042 ASCII-only | Non-ASCII scan of L3147–L3241 → zero non-ASCII characters; `"PTT-BE-Target-"` and `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets):"` are pure ASCII | **PASS** |
| SCAN-05 | JS-CYC budget | `HasActivePttBeTargets` CYC = 8, spec budget ≤ 8 | **PASS** |
| SCAN-06 | NT8 API correctness | `acc.Orders.ToList()` at L3151 (snapshot pattern, no lock); `o.Instrument?.FullName` at L3154 (null-safe navigation) — both confirmed AddOnBase-safe patterns | **PASS** |
| SCAN-07 | Signal continuity | Signal 3 (`HasSettlingBeTargets`) L3204 → Signal 4 (`HasActivePttBeTargets`) L3213 → Signal 5 (`IsBeSlotNonTerminal`) L3222 — correct position confirmed | **PASS** |

**All 7 scans: PASS. Zero violations.**

---

## 6. Notes

- **VIOLATION-01 (pre-existing, not a B164 defect):** `IsFollowerPositionBlocked` inline comment at L3178 states `CYC=8`. Actual CYC is 9 due to B165+B166 adding Signals 5 and 6 after B164 was authored. The comment is stale but the code is correct. The ticket explicitly anticipated this discrepancy.
- No code was modified. This task was verification-only.

---

## Final Status

**TICKET_COMPLETE**

All required elements are present:
- `HasActivePttBeTargets` helper at L3147–L3158 matches spec exactly.
- Signal 4 block at L3213–L3221 matches spec exactly.
- Signal ordering: Signal 3 → Signal 4 → Signal 5 is correct.
- `HasActivePttBeTargets` CYC = 8 (within budget).
- `IsFollowerPositionBlocked` CYC = 9 (VIOLATION-01 acknowledged, pre-existing per ticket).
- All 7 scans pass with zero violations.
