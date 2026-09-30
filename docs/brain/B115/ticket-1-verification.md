# B115 Ticket T1 — Verification Report

**Block**: B115
**Ticket**: T1
**Verifier**: ptt-verifier
**Date**: 2026-08-27
**DW Reference**: DW-B121
**Input file**: `src/PropTraderTools/Tests/B113Tests.cs`
**Engineer completion report**: `docs/brain/B115/ticket-1-completion.md`
**Ticket spec**: `docs/brain/B115/04-tickets.md` (T1 section)

---

## Ticket T1 Scope

Two numeric literals updated inside the existing `[Fact]`
`QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` in `B113Tests.cs`:

| Location | Before | After |
|----------|--------|-------|
| Arrange — expiry seed (L32) | `DateTime.UtcNow.AddSeconds(2)` | `DateTime.UtcNow.AddSeconds(10)` |
| Assert — upper-bound guard (L42) | `DateTime.UtcNow.AddSeconds(3)` | `DateTime.UtcNow.AddSeconds(11)` |

Rationale: production TTL raised to 10 s by DW-B121 (`PttGlobalQuickExit.cs` L165).
Test constants must mirror the production value.

---

## Layer 3 Scan Results — All 7 Scans (Independent Re-Run)

### SCAN-01 — lock() check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "lock\("
Result:   (no output — 0 matches)
Status:   PASS
```

### SCAN-02 — async void check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "async void"
Result:   src\PropTraderTools\Tests\B113Tests.cs:2:// Block: B113. Framework: xUnit [Fact] only. JS-021: no lock. JS-033: no async void.
Status:   PASS — sole hit is the file header COMMENT on L2 ("// JS-033: no async void.")
          No actual async void method declaration exists anywhere in the file.
```

### SCAN-03 — throw new check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "throw new"
Result:   (no output — 0 matches)
Status:   PASS
```

### SCAN-04 — return null check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "return null"
Result:   (no output — 0 matches)
Status:   PASS
```

### SCAN-05 — new byte[] allocation check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "new byte\["
Result:   (no output — 0 matches)
Status:   PASS
```

### SCAN-06 — CYC / AddSeconds constant check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "AddSeconds"
Result:
  src\PropTraderTools\Tests\B113Tests.cs:32:            var expiry = DateTime.UtcNow.AddSeconds(10);
  src\PropTraderTools\Tests\B113Tests.cs:42:            Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(11));
  src\PropTraderTools\Tests\B113Tests.cs:83:                Expiry: DateTime.UtcNow.AddSeconds(-1)

Analysis:
  AddSeconds(10) present at L32 (expiry seed in T_B113_01 Arrange) — CORRECT
  AddSeconds(11) present at L42 (upper-bound in T_B113_01 Assert)  — CORRECT
  AddSeconds(-1) at L83 is in T_B113_03 (already-expired entry seed) — UNCHANGED, CORRECT
  AddSeconds(2)  — ABSENT (stale constant fully removed)
  AddSeconds(3)  — ABSENT (stale constant fully removed)
  CYC of QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower: 1
    (no if/for/while/switch/ternary/&& added; method body is pure linear Arrange-Act-Assert)

Status:   PASS
```

### SCAN-07 — ASCII-only check

```
Command:  Select-String -Path "src\PropTraderTools\Tests\B113Tests.cs" -Pattern "[^\x00-\x7F]"
Result:   (no output — 0 non-ASCII bytes)
Status:   PASS
```

---

## Cross-Check: Layer 3 vs Layer 2

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Agreement |
|------|--------------------|--------------------|-----------|
| SCAN-01 lock() | 0 matches | 0 matches | AGREE |
| SCAN-02 async void | 1 comment line only, no code | 1 hit L2 comment only, no code | AGREE |
| SCAN-03 throw new | 0 matches | 0 matches | AGREE |
| SCAN-04 return null | 0 matches | 0 matches | AGREE |
| SCAN-05 new byte[] | 0 matches | 0 matches | AGREE |
| SCAN-06 CYC | CYC=1 unchanged | CYC=1, AddSeconds(10)/(11) present, (2)/(3) absent | AGREE |
| SCAN-07 ASCII-only | 0 matches | 0 matches | AGREE |

**All 7 scans: AGREE. No discrepancies between Layer 2 self-report and Layer 3 independent run.**

---

## Change Correctness (V1–V6)

| Check | Evidence | Result |
|-------|----------|--------|
| V1. `AddSeconds(2)` absent from T_B113_01 method | SCAN-06: `AddSeconds(2)` absent from entire file | **PASS** |
| V2. `AddSeconds(10)` is expiry seed at L32 | SCAN-06: L32 `var expiry = DateTime.UtcNow.AddSeconds(10);` | **PASS** |
| V3. `AddSeconds(11)` is upper-bound at L42 | SCAN-06: L42 `Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(11));` | **PASS** |
| V4. `Assert.True(entry.Expiry > DateTime.UtcNow)` unchanged | L41 present unchanged in source read | **PASS** |
| V5. Method structure intact (only two constants changed) | Source read confirms Arrange/Act/Assert blocks intact; no added/removed lines | **PASS** |
| V6. T_B113_02..T_B113_04 unchanged | Source read: all three methods present; no `AddSeconds(2)/(3)` in their bodies; T_B113_03's `AddSeconds(-1)` unchanged | **PASS** |

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() ban | SCAN-01: 0 matches | PASS |
| JS-033 async void ban | SCAN-02: comment-only, no async void method | PASS |
| JS-001 no throw in hot paths | SCAN-03: 0 matches | PASS |
| JS-002 no return null | SCAN-04: 0 matches | PASS |
| JS-036/037 no byte[] alloc | SCAN-05: 0 matches | PASS |
| ASCII-only | SCAN-07: 0 non-ASCII bytes | PASS |
| CYC <= 8 | SCAN-06: CYC=1 | PASS |

---

## Architecture Compliance

- Ticket scope: edit-only to existing `[Fact]` — no new methods, no structural changes. ✅
- File: `src/PropTraderTools/Tests/B113Tests.cs` only. ✅
- Framework: xUnit `[Fact]`, synchronous void, no `[Theory]`, no NUnit, no MSTest. ✅
- Test accesses `CopyEngine.Instance._qxPendingFollowerCleanup` via `internal` seam
  (`[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`). ✅
- Two constants updated to mirror DW-B121 production TTL (10 s). ✅

---

## Overall Verdict

**VERIFY_PASS**

All 7 independent Layer 3 scans: zero violations.
All 7 scans agree with Layer 2 engineer report (no discrepancies).
Change correctness V1–V6: all PASS.
DNA rules: all PASS.