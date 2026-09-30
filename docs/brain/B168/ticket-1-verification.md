# Ticket B168-1 Verification Report
## PTT Verifier — Independent Verification
## Block: B168 | Ticket: B168-1 | Verifier: ptt-verifier

---

## Verdict: VERIFY_PASS

---

## Scope

File verified: `src/PropTraderTools/CopyEngine.cs`
Spec: DW-B168-01 design doc + 04-tickets.md B168-1

All checks run INDEPENDENTLY. Engineer scan results were NOT trusted until cross-checked.

---

## Check 1 — IsLeaderLegBeingChanged occurrence count

Command:
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsLeaderLegBeingChanged"
```

Results (6 total hits):
| Line | Type | Content |
|------|------|---------|
| 3815 | DEFINITION | `private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>` |
| 3820 | COMMENT | `// Test seam: exposes private IsLeaderLegBeingChanged for unit tests.` |
| 3823 | SEAM DECL | `internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>` |
| 3824 | SEAM CALL | `IsLeaderLegBeingChanged(o);` |
| 3952 | COMMENT | `// CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).` |
| 3983 | CALL SITE | `if (IsLeaderLegBeingChanged(leaderLeg))` |

Code occurrences (non-comment): **3** — definition (3815), seam call (3824), call site (3983).
Ticket requires exactly 3 code occurrences. **PASS.**

Matches engineer self-report (SCAN-04): YES — identical lines reported.

---

## Check 2 — lock() count

Command:
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
```

All 14 hits returned are comment-only lines referencing "no lock()" or "lock-free". Zero actual `lock(` syntax calls in executable code.

**PASS — 0 new lock() calls added.**

Matches engineer self-report (SCAN-01): YES.

---

## Check 3 — Method signatures and guard positioning

### Site A: IsLeaderLegBeingChanged (lines 3804–3818)

```csharp
// DW-B168-01: returns true when the leader's Stop{suffix} is mid-drag.
// Used by ResubmitCollateralLegs to suppress collateral cancel+resubmit
// for legs that have their own HandleAtmStopSync in flight.
// ChangePending: drag gesture dispatched to NT8.
// ChangeSubmitted: NT8 dispatched to broker, Accepted callback pending.
// Both states guarantee a concurrent HandleAtmStopSync is in flight.
// Zero-regression: single-stop drag leaves other legs in Working/Accepted
//   -- neither state matches -> guard is false -> unchanged behaviour.
// CYC=2: null guard(1) + expression body with state check(1) = CYC 2.
// JS-002: bool. JS-021: no lock. JS-001: no throw. ASCII-only.
// JS-036: no heap -- reads field on existing leaderLeg object, no ToList.
private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
    leaderLeg != null
    && (leaderLeg.OrderState == OrderState.ChangePending
        || leaderLeg.OrderState == OrderState.ChangeSubmitted);
```

Verification:
- Access modifier: `private static` ✅
- Return type: `bool` ✅
- Expression body: `=>` ✅
- Null guard: `leaderLeg != null` ✅
- State check: `ChangePending || ChangeSubmitted` ✅
- Comment header: matches spec verbatim ✅

### Site B: IsLeaderLegBeingChangedTestable (lines 3820–3824)

```csharp
// Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
// CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
// DW-B168-01: nullable param mirrors production signature -- required for Null test case.
internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
    IsLeaderLegBeingChanged(o);
```

Verification:
- Access modifier: `internal static` ✅
- Return type: `bool` ✅
- Expression body: `=>` ✅
- Delegates to private: `IsLeaderLegBeingChanged(o)` ✅
- Comment matches ticket verbatim ✅

### Site C: Guard inside ResubmitCollateralLegs (lines 3981–3984)

```
3981:   Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
3982:   // DW-B168-01: skip -- concurrent drag for this leg is in flight.
3983:   if (IsLeaderLegBeingChanged(leaderLeg))
3984:       continue;
3985:   ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
```

Positioning verification:
- Guard is AFTER FindLeaderCollateralOrder call (line 3981) ✅
- Guard is BEFORE ResubmitOneCollateralLeg call (line 3985) ✅
- Comment matches ticket verbatim ✅

**PASS — all 3 edit sites conform to specification.**

---

## Check 4 — No spurious whitespace outside 3 edit sites

Lines immediately before/after each edit site inspected:

- Before Site A (3802): `}` — closing brace of FindLeaderCollateralOrder. Clean. ✅
- After Site A (3819): blank separator line. Normal method separator. ✅
- After Site B (3825): blank separator line. Normal method separator. ✅
- Before Site C (3981): `FindLeaderCollateralOrder` call. No spurious whitespace. ✅
- After Site C (3985): `ResubmitOneCollateralLeg` call. No spurious whitespace. ✅

**PASS — no whitespace-only mutations outside the 3 edit sites.**

Note: Engineer's completion report claimed no whitespace-only mutations. Confirmed.

---

## Check 5 — CYC comment updated on ResubmitCollateralLegs

Lines 3951–3953:
```
3951: // DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.
3952: // CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).
3953: // CYC=3. Well within <=8.
```

Ticket requirement: DW-B142-QTY-DESYNC-01 + DW-B168-01 annotation and CYC=3. **PRESENT. PASS.**

Note: Old `// CYC=4:` comment at line 3954 is preserved (the engineer prepended the new block above). Both coexist. The new CYC=3 block is the current annotation. This is not a violation — the ticket said "add above", not "replace old line."

Matches engineer self-report (SCAN-06): YES.

---

## Check 6 — ASCII-only scan

Command:
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"
```

Result: **(no output)** — zero non-ASCII characters in entire file.

**PASS.**

Matches engineer self-report (SCAN-03): YES.

---

## Check 7 — ptt-sync.ps1

Engineer reported: exit code **0**, output: `ptt-sync complete: 18 linked, 0 errors`.

Per role protocol, ptt-sync.ps1 is NOT re-run by the verifier (it modifies the live NT8 path). Engineer report accepted as reported. No discrepancy to flag.

**NOTED. PASS.**

---

## DNA Rule Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw` in IsLeaderLegBeingChanged or ResubmitCollateralLegs | PASS |
| JS-002 | Returns `bool` not null | PASS |
| JS-021 | No `lock(` in executable code | PASS |
| JS-036 | No heap allocation — reads existing field only | PASS |
| ASCII | Zero non-ASCII characters | PASS |
| CYC<=8 | IsLeaderLegBeingChanged=2, IsLeaderLegBeingChangedTestable=1, ResubmitCollateralLegs=3 | PASS |
| NT8 async/await | No async/await in modified methods | PASS |
| DateTime.Now | No DateTime.Now usage in new code | PASS |

---

## Comparison vs Engineer Self-Report

| Scan | Engineer | Verifier | Match |
|------|----------|----------|-------|
| SCAN-01 lock() | 0 actual lock() calls (only in comments) | 0 actual lock() calls | YES |
| SCAN-02 CYC | CYC=2/1/3 | CYC=2/1/3 | YES |
| SCAN-03 ASCII | 0 non-ASCII | 0 non-ASCII | YES |
| SCAN-04 occurrences | 3 code sites (lines 3815,3824,3983) | 3 code sites (lines 3815,3824,3983) | YES |
| SCAN-05 whitespace | 0 spurious mutations | 0 spurious mutations | YES |
| SCAN-06 CYC comment | CYC=3 at lines 3951-3953 | CYC=3 at lines 3951-3953 | YES |
| SCAN-07 ptt-sync | exit 0, 18 linked | noted, not re-run | N/A |

**No discrepancies found. Engineer self-report is accurate.**

---

## Verdict: VERIFY_PASS
