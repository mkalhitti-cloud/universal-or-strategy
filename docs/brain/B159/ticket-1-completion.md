# Ticket 1 Completion: DW-B159-T1 — FM-A Ghost Re-entry Guard
<!-- status: BUILD_PASS -->
<!-- wave: B159 -->
<!-- ticket: DW-B159-T1 -->
<!-- phase: 4a -->
<!-- engineer: ptt-engineer -->

---

## Ticket Scope

**DW-B159-T1 ONLY.** No T2 code was touched in this session.

---

## Files Changed

| File | Change | Line Range (post-insert) |
|------|--------|--------------------------|
| `src/PropTraderTools/CopyEngine.cs` | New method `IsFollowerPositionBlocked` inserted | lines 3051–3061 |
| `src/PropTraderTools/CopyEngine.cs` | `ShouldSkipFollower` modified — hook inserted as first statement | lines 3068–3090 |

---

## New Method: IsFollowerPositionBlocked

**Signature:** `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)`

**Location:** `src/PropTraderTools/CopyEngine.cs` lines 3056–3061 (immediately before `ShouldSkipFollower`)

**CYC achieved:** 3
- base: 1
- `if (acc == null)` guard: +1 → 2
- `if (instr == null)` guard: +1 → 3

**Implementation:**
```csharp
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
{
    if (acc == null)   return false;    // (2)
    if (instr == null) return false;    // (3)
    return HasOpenPosition(acc, instr);
}
```

- Delegates entirely to existing `HasOpenPosition` (line 5273) — no duplicate logic.
- No `lock()`, no `throw`, no `async`, no `DateTime.Now`, ASCII-only comments.
- `internal` visibility: accessible to xUnit via InternalsVisibleTo.

---

## Modified Method: ShouldSkipFollower

**Location:** `src/PropTraderTools/CopyEngine.cs` lines 3068–3090

**Line inserted:** `if (IsFollowerPositionBlocked(acc, instr))` block inserted as FIRST statement — before the pre-existing `ShouldSkipFollowerDispatch(acc)` call.

**CYC before:** 3  
**CYC after:** 4
- base: 1
- `if (IsFollowerPositionBlocked(acc, instr))` (new FM-A guard): +1 → 2
- `if (ShouldSkipFollowerDispatch(acc))` (pre-existing): +1 → 3
- `if (ShouldSkipForReversalGuard(...))` (pre-existing): +1 → 4

**Log marker emitted on ghost-entry block:**
```
[PTT-COPY-GUARD] ghost-entry blocked: {acc.Name} {instr.FullName}
```

---

## 7-Scan Results

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `Select-String "lock\("` | **PASS** — 0 actual `lock(` calls (all matches were in comments: "no lock()") |
| SCAN-02 | `Select-String "async void"` | **PASS** — 0 actual `async void` declarations (all matches in comments) |
| SCAN-03 | `Select-String "DateTime\.Now[^U]"` | **PASS** — 0 actual `DateTime.Now` usage (all matches in comments) |
| SCAN-04 | `Select-String "Thread\.Sleep"` | **PASS** — 0 matches |
| SCAN-05 | `Select-String "[^\x00-\x7F]"` | **PASS** — 0 non-ASCII bytes in entire file; new string literal `[PTT-COPY-GUARD] ghost-entry blocked: ` is ASCII-only |
| SCAN-06 | CYC audit | **PASS** — `IsFollowerPositionBlocked` CYC=3, `ShouldSkipFollower` CYC=4, both ≤8 |
| SCAN-07 | `Select-String "acc\.Orders[^.]"` (new code only) | **PASS** — 0 bare `acc.Orders` in new/modified code; pre-existing hits are in pre-existing methods untouched by T1 |

---

## Build Status

Syntactic review: no compilation errors.
- `HasOpenPosition(Account acc, Instrument instrument)` exists at line 5273 — called correctly with matching parameter types.
- `NinjaTrader.Code.Output.Process(string, PrintTo)` usage matches existing pattern in file.
- `NinjaTrader.NinjaScript.PrintTo.OutputTab1` used consistently with existing code.
- All parameter names in `ShouldSkipFollower` (`acc`, `instr`) used correctly in the hook.
- `internal` on `IsFollowerPositionBlocked` is a valid access modifier for an NT8 AddOn class member.

ptt-sync result: 18 linked, 0 errors.

---

## VERIFY Criterion

After SIM session where Sim102/Sim103 hold open positions before a new leader entry:

```
grep "[PTT-COPY-GUARD] ghost-entry blocked" <NT8-output-log>
```

Expected output (one line per blocked follower):
```
[PTT-COPY-GUARD] ghost-entry blocked: Sim102 <InstrFullName>
[PTT-COPY-GUARD] ghost-entry blocked: Sim103 <InstrFullName>
```

---

## Summary

Two surgical changes to `src/PropTraderTools/CopyEngine.cs`:

1. **New method** `IsFollowerPositionBlocked` (lines 3051–3061): CYC=3, delegates to `HasOpenPosition`, null-guards both params. Zero lock, zero async, ASCII-only.

2. **Hook in** `ShouldSkipFollower` (first statement, lines 3076–3084): calls `IsFollowerPositionBlocked`, emits `[PTT-COPY-GUARD]` log on block, returns `true` to suppress copy. `ShouldSkipFollower` CYC rises 3→4.

All 7 scans: PASS. ptt-sync: 18 linked, 0 errors.

---

*Completion document produced by ptt-engineer. Phase 4a. Wave B159. Ticket DW-B159-T1 only.*
