# Ticket 3 Completion: B162B-VERIFY
## Status: TICKET_COMPLETE

Date: 2025-07-29
Engineer: ptt-engineer (verification-only run)
File: src/PropTraderTools/CopyEngine.cs
Ticket scope: VERIFICATION ONLY — no src modifications made.

---

## 1. HasActivePttBeExits — Exact Lines Found

**Method span: L3104–L3115**

```
L3110:  if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;   // (5) (6) B162-B: bare+suffixed
```

- Uses `"PTT-BE-Stop"` WITHOUT trailing dash. ✅
- `"PTT-BE-Stop-"` WITH trailing dash is **absent** from this method. ✅

---

## 2. StillAtNonBePrice — Exact Lines Found

**Method span: L7181–L7196**

```
L7187:  if (o.Name == null
L7188:      || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))     // B162-B: bare+suffixed
L7189:      continue;                                                             // (5)
```

- Uses `"PTT-BE-Stop"` WITHOUT trailing dash. ✅
- `"PTT-BE-Stop-"` WITH trailing dash is **absent** from this method. ✅

---

## 3. "PTT-BE-Stop-" With Dash — Absence Confirmation

Ran full-file grep for `PTT-BE-Stop-`. Matches found and classified:

| Line  | Method                | Code/Comment | Classification           |
|-------|-----------------------|--------------|--------------------------|
| 1750  | —                     | Comment      | Not code — no action     |
| 1752  | —                     | Comment      | Not code — no action     |
| 1875  | —                     | Comment      | Not code — no action     |
| 3162  | —                     | Comment      | Not code — no action     |
| 5120  | —                     | Comment      | Not code — no action     |
| 5174  | —                     | Comment      | Not code — no action     |
| 5177  | —                     | Comment      | Not code — no action     |
| **5193** | **TryTriggerBeRecovery** | **Code** | **INTENTIONAL — see §4** |
| 5288  | —                     | Comment      | Not code — no action     |
| 7175  | —                     | Comment      | Not code — no action     |
| 7199  | —                     | Comment      | Not code — no action     |
| **7222** | **TryFireBePriceRetry** | **Code** | **Intentional (B162-A atomic claim gate)** |

**Conclusion**: `"PTT-BE-Stop-"` with dash does NOT appear in `HasActivePttBeExits` or `StillAtNonBePrice` — confirmed absent from both target methods. ✅

---

## 4. TryTriggerBeRecovery — Intentional Dash (NOT a Discrepancy)

**L5193:**
```csharp
if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
    return; // (5) DW-B90: only Stop-* triggers recovery
```

This is **intentional design**. This guard controls OCO-pair bracket recovery for followers only.
Bare `"PTT-BE-Stop"` (without a suffix like `-1`, `-2`) must NOT trigger bracket recovery.
Only suffixed order names (`PTT-BE-Stop-1`, `PTT-BE-Stop-2`, etc.) are valid recovery triggers.

**TryFireBePriceRetry L7222** also uses `"PTT-BE-Stop-"` with dash — intentional (B162-A atomic claim gate, fires only on exact suffixed names).

**Neither occurrence is a discrepancy. Do NOT flag.**

---

## 5. CYC Counts

### HasActivePttBeExits — CYC = 8 ✅ (budget: ≤ 8)

Branches counted from source (L3104–L3115):
1. Base = 1
2. `if (acc == null)` L3106 = +1 → 2
3. `if (instr == null)` L3107 = +1 → 3
4. `foreach` L3108 = +1 → 4
5. `o.Name == null` (compound LHS) L3110 = +1 → 5
6. `!o.Name.StartsWith(...)` (compound RHS) L3110 = +1 → 6
7. `if (o.Instrument?.FullName != instr.FullName)` L3111 = +1 → 7
8. `if (IsPttBeStopActiveState(o.OrderState))` L3112 = +1 → **8**

Matches comment at L3100. Expected CYC=8 — **confirmed unchanged**.

### StillAtNonBePrice — CYC = 7 ✅ (budget: ≤ 8)

Branches counted from source (L7181–L7196):
1. Base = 1
2. `if (acc == null)` L7183 = +1 → 2
3. `if (instr == null)` L7184 = +1 → 3
4. `foreach` L7185 = +1 → 4
5. `o.Name == null || !o.Name.StartsWith(...)` (one `if` guard) L7187-7189 = +1 → 5
6. `if (o.Instrument?.FullName != instr.FullName)` L7190 = +1 → 6
7. `if (o.OrderState != OrderState.Working)` L7191 = +1 → **7**

Matches comment at L7178. Expected CYC=7 — **confirmed unchanged**.

---

## 6. 7-Scan Checklist

| Scan | Rule | Check | Result |
|------|------|-------|--------|
| SCAN-01 | JS-001 no-throw | Both methods use only `return false`/`return true`/`continue` — no `throw` | ✅ PASS |
| SCAN-02 | JS-002 no-null-return | `o.Name == null` checked at L3110 and L7187 before any `.Name` dereference | ✅ PASS |
| SCAN-03 | JS-021 no-lock | No `lock(` in either method; `acc.Orders.ToList()` snapshot is lock-free | ✅ PASS |
| SCAN-04 | JS-042 ASCII-only | `"PTT-BE-Stop"` — all characters are 7-bit ASCII | ✅ PASS |
| SCAN-05 | JS-CYC within budget | `HasActivePttBeExits` CYC=8 ≤ 8; `StillAtNonBePrice` CYC=7 ≤ 8 | ✅ PASS |
| SCAN-06 | NT8 API safe | Both use `acc.Orders.ToList()` snapshot — AddOnBase-safe, no live-collection mutation | ✅ PASS |
| SCAN-07 | Signal continuity | `HasActivePttBeExits` called at L3195 as Signal 2 `(5)` inside `IsFollowerPositionBlocked` | ✅ PASS |

All 7 scans: **0 violations**.

---

## 7. Final Status

**TICKET_COMPLETE**

Both `HasActivePttBeExits` and `StillAtNonBePrice` correctly use `"PTT-BE-Stop"` without trailing dash,
matching the B162-B specification (bare+suffixed — catches both `PTT-BE-Stop` bare and `PTT-BE-Stop-N` suffixed names).
`"PTT-BE-Stop-"` with dash is absent from both target methods.
`TryTriggerBeRecovery` and `TryFireBePriceRetry` retain `"PTT-BE-Stop-"` with dash — intentional, not discrepancies.
CYC counts unchanged: HasActivePttBeExits=8, StillAtNonBePrice=7.
All 7 scans pass with 0 violations.
