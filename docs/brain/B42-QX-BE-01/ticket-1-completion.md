# B42-QX-BE-01 — Ticket T1 Completion Report

**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-08-05
**Ticket**: T1 — PttBreakEven.cs: Add `IsPttQxTarget` + extend `SnapshotTargetsLocal`
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs`
**Prerequisite**: TICKET_REVIEW_PASS confirmed in `04-ticket-review.md` (Iteration 2)

---

## RULES CATALOG GATE: PASS

| Rule | Verdict |
|------|---------|
| JS-021 (lock) | PASS — no `lock(` introduced; `IsPttQxTarget` is pure static computation |
| JS-002 (return null) | PASS — `IsPttQxTarget` returns `bool`; no null return in new code |
| JS-001 (throw) | PASS — no throw; early return guard only |
| JS-033 (async void) | PASS — all methods synchronous |
| NT8-006 (no LINQ) | PASS — `IsPttQxTarget` uses char primitives + `.Length` only; no LINQ import |

---

## Changes Applied

### Change 1 of 2 — Add `IsPttQxTarget` method (inserted after line 245)

**Location**: Immediately after the closing `}` of `IsAtmTargetName()` (original line 245).
**New lines inserted**: 245 (blank) + 246-259 (doc comment + method body) = 15 new lines.
**Method now at line 254** in updated file (254 lines offset after insert).

```csharp
/// <summary>
/// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
/// These are plain Limit orders -- LimitPrice and Quantity are readable.
/// BUG-B42-QX-BE-01 FIX (Direction 1): BE All after Quick All must recognise QX targets.
/// CYC=2: (1) length+null guard, (2) char-index body.
/// JS-021: no lock. JS-002: returns bool. NT8-006: no LINQ, char primitives only.
/// </summary>
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9) return false;                     // (1)
    return name[0] == 'P' && name[1] == 'T' && name[2] == 'T'
           && name[3] == '-' && name[4] == 'Q' && name[5] == 'X'
           && name[6] == '-' && name[7] == 'T'
           && name[8] >= '1' && name[8] <= '3';                            // (2)
}
```

### Change 2 of 2 — Extend `SnapshotTargetsLocal` filter (original line 266, now line 282)

**Before**:
```csharp
if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue;    // (3)
```

**After**:
```csharp
if (!stateOk || !instrOk || (!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))) continue; // (3) BUG-B42-QX-BE-01
```

**Rationale**: Negated-AND means: skip order only when it satisfies NEITHER predicate.
`"PTT-QX-T1"` satisfies `IsPttQxTarget` → included. `"Target1"` satisfies `IsAtmTargetName` → still included.

---

## 7-Scan Results

### SCAN-01: JS-021 lock check
```
Select-String -Path PttBreakEven.cs -Pattern "lock\s*\("
```
**Result**: 0 matches — **PASS**

### SCAN-02: JS-002 return null in new code
```
Select-String -Path PttBreakEven.cs -Pattern "return null"
```
**Result**: 2 matches — both are pre-existing in `FindPositionLocal` (lines 222, 226), which predates T1.
`IsPttQxTarget` returns `bool` — zero null returns in new code. **PASS** (new code only, per ticket §T1 intent)

### SCAN-03: JS-033 async void check
```
Select-String -Path PttBreakEven.cs -Pattern "async void"
```
**Result**: 0 matches — **PASS**

### SCAN-04: NT8-006 LINQ check
```
Select-String -Path PttBreakEven.cs -Pattern "\.Where|\.ToList|\.Select|\.Any|\.First|System\.Linq"
```
**Result**: 2 matches — both in `///` XML doc comments (documentation notes, not executable code).
Zero LINQ in executable code. No `using System.Linq` in file. **PASS**

### SCAN-05: CYC manual count
- `IsPttQxTarget`: `if`-guard (branch 1) + compound `&&` return (1 linear path) = **CYC=2** ✓
- `SnapshotTargetsLocal`: filter line changed from `!IsAtmTargetName` to `(!IsAtmTargetName && !IsPttQxTarget)` inside existing `if...continue` — no new branch node added. **CYC stays 3** ✓
- Both ≤ 8. **PASS**

### SCAN-06: IsAtmTargetName body unchanged
```
Select-String -Path PttBreakEven.cs -Pattern "string\.IsNullOrEmpty"
```
**Result**: Line 242: `if (string.IsNullOrEmpty(name) || name.Length < 7) return false;` — body intact.
`IsAtmTargetName` signature at line 240, body at 241-244, closing `}` at 245. **PASS**

### SCAN-07: No new instance fields added
```
Select-String -Path PttBreakEven.cs -Pattern "private\s+(volatile|readonly|int|double|bool|string)" | Where-Object { $_ -notmatch "private static" }
```
**Result**: Only pre-existing `private volatile int _beOcoSeq = 0;` (line 36). `IsPttQxTarget` is a static method, not a field. **PASS**

---

## Summary

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | `lock(` — 0 matches | PASS |
| SCAN-02 | `return null` — 0 in new code (pre-existing FindPositionLocal return null untouched) | PASS |
| SCAN-03 | `async void` — 0 matches | PASS |
| SCAN-04 | LINQ — 0 in executable code (2 doc comments only) | PASS |
| SCAN-05 | CYC: IsPttQxTarget=2, SnapshotTargetsLocal=3 | PASS |
| SCAN-06 | `IsAtmTargetName` body starts `string.IsNullOrEmpty` — unchanged | PASS |
| SCAN-07 | No new instance or static fields | PASS |

---

## BUILD_PASS

All 7 scans zero. Two surgical edits applied:
1. `IsPttQxTarget` private static method added after `IsAtmTargetName` — CYC=2, char primitives, no LINQ.
2. `SnapshotTargetsLocal` filter extended with `!IsPttQxTarget(o.Name)` — QX targets now included in snapshot.

`IsAtmTargetName` body untouched. Zero new instance fields. All JS/NT8 rules satisfied.

**Next ticket**: T2 — `CopyEngine.cs`: Flip `cancelPttBe: false` → `cancelPttBe: true` in `CancelQxBrackets`.
