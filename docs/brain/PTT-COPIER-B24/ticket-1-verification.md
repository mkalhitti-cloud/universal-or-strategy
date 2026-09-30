# PTT-COPIER-B24 — Ticket 1 Verification Report
**Phase**: 4b (Verifier)
**Verifier**: ptt-verifier
**Date**: 2026-07-07
**Defect**: DW-B23-BE-ALLACCOUNTS-01
**Source file verified**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

---

## Verdict

**VERIFY_PASS**

All checks A–F passed. Zero DNA violations in new or modified code.

---

## Check A — New overload location and signature

**PASS**

- `Select-String -Pattern "internal void BreakEven"` returned **two** matches:
  - Line 1176: `internal void BreakEven(Instrument instrument, int bufferTicks)` — existing 2-param overload
  - Line 1185: `internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)` — new B24 overload
- New overload appears **after** the existing one (1185 > 1176) ✅
- `Account leader` is **first** parameter ✅
- Gap between old overload end (line 1180) and new start (line 1185): 4 lines including comment block ✅
- Engineer reported: inserted at line 1183 (comment) / 1185 (declaration) — confirmed ✅

---

## Check B — New overload body

**PASS**

Source lines 1185–1198 read verbatim:
```csharp
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (leader == null)                                      // (1) null guard
    {
        StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
        return;
    }
    MoveStopToBreakEven(leader, instrument, bufferTicks);   // leader direct, no rule needed
    foreach (var acc in AllAccounts(instrument))            // (2) follower fan-out
    {
        if (acc == leader) continue;                        // (3) skip duplicate
        MoveStopToBreakEven(acc, instrument, bufferTicks);
    }
}
```

- `leader == null` guard → `StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped")` + `return` ✅
- `MoveStopToBreakEven(leader, instrument, bufferTicks)` called **before** the foreach ✅
- `foreach (var acc in AllAccounts(instrument))` ✅
- `if (acc == leader) continue;` skip-duplicate guard ✅
- No `lock(` inside method body ✅
- CYC = 4 (null guard=1, foreach=2, if-continue=3, base=1) ≤ 8 ✅

---

## Check C — OnPendingBeAccountUpdate call site

**PASS**

Source lines 1408–1415:
```csharp
var acc   = _pendingBeAccount;      // line 1408
var instr = _pendingBeInstrument;   // line 1409
var buf   = _pendingBeBufferTicks;  // line 1410
if (acc != null)
    acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
_pendingBeAccount    = null;
_pendingBeInstrument = null;
BreakEven(acc, instr, buf);         // line 1415
```

- Line 1415 reads `BreakEven(acc, instr, buf)` — 3-param call ✅
- `acc` is the local variable capturing `_pendingBeAccount` at line 1408 — NOT a new allocation ✅
- No `BreakEven(instr, buf)` (2-param) matches in `OnPendingBeAccountUpdate` — confirmed by
  `Select-String -Pattern "BreakEven\(instr, buf\)"` returning 0 results ✅

---

## Check D — Unchanged-code contract

**PASS**

| Symbol | Line | Status |
|--------|------|--------|
| `BreakEven(Instrument, int)` | 1176–1180 | UNCHANGED — 2-liner body identical |
| `MoveStopToBreakEven(Account, Instrument, int)` | 1133 | UNCHANGED — signature intact |
| `AllAccounts(Instrument)` | 1050 | UNCHANGED — signature intact |
| All `OnPendingBeAccountUpdate` lines except 1415 | — | UNCHANGED |

2-param `BreakEven` body (lines 1177-1180) confirmed:
```csharp
{
    foreach (var acc in AllAccounts(instrument))
        MoveStopToBreakEven(acc, instrument, bufferTicks);
}
```
This is the original 2-liner — not touched ✅

---

## Check E — Independent 7-scan results

### SCAN-01 — `lock\s*\(` in CopyEngine.cs
```
Select-String -Path "...\CopyEngine.cs" -Pattern "lock\s*\("
```
**Result**: 5 matches — ALL in comments (`-- no lock (JS-021)`, `try block(0)`, `ConcurrentBag rebuild pattern -- no lock`). Zero actual `lock(` call expressions in any line.
**Criterion**: 0 usages in new or modified code ✅

### SCAN-02 — `async void` in CopyEngine.cs
```
Select-String -Path "...\CopyEngine.cs" -Pattern "async void "
```
**Result**: 0 matches ✅

### SCAN-03 — `return null;` in new code
```
Select-String -Path "...\CopyEngine.cs" -Pattern "return null;"
```
**Result**: 4 matches at lines 663, 1067, 1073, 1126 — ALL in pre-existing methods:
- Line 663: `FindFollowerBracketOrder` (pre-B24)
- Lines 1067/1073: `FindRule` (pre-B24)
- Line 1126: `FindPosition` (pre-B24)

New overload (1185-1198) and modified call site (1415): **zero** `return null` ✅

### SCAN-04 — CYC of new overload (manual count)
- Base: 1
- `if (leader == null)`: +1 → 2
- `foreach (var acc in AllAccounts(instrument))`: +1 → 3
- `if (acc == leader) continue`: +1 → 4
- **CYC = 4** ≤ 8 ✅

### SCAN-05 — `\?\.\w+\s*-=` (null-conditional event unsubscription)
```
Select-String -Path "...\CopyEngine.cs" -Pattern "\?\.\w+\s*-="
```
**Result**: 0 matches ✅
(Existing `acc.AccountItemUpdate -= OnPendingBeAccountUpdate` at line 1412 is guarded by `if (acc != null)` — not a null-conditional unsubscription)

### SCAN-06 — `[Fact]` count in CopyEngineTests.cs
```
Select-String -Path "...\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Result**: Count = **126** ✅
(T1 does not add tests; T2 will raise this to 128)

### SCAN-07 — Syntax inspection of new block (lines 1182–1198)
- Comment block: 3 lines, well-formed ✅
- Method declaration: `internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)` ✅
- Opening brace on next line ✅
- `if` block: properly braced, 3 lines ✅
- `MoveStopToBreakEven(...)` call: correct 3-arg form ✅
- `foreach` block: properly braced ✅
- `if (acc == leader) continue;` single-statement — acceptable ✅
- Closing brace of `foreach`: ✅
- Closing brace of method: ✅
- No dangling tokens, no unclosed braces, no missing semicolons ✅

---

## Check F — Cross-check vs engineer's Layer 2 report

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|------|-------------------|-------------------|--------|
| SCAN-01 `lock(` | 5 comment-only matches, 0 actual | 5 comment-only matches, 0 actual | ✅ MATCH |
| SCAN-02 `async void` | 0 | 0 | ✅ MATCH |
| SCAN-03 `return null` in new code | 0 in new code | 0 in new code (4 in pre-existing) | ✅ MATCH |
| SCAN-04 CYC | 4 | 4 | ✅ MATCH |
| SCAN-05 null-conditional unsubscription | 0 | 0 | ✅ MATCH |
| SCAN-06 `[Fact]` count | 126 | 126 | ✅ MATCH |
| SCAN-07 Syntax | PASS | PASS | ✅ MATCH |

**Zero discrepancies** between engineer's Layer 2 self-report and verifier's independent Layer 3 re-run.

---

## Additional DNA checks (per role definition)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock(` anywhere | 0 actual lock calls | ✅ PASS |
| JS-001 `throw new ...Exception` in method | 0 in new overload | ✅ PASS |
| JS-002 `return null` where non-null expected | 0 in new overload | ✅ PASS |
| JS-033 `async void` | 0 | ✅ PASS |
| CYC ≤ 8 | CYC = 4 | ✅ PASS |
| NT8: `FontFamily=` | 0 in CopyEngine.cs | ✅ PASS |
| NT8: `#RRGGBB` hex color | 0 in CopyEngine.cs | ✅ PASS |
| NT8: `DateTime.Now` in new code | 0 in new overload (pre-existing line 766 in SendCopy, not touched) | ✅ PASS |
| NT8: `CreateOrder` with PTT- prefix | No CreateOrder in new overload | ✅ PASS |
| Singleton: `private CopyEngine()` | Unchanged | ✅ PASS |
| `TradeCopierWindow` not `sealed` | Not touched by T1 | ✅ N/A |

---

## Architecture compliance

- New overload `BreakEven(Account, Instrument, int)` placed immediately after existing `BreakEven(Instrument, int)` — correct per architecture plan ✅
- `OnPendingBeAccountUpdate` call site updated to `BreakEven(acc, instr, buf)` — correct per defect fix DW-B23-BE-ALLACCOUNTS-01 ✅
- `AllAccounts(instrument)` used for fan-out (includes both master and followers from rule) ✅
- Leader is first in fan-out via direct `MoveStopToBreakEven(leader, ...)` call, then followers iterate ✅
- `if (acc == leader) continue` prevents double-BE on leader account ✅

---

## Spec coverage

Defect **DW-B23-BE-ALLACCOUNTS-01**: `OnPendingBeAccountUpdate` was calling 2-arg `BreakEven(instr, buf)` which only iterated `AllAccounts` — skipping the leader if the leader was not in the follower list. The fix:
1. Adds a 3-param overload that fires the leader directly first, then iterates followers with a skip guard ✅
2. Updates the call site to route through the new overload with `acc` (leader) as first arg ✅

---

## Result

**VERIFY_PASS**

*ptt-verifier · PTT-COPIER-B24 · Ticket 1 · 2026-07-07*
