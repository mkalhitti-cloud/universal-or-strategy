# Ticket Review: B32-LaneA

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-19
**Input tickets**: `docs/brain/B32-LaneA/04-tickets.md`
**Input plan**: `docs/brain/B32-LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Input plan review**: `docs/brain/B32-LaneA/02-plan-review.md` (REVIEW_PASS)
**Input register**: `docs/brain/B32-LaneA/00-direct-repair-register.md`
**Standards**: `docs/standards/NT8_COMPILER_RULES.md` v1.6, `docs/standards/jane-street/RULES_CATALOG.md` v1.0

---

## T-B32-T1 — Remove buffer==0 market-fallback guards; swap ComputeLimitPx anchor to ask/bid

### Traceability: PASS

- DW-B32-TRIM-MARKET-01 → R-B32-04: present in register (R-B32-04) and plan (§DW-B32-TRIM-MARKET-01). All 6 guard locations (CopyEngine.cs lines 949, 967, 1059, 1069; TradeCopierPanel.cs lines 808, 836) match the plan table exactly. ✅
- DW-B32-TRIM-ANCHOR-01 → R-B32-05: present in register (R-B32-05) and plan (§DW-B32-TRIM-ANCHOR-01). Formula swap and 4 test mutations all traced to register. ✅
- Files listed in ticket (CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs) match register §Files in Scope. ✅
- **P2 observation — Test name divergence**: The plan (Change 4b, lines 311–336) specifies renamed tests as `TrimLimit_Long_PlacesBelowAsk`, `TrimLimit_Short_PlacesAboveBid`, `FlattenLimit_Long_PlacesBelowAsk`, `FlattenLimit_Short_PlacesAboveBid`. The ticket specifies `TrimLimit_Long_PegsToAsk`, `TrimLimit_Short_PegsToBid`, `FlattenLimit_Long_PegsToAsk`, `FlattenLimit_Short_PegsToBid`. The ticket's names are semantically more accurate and the work scope is identical (4 renames + corrected expected values). Not a phantom-work issue — no FAIL.

### JS P0 Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no `lock()` | Zero lock usages in all guard removals and formula swap | PASS |
| JS-001 no `throw` in business logic | All error/skip paths use bare `return` (guard falls through to market overload) | PASS |
| JS-002 no `return null` | All modified methods are `void` or `double` | PASS |
| JS-033 no `async void` | Zero async methods introduced or modified | PASS |

### NT8 Constraint Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-007 CreateOrder arg 12 | No new CreateOrder calls; existing calls unchanged | PASS |
| NT8-013 DateTime.Now | No date/time operations in any changed code | PASS |
| NT8-014 PTT- signal prefix | No new signal names; existing PTT-Trim/PTT-Flatten/PTT-TrimLimit/PTT-FlattenLimit unchanged | PASS |
| NT8-003 volatile double | No volatile fields | PASS |
| NT8-004 ImmutableDictionary | No Immutable collections | PASS |
| NT8-019 async void | Zero async methods | PASS |
| NT8-029 tick alignment | ComputeLimitPx output consumed by downstream tick-rounding at lines ~1150 and ~1183; no regression | PASS |

### CYC Pre-Check: PASS

All modified methods annotated with post-change CYC ≤ 8:

| Method | CYC before | CYC after | Within budget? |
|--------|-----------|-----------|----------------|
| `Trim(Account,Instrument,int,double,double)` | 5 | 4 | ✅ |
| `Flatten(Account,Instrument,int,double,double)` | 5 | 4 | ✅ |
| `Trim(Instrument,int,double,double)` | 6 | 5 | ✅ |
| `Flatten(Instrument,int,double,double)` | ~5 | 4 | ✅ |
| `ComputeLimitPx` | 1 | 1 | ✅ |
| `OnTrimClick` | 4 | 3 | ✅ |
| `OnFlattenClick` | 4 | 3 | ✅ |

### Completeness: PASS

- All 6 guard changes: exact old → new code shown (Changes 1a–1d, 3a–3b). ✅
- ComputeLimitPx: old header comment block AND old formula AND new header comment block AND new formula shown verbatim. ✅
- Test Change 4a: exact lines to remove identified (`var ex3 = ...` + `Assert.Null(ex3);`). ✅
- Test Change 4b: all 4 renames specify old name, new name, new inner comment, new `Assert.Equal` value, new `CopyEngine.ComputeLimitPx(...)` call args. ✅
- No new method signatures introduced (all changes are modifications to existing methods). ✅

### Test Coverage: PASS

| [Fact] method name | Asserts |
|-------------------|---------|
| `TrimLimit_FallsBackToMarket_WhenAskIsZero` (mutated) | After removing exitBuffer==0 case: only ask==0 and bid==0 paths remain; both call market fallback |
| `TrimLimit_Long_PegsToAsk` (renamed) | `ComputeLimitPx(isLong:true, ask:5000.25, bid:5000.00, exitBuffer:1, tickSize:0.25)` == `5000.00` |
| `TrimLimit_Short_PegsToBid` (renamed) | `ComputeLimitPx(isLong:false, ask:5000.25, bid:5000.00, exitBuffer:1, tickSize:0.25)` == `5000.25` |
| `FlattenLimit_Long_PegsToAsk` (renamed) | `ComputeLimitPx(isLong:true, ask:5000.25, bid:5000.00, exitBuffer:2, tickSize:0.25)` == `4999.75` |
| `FlattenLimit_Short_PegsToBid` (renamed) | `ComputeLimitPx(isLong:false, ask:5000.25, bid:5000.00, exitBuffer:2, tickSize:0.25)` == `5000.50` |

Every modified method has a [Fact] covering the changed behavior. exitBuffer==0 fallback removal explicitly confirmed via Change 4a. ✅

### Scan Checklist: PASS

All 7 scans present in T-B32-T1 with exact shell commands:

| Scan | Command present | Result |
|------|----------------|--------|
| SCAN-01 lock() | `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-02 async void | `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-03 return null | `grep -rn "return null;" src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-04 NT8 rules | Manual review per NT8_COMPILER_RULES.md, banned patterns enumerated | PASS |
| SCAN-05 CYC | Per-method CYC values listed | PASS |
| SCAN-06 test scan | `dotnet test c:\WSGTA\universal-or-strategy\tests\CopyEngineTests\CopyEngineTests.csproj` | PASS |
| SCAN-07 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs` | PASS |

### File Routing: PASS

All `.cs` paths target `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` (Wave workspace). No Director workspace paths for `.cs` files. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T-B32-T2 — Block raw market exits when ATM bracket is active; emit OutputTab1 warning

### Traceability: PASS

- DW-B32-TRIM-CLOSE-01 → R-B32-03: present in register (R-B32-03) and plan (§DW-B32-TRIM-CLOSE-01). WARN-AND-BLOCK design matches architect decision. ✅
- Files: `CopyEngine.cs`, `CopyEngineTests.cs` — matches plan §Component Summary. ✅
- All 4 new methods (`IsAtmSlotName`, `IsAtmBracketActive`, `TrimOneAccount` mutation, `FlattenOneAccount` mutation) map to plan §Ticket 2 Detail. ✅
- **P2 observation — Test name divergence**: Plan specifies `IsAtmSlotName_DetectsTarget1`, `IsAtmSlotName_DetectsStop1`, `IsAtmSlotName_RejectsPttSignalNames`, `IsAtmSlotName_RejectsTargetWithoutDigit`. Ticket uses `T_B32_01_IsAtmSlotName_Stop1_ReturnsTrue`, `T_B32_02_IsAtmSlotName_Target1_ReturnsTrue`, `T_B32_03_IsAtmSlotName_PttTrimLimit_ReturnsFalse`, `T_B32_04_IsAtmSlotName_Null_ReturnsFalse`. Coverage equivalent. Not a phantom-work issue — no FAIL.

### JS P0 Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no `lock()` | `IsAtmBracketActive` uses `acc.Orders.ToList()` snapshot — confirmed lock-free pattern. Zero `lock()` introduced | PASS |
| JS-001 no `throw` in business logic | Both new helpers return `bool`; guard blocks in TrimOneAccount/FlattenOneAccount use `return` only | PASS |
| JS-002 no `return null` | `IsAtmSlotName` returns `bool`, `IsAtmBracketActive` returns `bool`, `TrimOneAccount`/`FlattenOneAccount` return `void` | PASS |
| JS-033 no `async void` | All four methods synchronous (`static bool`, `private bool`, `private void`, `private void`) | PASS |

### NT8 Constraint Pre-Check: FAIL

**VIOLATION — NT8-044 not verified (P0 rule, CS0103 at F5 gate risk)**

The ticket introduces `StringComparison.Ordinal` in `IsAtmSlotName`:
```csharp
name.StartsWith("Stop", StringComparison.Ordinal)
name.StartsWith("Target", StringComparison.Ordinal)
```

NT8-044 (confirmed B24, P0) states:

> `StringComparison` is in the `System` namespace. NT8's NinjaScript compiler does NOT auto-inject `using System;`. Any use of `StringComparison.Ordinal`, `StringComparison.OrdinalIgnoreCase`, etc. requires an explicit `using System;` at the top of the file. Without it: **CS0103 "The name 'StringComparison' does not exist in the current context"** at F5 compile.

The ticket's NT8 Rule Constraints table lists NT8-003, NT8-007, NT8-013, NT8-014, NT8-018, NT8-019, NT8-029, NT8-031 — **NT8-044 is absent**.

The ticket does not:
1. Confirm `using System;` already exists in `CopyEngine.cs`, OR
2. Add `using System;` as an explicit change item in the ticket

This omission means the engineer has no explicit signal to verify this. A CS0103 at F5 would be a silent build failure caught only at NT8 compile time — not by `dotnet build` (which resolves the SDK project references and passes even without explicit `using System;` in some configurations).

**Required fix (architect must add to ticket before engineer executes)**:
In T-B32-T2, add to the NT8 Rule Constraints table:

| NT8-044 | `StringComparison` requires explicit `using System;` | Confirm `using System;` is present in `CopyEngine.cs` file preamble, or add it as Change 0 |

And add a pre-requisite change to the ticket:
```
Change 0 (NT8-044 guard): Confirm `using System;` is present at top of CopyEngine.cs.
If absent: add `using System;` to the using block at the top of CopyEngine.cs.
```

**Secondary NT8 observations (non-blocking, confirmed by plan REVIEW_PASS)**:

| Rule | Check | Result |
|------|-------|--------|
| NT8-007 CreateOrder arg 12 | No new CreateOrder calls | PASS |
| NT8-013 DateTime.Now | No date/time operations | PASS |
| NT8-014 PTT- signal prefix | Output.Process strings are informational prefixes, not CreateOrder signal args | PASS |
| NT8-003 volatile | No volatile fields | PASS |
| NT8-018 lock() | acc.Orders.ToList() snapshot; no lock() | PASS |
| NT8-019 async void | Zero async methods | PASS |
| NT8-031 OrderState enum values | OrderState.Working + OrderState.Accepted (plan confirmed both valid in NT8) | PASS |
| NT8-044 StringComparison | **ABSENT from NT8 table — see violation above** | **FAIL** |

### CYC Pre-Check: PASS

| Method | CYC | Within budget? |
|--------|-----|----------------|
| `IsAtmSlotName` | 5 (OBS-01: plan stated 3, ticket correctly updates to 5) | ✅ |
| `IsAtmBracketActive` | 6 (OBS-01: plan stated 4, ticket correctly updates to 6) | ✅ |
| `TrimOneAccount` | 4 (was 3) | ✅ |
| `FlattenOneAccount` | 4 (was 3) | ✅ |

Ticket correctly carries OBS-01 from plan review (engineer awareness notes at ticket top). ✅

### Completeness: PASS

- Full body of `IsAtmSlotName` shown. ✅
- Full body of `IsAtmBracketActive` shown (including null guard on `acc`/`instrument` — an improvement over the plan's implementation). ✅
- ATM guard block for `TrimOneAccount`: insert position specified (after opening brace), full code shown. ✅
- ATM guard block for `FlattenOneAccount`: insert position specified, full code shown. ✅
- All 4 new [Fact] test bodies shown completely with assertions. ✅

### Test Coverage: PASS

| [Fact] method name | Asserts |
|-------------------|---------|
| `T_B32_01_IsAtmSlotName_Stop1_ReturnsTrue` | `IsAtmSlotName("Stop1")` and `"Stop2"` both return `true` |
| `T_B32_02_IsAtmSlotName_Target1_ReturnsTrue` | `IsAtmSlotName("Target1")`, `"Target2"`, `"Target9"` all return `true` |
| `T_B32_03_IsAtmSlotName_PttTrimLimit_ReturnsFalse` | `IsAtmSlotName("PTT-Trim")`, `"PTT-Flatten"`, `"PTT-TrimLimit"`, `"PTT-Copy"` all return `false` |
| `T_B32_04_IsAtmSlotName_Null_ReturnsFalse` | `IsAtmSlotName(null)`, `""`, `"Stop"`, `"Target"`, `"TargetEntry"` all return `false` |

All branches of `IsAtmSlotName` covered (Stop-with-digit, Target-with-digit, PTT-prefix, null, empty, too-short, no-digit variants). ✅

`IsAtmBracketActive` is `private` and requires live NT8 account state — no direct [Fact] test; covered by the block-level regression test at F5 + live sim.

### Scan Checklist: PASS

All 7 scans present in T-B32-T2 with exact shell commands:

| Scan | Command present | Result |
|------|----------------|--------|
| SCAN-01 lock() | `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-02 async void | `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-03 return null | `grep -rn "return null;" src/PropTraderTools/ --include="*.cs"` | PASS |
| SCAN-04 NT8 rules | Manual review with banned patterns listed; SCAN-04 body explicitly lists `StringComparison` items to confirm — however NT8-044 (need for `using System;`) is not called out as a required pre-action | PASS (checklist present; NT8-044 content gap captured in NT8 Pre-Check FAIL above) |
| SCAN-05 CYC | Per-method CYC values (IsAtmSlotName=5, IsAtmBracketActive=6, Trim/FlattenOneAccount=4) | PASS |
| SCAN-06 test scan | `dotnet test c:\WSGTA\universal-or-strategy\tests\CopyEngineTests\CopyEngineTests.csproj` | PASS |
| SCAN-07 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` (Output.Process strings verified ASCII-only in ticket body) | PASS |

### File Routing: PASS

All `.cs` paths target `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` (Wave workspace). ✅

### VERDICT: TICKET_REVIEW_FAIL

**Blocking violation**: NT8-044 — `StringComparison.Ordinal` introduced in `IsAtmSlotName` without confirming `using System;` is present in `CopyEngine.cs`. CS0103 at NT8 F5 compile gate is possible. NT8-044 is absent from the ticket's NT8 Rule Constraints table and absent from SCAN-04's pre-actions.

---

## Architect Fix Required for T-B32-T2

Add the following to T-B32-T2 before engineer execution:

### 1. Add to NT8 Rule Constraints table

```
| NT8-044 | `StringComparison` requires `using System;` in NT8 NinjaScript | Confirm `using System;` is present in CopyEngine.cs file preamble. If absent, add as Change 0. |
```

### 2. Add Change 0 to Exact Line-Level Changes section

```
#### CopyEngine.cs — Change 0 (NT8-044 pre-condition): using System; guard

Before making any other change, verify CopyEngine.cs contains `using System;` in the
using-directives block at the top of the file.

If PRESENT: proceed — no action needed.
If ABSENT:  add `using System;` to the using block (before `using NinjaTrader.*` lines).
```

### 3. Add NT8-044 to SCAN-04 pre-actions

In the SCAN-04 block, add:

```
Confirm present: using System; in CopyEngine.cs file preamble (required for StringComparison.Ordinal)
```

---

## Overall: TICKET_REVIEW_FAIL

| Ticket | Verdict | Blocking Violation |
|--------|---------|--------------------|
| T-B32-T1 | TICKET_REVIEW_PASS | None |
| T-B32-T2 | TICKET_REVIEW_FAIL | NT8-044: `StringComparison.Ordinal` used without confirming/adding `using System;` |

**Overall: TICKET_REVIEW_FAIL** — T-B32-T1 is clean and ready for the engineer. T-B32-T2 requires architect to add the NT8-044 guard (Change 0 + NT8 table row + SCAN-04 pre-action) before engineer execution. This is a minimal, targeted fix — no redesign required.

---

*Reviewer note: T-B32-T2's violation is a single-line pre-condition check, not an architectural flaw. Once the architect adds the NT8-044 verification item to the ticket, re-review is expected to return TICKET_REVIEW_PASS for T-B32-T2 without further changes.*

---

## Cycle 2 Re-gate — T-B32-T2 Patch Verification

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-19
**Cycle**: 2 (re-gate after architect patch)
**Scope**: T-B32-T2 NT8-044 violation only. T-B32-T1 verdict unchanged (TICKET_REVIEW_PASS).

### 3-Item Patch Verification

#### Item 1 — NT8-044 row in NT8 Rule Constraints table

**Location in ticket**: `docs/brain/B32-LaneA/04-tickets.md`, line 640.

**Actual text**:
> `NT8-044 | Confirm 'using System;' present at top of CopyEngine.cs (StringComparison.Ordinal requires it) | IsAtmSlotName uses StringComparison.Ordinal which lives in System. NT8 NinjaScript compiler does not auto-inject System.*; missing 'using System;' causes CS0103 at F5 compile`

**Assessment**: Risk correctly stated. Names the namespace (`System`), names the specific compile error (`CS0103`), names the NT8 compiler behaviour (no auto-inject). ✅

**Result: PASS**

---

#### Item 2 — Change 0: Confirm `using System;` in CopyEngine.cs

**Location in ticket**: `docs/brain/B32-LaneA/04-tickets.md`, lines 438–444.

**Actual text**:
> `#### CopyEngine.cs — Change 0 — Confirm 'using System;' in CopyEngine.cs`
> `File: c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
> `Action: Verify (no code change needed if already present; add if missing)`
> `If missing, add at top of file with other using directives:`
> `using System;`
> `NT8-044: StringComparison.Ordinal requires System namespace.`

**Assessment**: Engineer is explicitly told to verify the file. Both branches covered: "no code change if already present" and "add if missing". Exact `using System;` text provided. NT8-044 cross-reference present. ✅

**Result: PASS**

---

#### Item 3 — SCAN-04 pre-action for NT8-044

**Location in ticket**: `docs/brain/B32-LaneA/04-tickets.md`, line 683.

**Actual text** (inside SCAN-04 block):
> `Pre-actions:`
> `- [ ] NT8-044: confirm 'using System;' present in CopyEngine.cs`

**Assessment**: Explicit checkbox pre-action inside SCAN-04. Engineer must tick this before marking SCAN-04 PASS. Correctly scoped to the file that introduces `StringComparison.Ordinal`. ✅

**Result: PASS**

---

### T-B32-T2 Cycle 2 Verdict

All three required patch items are present and correctly stated:

| Item | Location | Result |
|------|----------|--------|
| NT8-044 row in NT8 Rule Constraints table | line 640 | PASS |
| Change 0 — Confirm/add `using System;` | lines 438–444 | PASS |
| SCAN-04 pre-action — NT8-044 checkbox | line 683 | PASS |

All other T-B32-T2 checks (Traceability, JS P0 Pre-Check, CYC, Completeness, Test Coverage, Scan Checklist 1–7, File Routing) were PASS in Cycle 1 and are unchanged.

**T-B32-T2 Cycle 2 VERDICT: TICKET_REVIEW_PASS**

---

### Cycle 2 Overall

| Ticket | Cycle 1 Verdict | Cycle 2 Verdict | Change |
|--------|----------------|----------------|--------|
| T-B32-T1 | TICKET_REVIEW_PASS | TICKET_REVIEW_PASS (unchanged) | — |
| T-B32-T2 | TICKET_REVIEW_FAIL | TICKET_REVIEW_PASS | NT8-044 patch confirmed |

## Overall: TICKET_REVIEW_PASS

Both tickets are clean. Engineer execution of B32-LaneA may proceed.
