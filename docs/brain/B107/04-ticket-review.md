# B107 Ticket Review

**Reviewer**: ptt-ticket-reviewer
**Epic**: B107-T1
**Tickets file**: `docs/brain/B107/04-tickets.md`
**Plan file**: `docs/brain/B107/02-architecture-plan.md`
**Plan review**: `docs/brain/B107/02-plan-review.md` — REVIEW_PASS (14/14 criteria)
**Date**: 2026-08-10

---

## Ticket B107-T1

### TC1–TC14 Pass/Fail Table

| ID | Criterion | Result | Ticket Section | Notes |
|----|-----------|--------|---------------|-------|
| TC1 | Traceability: DW-B105 + DW-B106 each mapped to ≥1 code change | PASS | §Spec-to-Change Traceability | DW-B105 → CHANGE A+B+C; DW-B106 → CHANGE D+E. All 5 changes anchored to spec items. |
| TC2 | 7-scan checklist present (SCAN-01 through SCAN-07) | PASS | §7-Scan Checklist | SCAN-01 lock(), SCAN-02 async void, SCAN-03 return null, SCAN-04 non-ASCII, SCAN-05 CYC, SCAN-06 field visibility, SCAN-07 try/finally — all 7 present. |
| TC3 | Each scan has exact command with flags + expected result | PASS | §7-Scan Checklist — each scan sub-section | Every scan has verbatim `grep -rn`/`powershell` command and explicit "Expected: Zero …" statement. SCAN-05 provides `python scripts/complexity_audit.py` plus manual CYC table. SCAN-07 is manual inspection with 5 numbered items. |
| TC4 | Acceptance criteria T1–T7 present and code-inspection-verifiable | PASS | §Acceptance Criteria | T1–T7 all present under DW-B105 (T1–T4) and DW-B106 (T5–T7). Each is a binary pass/fail against a specific code construct with no runtime test required. T7 (no existing tests broken) correctly defers to code inspection. |
| TC5 | 5 code changes verbatim or citing exact plan section — no vague "as described" | PASS | §Precise Code Changes (CHANGE A–E) | All 5 changes provide verbatim code blocks with exact insertion points and line references matching the architecture plan (§2 CHANGE A/B/C, FIX 1, FIX 2). No vague descriptions. |
| TC6 | Files in scope: exactly 3 named, no additional files | PASS | §Files In Scope | Table lists exactly 3 files. "New files created: 0", "Test project files changed: 0", "Interface files changed: 0", "Other PropTraderTools files changed: 0" — all explicit. |
| TC7 | CYC before/after table present; all values ≤ 8 after | PASS | §CYC Before/After Table | Table covers 4 methods. All CYC-after values: TryReplacePttBeBrackets=7, ExecuteOne=2, SnapshotTargetOrders=7, ResolveTargetCount=2. Max = 7 ≤ 8. |
| TC8 | try/finally integrity explicitly stated (TryAdd before try, TryRemove in finally) | PASS | §CHANGE C (Invariant paragraph) + §SCAN-07 | CHANGE C: "TryAdd MUST appear BEFORE the try block. TryRemove MUST appear INSIDE the finally block." SCAN-07 items 1–3 repeat the check as independent inspection steps. |
| TC9 | `_qxCancelInProgress` declared as `internal readonly ConcurrentDictionary<string,bool>` | PASS | §CHANGE A | Code block shows exactly: `internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress = new ConcurrentDictionary<string, bool>();` — type, modifier, name, initialiser all match. |
| TC10 | No lock() anywhere in new code blocks shown in ticket | PASS | §JS Rule Constraints + all 5 CHANGE blocks | No `lock(` keyword appears in any of the 5 code blocks. SCAN-01 targets this explicitly. SCAN-07 item 4 cross-checks it at the ExecuteOne site. |
| TC11 | ResolveTargetCount fallback default is 3, cap is Math.Min(raw,3) | PASS | §CHANGE E | `leaderCount > 0 ? leaderCount : 3` (not 2) confirmed in code block. `return Math.Min(raw, 3)` present with DW-B106 comment. Source at line 258 currently has `2` — ticket correctly replaces it with `3`. |
| TC12 | SnapshotTargetOrders return: `nativeTargets.Count>0 ? nativeTargets : pttTargets` | PASS | §CHANGE D (line 166 of code block) | Return statement in verbatim code block: `return nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (6)` — exact match. |
| TC13 | Build gate present: ptt-sync-and-verify.ps1 + F5 compilation | PASS | §Build Verification Steps | Item 2: `powershell -File scripts\ptt-sync-and-verify.ps1` — must show 0 MISMATCH lines. Item 3: "Press F5 in NinjaTrader 8 — must compile green (zero errors)." Both present and explicit. |
| TC14 | Commit message format specified | PASS | §Build Verification Steps (item 5) | `git commit -m "feat(ptt): B107 DW-B105 + DW-B106 intent-guard + target-count cap"` — verbatim commit message present. |

---

### JS Pre-Check (rule violations described in ticket)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in any new code description | PASS — ConcurrentDictionary TryAdd/TryRemove/ContainsKey used throughout; no lock() |
| JS-001 | No `throw new Exception` in hot path | PASS — all new paths use early `return` |
| JS-002 | No `return null` for optional value | PASS — SnapshotTargetOrders returns empty list on null input |
| JS-033 | No `async void` added | PASS — all new code is synchronous |
| ASCII-only | No Unicode in new string literals or identifiers | PASS — "[PTT-QX-GUARD]...", "Target", "PTT-QX-T", "PTT-BE-Target-", `_qxCancelInProgress` all pure ASCII |

### NT8 Constraint Check

| Constraint | Check | Result |
|-----------|-------|--------|
| No async/await in lifecycle method | No async in any described method | PASS |
| No sealed on TradeCopierWindow | Not applicable — no window class touched | PASS |
| No DateTime.Now usage | Not present in any new code | PASS |
| CreateOrder naming | No CreateOrder calls added | PASS |

### CYC Pre-Check

All described methods: TryReplacePttBeBrackets (7), ExecuteOne (2), SnapshotTargetOrders (7), ResolveTargetCount (2). All ≤ 8. **PASS**

### Test Coverage Check

This ticket has **no new public or internal methods** — it modifies 4 existing methods and inserts 1 new field. The acceptance criteria T1–T7 are verifier inspection criteria (code inspection, not executable tests). No [Fact] test methods are required because the changes are surgical modifications to existing non-testable NT8 methods in a project that documents pre-existing test compilation deferrals (DW-B102-DEFER-01 and DW-B102-DEFER-02) in T7. The ticket correctly defers runtime testing to NT8 F5 gate.

**PASS** (no new public/internal testable methods; inspection criteria T1–T7 cover the contract)

### Scan Checklist Presence

SCAN-01 (lock) ✓ SCAN-02 (async void) ✓ SCAN-03 (return null) ✓ SCAN-04 (non-ASCII) ✓ SCAN-05 (CYC) ✓ SCAN-06 (field visibility) ✓ SCAN-07 (try/finally integrity) ✓

**All 7 scans present with commands and expected results. PASS**

### File Routing Check

All 3 source files route to `src/PropTraderTools/` (Wave workspace `c:\WSGTA\universal-or-strategy`). No Director workspace paths used for .cs files. **PASS**

---

## VIOLATIONS

**None.**

All 14 criteria (TC1–TC14) PASS. No JS rule violations, NT8 constraint violations, CYC exceedances, missing scan checklists, file routing errors, or missing traceability found in the ticket description.

---

## Source Anchor Verification (reviewer spot-checks)

| Anchor | Expected | Actual (source read) | Match |
|--------|----------|----------------------|-------|
| `_beReplaceAttempts` insertion point | Ends at `new ConcurrentDictionary<string, int>();` line 257-258 | Line 257-258: `private readonly ConcurrentDictionary<string, int> _beReplaceAttempts = new ConcurrentDictionary<string, int>();` | ✓ |
| Guard (3b) insertion between line 2283–2284 | `return; // (3)` at 2283, `var acc =` at 2285 | Line 2283: `return; // (3)`, line 2285: `var acc = cancelledStop.Account;` | ✓ |
| ExecuteOne existing code (lines 145-153) | if (!skipIfFollower) block with CancelQxBrackets (no try/finally yet) | Lines 145-153 show `if (!skipIfFollower)` block with `CancelQxBrackets` — no try/finally present, confirming replacement is needed | ✓ |
| SnapshotTargetOrders existing (lines 172-210) | Single flat list, no native/ptt split | Lines 172-210 show single `result` list, `isTarget` combines all three name patterns — confirming two-pass split is needed | ✓ |
| ResolveTargetCount existing (line 255-258) | Expression body with fallback `2` | Line 258: `=> own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 2)` — fallback is `2`, confirming change to `3` is needed | ✓ |

All 5 source anchors verified against actual code. Insertion points are correct. Replacements are required.

---

## Overall

**TICKET_REVIEW_PASS**

All 14 criteria pass. All 7 scans present. All JS rules satisfied in ticket descriptions. All source anchors verified. No violations. The engineer may proceed with B107-T1.
