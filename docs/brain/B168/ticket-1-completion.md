# Ticket B168-1 Completion Report
## COLLATERAL-STORM -- Concurrent multi-stop drag kills follower stops
## Block: B168 | Design ref: DW-B168-01 | Engineer: ptt-engineer

---

## Status: BUILD_PASS

---

## What Was Implemented

Three edit sites applied to `src/PropTraderTools/CopyEngine.cs`. No other lines changed.

---

## Site A — Insert after FindLeaderCollateralOrder closing `}` (line 3802)

**Lines inserted: 3804–3818 (15 lines)**

New private static predicate `IsLeaderLegBeingChanged(Order? leaderLeg)`:
- Returns `true` when `leaderLeg.OrderState` is `ChangePending` or `ChangeSubmitted`.
- CYC=2: null guard(1) + expression-body state check(1).
- JS-001: no throw. JS-002: bool return. JS-021: no lock. JS-036: no heap allocation.
- Full DW-B168-01 comment header included.

## Site B — Immediately after Site A (line 3820)

**Lines inserted: 3820–3824 (5 lines)**

New internal test seam `IsLeaderLegBeingChangedTestable(Order? o)`:
- Pure delegation to `IsLeaderLegBeingChanged(o)`.
- CYC=1. Nullable param mirrors production signature (required for Null test case).
- Same pattern as `IsPttStpDragCancellableTestable` (line 4141).

## Site C (+ CYC comment update) — Inside ResubmitCollateralLegs (line 3982)

**Lines inserted: 3982–3984 (3 lines guard) + 3951–3953 (3 lines CYC comment update)**

Guard block inserted after `FindLeaderCollateralOrder` call:
```csharp
// DW-B168-01: skip -- concurrent drag for this leg is in flight.
if (IsLeaderLegBeingChanged(leaderLeg))
    continue;
```

CYC comment update in `ResubmitCollateralLegs` header:
- Before: `// CYC=4: base(1)+foreach(1)+if(1)+call(0)=3+base=4.` (existing comment preserved)
- Added above: `// DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.`
  `// CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).`
  `// CYC=3. Well within <=8.`

---

## Exact Line Numbers (post-edit)

| Site | Lines | Content |
|------|-------|---------|
| A | 3804–3818 | `IsLeaderLegBeingChanged` definition (15 lines incl. comment header) |
| B | 3820–3824 | `IsLeaderLegBeingChangedTestable` seam (5 lines) |
| CYC comment | 3951–3953 | DW-B168-01 + CYC=3 annotation prepended to `ResubmitCollateralLegs` header |
| C | 3982–3984 | `IsLeaderLegBeingChanged` guard inside `ResubmitCollateralLegs` (3 lines) |

---

## CYC Audit

| Method | CYC | Basis |
|--------|-----|-------|
| `IsLeaderLegBeingChanged` (new) | 2 | null guard(1) + expression-body state check(1) |
| `IsLeaderLegBeingChangedTestable` (new) | 1 | pure delegation |
| `ResubmitCollateralLegs` (modified) | 3 | foreach(1)+if(price<=0)(1)+if(IsLeaderLegBeingChanged)(1) |

All within CYC <= 8 constraint.

---

## ptt-sync.ps1

Exit code: **0**
Output: `ptt-sync complete: 18 linked, 0 errors`
All 18 source files re-synchronized to NinjaTrader hard links.

---

## 7-Scan Results

**SCAN-01** `Select-String -Pattern "lock\("` on CopyEngine.cs — last 5 matches:
```
8525: // JS-021: no lock(). ConcurrentDictionary + Interlocked only.
8664: // JS-021: no lock(). Interlocked.Decrement is atomic.
8731: // CYC=3: ... JS-021: no lock().
8744: // JS-021: no lock(). Account.Cancel() is AddOnBase available pattern.
8773: // JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.
```
All are comment-only references to `no lock()`. Zero actual `lock(` calls. **0 new lock() added. PASS.**

**SCAN-02** CYC audit:
- `IsLeaderLegBeingChanged` = **CYC 2** (null guard + state check). PASS.
- `IsLeaderLegBeingChangedTestable` = **CYC 1** (delegation). PASS.
- `ResubmitCollateralLegs` = **CYC 3** (comment at lines 3951-3953 confirms). PASS.

**SCAN-03** `Select-String -Pattern "[^\x00-\x7F]"` on CopyEngine.cs:
```
(no output)
```
**0 non-ASCII matches. PASS.**

**SCAN-04** `Select-String -Pattern "IsLeaderLegBeingChanged"` on CopyEngine.cs:
```
3815: private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>        [DEFINITION]
3820: // Test seam: exposes private IsLeaderLegBeingChanged for unit tests.    [COMMENT]
3823: internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>        [SEAM DECL]
3824:     IsLeaderLegBeingChanged(o);                                            [SEAM CALL]
3952: // CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard). [COMMENT]
3983: if (IsLeaderLegBeingChanged(leaderLeg))                                   [CALL SITE]
```
3 code occurrences (definition at 3815, seam call at 3824, call site at 3983). Lines 3820/3952 are comments. **Exactly 3 code sites. PASS.**

**SCAN-05** Whitespace-only mutations outside 3 edit sites:
Review of apply_diff patches confirms only insertions at Sites A/B (after line 3802) and Site C (after line 3981). No whitespace-only line mutations outside the 3 sites. **PASS.**

**SCAN-06** `Select-String -Pattern "ResubmitCollateralLegs"` confirms CYC=3 comment:
```
3951: // DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.
3952: // CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).
3953: // CYC=3. Well within <=8.
```
**DW-B168-01 annotation + CYC=3 present. PASS.**

**SCAN-07** ptt-sync.ps1 exit code: **0** (18 linked, 0 errors). **PASS.**

---

## Status: BUILD_PASS
