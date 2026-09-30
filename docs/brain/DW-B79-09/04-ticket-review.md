# DW-B79-09 — Ticket Review (Phase 3.5)

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-21
**Pass**: Second pass (V-01 fix applied by architect)
**Tickets reviewed**: `docs/brain/DW-B79-09/04-tickets.md`
**Architecture plan**: `docs/brain/DW-B79-09/02-architecture-plan.md`
**Plan review gate**: REVIEW_PASS (ptt-plan-reviewer, 2026-08-21)
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-041 read)

---

## T1 — DW-B79-09-TICKET-1

### Traceability

| Check | Result | Notes |
|-------|--------|-------|
| Ticket references spec req DW-B79-09 | ✓ PASS | "Spec Requirement IDs" section explicitly cites DW-B79-09 |
| All 3 fix targets from architecture plan covered | ✓ PASS | CancelQxBrackets 2-param (L630), 3-param (L702), CancelStaleBracketsLocal (L193) — each has a named Edit section |
| All 3 test IDs present (T_DW_B79_09_01/02/03) | ✓ PASS | All three [Fact] methods present with full source bodies |
| No phantom work (ticket items not in plan/spec) | ✓ PASS | Ticket scope is identical to plan §2 and §5 |
| No missing work (plan items absent from ticket) | ✓ PASS | All plan items at §3a–§3c and §5 are covered |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Requirement | Ticket Claim | Result |
|------|-------------|-------------|--------|
| JS-021 | No `lock()` added | RemoveAll on local List<T>, no shared state, no lock added | ✓ PASS |
| JS-001 | No `throw` in hot path | RemoveAll with valid predicate does not throw; existing catch{} retained | ✓ PASS |
| JS-002 | No `return null` | All three methods are void; no return value | ✓ N/A |
| JS-033 | No `async void` | No async/await introduced | ✓ PASS |
| ASCII-only | No Unicode/emoji/curly quotes in C# source | Inserted line confirmed 100% ASCII | ✓ PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | CYC Before | CYC After | Budget | Result |
|--------|-----------|-----------|--------|--------|
| CancelQxBrackets 2-param | 6 | 6 | ≤8 | ✓ PASS |
| CancelQxBrackets 3-param | 7 | 7 | ≤8 | ✓ PASS |
| CancelStaleBracketsLocal | 6 | 6 | ≤8 | ✓ PASS |

RemoveAll(predicate) is a single List<T> method call — not a control-flow branch. CYC is
correctly stated as unchanged. No extraction required.

**CYC Pre-Check: PASS**

---

### NT8 Check

| NT8 Constraint | Check | Result |
|----------------|-------|--------|
| No async/await in lifecycle methods | None introduced | ✓ PASS |
| No Account.All outside Loaded handler | Not used | ✓ N/A |
| No sealed on TradeCopierWindow | Not applicable | ✓ N/A |
| No FontFamily set on WPF element | Not applicable | ✓ N/A |
| No hardcoded hex color | Not applicable | ✓ N/A |
| No CreateOrder without "PTT-" prefix | Not applicable | ✓ N/A |
| No DateTime.Now usage | Not introduced | ✓ PASS |
| List<T>.RemoveAll is BCL, not NT8-specific | Confirmed | ✓ PASS |
| acc.Cancel(Order[]) is pre-existing call site, not new | Confirmed | ✓ PASS |

**NT8 Check: PASS**

---

### BEFORE/AFTER Code Block Accuracy (includes V-01 resolution check)

**V-01 RESOLUTION — Edit 3 BEFORE block vs `PttBreakEven.cs` L190–198 (HEAD 5925b618)**

Actual source (read_file L185–205):
```
if (stale.Count == 0) return;                                         // (3)
try
{
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(
        "[BE] CancelStaleBracketsLocal: " + stale.Count + " orders cancelled",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
catch { /* cancel on already-filled orders is non-fatal */ }
```

Ticket Edit 3 BEFORE block:
```
if (stale.Count == 0) return;                                         // (3)
try
{
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(
        "[BE] CancelStaleBracketsLocal: " + stale.Count + " orders cancelled",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
catch { /* cancel on already-filled orders is non-fatal */ }
```

Line-by-line comparison: **9/9 lines match verbatim.** The ellipsis `...` placeholder from the
first pass is gone. Both `NinjaTrader.Code.Output.Process(` arguments are now spelled out in full.

**V-01: RESOLVED**

| Edit | BEFORE Block | AFTER Block | Result |
|------|-------------|-------------|--------|
| Edit 1 (CopyEngine.cs L630) | `try { acc.Cancel(stale.ToArray()); } catch { }` — verbatim; plan §3a confirms L630 exact match | RemoveAll one-liner + comment precedes try block | ✓ PASS |
| Edit 2 (CopyEngine.cs L702) | `if (stale.Count == 0) return; // (7)` + `try { acc.Cancel(stale.ToArray()); } catch { }` — verbatim; plan §3b confirms L701-702 match | RemoveAll one-liner inserted after guard, before try block | ✓ PASS |
| Edit 3 (PttBreakEven.cs L193) | Full verbatim block including both NinjaTrader.Code.Output.Process arguments — **V-01 RESOLVED** | RemoveAll as first statement inside try block, before acc.Cancel — correct | ✓ PASS |

**BEFORE/AFTER Code Block Accuracy: PASS**

---

### Test Coverage

| Test ID | Method Under Test | Contract Described | Access Pattern | Result |
|---------|-------------------|-------------------|---------------|--------|
| T_DW_B79_09_01 | CancelQxBrackets 2-param | IL body contains RemoveAll call token before acc.Cancel | BindingFlags.NonPublic \| Instance, 2-param signature | ✓ PASS |
| T_DW_B79_09_02 | CancelQxBrackets 3-param | IL body contains RemoveAll call token before acc.Cancel | BindingFlags.NonPublic \| Instance, 3-param signature | ✓ PASS |
| T_DW_B79_09_03 | CancelStaleBracketsLocal (private static) | IL body contains RemoveAll call token | BindingFlags.NonPublic \| Static — consistent with PttBreakEvenB72Tests.cs | ✓ PASS |
| ContainsMethodToken helper | Shared IL byte scanner | Loop scans 0x28/0x6F opcodes + 4-byte token | N/A (static helper, not a public method — no [Fact] required) | ✓ PASS |

All three [Fact] methods have described contracts (not just names). The private-method access
pattern is explicitly addressed using the established NonPublic|Static reflection pattern.
Test count delta correctly stated: 292 → 295 (+3).

**Test Coverage: PASS**

---

### Scan Checklist Presence (defense in depth — all 7 required)

| Scan | Command Present | Expected Result Stated | Result |
|------|----------------|----------------------|--------|
| SCAN-01 — lock scan | `Select-String -Path "src/**/*.cs" -Pattern "lock\("` | 0 results | ✓ PASS |
| SCAN-02 — async-void scan | `Select-String -Path "src/**/*.cs" -Pattern "async void "` | 0 results | ✓ PASS |
| SCAN-03 — return-null scan | `Select-String -Path "src/**/*.cs" -Pattern "return null;"` | 0 results | ✓ PASS |
| SCAN-04 — complexity audit | `python scripts/complexity_audit.py` | all CYC ≤ 8 | ✓ PASS |
| SCAN-05 — dotnet build | `dotnet build` | 0 errors | ✓ PASS |
| SCAN-06 — dotnet test | `dotnet test` | 295 PASS (+3) | ✓ PASS |
| SCAN-07 — CSharpier check | `dotnet csharpier check src/` | 0 issues | ✓ PASS |

All 7 scans present with exact commands and expected results. Defense-in-depth contract intact.

**Scan Checklist Presence: PASS**

---

### Completeness

| Check | Result |
|-------|--------|
| Engineer instructions specify apply_diff or search_and_replace (not write_file for existing files) | ✓ PASS — explicitly stated in each Edit section and Engineer Instructions §1 |
| Build + test commands specified | ✓ PASS — "Build + Test Commands" section with exact commands |
| Output artifact (ticket-1-completion.md) specified | ✓ PASS — "Output Artifact (Ph4a)" section present |
| deploy-sync.ps1 step specified | ✓ PASS — Engineer Instructions §6 |
| Recommended edit order specified | ✓ PASS — Engineer Instructions §4 |

**Completeness: PASS**

---

### File Routing

| File | Path in Ticket | Workspace | Result |
|------|---------------|-----------|--------|
| CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | Wave workspace ✓ | ✓ PASS |
| PttBreakEven.cs | `src/PropTraderTools/Features/PttBreakEven.cs` | Wave workspace ✓ | ✓ PASS |
| CopyEngineTests.cs | `src/PropTraderTools/CopyEngineTests.cs` | Wave workspace ✓ | ✓ PASS |

No paths pointing to Director workspace for .cs files.

**File Routing: PASS**

---

### Violation Log

| # | Violation ID | Severity | Status | Description |
|---|-------------|----------|--------|-------------|
| 1 | V-01 | P1 | **RESOLVED** | Edit 3 BEFORE block previously contained `NinjaTrader.Code.Output.Process(...)` with a non-literal `...` ellipsis. Architect applied surgical fix. BEFORE block now matches `PttBreakEven.cs` L190–198 verbatim (9/9 lines). |

**Total active violations: 0**

---

### VERDICT: TICKET_REVIEW_PASS

All 14 checks PASS. V-01 is resolved. The ticket is architecturally sound, JS-compliant,
CYC-neutral, verbatim-accurate, and complete in every dimension. Safe to hand to engineer.

---

## Overall: TICKET_REVIEW_PASS

| Check | First Pass | Second Pass |
|-------|-----------|------------|
| Traceability | PASS | PASS |
| Spec Coverage | PASS | PASS |
| JS Pre-Check (JS-021/001/002/033/ASCII) | PASS | PASS |
| CYC Pre-Check (JS-080) | PASS | PASS |
| NT8 Check | PASS | PASS |
| BEFORE/AFTER Block Accuracy | FAIL (V-01) | **PASS (V-01 resolved)** |
| Test Coverage | PASS | PASS |
| Scan Checklist Presence (SCAN-01..07) | PASS | PASS |
| Completeness | PASS | PASS |
| File Routing | PASS | PASS |

**Active violations**: 0
**Resolved violations**: 1 (V-01)
**Gate**: **TICKET_REVIEW_PASS — safe to spawn engineer (Phase 4a)**
