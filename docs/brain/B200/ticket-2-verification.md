# Ticket 2 Verification: B201-ENTRY-CANCEL-01 Code Fix

**Epic**: PTT-COPIER-B200 + B201
**Ticket**: T2 — Scope `IsQxCancelCandidate` "Entry" cancel to follower accounts only
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-15
**Source**: `docs/brain/B200/04-tickets.md` T2 section
**Engineer artifact**: `docs/brain/B200/ticket-2-completion.md`

---

## Pre-Verification Context

**Critical override carried forward from `04-ticket-review.md` and `02-architecture-plan.md`**:

- `BuildQxSnapshot` is `internal static` — cannot call instance method `IsFollowerAccount`.
- Correct implementation uses `bool isFollower = false` as a **defaulted 3rd parameter** in the signature.
- `bool isFollower = IsFollowerAccount` grep MUST return **2 matches** (NOT 3).
  The orchestrator prompt (`B200-B201-orchestrator-prompt.md`) incorrectly says 3 matches.
  The plan review override (Section D and O in `02-plan-review.md`, confirmed in `04-ticket-review.md`) is authoritative: **2 matches**.

---

## 7-Scan Independent Results (Layer 3)

All scans run independently via `execute_command`. Engineer Layer 2 results NOT trusted until cross-checked.

### SCAN 1 — Lock ban (JS-021)

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Measure-Object
```

**Verifier result: Count = 9**

All 9 hits are comment strings containing the phrase "no lock()" or "without lock()":

| Line | Content |
|------|---------|
| L326 | `// JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.` |
| L360 | `// ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.` |
| L2337 | `// Value: ConcurrentBag<Order> -- thread-safe add, no lock().` |
| L4875 | `// ASCII-only. No DateTime.Now. No lock().` |
| L4899 | `// JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.` |
| L7303 | `// JS-021: no lock(). GetCloneAtmMode reads volatile field...` |
| L7445 | `// JS-021: no lock(). Interlocked.Decrement is atomic.` |
| L7519 | `// CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().` |
| L7530 | `// JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.` |

**Zero actual `lock(` statements in source. SCAN 1: PASS**

### SCAN 2 — Async void ban (JS-033)

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
```

**Verifier result: Count = 0 — SCAN 2: PASS**

### SCAN 3 — B201 Fix Verification (CRITICAL)

#### 3a. isFollower guard in body

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "isFollower && o\.Name" | Measure-Object
```

**Verifier result: Count = 1**

Match at **L995**: `if (isFollower && o.Name == "Entry")`

**SCAN 3a: PASS (1 match)**

#### 3b. IsFollowerAccount assignment count

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = IsFollowerAccount" | Measure-Object
```

**Verifier result: Count = 2**

| Line | Location |
|------|---------|
| L1012 | `bool isFollower = IsFollowerAccount(acc);` inside `CancelQxBrackets` 2-arg body |
| L1212 | `bool isFollower = IsFollowerAccount(acc);` inside `CancelQxBrackets` 3-arg body |

**SCAN 3b: PASS — exactly 2 matches. BuildQxSnapshot correctly uses defaulted param (NOT IsFollowerAccount call). Static constraint honored.**

#### 3c. BuildQxSnapshot signature defaulted param

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = false"
```

**Verifier result**: Match at **L1055**: `bool isFollower = false`

Confirmed L1055 is inside the `BuildQxSnapshot` method signature (between `(` at L1052 and `)` at L1056), NOT a body assignment:

```csharp
// L1052:
internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
    NinjaTrader.Cbi.Account acc,          // L1053
    NinjaTrader.Cbi.Instrument instr,     // L1054
    bool isFollower = false               // L1055  <-- PARAMETER, not body statement
)                                         // L1056
{                                         // L1057
```

**SCAN 3c: PASS — `bool isFollower = false` is a defaulted parameter in the signature**

Additional static constraint verification — `IsFollowerAccount` NOT present in `BuildQxSnapshot` body (L1057-L1081):

```powershell
Get-Content CopyEngine.cs | Select-Object -Index (1056..1081) | Select-String "IsFollowerAccount"
# Result: no output (0 matches)
```

**Static constraint: PASS**

### SCAN 4 — Test Suite

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```

**Verifier result**: Failed: **24**, Passed: **164**, Skipped: **531**, Total: **719**, Duration: ~936ms

Pre-existing 24 failures confirmed. **0 new failures introduced by T2.**

**SCAN 4: PASS**

### SCAN 5 — ptt-sync

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**Verifier result**: `ptt-sync complete: 19 linked, 0 errors`

**SCAN 5: PASS**

### SCAN 6 — ASCII Check

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[\x80-\xFF]" | Measure-Object
```

**Verifier result: Count = 0 — SCAN 6: PASS**

### SCAN 7 — Deletion Audit + Single-Hunk Ban + Guardian

#### 7a. Deletion audit

```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\-\s+(private|internal|public)"
```

Note: CopyEngine.cs is not yet committed (modified vs HEAD at B194 commit). Diff is vs HEAD.

**Verifier result** — 6 deleted symbol lines:

| Deleted Symbol | T2 Scope? | Authorized? |
|---------------|-----------|-------------|
| `internal static bool IsQxCancelCandidate(Order o)` | YES — T2 signature change | YES — B201-ENTRY-CANCEL-01 fix design |
| `internal bool ShouldSkipFollowerDispatch(Account acc)` | NO — pre-existing divergence | N/A — prior epic, not T2 |
| `private bool SendCopyWithAtm(` | NO — pre-existing divergence | N/A — method still exists at L5307 with updated sig |
| `internal void EvictDedup(string orderId, OrderState state)` | NO — pre-existing divergence | N/A — method still exists at L6060 with updated sig |
| `private bool PassesDailyCapCheck(Account acc)` | NO — pre-existing divergence | N/A — method still exists at L6188 |
| `private static bool HasNakedPosition(Account acct)` | NO — pre-existing divergence | N/A — method still exists at L7263 |

All 5 non-T2 deleted symbol lines are pre-existing git HEAD divergence from prior epics (B198/B199/B200 work not yet committed to main). All methods confirmed present in current working file. **T2-scope deletion: only `IsQxCancelCandidate` signature replaced (authorized). SCAN 7a: PASS**

#### 7b. Single-hunk ban

```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\@\@").Count
```

**Verifier result: Count = 79**

79 hunks >> 1. **SCAN 7b: PASS (not a full-file replace)**

#### 7c. Guardian script

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

**Verifier result**: `GUARDIAN FAIL -- missing symbols and/or gate violations:`
`- TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`

This is **pre-existing failure DW-B198-GUARDIAN-01** — documented in ticket scope. T2 did not touch `TradeCopierPanel.cs`. **NOT a T2 regression. SCAN 7c: PASS for T2 scope.**

---

## Code Inspection — 5 Touch Points

Independent code reads of CopyEngine.cs touch areas:

### TP1 + TP2 — IsQxCancelCandidate signature + body (L983-L997)

```csharp
// L981: CYC=7: 1 (base) + 6 if-branches. JS-021: no lock. JS-001: no throw. ASCII-only.
// L982: B201-ENTRY-CANCEL-01: isFollower param scopes "Entry" cancel to follower accounts only.
// L983:
internal static bool IsQxCancelCandidate(Order o, bool isFollower)
{
    if (o == null || o.Name == null)
        return false; // (1)
    if (IsAtmBracketName(o.Name))
        return true; // (2)
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal))
        return true; // (3)
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
        return true; // (4)
    if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
        return true; // (5) B70 DW-B70-02
    if (isFollower && o.Name == "Entry")
        return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only (B201-ENTRY-CANCEL-01 FIX)
    return false;
}
```

**TP1**: Signature `(Order o, bool isFollower)` — CORRECT ✓
**TP2**: Guard `if (isFollower && o.Name == "Entry")` at L995 — CORRECT ✓
**CYC comment**: L981 confirms `CYC=7` — unchanged (+0 delta) ✓

### TP3 — CancelQxBrackets 2-arg body (L1008-L1041)

**Signature** at L1008: `internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)` — UNCHANGED ✓

Body (after null guard at L1010-1011):
- **L1012**: `bool isFollower = IsFollowerAccount(acc);` — CORRECT ✓ (added at correct position)
- **L1028**: `if (IsQxCancelCandidate(o, isFollower))` — CORRECT ✓ (call threaded)

CYC comment at L1006: `CYC=6` — unchanged ✓

### TP4 — BuildQxSnapshot signature + body (L1052-L1081)

**Signature** (L1052-L1056):
```csharp
internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    bool isFollower = false
)
```
**CORRECT**: `bool isFollower = false` is a defaulted **parameter** — NOT an `IsFollowerAccount(acc)` call inside the body. Static constraint honored. ✓

Body at L1073: `if (IsQxCancelCandidate(o, isFollower))` — CORRECT ✓

`IsFollowerAccount` confirmed absent from method body (L1057-L1081): **PASS** ✓

### TP5 — CancelQxBrackets 3-arg body (L1204-~L1250)

**Signature** at L1204-1208: `internal void CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)` — UNCHANGED ✓

Body (after null guard at L1210-1211):
- **L1212**: `bool isFollower = IsFollowerAccount(acc);` — CORRECT ✓ (added at correct position)
- **L1234**: `if (IsQxCancelCandidate(o, isFollower))` — CORRECT ✓ (call threaded)

CYC comment at L1200-1201: `CYC=7` — unchanged ✓

---

## Acceptance Criteria Verification

| Criterion | Expected | Verifier Result | Status |
|-----------|---------|-----------------|--------|
| `isFollower && o.Name` grep | 1 match | 1 match at L995 | ✓ PASS |
| `bool isFollower = IsFollowerAccount` grep | **2 matches** (NOT 3) | 2 matches (L1012, L1212) | ✓ PASS |
| `bool isFollower = false` grep | >= 1 match | 1 match at L1055 (in signature) | ✓ PASS |
| `BuildQxSnapshot` signature includes `bool isFollower = false` | YES | L1055 confirmed in signature | ✓ PASS |
| `IsQxCancelCandidate` signature updated to `(Order o, bool isFollower)` | YES | L983 confirmed | ✓ PASS |
| `IsFollowerAccount` NOT in `BuildQxSnapshot` body | 0 matches | 0 matches in L1057-L1081 | ✓ PASS |
| lock() = 0 actual statements | 0 | 9 comment hits, 0 actual statements | ✓ PASS |
| All existing tests: 24 failures (same as pre-T2, 0 new) | 24 pre-existing only | 24 Failed, 164 Passed, 531 Skipped | ✓ PASS |
| ptt-sync 19/0 | 19 linked, 0 errors | 19 linked, 0 errors | ✓ PASS |
| Ph4b deletion audit: IsQxCancelCandidate sig change authorized | 1 authorized deletion | 1 T2 deletion (sig replaced), 5 pre-existing divergence | ✓ PASS |
| Single-hunk ban: hunks > 1 | > 1 | 79 hunks | ✓ PASS |
| Guardian: pre-existing DW-B198-GUARDIAN-01 only | Known pre-existing | TradeCopierPanel BrushTeal — pre-existing | ✓ PASS |
| No Feature files modified | NO | Only CopyEngine.cs modified | ✓ PASS |

---

## JS DNA Rule Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | 0 actual lock() statements | ✓ PASS — 9 hits are all comments |
| JS-033 (no async void) | 0 matches | ✓ PASS |
| JS-001 (no throw in hot path) | No new throw statements in any TP | ✓ PASS |
| JS-002 (no return null) | BuildQxSnapshot returns empty HashSet on null — no change | ✓ PASS |
| JS-066 (CYC <= 8) | IsQxCancelCandidate=7, CancelQxBrackets 2-arg=6, CancelQxBrackets 3-arg=7, BuildQxSnapshot=4 | ✓ PASS — all <= 8, all +0 delta |
| ASCII-only | SCAN 6 = 0 non-ASCII bytes | ✓ PASS |
| Static constraint | IsFollowerAccount NOT called inside static BuildQxSnapshot | ✓ PASS |

---

## Layer 2 vs Layer 3 Comparison

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|-------------------|--------------|
| SCAN 1 (lock) | 9 hits, all comments, lines L326/L360/L2337/L4875/L4899/L7303/L7445/L7519/L7530 | 9 hits, same lines, all comments | None |
| SCAN 2 (async void) | Count: 0 | Count: 0 | None |
| SCAN 3a (isFollower && o.Name) | Count: 1 at ~L994 | Count: 1 at L995 | 1-line approximation (engineer wrote ~L994, actual L995) — non-material |
| SCAN 3b (bool isFollower = IsFollowerAccount) | Count: 2 | Count: 2 at L1012, L1212 | None |
| SCAN 3c (BuildQxSnapshot sig) | defaulted param confirmed | L1055 in signature | None |
| SCAN 4 (dotnet test) | Failed: 24, Passed: 164, Skipped: 531 | Failed: 24, Passed: 164, Skipped: 531 | None |
| SCAN 5 (ptt-sync) | 19 linked, 0 errors | 19 linked, 0 errors | None |
| SCAN 6 (ASCII) | Count: 0 | Count: 0 | None |
| SCAN 7a (deletion audit) | 6 deleted lines, T2 scope: IsQxCancelCandidate only | Same 6 deleted lines, same attribution | None |
| SCAN 7b (single-hunk ban) | 79 hunks | 79 hunks | None |
| SCAN 7c (guardian) | Pre-existing DW-B198-GUARDIAN-01 | Pre-existing DW-B198-GUARDIAN-01 | None |

**Zero discrepancies between engineer Layer 2 and verifier Layer 3.**

---

## Warnings

None. All acceptance criteria met. All DNA rules satisfied. All 5 touch points correctly implemented.

---

## Overall Verdict

**VERIFY_PASS**

All 7 scans independently confirmed. All 5 touch points verified in source. BuildQxSnapshot static constraint correctly honored (defaulted parameter, NOT IsFollowerAccount inside static method). `bool isFollower = IsFollowerAccount` grep = exactly 2 matches (correct per plan review override — NOT 3). Zero new test failures. ptt-sync 19/0. Single-hunk ban (79 hunks). Guardian failure is pre-existing DW-B198-GUARDIAN-01 only.

---

*Written by: ptt-verifier (Phase 4b)*
*Epic: B200+B201*
*Date: 2026-10-15*
*Source: docs/brain/B200/ticket-2-completion.md (engineer Layer 2) + independent Layer 3 scans*
*Override carried forward: BuildQxSnapshot uses bool isFollower = false defaulted param (NOT IsFollowerAccount). 2 grep matches for IsFollowerAccount (NOT 3).*