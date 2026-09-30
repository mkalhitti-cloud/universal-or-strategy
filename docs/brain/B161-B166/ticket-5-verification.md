# Ticket 5 (B164-VERIFY) — Independent Verification Report

**Epic:** B161-B166
**Ticket:** 5 — B164-VERIFY
**Verifier role:** ptt-verifier (INDEPENDENT — READ-ONLY, no src modifications)
**Date:** 2026-09-14
**Verification scope:** HasActivePttBeTargets helper + Signal 4 block in IsFollowerPositionBlocked

---

## 1. Independent Read: HasActivePttBeTargets (L3147–L3158)

Source read independently from `src/PropTraderTools/CopyEngine.cs` L3147–L3158:

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

### Spec compliance check:

| Spec requirement | Actual | Status |
|-----------------|--------|--------|
| Signature: `internal bool HasActivePttBeTargets(Account acc, Instrument instr)` | Exact match at L3147 | PASS |
| acc null guard ? return false | L3149: `if (acc == null) return false;` | PASS |
| instr null guard ? return false | L3150: `if (instr == null) return false;` | PASS |
| foreach over `acc.Orders.ToList()` | L3151 | PASS |
| `"PTT-BE-Target-"` prefix check with `StringComparison.Ordinal` | L3153 | PASS |
| `o.Name == null` null-guard in same condition | L3153 (left side of `||`) | PASS |
| `o.Instrument?.FullName` null-safe instrument comparison | L3154 | PASS |
| `IsPttBeStopActiveState(o.OrderState)` ? return true | L3155 | PASS |
| `return false` terminal | L3157 | PASS |
| No `throw` statement | Zero throw statements in L3147–L3158 | PASS |
| Returns bool only (no null return) | Method return type bool; all paths return false or true | PASS |

**HasActivePttBeTargets: ALL SPEC REQUIREMENTS MET.**

---

## 2. Independent Read: Signal 4 Block in IsFollowerPositionBlocked (L3213–L3221)

Source read independently from `src/PropTraderTools/CopyEngine.cs` L3213–L3221:

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

### Spec compliance check:

| Spec requirement | Actual | Status |
|-----------------|--------|--------|
| `if (HasActivePttBeTargets(acc, instr))` condition present | L3213 exact | PASS |
| Log tag: `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "` | L3216 exact | PASS |
| `acc.Name + "\n" + instr.FullName` in log | L3217 exact | PASS |
| `PrintTo.OutputTab1` | L3218 | PASS |
| `return true;` | L3220 | PASS |

**Signal 4 block: ALL SPEC REQUIREMENTS MET.**

---

## 3. Signal Ordering Verification

Source read of `IsFollowerPositionBlocked` L3182–L3241:

| Step | Signal | Method | Line | Source comment |
|------|--------|--------|------|----------------|
| (2)  | guard  | `if (acc == null)` | L3184 | acc-null guard |
| (3)  | guard  | `if (instr == null)` | L3185 | instr-null guard |
| (4)  | Signal 1 | `HasOpenPosition` | L3186 | `// (4) Signal 1 -- normal case (B159)` |
| (5)  | Signal 2 | `HasActivePttBeExits` | L3195 | `// (5) Signal 2 -- pre-fill race` |
| **(6)**  | **Signal 3** | **`HasSettlingBeTargets`** | **L3204** | `// (6) Signal 3 -- confirmed OCO-settle race` |
| **(7)**  | **Signal 4** | **`HasActivePttBeTargets`** | **L3213** | `// (7) Signal 4 -- post-Stop-fill race (B164)` |
| **(8)**  | **Signal 5** | **`IsBeSlotNonTerminal`** | **L3222** | `// (8) Signal 5 -- BE settle window (B165)` |
| (9)  | Signal 6 | `IsBeRetryArmedFlat` | L3231 | `// (9) Signal 6 -- post-fallback-BE window (B166)` |

**Signal 3 (L3204) ? Signal 4 (L3213) ? Signal 5 (L3222): ORDERING CONFIRMED CORRECT.**

---

## 4. 5-Line Context Around Signal 4 (No Side Effects)

Lines L3208–L3226 (5 before + Signal 4 block + 5 after):

```
L3208:      NinjaTrader.NinjaScript.PrintTo.OutputTab1
L3209:      );
L3210:      return true;
L3211:  }
L3212:  (blank)
L3213:  if (HasActivePttBeTargets(acc, instr))  // (7) Signal 4 -- post-Stop-fill race (B164)
L3214:  {
L3215:      NinjaTrader.Code.Output.Process(
L3216:          "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
L3217:              + acc.Name + "\n" + instr.FullName,
L3218:          NinjaTrader.NinjaScript.PrintTo.OutputTab1
L3219:      );
L3220:      return true;
L3221:  }
L3222:  if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
L3223:  {
```

**No unintended side effects:** Signal 3 ends cleanly with `return true; }`. Signal 4 is a standalone `if` block with only a log + return. Signal 5 begins immediately after. No shared state mutations, no field writes, no other Output.Process calls outside the guarded blocks.

---

## 5. Independent Scan Results (Layer 3 — Verifier's Own Runs)

### SCAN-01: lock( usage

**Command:** `Select-String -Path src/PropTraderTools/*.cs -Pattern 'lock\s*\('`

**Result for L3147–L3241:** All matches are in comments (e.g., `// JS-021: no lock (ToList snapshot)`).
Zero actual `lock(` statements in executable code. **PASS.**

### SCAN-02: Non-ASCII characters

**Command:** `Get-Content src/PropTraderTools/CopyEngine.cs | Select-Object -Skip 3146 -First 95 | Where-Object { $_ -match '[^\x00-\x7F]' }`

**Result:** No output. Zero non-ASCII characters in L3147–L3241. **PASS.**

### SCAN-03: FontFamily

**Command:** `Select-String ... -Pattern 'FontFamily'` filtered to L3147–L3241

**Result:** No output. Zero hits. **PASS.**

### SCAN-04: Hex color #RRGGBB

**Command:** `Select-String ... -Pattern '#[0-9A-Fa-f]{6}'` filtered to L3147–L3241

**Result:** No output. Zero hits. **PASS.**

### SCAN-05: CreateOrder PTT- prefix

**Command:** `Select-String ... -Pattern 'CreateOrder'` filtered to L3147–L3241

**Result:** No output. Zero CreateOrder calls in target range (predicate-only methods). **PASS.**

### SCAN-06: DateTime.Now

**Command:** `Select-String ... -Pattern 'DateTime\.Now[^U]'` filtered to L3147–L3241

**Result:** One hit at L3179 — `// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only. No DateTime.Now.` — comment only, zero actual usage. **PASS.**

### SCAN-07: throw statement

**Command:** `Select-String ... -Pattern '\bthrow\b'` filtered to L3147–L3241

**Result:** One hit at L3179 — same comment line. Zero actual throw statements. **PASS.**

**All 7 scans: PASS. Zero violations in target range L3147–L3241.**

---

## 6. Jane Street DNA Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-001: No throw in gate methods | Zero throw statements in HasActivePttBeTargets and IsFollowerPositionBlocked | PASS |
| JS-002: No null return | Both methods return bool; all code paths return true or false | PASS |
| JS-021: No lock() | acc.Orders.ToList() snapshot used instead; zero lock( in executable code | PASS |
| JS-042: ASCII-only string literals | "PTT-BE-Target-" and "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets):" — confirmed pure ASCII | PASS |
| NT8: No FontFamily= | Zero hits | PASS |
| NT8: No #RRGGBB hex | Zero hits | PASS |
| NT8: No DateTime.Now | Zero actual uses (comment only) | PASS |
| NT8: CreateOrder PTT- prefix | No CreateOrder calls in these methods | PASS |

---

## 7. NT8 API Correctness

- `acc.Orders.ToList()` at L3151: ToList() snapshot pattern for iterating NT8 Account.Orders safely — confirmed AddOnBase-safe (avoids ConcurrentModificationException during event-burst). **PASS.**
- `o.Instrument?.FullName` at L3154: Null-safe navigation — correct for NT8 Order objects where Instrument may be null during order state transitions. **PASS.**
- `IsPttBeStopActiveState(o.OrderState)` at L3155: Reuses the existing Active-state predicate (Working/Accepted/Submitted). This is the correct reuse for Target orders as they share the same active state enum values. **PASS.**

---

## 8. CYC Counts (Independent Branch Count)

### HasActivePttBeTargets (L3147–L3158)

| # | Decision point | Cumulative CYC |
|---|---------------|----------------|
| 1 | base | 1 |
| 2 | `if (acc == null)` L3149 | 2 |
| 3 | `if (instr == null)` L3150 | 3 |
| 4 | `foreach (...)` L3151 | 4 |
| 5 | `o.Name == null` (left of `||`) L3153 | 5 |
| 6 | `!o.Name.StartsWith(...)` (right of `||`) L3153 | 6 |
| 7 | `if (o.Instrument?.FullName != ...)` L3154 | 7 |
| 8 | `if (IsPttBeStopActiveState(...))` L3155 | **8** |

**CYC = 8. Spec budget: = 8. PASS.**

### IsFollowerPositionBlocked (L3182–L3241)

| # | Decision point | Cumulative CYC |
|---|---------------|----------------|
| 1 | base | 1 |
| 2 | `if (acc == null)` L3184 | 2 |
| 3 | `if (instr == null)` L3185 | 3 |
| 4 | `if (HasOpenPosition(...))` L3186 | 4 |
| 5 | `if (HasActivePttBeExits(...))` L3195 | 5 |
| 6 | `if (HasSettlingBeTargets(...))` L3204 | 6 |
| 7 | `if (HasActivePttBeTargets(...))` L3213 | 7 |
| 8 | `if (IsBeSlotNonTerminal(...))` L3222 | 8 |
| 9 | `if (IsBeRetryArmedFlat(...))` L3231 | **9** |

**CYC = 9.** Exceeds =8 budget.

---

## 9. VIOLATION-01 (Pre-existing, Not a B164 Defect)

**File:** `src/PropTraderTools/CopyEngine.cs`
**Line:** L3178
**Issue:** Inline comment states `CYC=8` but actual CYC is 9.
**Root cause:** Comment was accurate when written for B164 (Signals 1–4 only = CYC=8). Subsequent tickets B165 (Signal 5) and B166 (Signal 6) each added +1 branch, raising the actual count to 9. The comment was not updated.
**Severity:** Stale documentation — not a code defect. The code is correct.
**Acknowledgment:** Ticket 5 explicitly anticipated this: *"Note VIOLATION-01: IsFollowerPositionBlocked CYC may be 9"* and *"actual may be 9 after B165+B166 added more — report actual count."*
**Design intent confirmed:** L3176–L3177 states Signal 6 (`IsBeRetryArmedFlat CYC=2`) was *"delegated to IsBeRetryArmedFlat to keep parent at CYC=8"* — the architecture was designed to offload complexity to a helper, but the +1 branch from the call site itself still counts.
**Disposition:** Pre-existing, ticket-documented, non-blocking for B164 scope. Recommend stale comment be updated in a future cleanup ticket.

---

## 10. Cross-check with Engineer's Layer 2 Report

| Engineer claimed | Verifier independently confirmed | Match |
|-----------------|----------------------------------|-------|
| HasActivePttBeTargets at L3147–L3158 | Confirmed at L3147–L3158 | ? |
| Signal 4 at L3213–L3221 | Confirmed at L3213–L3221 | ? |
| Signal 3 L3204 ? Signal 4 L3213 ? Signal 5 L3222 | Confirmed | ? |
| HasActivePttBeTargets CYC = 8 | Independently counted: 8 | ? |
| IsFollowerPositionBlocked CYC = 9 | Independently counted: 9 | ? |
| All 7 scans PASS | All 7 scans independently confirmed PASS | ? |
| VIOLATION-01 acknowledged as pre-existing | Confirmed: pre-existing, B165+B166 caused it, ticket-documented | ? |

**No discrepancies between Layer 2 (engineer self-report) and Layer 3 (verifier independent scans).**

---

## Final Status

### **VERIFY_PASS**

All required elements are present and correct:

1. ? `HasActivePttBeTargets` at L3147–L3158 matches spec exactly (signature, guards, foreach snapshot, prefix check, null-safe instrument, Active-state predicate, return false terminal).
2. ? Signal 4 block at L3213–L3221 matches spec exactly (condition, log tag, acc+instr concat, PrintTo, return true).
3. ? Signal ordering: Signal 3 (L3204) ? Signal 4 (L3213) ? Signal 5 (L3222) — correct.
4. ? No side effects in 5-line context around Signal 4.
5. ? All Jane Street DNA rules pass (JS-001, JS-002, JS-021, JS-042, NT8 constraints).
6. ? NT8 API usage correct (acc.Orders.ToList() snapshot, null-safe instrument navigation).
7. ? HasActivePttBeTargets CYC = 8 (within =8 budget).
8. ?? IsFollowerPositionBlocked CYC = 9 (VIOLATION-01: stale comment, pre-existing, ticket-documented, non-blocking).
9. ? Zero discrepancies between engineer Layer 2 report and verifier Layer 3 scans.

**VIOLATION-01 is pre-existing and explicitly acknowledged by the ticket. It does not block VERIFY_PASS for Ticket 5 (B164 scope).**
