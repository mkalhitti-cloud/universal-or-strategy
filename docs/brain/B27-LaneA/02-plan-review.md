# PTT-COPIER-B27 Lane A — Plan Review Report
# Reviewer: ptt-plan-reviewer
# Date: 2026-07-16
# Status: REVIEW_PASS

---

## Executive Summary

**Architecture Plan Reviewed**: [`02-architecture-plan.md`](02-architecture-plan.md)  
**Spec Requirement**: DW-B27-01 (P0) — Singleton BE fields corrupted by second-account arm  
**Review Result**: **REVIEW_PASS** — Zero violations found

All 10 mandatory checklist items evaluated PASS. No DNA violations, no NT8 compiler violations, no scope creep. Plan is ready for Phase 4 (Ticket Generation).

---

## Checklist Results

### Item 1: DATA MODEL COMPLETENESS ✅ PASS

**All 9 singleton fields scheduled for deletion**:
- Verified against live source ([`CopyEngine.cs:96-114`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:96))
- Plan §3.1 lists all 9 fields with correct line numbers
- Fields: `_pendingBeStates`, `_pendingBeBufferTicks`, `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeStates`, `_trailBeBufferTicks`, `_trailBeLastPnl`, `_trailBeAccount`, `_trailBeInstrument`

**PendingBeSlot struct present**:
- Plan §3.2 shows `private struct` (NOT `readonly struct` — avoids NT8-005)
- Fields: `internal readonly Account`, `internal readonly Instrument`, `internal readonly int BufferTicks`
- Explicit constructor — no `{ get; init; }` (NT8-001 compliant)

**TrailBeSlot struct present**:
- Plan §3.2 shows identical structure to PendingBeSlot
- Fields: `internal readonly Account`, `internal readonly Instrument`, `internal readonly int BufferTicks`
- Explicit constructor — no `{ get; init; }` (NT8-001 compliant)

**Separate `_trailBeLastPnlBits` dict present**:
- Plan §3.3 shows `ConcurrentDictionary<string, long>` (key = account.Name)
- Stores `BitConverter.DoubleToInt64Bits(pnl)` — avoids `volatile double` (NT8-003 compliant)
- Separate dict required: struct values in ConcurrentDictionary are boxed — cannot take ref for Interlocked CAS

**NT8-001 compliance** (no `{ get; init; }`):
- Plan §3.2 explicitly documents "internal readonly fields + explicit constructor" pattern
- Option A from NT8-001: readonly field with one-time ctor assignment
- Zero `{ get; init; }` anywhere in the design

---

### Item 2: METHOD COVERAGE ✅ PASS

**ArmPendingBe** (Plan §4.1):
- Singleton writes L1303-L1307 removed ✓
- Replaced with `_pendingBeSlots[masterAcc.Name] = new PendingBeSlot(...)` upsert ✓
- CYC=4 documented ✓
- Section cited: Plan §4.1

**DisarmPendingBe** (Plan §4.2):
- `_pendingBeSlots.TryRemove(leader.Name, out var slot)` replaces state dict TryRemove ✓
- Event unsubscribe: `if (slot.Account != null) slot.Account.AccountItemUpdate -= ...` (NT8-043 explicit guard) ✓
- CYC=3 documented ✓
- Section cited: Plan §4.2

**IsPendingBeArmed** (Plan §4.3):
- Scheduled for DELETE (L1336-L1339) ✓
- Per-account check inlined into OnPendingBeAccountUpdate via `TryGetValue` ✓
- Private method, no external callers ✓
- Section cited: Plan §4.3

**OnPendingBeAccountUpdate** (Plan §4.7):
- Full rewrite documented ✓
- accName derivation: `(sender as Account)?.Name ?? string.Empty` ✓
- TryGetValue gates 2-6: `if (!_pendingBeSlots.TryGetValue(accName, out var slot)) return;` ✓
- TryRemove gate 7: `if (!_pendingBeSlots.TryRemove(accName, out var removed)) return;` ✓
- Event unsubscribe: `if (removed.Account != null) removed.Account.AccountItemUpdate -= ...` ✓
- CYC=8 documented ✓
- Section cited: Plan §4.7

**ArmTrailBe** (Plan §4.4):
- Singleton writes L1358-L1363 removed ✓
- Replaced with `_trailBeSlots[masterAcc.Name] = new TrailBeSlot(...)` upsert ✓
- `_trailBeLastPnlBits[masterAcc.Name] = BitConverter.DoubleToInt64Bits(currentPnl)` upsert ✓
- CYC=4 documented ✓
- Section cited: Plan §4.4

**DisarmTrailBe** (Plan §4.5):
- `_trailBeSlots.TryRemove(leader.Name, out var slot)` ✓
- `_trailBeLastPnlBits.TryRemove(leader.Name, out _)` for PnL cleanup ✓
- Event unsubscribe: `if (slot.Account != null) slot.Account.AccountItemUpdate -= ...` (NT8-043 explicit guard) ✓
- CYC=3 documented ✓
- Section cited: Plan §4.5

**IsTrailBeArmed** (Plan §4.6):
- Scheduled for DELETE (L1390-L1393) ✓
- Per-account check inlined into OnTrailBeAccountUpdate via `TryGetValue` ✓
- Private method, no external callers ✓
- Section cited: Plan §4.6

**OnTrailBeAccountUpdate** (Plan §4.8):
- Full rewrite documented ✓
- accName derivation: `(sender as Account)?.Name ?? string.Empty` ✓
- `TryGetValue(_trailBeSlots, accName, out var slot)` gate 2 ✓
- `TryGetValue(_trailBeLastPnlBits, accName, out long oldBits)` PnL read ✓
- `AddOrUpdate(_trailBeLastPnlBits, accName, newBits, (k, cur) => cur < newBits ? newBits : cur)` CAS-style high-water ✓
- `AddOrUpdate(_trailBeSlots, accName, new TrailBeSlot(... BufferTicks+1))` buffer advance ✓
- CYC≤8 (annotated as CYC≤6 in plan) ✓
- Section cited: Plan §4.8

---

### Item 3: JS-021 COMPLIANCE ✅ PASS

**Rule Citation**: JS-021 (P0 CRITICAL) — No Lock() Usage

**Plan Evidence**:
- Plan §1 Rules Catalog Gate Result: "JS-021 | No `lock()` anywhere | PASS — ConcurrentDictionary only"
- Plan §5 Threading Model: All operations use `ConcurrentDictionary` indexer write (atomic), `TryGetValue` (read-only), `TryRemove` (atomic), `AddOrUpdate` (CAS loop internal to ConcurrentDict)
- Plan §9 Compliance Checklist SCAN-01: "No `lock()` in any new or modified code | PASS — zero lock() calls"

**Verification**: Zero `lock()` in design. All concurrency via ConcurrentDictionary lock-free primitives.

---

### Item 4: NT8-003 COMPLIANCE ✅ PASS

**Rule Citation**: NT8-003 — No `volatile double` or `volatile long`

**Plan Evidence**:
- Plan §1 Rules Catalog Gate Result: "NT8-003 | No `volatile double` | PASS — long stored via BitConverter"
- Plan §3.3 `_trailBeLastPnlBits`: `ConcurrentDictionary<string, long>` (NOT a `volatile long` field)
- Plan §4.8 OnTrailBeAccountUpdate: Uses `AddOrUpdate` CAS-style high-water merge (no Interlocked on volatile field)
- Plan §9 Compliance Checklist SCAN-02: "No `volatile double` or `volatile long` | PASS — BitConverter + ConcurrentDict<string,long>"

**Verification**: No volatile on long fields. PnL stored as `long` bits via BitConverter; ConcurrentDictionary.AddOrUpdate provides memory barrier.

---

### Item 5: NT8-004 COMPLIANCE ✅ PASS

**Rule Citation**: NT8-004 — ImmutableDictionary banned; ConcurrentDictionary<K,V> safe

**Plan Evidence**:
- Plan §1 Rules Catalog Gate Result: "NT8-004 | No ImmutableDictionary | PASS — ConcurrentDictionary<string,TStruct>"
- Plan §3.3: All three replacement fields are `ConcurrentDictionary<K,V>`, NOT `ImmutableDictionary<K,V>`
- Plan §9 Compliance Checklist: Implicit pass (no ImmutableDictionary anywhere)

**Verification**: All dicts are `ConcurrentDictionary`. Struct values in ConcurrentDictionary are boxed but safe in NT8.

---

### Item 6: CYC CEILING ✅ PASS

**Rule Citation**: Jane Street CYC≤8 strict standard

**Plan Evidence** (Plan §4 Method-by-Method Changes):
- ArmPendingBe: CYC=4 ✓
- DisarmPendingBe: CYC=3 ✓
- ArmTrailBe: CYC=4 ✓
- DisarmTrailBe: CYC=3 ✓
- OnPendingBeAccountUpdate: CYC=8 ✓
- OnTrailBeAccountUpdate: CYC≤6 (plan ceiling is ≤8, actual annotation is 6) ✓
- Plan §9 Compliance Checklist SCAN-05: "CYC ≤ 8 for every method | PASS — all annotated above"

**Verification**: All methods ≤ 8. Maximum CYC is 8 (OnPendingBeAccountUpdate).

---

### Item 7: TEST CHANGES ✅ PASS

**Test 1: `ArmTrailBe_NullInstrument_NoException` (Plan §8.1)**:
- Update documented at lines ~L1667-L1672 (matches live source [`CopyEngineTests.cs:1667-1672`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs:1667))
- Field name: `_trailBeStates` → `_trailBeSlots` ✓
- Type cast: `ConcurrentDictionary<string, int>` → `ConcurrentDictionary<string, TrailBeSlot>` ✓
- Comment updated to match new field name ✓

**Test 2: `T_B27_01` (Plan §8.2)** — new [Fact]:
- Target: `_pendingBeSlots` field existence ✓
- Reflection: `PendingBeSlot` nested type with correct fields (`Account`, `Instrument`, `BufferTicks`) ✓
- Structural proof of per-account isolation (no live NT8 session needed) ✓
- Append location: after line 2406 ✓

**Test 3: `T_B27_02` (Plan §8.3)** — new [Fact]:
- Target: All three replacement dicts exist (`_pendingBeSlots`, `_trailBeSlots`, `_trailBeLastPnlBits`) ✓
- Structural proof of complete migration from singleton fields ✓
- Append location: after `T_B27_01` ✓

**[Fact] target count**: Plan §8 closing note states "[Fact] target: 135 (was 133)" — 1 update + 2 adds = +2 net tests. ✓

---

### Item 8: SCOPE ISOLATION ✅ PASS

**Files In Scope** (Plan §2.1):
- `CopyEngine.cs`: Delete 9 fields, add 2 structs + 3 dicts, rewrite 6 methods, delete 2 methods ✓
- `CopyEngineTests.cs`: Update 1 test, add 2 tests ✓

**Files Out of Scope** (Plan §2.2):
- `TradeCopierPanel.cs`: "All call sites pass Account param — method signatures unchanged" ✓
- All other .cs files: "No API surface change; no callers outside CopyEngine.cs for deleted methods" ✓

**Verification**: No scope creep. Only 2 files modified. External API unchanged.

---

### Item 9: ASCII-ONLY ✅ PASS

**Rule Citation**: ASCII-only identifiers and string literals (V12 DNA mandate)

**Plan Evidence**:
- Plan §1 Rules Catalog Gate Result: "ASCII-only | No unicode/emoji in literals | PASS"
- Plan §9 Compliance Checklist SCAN-06: "ASCII-only identifiers and string literals | PASS"

**Verification**: No unicode, emoji, or curly quotes planned in any code literals.

---

### Item 10: SPEC TRACEABILITY ✅ PASS

**Spec Requirement** (Plan §0):
> DW-B27-01 (P0): `CopyEngine` pending-BE and trail-BE arm/disarm methods hold per-callback
> data in nine singleton plain fields. A second `ArmPendingBe()` or `ArmTrailBe()` call from a
> different account overwrites the first account's `Account`, `Instrument`, and `BufferTicks`
> references. The NT8 background callback then reads stale/wrong refs; the stop never moves for
> account 2 (or account 1 if arm order reverses).

**Plan §3.1**: Deletes all 9 singleton fields ✓

**Plan §3.3**: Replaces with per-account `ConcurrentDictionary<string, TSlot>` keyed by `account.Name` ✓

**Plan §6 Data Flow**: Demonstrates two-account scenario:
- `_pendingBeSlots["Sim101"]` and `_pendingBeSlots["SimApex02"]` exist simultaneously ✓
- Each callback, keyed by `accName` derived from `(sender as Account)?.Name`, reads and mutates ONLY its own slot ✓
- Zero data crosses between accounts ✓

**Root Cause**: Directly addressed. Singleton fields replaced with per-account dictionary slots.

---

## DNA Violations

**Count**: 0

**No violations found.**

---

## NT8 Compiler Violations

**Count**: 0

**No violations found.**

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Delete 9 singleton fields causing per-account overwrite | ✅ YES | §3.1 |
| Replace with per-account dictionary slots | ✅ YES | §3.3 |
| Rewrite 6 methods to use per-account slots | ✅ YES | §4.1-4.8 |
| Delete 2 now-unused helper methods | ✅ YES | §4.3, §4.6 |
| Update 1 existing test | ✅ YES | §8.1 |
| Add 2 new structural tests | ✅ YES | §8.2, §8.3 |
| Zero scope creep (TradeCopierPanel.cs unchanged) | ✅ YES | §2.2 |
| JS-021: no `lock()` | ✅ YES | §1, §5 |
| NT8-003: no `volatile double` | ✅ YES | §1, §3.3 |
| NT8-004: no ImmutableDictionary | ✅ YES | §1, §3.3 |
| NT8-001: no `{ get; init; }` | ✅ YES | §1, §3.2 |
| CYC≤8: all methods | ✅ YES | §4 |

**Coverage**: 12/12 requirements addressed.

---

## Recommendations

**None**. Plan is ready for Phase 4 (Ticket Generation).

---

## Final Result

**REVIEW_PASS**

---

## Reviewer Notes

1. **Structural testing strategy**: Plan §8 correctly identifies that runtime behavior tests require a live NT8 session with real Account objects, which is unavailable in the xUnit test environment. The two new [Fact] tests use reflection to verify the data model structure (field existence, nested type signature) — this is the correct contract verification approach for this environment.

2. **Separate PnL dictionary rationale**: Plan §3.3 explains why `_trailBeLastPnlBits` cannot be a field inside `TrailBeSlot` — struct values in ConcurrentDictionary are boxed; you cannot take a ref to a field inside a boxed struct for Interlocked CAS. This is a .NET limitation, not an NT8-specific constraint, but the plan correctly addresses it.

3. **NT8-043 compliance**: Plan §4.2, §4.5, §4.7, §4.8 all show explicit `if (acc != null)` guards before event unsubscribe (never `acc?.Event -=`). This is correct per NT8-043.

4. **CYC documentation quality**: Every method in Plan §4 includes a CYC annotation with gate-by-gate breakdown. This exceeds the minimum requirement and provides excellent auditability for Phase 3 DNA audit.

5. **Data flow diagram**: Plan §6 provides a concrete two-account scenario tracing dictionary writes and callback reads — this is the most valuable section for verifying the fix actually solves the reported defect.

**Overall assessment**: High-quality plan. Zero violations. Ready to proceed.
