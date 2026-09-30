# B141 Ticket 1 Completion Report — OCO Cascade Dual-Resubmit

**Block**: B141
**Ticket**: T1 (single ticket block)
**Engineer**: ptt-engineer
**Completed**: 2026-09-01
**Basis**: docs/brain/B141/04-tickets.md (TICKET_REVIEW_PASS confirmed)

---

## SCOPE LOCK

**Ticket 1 ONLY.** No other tickets read, referenced, or implemented in this session.

---

## Rules Catalog Gate

- **GATE RESULT: PASS**
- JS-021 (no `lock()`): confirmed applicable and satisfied
- JS-033 (no `async void`): confirmed applicable and satisfied
- JS-001 (no `throw` in hot path): confirmed applicable and satisfied
- JS-041 (CYC <= 8): confirmed applicable and satisfied
- Zero P0 violations in modified files confirmed by TICKET_REVIEW_PASS

---

## Summary of 5 Changes Made

### Change 1: `SyncFollowerBracket` branch (3) — modified (CopyEngine.cs ~line 2281)

**Before**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
{
    SyncAtmFollowerBracket(acc, fo, newPrice); // cancel+resubmit (acc.Change is no-op on ATM brackets)
    return;
}
```

**After**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + DW-B153
{
    double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, fo.Name); // B141: capture before cascade
    SyncAtmFollowerBracket(acc, fo, newPrice);   // cascade kills linked target (accepted, by design)
    if (capturedTargetPrice.HasValue)            // B141: +1 branch -> CYC 8 (at limit -- no further branching may be added)
        ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder);
    return;
}
```

Invariants preserved:
- `SyncAtmFollowerBracket` called UNCONDITIONALLY (not gated on HasValue)
- `ResubmitTargetAfterCascade` called ONLY when `capturedTargetPrice.HasValue`

### Change 2: `CaptureLinkedTargetPrice` — new private method (after SyncAtmFollowerBracket closing brace)

Captures `LimitPrice` of linked NT8 ATM target before Stop cancel+resubmit triggers OCO cascade.
Maps: `"Stop1"` -> `"Target1"`, `"Stop2"` -> `"Target2"`, `"Stop3"` -> `"Target3"`.
Returns `null` if target not found or suffix not in {1,2,3}.

### Change 3: `TryParseStopSuffix` — new private static method

Pure predicate: extracts suffix from NT8 ATM stop name.
Accepts only integer suffixes 1, 2, or 3. Rejects `null`, length < 5, suffix out of range.

### Change 4: `IsTargetOrderLive` — new private static method (expression body)

Pure state predicate: returns `true` if order is `Working` or `Accepted`.
CYC=1. JS-002: bool return, never null.

### Change 5: `ResubmitTargetAfterCascade` — new private method

After OCO cascade cancels linked ATM target, resubmits a standalone `PTT-TGT-Drag` limit order
at the captured price.

Block A-Prime: sweeps stale `PTT-TGT-Drag` (prevents accumulation on consecutive drags, DW-B139 pattern).
Block B: `CreateOrder` + `Submit`. `oco=""` — PTT-TGT-Drag is NOT in any ATM OCO group.

---

## File Outputs

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Modified (Change 1 + Changes 2-5 inserted) |
| `tests/PropTraderTools.Tests/B141Tests.cs` | Created (7 xUnit [Fact] tests) |

---

## SCAN-01: No `lock()` in modified range

**Command**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "^\s*lock\s*\(" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2700 }
```

**Output**: (no output)

**Result**: PASS — 0 hits

---

## SCAN-02: No `async void`

**Command**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "async void"
```

**Output**: 1 comment-only hit (line 1632, contains "JS-033: Tick is not async void") — no actual `async void` declaration.

**Result**: PASS — 0 actual `async void` declarations

---

## SCAN-03: No `throw new` in modified range

**Command**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2700 }
```

**Output**: (no output)

**Result**: PASS — 0 hits

---

## SCAN-04: CYC Counts (manual line-by-line)

Convention: base=1, each `if`/`foreach`/`for`/`while`/`?:`=+1, `&&`/`||`=0, `catch`=0.

| Method | CYC Branches | Total CYC | Limit | Status |
|--------|-------------|-----------|-------|--------|
| `SyncFollowerBracket` (modified) | base(1)+fo-null(1)+price-delta(1)+ATM-STP-branch3(1)+ATM-TGT-branch3b(1)+IsTrailingStop(1)+isStop-inner(1)+HasValue-B141(1) | **8** | 8 | **PASS — at limit** |
| `CaptureLinkedTargetPrice` (new) | base(1)+if-TryParse(1)+foreach(1)+if-IsTargetOrderLive(1) | **4** | 8 | PASS |
| `TryParseStopSuffix` (new) | base(1)+if-null-length(1)+if-TryParse-range(1) | **3** | 8 | PASS |
| `IsTargetOrderLive` (new) | base(1) — pure expression body, no if/foreach | **1** | 8 | PASS |
| `ResubmitTargetAfterCascade` (new) | base(1)+foreach-A-Prime(1)+if-Working(1)+if-newTarget-null(1), both catch=0 | **4** | 8 | PASS |

**SCAN-04 Result**: PASS — all methods CYC <= 8.

---

## SCAN-05: ASCII-only check on new string literals

**Command**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2700 }
```

**Output**: (no output)

New string literals verified ASCII-only:
- `"Target"` + suffix
- `"PTT-TGT-Drag"`
- `"B141 TGT CreateOrder returned null"`
- `"B141 TGT resubmit after cascade -> "`
- `"B141 TGT create error: "`
- `"TGT pre-cancel error (B141): "`

**Result**: PASS — 0 non-ASCII characters in modified range

---

## SCAN-06: Build Check

**Command**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

**Output**:
```
Build succeeded.
    1 Warning(s)  [pre-existing xUnit2004 in B131Tests.cs -- not introduced by B141]
    0 Error(s)
```

**Result**: PASS — 0 errors, 0 CS1503, 0 CS0246

---

## SCAN-07: Test Run

**Command**:
```powershell
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --filter "B141" --verbosity minimal
```

**Output**:
```
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 5 ms - PropTraderTools.Tests.dll (net8.0)
```

**Tests Passed**:
- T_B141_01: `CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice` — PASS
- T_B141_02: `CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice` — PASS
- T_B141_03: `CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice` — PASS
- T_B141_04: `CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull` — PASS
- T_B141_05: `SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound` — PASS
- T_B141_06: `SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent` — PASS
- T_B141_07: `SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled` — PASS

**Result**: PASS — 7/7

---

## ptt-sync-and-verify.ps1

**Command**:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

**Output**:
```
COPIED:  CopyEngine.cs
  Copied:   1  |  In-sync: 17  |  Excluded: 62
=== PTT VERIFY: MD5 check every synced file ===
  OK       CopyEngine.cs
=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

**Result**: 0 MISMATCH — PASS

---

## Deferred Work Updates Post-T1

| ID | Post-T1 Status |
|----|----------------|
| DW-B153 | CLOSED — re-closed by B141 T1 dual-resubmit |
| DW-B154 | DOCUMENTED — unchanged |
| DW-B140-01 | CLOSED — superseded (acc.Change no-op confirmed) |
| DW-B140-02 | CLOSED — superseded (acc.Change approach abandoned) |
| DW-B140-03 | CLOSED — superseded (B141 Gate 3 replaces) |
| DW-B141-STP-CYC8-WALL | OPEN — SyncFollowerBracket at CYC 8 limit; no further branching permitted |

---

## NT8 Compliance Notes

- `acc.Orders.ToList()` — snapshot enumeration (safe, no lock required on NT8 dispatch thread)
- `acc.Cancel(new Order[] { o })` — AddOnBase-available
- `acc.CreateOrder(12 params)` — 12-parameter signature with `(NinjaTrader.Cbi.CustomOrder)null` as arg12 (NT8-007 CS1503 guard)
- `acc.Submit(new[] { newTarget })` — AddOnBase-available
- `NinjaTrader.Core.Globals.MaxDate` — used for order expiry (DateTime.Now BANNED)
- `"PTT-TGT-Drag"` — PTT- prefix compliant (NT8-014)
- `oco=""` — PTT-TGT-Drag is NOT in any ATM OCO group

---

## Final Verdict

**BUILD_PASS**

All 7 scans: ZERO.
All 7 tests: 7/7 pass.
Sync + MD5 verify: 0 MISMATCH.
CYC: all methods <= 8 (SyncFollowerBracket at CYC 8 limit, not exceeded).
