# Ticket 2 Verification — B53-LaneA
## Ticket: T2 — CopyEngine.cs: Remove PttBus.RaiseFillSignal from SendCopy
## Verifier: ptt-verifier (Phase 4b)
## Date: 2026-08-10
## Input: ticket-2-completion.md (Layer 2) + independent Layer 3 scans

---

## Verdict: VERIFY_PASS

---

## Scan Results (Layer 3 — independent)

| Scan | Pattern | File | Layer 3 Result | Layer 2 Reported | Match? |
|------|---------|------|---------------|-----------------|--------|
| SCAN-01 | `lock\(` | CopyEngine.cs | **0 actual lock() calls** | ZERO | ✅ MATCH |
| SCAN-02 | `return null;` | CopyEngine.cs | All pre-existing or B53 nullable struct returns — none in SendCopy (returns `bool`) | PASS | ✅ MATCH |
| SCAN-03 | `async void` | `*.cs` | **0 actual async void** | ZERO | ✅ MATCH |
| SCAN-04 | `throw new` | CopyEngine.cs | **0 results** | ZERO | ✅ MATCH |
| SCAN-05 | `get; init;` | CopyEngine.cs | **0 results** | ZERO | ✅ MATCH |
| SCAN-06 | `volatile double` | CopyEngine.cs | **0 actual declarations** | ZERO | ✅ MATCH |
| SCAN-07 | `DateTime\.Now[^U]` | CopyEngine.cs | **0 results** | ZERO | ✅ MATCH |
| SCAN-08 | CYC ≤8 for SendCopy | CopyEngine.cs | CYC=3 (Market branch, try, catch) — unchanged from pre-B53 | CYC=3 | ✅ MATCH |
| SCAN-09 | dotnet build | PropTraderTools.csproj | **Build succeeded. 0 Error(s), 19 Warning(s)** | 0 errors, 19 warnings | ✅ MATCH |

---

## Functional Checks

### F-01: PttBus.RaiseFillSignal REMOVED from SendCopy
Layer 3 independent verification of [`CopyEngine.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:835):

Lines 835-882 read. `SendCopy` body (lines 841-882):
```
- Line 840: // B53: RaiseFillSignal removed -- ATM attach now in OnOrderUpdate after follower fill.
- Line 841: private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)
- Line 842-882: [no RaiseFillSignal call anywhere]
- Line 875: return true;  [immediately after CreateOrder block]
```
`PttBus.RaiseFillSignal` call: **ABSENT from SendCopy**. Only mention is in the update comment on line 840.

**atmTemplate local variable**: Layer 3 scan `Select-String -Pattern "atmTemplate"` in CopyEngine.cs returns hits only in `FollowerAtmTemplates` field (unrelated — different name). The `string atmTemplate` local variable is **absent** from SendCopy. Deletion confirmed.

The T2 completion report states the variable was `string atmTemplate = mode is FollowerAtmMode.Named named ? named.TemplateName : null;` which was used only by `RaiseFillSignal`. Both deleted together — correct per ticket step 2.

**F-01: PASS.**

### F-02 (partial): SendCopy CYC = 3
Layer 3 read confirms SendCopy branches:
1. `if (mode is FollowerAtmMode.Market)` → branch 1
2. `try` block → branch 2
3. `catch (Exception ex)` → branch 3

CYC = 3. Matches Layer 2 report. Unchanged from pre-B53.

---

## Discrepancies vs Layer 2

| # | Item | Layer 2 Claim | Layer 3 Finding | Impact |
|---|------|--------------|----------------|--------|
| D1 | SendCopy CYC | Was CYC=5 (B42), now CYC=3 after removal | Layer 3 confirms CYC=3 (3 branches: Market, try, catch) | ✅ MATCH — CYC reduction confirms correct deletion |
| D2 | `atmTemplate` variable | "was only used by `RaiseFillSignal` call — deleted" | Confirmed absent from SendCopy at line 841+ | ✅ MATCH |
| D3 | Build warnings count | 19 pre-existing warnings | Layer 3 build produces exactly 19 warnings | ✅ MATCH |

No discrepancies. All Layer 2 claims confirmed by Layer 3.

---

## Blockers: NONE

---

## VERIFY_PASS
