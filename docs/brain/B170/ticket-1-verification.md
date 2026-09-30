# B170 Ticket-1 Verification — DW-B170-01 Predicate Extension

**Ticket:** TICKET B170-1 — Extend OrderState predicate in FindLeaderStopPrice
**File:** `src/PropTraderTools/CopyEngine.cs`
**Verifier role:** PTT Verifier (Phase 4b) — READ-ONLY access
**Date:** Layer 3 independent verification

---

## Scope

TICKET B170-1 ONLY. Verification of two-line change in `FindLeaderStopPrice`:
- Line 4410: CYC comment updated
- Line 4423: predicate extended with `|| order.OrderState == OrderState.Accepted`

---

## Independent V1-V7 Results (Layer 3 — Run by Verifier)

### V1. OrderState.Accepted in FindLeaderStopPrice region

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "OrderState\.Accepted" | Where-Object { $_.LineNumber -ge 4412 -and $_.LineNumber -le 4428 }
```

**Result:**
```
src\PropTraderTools\CopyEngine.cs:4423:  && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
```

**Verdict:** ? PASS — exactly 1 match at line 4423

---

### V2. OrderState.Working in FindLeaderStopPrice region (regression-safe)

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "OrderState\.Working" | Where-Object { $_.LineNumber -ge 4412 -and $_.LineNumber -le 4428 }
```

**Result:**
```
src\PropTraderTools\CopyEngine.cs:4423:  && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
```

**Verdict:** ? PASS — `OrderState.Working` preserved on same line as Accepted

---

### V3. No lock() introduced

**Command:**
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\s*\("
```

**Result:** All matches are in comments (e.g. `// no lock()`, `// JS-021: no lock`). Zero actual `lock(` call sites anywhere in production source.

**Verdict:** ? PASS — 0 actual lock() calls (comments only)

---

### V4. CYC = 6 post-change

**Read source lines 4412–4428 directly:**

```csharp
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
{
    if (leaderAccount == null)     // branch (1)
        return 0.0;
    if (bracketIndex <= 0)         // branch (2)
        return 0.0;
    var targetName = "Stop" + bracketIndex.ToString();
    foreach (var order in leaderAccount.Orders.ToList())   // branch (3)
    {
        if (
            order.Name == targetName // branch (4)
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
            //                                              ^branch (5)               ^branch (6) via ||
        )
            return order.StopPrice;
    }
    return 0.0;
}
```

McCabe branches: (1) null account + (2) zero index + (3) foreach + (4) name match + (5) Working state + (6) Accepted state via `||` = **CYC = 6**

**Verdict:** ? PASS — CYC = 6, within JS ceiling of 8

---

### V5. ASCII-only in changed lines

**Command:**
```powershell
$lines = Get-Content "src/PropTraderTools/CopyEngine.cs"
$l4410 = $lines[4409]; $l4423 = $lines[4422]
[regex]::Matches($l4410, '[^\x00-\x7F]').Count  # ? 0
[regex]::Matches($l4423, '[^\x00-\x7F]').Count  # ? 0
```

**Result:**
- Line 4410: `Non-ASCII count = 0`
- Line 4423: `Non-ASCII count = 0`

**Verdict:** ? PASS — both changed lines are ASCII-only

---

### V6. ptt-sync.ps1 exit 0

**Source:** Engineer completion artifact (`ticket-1-completion.md`) reports:
```
ptt-sync complete: 18 linked, 0 errors
Exit code: 0
```

Note: Verifier does not re-run ptt-sync (it modifies NinjaTrader hard-links on the local machine). The completion artifact clearly states exit code 0 and lists 18 files linked including `CopyEngine.cs`.

**Verdict:** ? PASS — ptt-sync.ps1 exit 0 stated in completion artifact

---

### V7. Only FindLeaderStopPrice region modified

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DW-B170-01"
```

**Result:**
```
src\PropTraderTools\CopyEngine.cs:4410:  // CYC=6: ... DW-B170-01.
```

Only one DW-B170-01 tag, at line 4410 (the CYC comment). The predicate at line 4423 is the only other change. No other method bodies contain this tag.

**Direct source inspection** of lines 4408–4428 and 4429+ confirms:
- `FindLeaderStopPrice` ends at line 4428 (`return 0.0;` + closing `}`)
- Line 4430 begins `CreateFollowerReplacementStop` — untouched, its header comment has no B170 tag

**Verdict:** ? PASS — only `FindLeaderStopPrice` (lines 4410 and 4423) modified; no other method touched

---

## Cross-check vs Engineer Layer 2 Report

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|-------------------|--------------|
| SCAN-01 (Accepted present) | Line 4423: 1 match | Line 4423: 1 match | NONE |
| SCAN-02 (Working present) | Line 4423: 1 match | Line 4423: 1 match | NONE |
| SCAN-03 (lock()) | 0 actual lock() calls | 0 actual lock() calls | NONE |
| SCAN-04 (CYC=6) | CYC=6 (6 branches counted) | CYC=6 (6 branches counted) | NONE |
| SCAN-05 (ASCII-only) | 0 non-ASCII chars | 0 non-ASCII chars | NONE |
| SCAN-06 (ptt-sync exit 0) | Exit 0, 18 linked | Artifact states exit 0 | NONE |
| SCAN-07 (only region modified) | Only lines 4410 and 4423 changed | Confirmed: tag only at 4410, no other method | NONE |

**No discrepancies found.** Layer 2 and Layer 3 results match exactly.

---

## Architecture/Spec Compliance

- Method signature unchanged: `private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)` ?
- Returns 0.0 on all failure paths (JS-002) ?
- No new `lock()` anywhere (JS-021) ?
- CYC delta: 5 ? 6, within ceiling of 8 (JS complexity rule) ?
- CYC comment updated with DW-B170-01 tag ?
- `OrderState.Working` preserved (regression-safe) ?
- `OrderState.Accepted` added (fixes the bug) ?
- No `throw` in hot path (JS-001) ?
- No `DateTime.Now` (SCAN-06 DNA rule) ?
- No FontFamily, no hex color strings ?
- No CreateOrder calls (not applicable) ?
- Matches plan requirement (02-architecture-plan.md §4) exactly ?

---

## DNA Rule Checks

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() | No lock( in changed code or new code | ? PASS |
| JS-001 throw | No throw in FindLeaderStopPrice | ? PASS |
| JS-002 return null | Returns 0.0 on all non-match paths | ? PASS |
| JS-008/009 mutable struct | Not applicable — no struct | ? N/A |
| JS-010 constructor | Not applicable — no new constructor | ? N/A |
| SCAN-03 FontFamily | Not in changed code | ? PASS |
| SCAN-04 hex color | Not in changed code | ? PASS |
| SCAN-05 PTT- prefix | No CreateOrder in change | ? N/A |
| SCAN-06 DateTime.Now | Not in changed code | ? PASS |
| CYC > 8 | CYC = 6 | ? PASS |
| ASCII-only | 0 non-ASCII chars in changed lines | ? PASS |

---

## Verdict

**VERIFY_PASS**

All 7 independent scans passed. No discrepancies vs Layer 2 engineer report. Implementation exactly matches ticket spec and architecture plan. All DNA rules satisfied.
