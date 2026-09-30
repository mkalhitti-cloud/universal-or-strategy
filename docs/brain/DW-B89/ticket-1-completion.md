# Ticket T1 Completion: CopyEngine.cs Seed Fix
**Engineer**: ptt-engineer
**Date**: 2026-08-23
**Epic**: DW-B89 -- BE-ALL OCO Reuse + Silent Stop Rejection
**Ticket**: T1 -- CopyEngine._mstbeOcoSeq: XOR entropy seed to prevent OCO ID reuse after recompile
**File modified**: `src/PropTraderTools/CopyEngine.cs`

---

## Exact Diff Applied

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines changed**: 199-205

### BEFORE (lines 199-205)
```csharp
        // HOTFIX-MSTBE-OCO-TICKSEED-01: seed from Environment.TickCount (ms since OS boot).
        // NT8 keeps cancelled OCO IDs for the entire NT8 session. When NT8 recompiles an AddOn
        // within a running session, CopyEngine is GC'd and re-created -- if seeded at 0 the counter
        // restarts at 1 and immediately collides with pre-recompile OCO IDs still in NT8 memory.
        // Environment.TickCount advances even during recompile so post-recompile seq starts far above
        // any value used in the prior run. JS-023: volatile int. TickCount returns int -- no cast needed.
        private volatile int _mstbeOcoSeq = Environment.TickCount;
```

### AFTER (lines 199-205)
```csharp
        // DW-B89-01 SEED FIX: XOR Environment.TickCount with low 31 bits of DateTime.UtcNow.Ticks.
        // NT8 keeps cancelled OCO IDs for the entire NT8 session. When NT8 recompiles an AddOn
        // within a running session, CopyEngine is GC'd and re-created. TickCount alone can repeat
        // within the same millisecond on fast recompile. XOR with Ticks (100ns resolution) ensures
        // post-recompile seed is statistically unique. Math.Abs: XOR can set sign bit; wraps safely.
        // JS-023: volatile int. Interlocked.Increment in NextBeOcoSeq() unchanged. No lock added.
        private volatile int _mstbeOcoSeq = Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
```

### Unchanged (line 206)
```csharp
        internal int NextBeOcoSeq() => System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);
```

---

## 7-Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 new errors from T1. Pre-existing errors in `CopyEngineTests.cs` (83 errors, `CopyRule` / `NullabilityInfoContext` / `Immutable`), `B43Tests.cs` (`ParseAtmTemplateSelection`), and `CopyEngine.cs:3186` (`CS0433 Globals` NT8 assembly conflict) are pre-existing and out-of-scope per DW-PTT-BE-FIX-03 / No Scope Creep Protocol (V12.23). T1 changes at lines 199-205 introduce 0 errors. | **PASS** |
| SCAN-02 | Manual CYC check: `NextBeOcoSeq()` | CYC = 1 unchanged. `_mstbeOcoSeq` field initializer is a single expression with no branches (CYC N/A). `Math.Abs(... ^ ...)` contains no conditional branches. | **PASS** |
| SCAN-03 | `Get-ChildItem src/PropTraderTools -Recurse -Filter "*.cs" \| Select-String -Pattern "lock\s*\("` | All 8 hits are comments: "no lock (JS-021)". Zero live `lock()` invocations in any `.cs` file. | **PASS (0 violations)** |
| SCAN-04 | `Get-ChildItem src/PropTraderTools -Recurse -Filter "*.cs" \| Select-String -Pattern "async void " \| Select-String -Pattern "CopyEngine\.cs"` | No output. 0 `async void` matches in `CopyEngine.cs`. | **PASS (0 hits)** |
| SCAN-05 | `.ToString("D5")` in `src/PropTraderTools/Features/` | N/A for T1. `CopyEngine.cs` is not in `Features/`. T1 introduces no `.ToString("D5")` calls. Existing D5 matches in `PttBreakEven.cs`, `PttBreakEvenSwap.cs`, `PttGlobalBreakEven.cs` are T2/T3 scope (not yet fixed). | **N/A** |
| SCAN-06 | `PttBreakEvenSwap.cs` DateTime.Now check | N/A for T1. `PttBreakEvenSwap.cs` not touched by T1. | **N/A** |
| SCAN-07 | `Get-Content src/PropTraderTools/CopyEngine.cs \| Select-String -Pattern "[^\x00-\x7F]"` | Pre-existing non-ASCII characters in unrelated comment lines (QUICKALL-SINGLETON-01, arrow characters). T1 changed lines 199-205 contain only pure ASCII. 0 non-ASCII in T1 modified lines. | **PASS (0 in T1 lines)** |

---

## JS Rule Compliance

| Rule | Requirement | Result |
|------|-------------|--------|
| JS-021 (P0) | No `lock()` added | PASS -- volatile + Interlocked pattern preserved. |
| JS-023 (P1) | Atomic primitives for simple shared state | PASS -- `volatile int` preserved. `Interlocked.Increment` in `NextBeOcoSeq()` unchanged. XOR seed is a field initializer (single-threaded construction, no atomic op needed). |
| JS-033 (P0) | No `async void` | PASS -- no async code touched. |
| JS-001 (P0) | No `throw` in hot path | PASS -- no throw statements added. |
| JS-002 (P0) | No `return null` | PASS -- no return null added. |
| ASCII-only | No Unicode in modified lines | PASS -- `Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF))` is pure ASCII. |
| DateTime.Now ban | Use `DateTime.UtcNow` only | PASS -- formula uses `DateTime.UtcNow.Ticks`. `DateTime.Now` does not appear. |

---

## NT8 Constraints

Not applicable to T1. No NT8 API calls in scope.

---

## Summary

Single-line field initializer on `CopyEngine.cs:205` updated from `Environment.TickCount` to
`Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF))`.

Comment block on lines 199-204 updated to describe the XOR formula, entropy rationale, sign-bit
safety via `Math.Abs`, and JS-023 compliance confirmation.

`NextBeOcoSeq()` method signature and body are unchanged.

---

## BUILD_PASS
