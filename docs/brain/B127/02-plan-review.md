# B127 Plan Review

**Block**: B127
**Phase**: 2 Plan Review
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-25
**Plan**: `docs/brain/B127/02-architecture-plan.md`
**Spec Source**: `docs/brain/B107/06-deferred-backlog.md` DW-PTT-BE-FIX-01

---

## Review Result: REVIEW_PASS

---

## Checklist Results

| # | Item | Result | Rule / Basis |
|---|------|--------|-------------|
| 1 | SPEC TRACE: Plan implements DW-PTT-BE-FIX-01 Option A lazy re-resolve | PASS | Spec: B107 deferred backlog line 117-127; addressed in plan §A, §C10, §D |
| 2 | READONLY STRUCT: JS-008 preserved (`readonly` fields on `readonly struct`) | PASS | JS-008 — `internal readonly string[] FollowerAccountNames` on `internal readonly struct CopyRule` |
| 3 | NO LOCK: Only lock-free primitives used (`ConcurrentDictionary`) | PASS | JS-021 — `ConcurrentDictionary.TryGetValue` + `TryAdd`; plan §C8 and §D confirm no `lock()` |
| 4 | NO THROW: No `throw` in AllAccounts/lazy path | PASS | JS-001 — C10 code uses only `yield return` / `continue` / Output.Process; no exception path |
| 5 | NO RETURN NULL: No null accounts yielded | PASS | JS-002 — null slots are resolved or skipped; `DeriveFollowerNames()` returns `Array.Empty<string>()` not null |
| 6 | CYC <= 8: AllAccounts() post-change CYC verified | PASS | Plan §E enumerates 7 decision points (≤ 8 limit). `DeriveFollowerNames()` = 2. |
| 7 | BACKWARD COMPAT: All existing callers of `CopyRule.Create`/`AddRule` still compile | PASS | Plan §I + §J: 8th param is optional (`= null`); `AddRule(3-arg)` line 1131 and `AddRule(5-arg)` line 1159 require zero source edits |
| 8 | CACHE STRATEGY: `_resolvedFollowers.Clear()` on `LoadRules()` | PASS | Plan §C9 and §D specify `_resolvedFollowers.Clear()` immediately after `_rules = new ConcurrentBag<CopyRule>()` |
| 9 | WARNING DESIGN: Warning on lazy fail consistent with DtoToRule | PASS | Plan §F shows 3 message types; lazy-fail warning mirrors DtoToRule load-time warning in tone and Output.Process call pattern |
| 10 | TEST CONTRACT: 3 [Fact] tests with clear pass criteria specified | PASS | Plan §G specifies exactly 3 xUnit `[Fact]` tests; each has Setup + Assert sections |
| 11 | CALLERS LIST: `SetRuleEnabled`, `SetFollowerMultiplier`, `SetAtmMode` covered with name preservation | PASS | Plan §C5, §C6, §C7 address each rebuild caller; §J provides full 6-site inventory with line numbers |
| 12 | DTOTORULEGAP: `DtoToRule` passes `dto.FollowerAccountNames` to `CopyRule.Create()` | PASS | Plan §C2 adds `dto.FollowerAccountNames` as the 8th arg to the `CopyRule.Create()` call at line 4289 |
| 13 | XUNIT ONLY: Tests specified as xUnit `[Fact]` (never NUnit/MSTest) | PASS | Plan §G states "Three xUnit [Fact] tests"; no NUnit/MSTest reference anywhere |
| 14 | FILES LISTED: Only `CopyEngine.cs` and `B127Tests.cs` listed as modified | PASS | Plan §K lists exactly two files; "Prohibited" subsection explicitly excludes all UI files and spec/protocol files |

---

## Violations

None.

---

## Reviewer Notes

The following observations do not block ticket generation but should inform the engineer and verifier:

1. **CYC count methodology**: The plan counts the `(names != null && i < names.Length)` ternary as 1 decision point. This is correct — a conditional expression is one branch. Verified against the code in §C10. CYC = 7 is accurate.

2. **`DeriveFollowerNames()` null-coalescing in ctor**: The constructor line `FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers)` means that all existing `AddRule` callers that pass fewer than 8 args will derive names from the already-resolved `Account[]`. This is safe because those callers only create rules where all accounts are immediately resolvable (not from deserialized DTO). The lazy-resolve benefit is only needed for the `DtoToRule` path, which passes the authoritative `dto.FollowerAccountNames` explicitly. This design is sound.

3. **Warning throttle decision**: Plan §F deliberately omits warning throttle for the lazy-fail case. This is acceptable given that `AllAccounts()` fires per trade-event (not per tick). If this generates noise in practice during a prolonged disconnect, a future block can add throttling — no action required now.

4. **`FindFollowerAccount` returns `null`**: Current code at line 4304 declares `private static Account? FindFollowerAccount(string name)` — the `?` nullable annotation is in place. The plan's C10 lazy-resolve path calls this and guards `if (resolved != null)`. This is JS-002 compliant.

5. **Test seam for `_resolvedFollowers`**: Plan §G Test 2 mentions verifying `_resolvedFollowers` dict "if accessible via test seam." The engineer should note that `_resolvedFollowers` is a `private` field on the `CopyEngine` class. If the test cannot access it directly, the verification can be inferred from the observable output (INFO message emitted and account yielded). This is a minor test-writing note, not a plan violation.

6. **No NT8 API concerns**: The plan uses only `NinjaTrader.Code.Output.Process` and `Account.All` — both valid `AddOnBase`-accessible NT8 APIs. No `AtmStrategyCreate` or `StrategyBase`-only APIs are referenced.

---

*Review complete. Status: REVIEW_PASS. Proceed to Phase 3: ptt-architect generates 04-tickets.md.*
