# B127 Ticket Review

**Block**: B127
**Phase**: 3.5 -- Ticket Review
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-25
**Ticket Source**: `docs/brain/B127/04-tickets.md`
**Plan Source**: `docs/brain/B127/02-architecture-plan.md` (REVIEW_PASS)
**Spec Source**: `docs/brain/B107/06-deferred-backlog.md` DW-PTT-BE-FIX-01

---

## Review Result: TICKET_REVIEW_PASS

---

## Checklist Results (T1.1 through T1.20)

| # | Item | Result | Citation |
|---|------|--------|----------|
| T1.1 | Ticket traces to DW-PTT-BE-FIX-01 spec requirement | PASS | Ticket lines 25-26; quotes exact spec text from `docs/brain/B107/06-deferred-backlog.md` |
| T1.2 | Every plan change (sections C1-C10) has a corresponding ticket step | PASS | Plan C1->Steps 1-4; C2->Step 11; C3->Step 4 note; C4->Step 4 note; C5->Step 8; C6->Step 9; C7->Step 10; C8->Step 5; C9->Step 6; C10->Step 7; G->Step 12 |
| T1.3 | No lock() in AllAccounts() design (JS-021) | PASS | Step 7 AllAccounts() uses only `ConcurrentDictionary.TryGetValue` and `TryAdd`; no `lock(` keyword present |
| T1.4 | No throw in hot path -- lazy path uses yield/continue/Output.Process (JS-001) | PASS | AllAccounts() body (ticket lines 189-234) and DeriveFollowerNames() contain zero `throw` statements; all error paths use `continue` or `Output.Process` |
| T1.5 | No null yielded from AllAccounts() (JS-002) | PASS | Null slots resolve to non-null Account then yield, or are skipped via `continue`; `DeriveFollowerNames()` returns `Array.Empty<string>()` not null |
| T1.6 | CopyRule.FollowerAccountNames is readonly field on readonly struct (JS-008) | PASS | Step 1: `internal readonly string[] FollowerAccountNames;` on `internal readonly struct CopyRule` (confirmed at CopyEngine.cs line 392) |
| T1.7 | AllAccounts() CYC post-change explicitly stated and <= 8 | PASS | Step 7 comment: CYC=7 with all 7 decision points enumerated; 7 <= 8 |
| T1.8 | DeriveFollowerNames() CYC stated and <= 8 | PASS | Step 2 comment: `CYC=2: null/length guard (1) + for loop (1)`; 2 <= 8 |
| T1.9 | No NT8-incompatible APIs (no System.Collections.Immutable, no async/task in hot path) | PASS | Uses `System.Collections.Concurrent.ConcurrentDictionary` (available .NET 4.0+); no `async`, no `Task`, no `System.Collections.Immutable` |
| T1.10 | ConcurrentDictionary is available in .NET Framework 4.8 (NT8 runtime) | PASS | `ConcurrentDictionary<K,V>` has been in mscorlib/System.dll since .NET 4.0; plan reviewer confirmed (02-plan-review.md item 3) |
| T1.11 | All 4 CopyRule.Create callers needing update listed with line numbers | PASS | Caller Inventory table (ticket lines 401-411): lines 1108, 1184, 2809, 4289 with "Edit Required" column; lines 1131, 1159 explicitly marked NO EDIT; all line numbers confirmed in source |
| T1.12 | LoadRules() _resolvedFollowers.Clear() step present | PASS | Step 6 (ticket lines 160-172): add `_resolvedFollowers.Clear();` at line 4361 immediately after `_rules = new ConcurrentBag<CopyRule>()` |
| T1.13 | AllAccounts() made internal (for InternalsVisibleTo test access) | PASS | Step 7 explicitly states "change the access modifier from `private` to `internal`"; code block shows `internal IEnumerable<Account> AllAccounts(...)` |
| T1.14 | 3 xUnit [Fact] tests present with clear test names | PASS | Step 12: T1_AllAccounts_ReturnsResolvedAccount_WhenAccountAvailableAtLoadTime, T2_AllAccounts_LazyResolves_WhenAccountAppearsAfterLoad, T3_AllAccounts_EmitsWarningAndSkips_WhenAccountNotResolvable |
| T1.15 | Tests cover: resolved-at-load (T1), lazy-success (T2), lazy-fail+warning (T3) | PASS | T1=fast path (acc != null), T2=lazy resolve success, T3=lazy resolve fail with WARNING message; all 3 scenarios covered |
| T1.16 | xUnit [Fact] only -- no NUnit, no MSTest | PASS | Step 12 stubs use `[Fact]` exclusively; no `[Test]`, `[TestMethod]`, `using NUnit`, or `using Microsoft.VisualStudio.TestTools` anywhere in ticket |
| T1.17 | All 7 scans present with exact PowerShell commands | PASS | 7-Scan Checklist section (ticket lines 447-492): SCAN 1-7 all present with exact `Select-String` / manual count / `dotnet build` commands |
| T1.18 | Each scan has clear required result (0 matches / build passes) | PASS | SCAN 1: "0 matches in modified code"; SCAN 2: "0 matches"; SCAN 3: "0 new occurrences"; SCAN 4: "Total = 7. PASS"; SCAN 5: xUnit present + 0 NUnit; SCAN 6: "0 matches"; SCAN 7: "0 errors. 0 new warnings" |
| T1.19 | Acceptance criteria are specific and verifiable (not vague) | PASS | 13 discrete checkbox items (ticket lines 429-443), each naming an exact field, method signature, file, or observable behavior |
| T1.20 | ticket-1-completion.md artifact spec is present | PASS | "Ticket Completion Artifact" section (ticket lines 497-507): specifies `docs/brain/B127/ticket-1-completion.md` with required contents (Steps 1-12 summary, CYC count, 7-scan results, build output, seam approach) |

---

## Violations

None.

---

## Engineer Notes

The following observations do not block implementation but the engineer must be aware of them:

1. **Test seam for Account.All**: `FindFollowerAccount()` at CopyEngine.cs line 3304 iterates `Account.All` (an NT8 API). The engineer must choose one of the three seam options described in Step 12 (injectable delegate, existing test pattern from B124/B126, or observable-output-only testing). Check `src/PropTraderTools/Tests/B126Tests.cs` FIRST -- if a seam for `Account.All` already exists there, match it exactly. Do not invent a new seam.

2. **`_resolvedFollowers` field access modifier**: The field is `private readonly` (Step 5). Test 2 in Step 12 notes it may not be directly accessible from the test project. The observable-output approach (account yielded + INFO message emitted) is the fallback. This is acceptable -- the plan reviewer confirmed this at 02-plan-review.md note 5.

3. **`ConcurrentDictionary` constructor uses `StringComparer.Ordinal`**: The ticket Step 5 specifies `new ConcurrentDictionary<string, Account>(StringComparer.Ordinal)`. The plan's C8 section uses the default constructor (no comparer). The ticket's version is strictly stronger (ordinal comparison is faster and avoids culture-sensitive surprises). This is not a conflict -- it is a correctness improvement. Use the ticket version (with `StringComparer.Ordinal`).

4. **`DtoToRule` is `private static`** (confirmed at CopyEngine.cs line 4236). The method is NOT changed to `internal` in this block. The `dto.FollowerAccountNames` argument (Step 11) is the only change required there. Do not change the access modifier.

5. **No changes to `RuleToDto`**: The plan section I explicitly confirms `RuleToDto` requires no changes. Do not touch it.

6. **Backward compat gate**: After Steps 1-11, run `Select-String -Pattern "CopyRule.Create" src/PropTraderTools/CopyEngine.cs` and verify all 6 call sites compile (3 with new 8th arg, 2 with no change, 1 with `dto.FollowerAccountNames`). The build (SCAN 7) is the final gate.

---

*Ticket review complete. Status: TICKET_REVIEW_PASS.*
*Next phase: ptt-engineer implements from 04-tickets.md.*
*After implementation: ptt-verifier reviews src vs this ticket and ticket-1-completion.md.*
