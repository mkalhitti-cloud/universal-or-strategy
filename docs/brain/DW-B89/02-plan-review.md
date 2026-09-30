# DW-B89 Plan Review
**Reviewed by**: ptt-plan-reviewer
**Review Phase**: Phase 2
**Date**: 2026-08-23
**Input**: `docs/brain/DW-B89/02-architecture-plan.md`
**Spec source**: User prompt (DW-B89-01 / DW-B89-02 verbatim requirements)
**Rules read**: `docs/standards/jane-street/RULES_CATALOG.md` JS-001..JS-033 (all P0/P1)

---

## Verdict: REVIEW_PASS

No violations found. All 14 checklist items pass.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B89-01: XOR seed formula (exact) | YES | §2 File 1 |
| DW-B89-01: D5 → D7 in PttBreakEvenSwap.cs | YES | §2 File 2 Change 1 |
| DW-B89-01: D5 → D7 in PttBreakEven.cs | YES | §2 File 3 Change 1 |
| DW-B89-02: IsStopPriceSubmittable signature | YES | §2 File 2 Change 3 |
| DW-B89-02: isLong → return true | YES | §2 File 2 Change 3 |
| DW-B89-02: ask==0 → return true (fail-open) | YES | §2 File 2 Change 3 |
| DW-B89-02: otherwise → stopPrice >= ask | YES | §2 File 2 Change 3 |
| DW-B89-02: Guard with-targets stop submit | YES | §2 File 2 Change 4 |
| DW-B89-02: Guard 0-targets bare-stop path | YES | §2 File 2 Change 5 |
| DW-B89-02: Replace all 3 bare catch blocks | YES | §2 File 2 Change 2 |
| DW-B89-02: [BE-ERR] prefix in log messages | YES | §2 File 2 Change 2 |
| DW-B89-02: CYC Execute() <= 8 after changes | YES | §3 |
| DW-B89-02: CYC IsStopPriceSubmittable <= 3 | YES (advisory note) | §3 |
| PttGlobalBreakEven.cs D5 stays / out of scope | YES | §2 File 3 |
| Test rename to T_OCO_SEED_03...D7...SevenDigitPadding | YES | §2 File 4 |
| Test asserts D7 (7-char padding) not D5 | YES | §2 File 4 |
| 7-scan checklist | YES | §5 |
| 3-ticket breakdown | YES | §7 |

---

## Checklist Results

| # | Item | Result | Notes |
|---|------|--------|-------|
| 1 | Plan addresses DW-B89-01 root cause (XOR formula exact) | **PASS** | `Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF))` — matches spec verbatim |
| 2 | Plan addresses DW-B89-02 root cause (IsStopPriceSubmittable signature + logic) | **PASS** | Signature matches spec; logic (isLong→true, ask==0→true, stopPrice>=ask) matches spec exactly |
| 3 | CYC analysis present and Execute() <= 8 after changes | **PASS** | §3 provides 8-branch table. CYC=8, limit=8. ✓ |
| 4 | IsStopPriceSubmittable CYC <= 3 | **PASS** (advisory note) | Plan claims CYC=2. See Advisory Note A1. |
| 5 | All 3 bare catch blocks identified and replacement specified | **PASS** | §2 File 2 Change 2: "All three `catch { /* non-fatal */ }` blocks" explicitly enumerated; `catch(Exception ex)` + OutputTab1 logging shown |
| 6 | 0-targets path also guarded (not just with-targets) | **PASS** | §2 File 2 Change 5: explicit `IsStopPriceSubmittable` guard on bare-stop path before `acc.Submit` |
| 7 | D7 alignment covers both PttBreakEvenSwap.cs AND PttBreakEven.cs | **PASS** | §2 File 2 Change 1 (Swap) and §2 File 3 Change 1 (BE) both show D5→D7 |
| 8 | PttGlobalBreakEven.cs explicitly called out as NOT in scope | **PASS** | §2 File 3: "PttGlobalBreakEven.cs uses prefix PTT-BEG-* … D5 is intentionally preserved … out of scope" |
| 9 | Test update (T_OCO_SEED_03 → D7) explicitly called out | **PASS** | §2 File 4: method rename + assertion update to 7-char minimum shown |
| 10 | JS-021 (no lock): plan adds no lock() | **PASS** | No `lock(` appears anywhere in plan code samples. §4 JS-021 row confirmed. |
| 11 | JS-023 (volatile int): plan retains volatile int | **PASS** | §2 File 1: "volatile int access pattern unchanged." §4 JS-023 confirmed. |
| 12 | JS-033 (no async void): plan has no async void | **PASS** | §4 JS-033: "`Execute()` is `static void` (synchronous) … No new async methods." |
| 13 | 7-scan checklist present | **PASS** | §5: SCAN-01 through SCAN-07 with commands and required results |
| 14 | 3-ticket breakdown present | **PASS** | §7: T1 (CopyEngine seed), T2 (PttBreakEvenSwap full), T3 (PttBreakEven D7 + test) |

---

## Violations Found

**None.**

No P0 or P1 JS rule violations identified. Full DNA block scanned:

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | `throw` in hot path / gate chain | PASS — no new `throw` statements; catch only logs |
| JS-002 | `return null` where value expected | PASS — `IsStopPriceSubmittable` returns `bool`; no null returns |
| JS-003 | Magic string for discriminated state | PASS — not applicable to this changeset |
| JS-008 | Mutable fields on struct | PASS — not applicable to this changeset |
| JS-009 | Dictionary for shared/thread-touched collection | PASS — no new collections |
| JS-010 | Public constructor on singleton/signal struct | PASS — not applicable |
| JS-021 | `lock()` anywhere | PASS — zero lock() in plan |
| JS-023 | `volatile int` preserved, no lock() for atomic | PASS — `_mstbeOcoSeq` remains `volatile int` |
| JS-033 | `async void` (non-event-handler) | PASS — no async methods anywhere in plan |
| NT8: DateTime.Now ban | `DateTime.UtcNow` used | PASS — `DateTime.UtcNow.Ticks` in XOR seed |
| NT8: `async/await` in lifecycle methods | None present | PASS |
| NT8: `Account.All` in constructor | Not present | PASS |
| NT8: `CreateOrder` without PTT- prefix | All names use `"PTT-BE-Stop-"` / `"PTT-BE-Stop"` | PASS |
| NT8: Hardcoded `#RRGGBB` hex | None present | PASS |
| NT8: FontFamily override | Not applicable | PASS |
| ASCII-only | `[BE-ERR]`, log string literals | PASS — all ASCII |
| CYC > 8 (any method) | `Execute()=8`, `IsStopPriceSubmittable=2`, `BuildBeOcoId=2` | PASS |

---

## Advisory Notes (non-blocking)

### A1 — IsStopPriceSubmittable null-conditional operator CYC count

**Item**: Checklist #4 / Plan §3.  
**Status**: Advisory — does not change verdict.

The plan states `IsStopPriceSubmittable` CYC = 2, counting only the two explicit `if` branches
(`isLong` and `ask == 0.0`). The implementation uses the null-safe chain:
```csharp
double ask = instr.MarketData?.Ask?.Price ?? 0.0;
```

Under Lizard and some strict McCabe counters, each `?.` null-conditional operator contributes +1
to the branch count. With two `?.` operators in the chain, strict CYC = 1 (base) + 1 (`isLong`)
+ 1 (`ask==0`) + 1 (`?.MarketData`) + 1 (`?.Ask`) = **5**, which would exceed the spec limit of 3.

Under Roslyn/standard McCabe (used by Codacy with threshold 8), null-conditionals are typically
not counted as separate decision points. In that case CYC = 2, comfortably within the limit.

**Engineer action required at SCAN-02**: Run the actual complexity scanner against
`IsStopPriceSubmittable` and confirm the measured CYC is ≤ 3. If the scanner reports CYC > 3,
refactor the null-safe chain to an explicit null check:
```csharp
var md = instr.MarketData;
var askData = md?.Ask;
double ask = (askData != null) ? askData.Price : 0.0;
```
This eliminates the `?.` operators and ensures CYC = 2 regardless of tool. This refactor is
not required before implementation — it is a verifier gate (SCAN-02).

---

## Summary

- **14 / 14** checklist items: PASS
- **0** JS rule violations
- **1** advisory note (A1): CYC tool-counting ambiguity for `?.` chain — verifier gate (SCAN-02)
- **All spec requirements** covered in the plan

**REVIEW_PASS — Phase 3 (ticket generation) is UNLOCKED.**
