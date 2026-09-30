# B42-LaneA — Ticket 1 Completion Report

**Block**: PTT-COPIER-B42 — PTTFollowerStrategy: Native ATM Brackets on Followers
**Ticket**: T1 — PttContracts.cs: FillSignalEventArgs struct + PttBus.FillSignal event
**Phase**: 4a — Engineer
**Engineer**: ptt-engineer
**Date**: 2026-08-05
**File modified**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs`

---

## What Was Implemented

### Change A — PttBus static class (after RaiseQuickExit, before closing `}`)

Added `FillSignal` event declaration + `RaiseFillSignal` method. Exact lines added:

```csharp
        // B42: Action<T> (not EventHandler<T>) because FillSignalEventArgs is a readonly struct,
        // not an EventArgs subclass. JS-021: CLR += / -= are atomic -- no lock needed.
        // PttFollowerStrategy (separate NT8 compilation unit) subscribes at State.Realtime.
        public static event Action<FillSignalEventArgs> FillSignal;

        // B42: NT8-043 local-copy-then-null-check pattern. CYC=2. JS-021: no lock.
        public static void RaiseFillSignal(FillSignalEventArgs args)
        {
            var h = FillSignal;
            if (h != null) h(args);
        }
```

### Change B — Namespace body (after QuickExitEventArgs closing `}`, before final namespace `}`)

Added `FillSignalEventArgs` struct declaration with 6 fields, private constructor, and `Create` factory.

**NT8-005 compliance note**: The ticket review approved `public readonly struct`. However, the NT8
Roslyn compiler raised CS8341 (`Auto-implemented instance properties in readonly structs must be
readonly`) because `{ get; private set; }` is not permitted in a `readonly struct` in C# 7.3 (NT8's
compiler version). Fix applied: changed `public readonly struct` → `public struct` per NT8-005 Option B.
All 6 properties remain `{ get; private set; }` — externally immutable. This is the correct NT8-safe
pattern for this compiler version. NT8-005 was already documented; this is a known constraint being
applied, not a new rule discovery.

---

## Lines Added

- **Change A**: 11 lines (comment block + event declaration + RaiseFillSignal method)
- **Change B**: 37 lines (comment block + struct with 6 properties + private ctor + Create factory)
- **Total**: 48 lines added

---

## 7-Scan Results (Layer 2)

All scans executed on `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs`
using `Select-String` (PowerShell, since grep is not available in this shell).

| Scan | Pattern | Command | Result | Status |
|------|---------|---------|--------|--------|
| SCAN-01 | `lock(` | `Select-String ... -Pattern "lock\("` | 0 matches | ✅ PASS |
| SCAN-02 | `async void` | `Select-String ... -Pattern "async void"` | 0 matches | ✅ PASS |
| SCAN-03 | `return null` | `Select-String ... -Pattern "return null"` | 0 matches | ✅ PASS |
| SCAN-04 | CYC manual | `RaiseFillSignal`=2, ctor=1, `Create`=1 | All ≤ 8 | ✅ PASS |
| SCAN-05 | `init;` | `Select-String ... -Pattern "init;"` | 0 matches | ✅ PASS |
| SCAN-06 | `volatile double` | `Select-String ... -Pattern "volatile double"` | 0 matches | ✅ PASS |
| SCAN-07 | `async void` (confirm) | `Select-String ... -Pattern "async void"` | 0 matches | ✅ PASS |

**All 7 scans: ZERO hits. All PASS.**

---

## CYC Detail (SCAN-04)

| Method | Branches | CYC | Limit | Status |
|--------|----------|-----|-------|--------|
| `RaiseFillSignal` | 1 assignment + 1 `if (h != null)` branch | 2 | ≤ 8 | ✅ |
| `FillSignalEventArgs` private ctor | 6 field assignments, 0 branches | 1 | ≤ 8 | ✅ |
| `FillSignalEventArgs.Create` | expression body, 0 branches | 1 | ≤ 8 | ✅ |

---

## Build Result

**Command**: `dotnet build c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj`

**T1-scope errors (PttContracts.cs)**: 0 errors ✅

**Pre-existing errors (not caused by T1)**:
- `AtrSizingEngine.cs(20,31): error CS0234` — pre-existing before T1 (confirmed via git stash baseline build)
- `AtrSizingEngine.cs(24,36): error CS0246` — pre-existing before T1 (confirmed via git stash baseline build)

**Baseline build** (before T1 stashed): 3 errors in `AtrSizingEngine.cs` + `CopyEngine.cs` (CS8370)
**Post-T1 build**: 2 errors in `AtrSizingEngine.cs` only (CopyEngine.cs CS8370 resolved by existing workspace changes)

**T1 introduced: 0 new errors. No-scope-creep protocol followed — pre-existing AtrSizingEngine.cs defect NOT touched.**

---

## JS Rule Compliance

| Rule | Check | Status |
|------|-------|--------|
| JS-021 | No `lock()` — local-copy-then-null-check pattern in `RaiseFillSignal` | ✅ |
| JS-001 | No `throw new XxxException` | ✅ |
| JS-002 | No `return null` — `RaiseFillSignal` is void; `Create` returns struct | ✅ |
| JS-008 | `FillSignalEventArgs` is a struct (immutable via private set) | ✅ |
| JS-010 | Private constructor + `Create` factory as only public construction path | ✅ |
| JS-033 | No `async void` | ✅ |

---

## NT8 Rule Compliance

| Rule | Check | Status |
|------|-------|--------|
| NT8-001 | All 6 properties use `{ get; private set; }` — no `init;` | ✅ |
| NT8-002 | `FillSignalEventArgs` is a `struct` (not record) | ✅ |
| NT8-003 | No `double` fields, no `volatile` fields | ✅ |
| NT8-005 | `public readonly struct` → changed to `public struct` (CS8341 fix) — Option B of NT8-005 | ✅ |

---

## Acceptance Criteria Check

- [x] `dotnet build` of `PropTraderTools` compiles with zero new errors after T1
- [x] `PttBus.FillSignal` is accessible as `public static event Action<FillSignalEventArgs>`
- [x] `FillSignalEventArgs.Create(...)` is the only public construction path (constructor is `private`)
- [x] All 7 scans at zero

---

## BUILD_PASS
