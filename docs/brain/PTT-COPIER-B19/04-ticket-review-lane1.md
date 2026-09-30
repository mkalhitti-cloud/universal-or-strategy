# Ticket Review: PTT-COPIER-B19 — Lane 1
**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5
**Date**: 2026-07-13
**Ticket file reviewed**: docs/brain/PTT-COPIER-B19/04-tickets-lane1.md
**Architecture plan**: docs/brain/PTT-COPIER-B19/02-architecture-plan.md (REVIEW_PASS Cycle 2)
**Rules catalog**: docs/standards/jane-street/RULES_CATALOG.md
**Lane scope**: Lane 1 only — Gate 2 account reference fix in CopyEngine.cs (DW-B19-COPIER-BUG-01)

---

## T1 — DW-B19-COPIER-BUG-01: Gate 2 Account Reference Fix

### Check 1 — Spec ID

**Result**: PASS

Ticket header explicitly declares `DW-B19-COPIER-BUG-01` as the ticket ID.
Spec requirement table maps to REQ-B19-01 through REQ-B19-04, all grounded in the architecture plan
and Director spec. No phantom IDs.

---

### Check 2 — Exact Fix String

**Result**: PASS

Ticket Section "Fix — One Line Change" provides the exact before/after:

```
BEFORE: e.Order.Account == rule.MasterAccount
AFTER:  e.Order.Account.Name == rule.MasterAccount?.Name
```

This matches the Director's spec requirement and the architecture plan Section 3 verbatim.
The null-conditional `?.Name` is explicitly justified (5+ existing tests pass `(Account)null`
as master; non-null-conditional would throw NRE on those rules at Gate 2). Rationale is correct.

---

### Check 3 — JS Rule Pre-Check

**Result**: PASS

| Rule | Ticket Claim | Verdict |
|------|-------------|---------|
| JS-021 — No `lock()` | Gate 2 is read-only `foreach` over `ConcurrentBag`. Fix changes comparison expression only. No lock introduced. | PASS |
| JS-001 — No `throw` in hot paths | `?.Name` evaluates to `null` on null input; no new exception path created. | PASS |
| JS-002 — No `return null` | No new methods introduced. | PASS |
| JS-033 — No `async void` | Fix is a single comparison sub-expression within an existing `if`. No new async code. | PASS |

No JS rule violations described anywhere in the ticket.

---

### Check 4 — CYC Pre-Check

**Result**: PASS

Ticket states: "OnOrderUpdate CYC unchanged at 7 (fix changes comparison type, not branch count)."
Architecture plan Section 6 confirms: "Fix changes type of comparison sub-expression. No branches
added or removed." One-line change to an `if` condition predicate — no new branches, no new loops.
CYC ≤ 8 confirmed. No at-risk methods.

---

### Check 5 — NT8 Constraints

**Result**: PASS

| NT8 Constraint | Evidence | Verdict |
|----------------|----------|---------|
| `Account.Name` is `string` | 10+ existing uses cited: CopyEngine.cs lines 456, 514, 589, 820, 843, 881, 925, 967, 997, 1068 | CONFIRMED |
| `?.` null-conditional in .NET 4.8 | C# 6+ / .NET 4.8 — confirmed valid | VALID |
| NT8-001 (`init;` ban) | No new properties introduced | CLEAN |
| NT8-002 (`record` ban) | No new record types | CLEAN |
| NT8-003 (`volatile double` ban) | No volatile fields | CLEAN |
| NT8-004 (`ImmutableDictionary` ban) | No immutable collections | CLEAN |
| NT8-007 (`CreateOrder` arg 12) | No `CreateOrder` calls in changed lines | CLEAN |

No NT8 constraint violations.

---

### Check 6 — Test 1 Implementation

**Result**: PASS

`Gate2_UsesAccountName_SourceContractVerified` is fully provided with:
- Complete `[Fact]` method body using reflection
- Assertion chain: `_rules` field → `CopyRule` generic element type → `MasterAccount` field →
  type name is `"Account"` → `Name` property exists → `PropertyType == typeof(string)`
- All `Assert.NotNull` and `Assert.Equal` calls present
- Implementation guide explains every step
- No NT8 runtime dependency (reflection-only)

---

### Check 7 — Test 2 Implementation

**Result**: PASS

`Gate2_NullMasterAccount_NoCopyOrder` is fully provided with:
- Complete `[Fact]` method body
- `SetEnabled(false)` guard to prevent copy dispatch
- `StatusUpdate` event subscription to detect spurious fires
- `AddRule("B19NULL", (Account)null, new Account[0])` follows established test pattern
- Reflection walk of `_rules` bag to retrieve rule and evaluate `?.Name` simulation
- `Assert.Null(name)` — verifies null-safe evaluation (no NRE)
- `Assert.True(foundNullMaster)` — verifies the rule was found
- `Assert.False(statusFired)` — verifies no copy dispatch
- No NT8 runtime dependency

---

### Check 8 — 7-Scan Checklist Presence

**Result**: PASS

All 7 scans present in the ticket body (Defense in Depth Layer 1):

| Scan | Command | Expected Result | Present |
|------|---------|-----------------|---------|
| SCAN-01 | `Select-String … -Pattern "e\.Order\.Account =="` | 0 results | ✅ |
| SCAN-02 | `Select-String … -Pattern "\.Account\.Name =="` | 1 result | ✅ |
| SCAN-03 | `Select-String … -Pattern "lock\s*\("` | 0 results | ✅ |
| SCAN-04 | `Select-String … -Pattern "async void "` | 0 results | ✅ |
| SCAN-05 | `dotnet build PropTraderTools.csproj` | 0 errors, 0 warnings | ✅ |
| SCAN-06 | `dotnet test --filter "Gate2"` | Both Gate2 tests pass | ✅ |
| SCAN-07 | `dotnet test` (full suite) | All 113 tests pass | ✅ |

---

### Check 9 — File Scope

**Result**: PASS

Files Modified section explicitly lists:
- `CopyEngine.cs` — 1 line change at line ~381
- `CopyEngineTests.cs` — 2 new `[Fact]` tests appended

Files NOT Modified section explicitly lists:
- `TradeCopierPanel.cs` — excluded
- `TradeCopierWindow.cs` — excluded
- `TradeCopierAddOn.cs` — excluded
- `AtrSizingEngine.cs` — excluded

File routing: both paths point to `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` (Wave workspace). No Director workspace `.cs` paths present.

---

### Check 10 — No Scope Creep

**Result**: PASS

Ticket contains explicit "Deferred Item — Do NOT Fix in This Ticket" section:

> `PopulateOrderMap` line 659 reference equality issue (DW-B19-02) — deferred to B20+.
> Engineer must NOT touch line 659 in this ticket.

Total source lines changed: 1. Total tickets: 1. Single concern (Gate 2 reference equality bug).
No related fixes bundled. AGENTS.md §11 (No Scope Creep Protocol) satisfied.

---

### Traceability Check

**Result**: PASS

| Ticket Item | Plan Reference | Status |
|-------------|---------------|--------|
| Gate 2 condition change (line 381) | Architecture plan §3 Exact Before/After Diff | ✅ Traced |
| REQ-B19-01 string name equality | Architecture plan §2 Root Cause + §3 Fix | ✅ Traced |
| REQ-B19-02 two [Fact] tests | Architecture plan §5 Test Design | ✅ Traced |
| REQ-B19-03 no regressions (113 total) | Architecture plan §8 SCAN-07 | ✅ Traced |
| REQ-B19-04 zero lock() | Architecture plan §6 JS-021 | ✅ Traced |
| DW-B19-02 deferred (line 659) | Architecture plan §4 Audit Table + §9 Deferred | ✅ Traced |

No phantom work (items in ticket not in plan/spec) found.
No missing work (items in plan not in ticket) found.

---

### Spec Coverage Check

**Result**: PASS

All spec requirements for Lane 1 are covered by T1:
- REQ-B19-01 — covered by fix description ✅
- REQ-B19-02 — covered by Test 1 + Test 2 implementations ✅
- REQ-B19-03 — covered by SCAN-07 (113-test assertion) ✅
- REQ-B19-04 — covered by SCAN-03 + JS-021 entry ✅

No duplicate coverage (single-ticket block, no overlap possible).

---

### File Routing Check

**Result**: PASS

All C# source paths reference `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` — the Wave
workspace. No Director workspace (`c:\WSGTA\universal-or-strategy-director`) paths for `.cs` files.

---

### T1 Verdict

| Check | Result |
|-------|--------|
| 1 — Spec ID | PASS |
| 2 — Exact fix string | PASS |
| 3 — JS Pre-Check (JS-021/001/002/033) | PASS |
| 4 — CYC Pre-Check | PASS |
| 5 — NT8 Constraints | PASS |
| 6 — Test 1 full implementation | PASS |
| 7 — Test 2 full implementation | PASS |
| 8 — 7-Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| 9 — File scope (CopyEngine.cs + CopyEngineTests.cs only) | PASS |
| 10 — No scope creep | PASS |
| Traceability | PASS |
| Spec coverage | PASS |
| File routing | PASS |

**T1 VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All 10 checks pass for T1 (the only ticket in this single-ticket lane).
Zero violations. Zero warnings. Zero phantom work. Zero missing work.
Engineer is cleared to proceed with T1 implementation.

**Gate cleared**: ptt-ticket-reviewer → Phase 4a (ptt-engineer)
