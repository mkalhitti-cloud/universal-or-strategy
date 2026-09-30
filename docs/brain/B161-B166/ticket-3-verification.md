# Ticket 3 Verification: B162B-VERIFY

**Verifier:** ptt-verifier
**Date:** 2025-07-29
**File:** `src/PropTraderTools/CopyEngine.cs` (READ-ONLY)
**Ticket scope:** VERIFICATION ONLY — no src modifications made or permitted.
**Final status: VERIFY_PASS**

---

## 1. Independent Source Reads

All findings are from direct independent reads of `src/PropTraderTools/CopyEngine.cs`.
Engineer's Layer 2 report is cross-checked in §8 below.

---

## 2. HasActivePttBeExits — Independent Read (L3104–L3115)

**Full method as read:**

```
L3104:  internal bool HasActivePttBeExits(Account acc, Instrument instr)
L3105:  {
L3106:      if (acc == null)   return false;    // (2)
L3107:      if (instr == null) return false;    // (3)
L3108:      foreach (var o in acc.Orders.ToList())   // (4) snapshot live collection, no lock
L3109:      {
L3110:          if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;   // (5) (6) B162-B: bare+suffixed
L3111:          if (o.Instrument?.FullName != instr.FullName) continue;   // (7)
L3112:          if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
L3113:      }
L3114:      return false;
L3115:  }
```

**Findings:**

| Check | Result |
|-------|--------|
| `"PTT-BE-Stop"` WITHOUT trailing dash present at L3110 | ✅ CONFIRMED |
| `"PTT-BE-Stop-"` WITH trailing dash ABSENT from L3104–L3115 | ✅ CONFIRMED |
| B162-B comment tag present at L3110 | ✅ CONFIRMED (`// (5) (6) B162-B: bare+suffixed`) |

---

## 3. StillAtNonBePrice — Independent Read (L7181–L7196)

**Full method as read:**

```
L7181:  internal bool StillAtNonBePrice(Account acc, Instrument instr, double bePrice)
L7182:  {
L7183:      if (acc == null)   return false;                                              // (2)
L7184:      if (instr == null) return false;                                              // (3)
L7185:      foreach (var o in acc.Orders.ToList())                                        // (4) JS-021: snapshot live collection
L7186:      {
L7187:          if (o.Name == null
L7188:              || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))     // B162-B: bare+suffixed
L7189:              continue;                                                             // (5)
L7190:          if (o.Instrument?.FullName != instr.FullName) continue;                  // (6)
L7191:          if (o.OrderState != OrderState.Working)        continue;                 // (7)
L7192:          double tick = instr.MasterInstrument.TickSize;
L7193:          return Math.Abs(o.StopPrice - bePrice) > tick * 0.5;
L7194:      }
L7195:      return true; // no working stop found -- still needs BE
L7196:  }
```

**Findings:**

| Check | Result |
|-------|--------|
| `"PTT-BE-Stop"` WITHOUT trailing dash present at L7188 | ✅ CONFIRMED |
| `"PTT-BE-Stop-"` WITH trailing dash ABSENT from L7181–L7196 | ✅ CONFIRMED |
| B162-B comment tag present at L7188 | ✅ CONFIRMED (`// B162-B: bare+suffixed`) |

---

## 4. TryTriggerBeRecovery — Intentional Asymmetry (L5182–L5198)

**Critical check — VERIFY_FAIL if dash absent.**

```
L5193:  if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
L5194:      return; // (5) DW-B90: only Stop-* triggers recovery
```

**Finding:** `"PTT-BE-Stop-"` WITH trailing dash is PRESENT at L5193. ✅
Intentional asymmetry preserved — bare `"PTT-BE-Stop"` will not trigger bracket recovery.
This is NOT a violation. Do NOT flag.

---

## 5. 5-Line Context — No Unintended Side Effects

### HasActivePttBeExits (changed line: L3110)

5 lines before L3110 → L3105–L3109: null guards and foreach setup — unchanged boilerplate.
5 lines after L3110 → L3111–L3115: instrument check, state check, return false — unchanged.

**Assessment:** Only string literal `"PTT-BE-Stop-"` → `"PTT-BE-Stop"` changed at L3110.
No surrounding logic altered. No side effects. ✅

### StillAtNonBePrice (changed lines: L7187–L7189)

5 lines before L7187 → L7182–L7186: null guards and foreach setup — unchanged.
5 lines after L7189 → L7190–L7194: instrument check, state guard, tick calc, return — unchanged.

**Assessment:** Only string literal `"PTT-BE-Stop-"` → `"PTT-BE-Stop"` changed at L7188.
No surrounding logic altered. No side effects. ✅

---

## 6. Jane Street Rules

### JS-042 — ASCII-only string literals

| Location | String | ASCII check |
|----------|--------|-------------|
| L3110 | `"PTT-BE-Stop"` | All 7-bit ASCII ✅ |
| L7188 | `"PTT-BE-Stop"` | All 7-bit ASCII ✅ |

### JS-001 — No exceptions thrown

- HasActivePttBeExits: `o.Name == null` guard appears before `StartsWith` call — null-safe, no throw. Returns `bool`. ✅
- StillAtNonBePrice: `o.Name == null` guard appears before `StartsWith` call — null-safe, no throw. Returns `bool`. ✅
- `StringComparison.Ordinal` on both calls — no culture exception. ✅

### JS-021 — No lock()

- HasActivePttBeExits (L3104–L3115): No `lock(` present. Uses `acc.Orders.ToList()` snapshot. ✅
  (Comment at L3102 confirms: `JS-021: no lock (ToList snapshot)`)
- StillAtNonBePrice (L7181–L7196): No `lock(` present. Uses `acc.Orders.ToList()` snapshot. ✅
  (Comment at L7180 confirms: `JS-021: no lock (acc.Orders is NT8 read-only)`)

---

## 7. CYC Counts

### HasActivePttBeExits — CYC = 8

Branch count (independent, from L3104–L3115):
1. Base = 1
2. `if (acc == null)` L3106 = +1 → 2
3. `if (instr == null)` L3107 = +1 → 3
4. `foreach` L3108 = +1 → 4
5. `o.Name == null` (LHS of `||`) L3110 = +1 → 5
6. `!o.Name.StartsWith(...)` (RHS of `||`) L3110 = +1 → 6
7. `if (o.Instrument?.FullName != instr.FullName)` L3111 = +1 → 7
8. `if (IsPttBeStopActiveState(o.OrderState))` L3112 = +1 → **8**

**CYC = 8 ✅ (expected: 8, budget: ≤ 8, unchanged)**
Architect's declaration at L3100 matches. String literal change added zero branches.

### StillAtNonBePrice — CYC = 7

Branch count (independent, from L7181–L7196):
1. Base = 1
2. `if (acc == null)` L7183 = +1 → 2
3. `if (instr == null)` L7184 = +1 → 3
4. `foreach` L7185 = +1 → 4
5. `o.Name == null || !o.Name.StartsWith(...)` (one compound `if`) L7187–7188 = +1 → 5
6. `if (o.Instrument?.FullName != instr.FullName)` L7190 = +1 → 6
7. `if (o.OrderState != OrderState.Working)` L7191 = +1 → **7**

**CYC = 7 ✅ (expected: 7, budget: ≤ 8, unchanged)**
Architect's declaration at L7178 matches. String literal change added zero branches.

---

## 8. Signal 2 Wiring — IsFollowerPositionBlocked (L3195)

**Read L3185–L3225:**

```
L3195:  if (HasActivePttBeExits(acc, instr))   // (5) Signal 2 -- pre-fill race
L3196:  {
L3197:      NinjaTrader.Code.Output.Process(
L3198:          "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "
L3199:              + acc.Name + "\n" + instr.FullName,
L3200:          NinjaTrader.NinjaScript.PrintTo.OutputTab1
L3201:      );
L3202:      return true;
L3203:  }
```

**Finding:** `HasActivePttBeExits` is called at L3195 as Signal 2 (branch 5) in `IsFollowerPositionBlocked`.
Position is correct: after Signal 1 `HasOpenPosition` (L3186) and before Signal 3 `HasSettlingBeTargets` (L3204).
Call site intact and correctly wired. ✅

---

## 9. 7-Scan Results (Independent — Layer 3)

| Scan | Rule | Check | Layer 3 Result |
|------|------|-------|----------------|
| SCAN-01 | JS-001 no-throw | Both methods return `bool`; `o.Name == null` guards before `StartsWith`; no `throw` | ✅ PASS |
| SCAN-02 | JS-002 no-null-return | Both return `false` (bool) on all guard exits; no null returned | ✅ PASS |
| SCAN-03 | JS-021 no-lock | No `lock(` in L3104–L3115 or L7181–L7196; `ToList()` snapshot used | ✅ PASS |
| SCAN-04 | JS-042 ASCII-only | `"PTT-BE-Stop"` at L3110 and L7188 — all 7-bit ASCII | ✅ PASS |
| SCAN-05 | JS-CYC within budget | HasActivePttBeExits CYC=8 ≤ 8; StillAtNonBePrice CYC=7 ≤ 8 | ✅ PASS |
| SCAN-06 | NT8 API safe | Both methods use `acc.Orders.ToList()` snapshot (L3108, L7185) — AddOnBase-safe | ✅ PASS |
| SCAN-07 | Signal continuity | HasActivePttBeExits called at L3195 as Signal 2 `(5)` in IsFollowerPositionBlocked; TryTriggerBeRecovery retains `"PTT-BE-Stop-"` with dash at L5193 (intentional asymmetry intact) | ✅ PASS |

**All 7 scans: 0 violations.**

---

## 10. Cross-Check With Engineer's Layer 2 Report

| Item | Engineer's claim | Verifier's independent finding | Match |
|------|-----------------|-------------------------------|-------|
| HasActivePttBeExits line | L3110 | L3110 | ✅ |
| HasActivePttBeExits string | `"PTT-BE-Stop"` no dash | `"PTT-BE-Stop"` no dash | ✅ |
| StillAtNonBePrice lines | L7187–L7188 | L7187–L7188 | ✅ |
| StillAtNonBePrice string | `"PTT-BE-Stop"` no dash | `"PTT-BE-Stop"` no dash | ✅ |
| TryTriggerBeRecovery L5193 | `"PTT-BE-Stop-"` with dash | `"PTT-BE-Stop-"` with dash | ✅ |
| HasActivePttBeExits CYC | 8 | 8 | ✅ |
| StillAtNonBePrice CYC | 7 | 7 | ✅ |
| Signal 2 wiring L3195 | HasActivePttBeExits called | Confirmed at L3195 | ✅ |
| 7-scan violations | 0 | 0 | ✅ |

**No discrepancies between engineer's Layer 2 report and verifier's Layer 3 independent scan.**

---

## 11. Final Verdict

**VERIFY_PASS**

Both `HasActivePttBeExits` (L3110) and `StillAtNonBePrice` (L7188) correctly use `"PTT-BE-Stop"`
without trailing dash, matching the B162-B specification (bare+suffixed pattern — catches both
`PTT-BE-Stop` bare leader stop and `PTT-BE-Stop-N` suffixed follower stops).

`"PTT-BE-Stop-"` with trailing dash is absent from both target methods (confirmed by direct read).

`TryTriggerBeRecovery` at L5193 retains `"PTT-BE-Stop-"` with dash — intentional asymmetry
preserved. This is by design and is NOT flagged.

CYC counts unchanged: `HasActivePttBeExits` = 8, `StillAtNonBePrice` = 7. Both within ≤ 8 budget.

All Jane Street rules satisfied. Signal 2 wiring intact at L3195. Zero violations across all 7 scans.
