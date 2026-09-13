# PTT-BUGS-LIVE-0911 Lane C — Ticket Review

**Status:** TICKET_REVIEW_PASS
**Phase:** 3.5 (Ticket Review)
**Author:** ptt-ticket-reviewer
**Tickets reviewed:** `docs/brain/PTT-BUGS-LIVE-0911/04-tickets-lane-c.md`
**Plan reviewed against:** `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-c.md` (REVIEW_PASS)
**Source verified:** `src/PropTraderTools/CopyEngine.cs` (live read, lines 2620–2661)
**Epic:** PTT-BUGS-LIVE-0911

---

## Review Summary — TICKET_REVIEW_PASS

**All checks PASS. No violations. Cleared for ptt-engineer.**

---

## Ticket LC-T1: Add TryLaneDHook Stub at Lane D Wiring Point in DispatchCopy

### Traceability Check — PASS

| Spec Requirement | Ticket Section | Coverage |
|---|---|---|
| Lane C #1 — `OnOrderUpdate` is the sole correct entry dispatch path | Spec Requirements table, row 1 | Mapped — confirmed by architecture plan Section 3 finding |
| Lane C #2 — Identify exact Lane D wiring point in `DispatchCopy` (after `ResolveBaseQty`, before B119) | Spec Requirements table, row 2 | Mapped — Step 1 inserts at line 2630, confirmed in source |
| Lane C #3 — Establish wiring contract via unconditional stub call | Spec Requirements table, row 3 | Mapped — Step 1 + Step 2 together |
| Lane C #4 — Zero behavior change; empty body; no dispatch path altered | Spec Requirements table, row 4 | Mapped — explicitly stated in Step 1 and NT8 Constraints table |

**No phantom work** (nothing in ticket not in plan/spec).
**No missing work** (all 4 requirements covered, single-ticket scope confirmed by plan Section 2 SINGLE-PIPELINE ruling).

---

### Implementation Instruction Clarity — PASS

| Check | Result |
|---|---|
| Exact before/after code block provided for `DispatchCopy` insertion | PASS — Step 1 shows full before/after context |
| Line numbers verified against live source | PASS — line 2629 is `int baseQty = ResolveBaseQty(...)`, line 2630 is blank, line 2631 is `// B119: DW-B128` — source confirmed at read time |
| `TryLaneDHook` method block exact text provided | PASS — Step 2 provides verbatim 8-line block including doc comment |
| Method placement instruction unambiguous | PASS — "after the closing brace of `DispatchCopy`" with closing brace confirmed at line 2661 in source |
| Test body fully specified (not just name) | PASS — full 6-line test body provided, no improvisation required |
| `GetMethod` helper verified at line 24 of test file | PASS — confirmed in `CopyEngineTests.cs` line 24 |
| No ambiguous "implement as appropriate" language | PASS — every insertion is exact text, no free-form instructions |

---

### CYC / JS Pre-Check — PASS

| Rule | Check | Result |
|---|---|---|
| JS-021 (P0) | No `lock()` in `TryLaneDHook` stub body | PASS — empty body, zero statements |
| JS-021 (P0) | No `lock()` in `DispatchCopy` call site insertion | PASS — `TryLaneDHook(order, rule, baseSignal, baseQty);` is a void call, no lock |
| JS-001 (P0) | No `throw` in `TryLaneDHook` body | PASS — empty body |
| JS-001 (P0) | No `throw` at the `DispatchCopy` call site | PASS — unconditional void call only |
| JS-002 (P0) | No null return path introduced | PASS — method is `void`; JS-002 N/A by design |
| async/await ban | No `async`/`await` in stub or call site | PASS — both are synchronous |
| CYC — `TryLaneDHook` | Empty body = CYC 1 (base path only) | PASS — within ≤8 limit |
| CYC — `DispatchCopy` | Unconditional void call adds 0 branches | PASS — McCabe: a call statement is not a branch; `DispatchCopy` CYC remains 8 |
| ASCII-only | Comment and identifier `TryLaneDHook` are ASCII | PASS — all characters in the inserted block are in [0x00–0x7F] |

---

### NT8 Constraint Check — PASS

| Constraint | Check | Result |
|---|---|---|
| `StartAtmStrategy` must be STATIC | Not called in stub (empty body) | PASS — N/A |
| `AtmStrategyCreate()` BANNED | Not called in stub | PASS — N/A |
| `AtmStrategyChangeStopTarget()` BANNED | Not called in stub | PASS — N/A |
| No `async`/`await` in lifecycle/event-handler descendants | `TryLaneDHook` is synchronous; called from `DispatchCopy` (NT8 account bg thread) | PASS |
| `DispatchCopy` thread context documented | NT8 Constraints table row explicitly notes "NT8 account background thread" and warns Lane D implementor | PASS |
| No `DateTime.Now` | Not present in stub | PASS |
| No `sealed` on `TradeCopierWindow` | Not referenced in this ticket | PASS — N/A |
| No hardcoded hex color | Not referenced in this ticket | PASS — N/A |
| No `FontFamily` set on WPF element | Not referenced in this ticket | PASS — N/A |
| No `Account.All` outside `Loaded` handler | Not called in stub | PASS — N/A |
| `CreateOrder` name prefix | Not called in stub | PASS — N/A |

---

### Test Coverage Check — PASS

| Method introduced | Test method | No-Skip rationale | Assertions |
|---|---|---|---|
| `private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)` | `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` | Reflection-only — no NT8 host required | (1) method != null, (2) ReturnType == void, (3) ParameterCount == 4 |

**No `Skip` attribute** on the new test — correct, since it uses `GetMethod` reflection only (no NT8 runtime needed).
All pre-existing tests carry `[Fact(Skip = "NT8-runtime: ...")]` — the ticket explicitly preserves their Skip status.

**Note on test name vs. plan:** The architecture plan (Section 6) lists the test name as `TryLaneDHook_MethodExists_WithCorrectSignature` and includes an assertion "does not throw when called with a null Order". The ticket uses `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` and omits the null-call assertion. The ticket's version is strictly safer — invoking the method with `null` would require a live `CopyEngine` instance, introducing NT8-host dependency. The ticket correctly eliminates this. The change is a tightening of scope, not a deviation. PASS.

---

### 7-Scan Checklist Completeness — PASS

All 7 canonical scans are present with exact grep commands and required results:

| Scan | Present | Exact command | Expected result stated |
|---|---|---|---|
| SCAN-01 — `lock()` grep | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-02 — CYC manual count | YES | Lizard or manual McCabe on `DispatchCopy` + `TryLaneDHook` | `DispatchCopy`=8, `TryLaneDHook`=1, both ≤8 |
| SCAN-03 — async/await grep | YES | `grep -n "async\|await" src/PropTraderTools/CopyEngine.cs` | Zero matches in new code |
| SCAN-04 — non-ASCII bytes | YES | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero new matches in added lines |
| SCAN-05 — ATM API grep | YES | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs` | Zero matches |
| SCAN-06 — dotnet build + test | YES | `dotnet build ...csproj` + `dotnet test ...cs` | 0 errors, 0 failures; new test passes; Skip tests remain Skipped |
| SCAN-07 — structural verification | YES | 7-row table: call site, B119 unchanged, method definition, method position, no new fields, no other method modified, test file check | Each item explicitly described |

All scans include both the verification command/procedure and the required result. This satisfies the defense-in-depth contract (engineer attestation anchor for Phase 4b verifier cross-check).

---

### File Routing Check — PASS

| File | Path used in ticket | Correct workspace |
|---|---|---|
| `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace |
| `CopyEngineTests.cs` | `src/PropTraderTools/CopyEngineTests.cs` | PASS — Wave workspace |

No `.cs` file paths point to the Director workspace.

---

### Scope Inflation Check — PASS

The ticket adds exactly:
1. One call-site line in `DispatchCopy` (1 line, zero logic)
2. One private method with empty body (8 lines including doc comment)
3. One `[Fact]` reflection test (6 lines)

No new fields, no new properties, no changes to existing methods, no new using directives, no new configuration. Matches the plan's "minimum required to establish the wiring contract" description precisely.

---

## Violations

**None.**

---

## Clearance for ptt-engineer

**TICKET_REVIEW_PASS.** Ticket LC-T1 is cleared for implementation by ptt-engineer.

The engineer must:
1. Execute Step 1 exactly as written — insert the single call-site line at line 2630 of `CopyEngine.cs`.
2. Execute Step 2 exactly as written — append the `TryLaneDHook` method stub after the closing brace of `DispatchCopy` (line 2661).
3. Append the `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` test to `CopyEngineTests.cs`.
4. Run all 7 scans and record results before submitting BUILD_PASS.
5. Confirm SCAN-02 manually: `DispatchCopy` CYC must remain 8; `TryLaneDHook` CYC must be 1.
6. Confirm SCAN-07 structural table item by item before marking complete.

*Return: TICKET_REVIEW_PASS*
