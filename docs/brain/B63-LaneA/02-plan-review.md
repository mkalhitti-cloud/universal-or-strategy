# B63-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-11
**Plan file**: docs/brain/B63-LaneA/02-architecture-plan.md

---

## Checklist Results

**R1 — NT8 Evidence**: PASS
- `NT8_FULL_REFERENCE.md` line 941 confirmed: `* OrderState.Accepted` — exact text cited in plan Section B.
- `NT8_FULL_REFERENCE.md` line 1005 confirmed: `"some stop orders may only reach 'Accepted' state if they are simulated/held on a broker's server"` — exact quote cited in plan Section B and the inline comment in the proposed code (Section C).
- NT8 lifecycle `Submitted -> Accepted -> (optionally) Working` is consistent with the reference file content.

**R2 — Exact Change Scope**: PASS
- `IsWorkingBracket` confirmed at file lines 811–813 (plan claims 811–813). ✓
- Gate B callsite at `OnOrderUpdate` confirmed at line 651. ✓
- `MirrorOrderUpdate` callsite confirmed at line 682. ✓
- Plan Section H explicitly states "No other lines touched" for `OnOrderUpdate`, `HandleBracketChange`, `SyncFollowerBracket`, `MirrorOrderUpdate`, `FindFollowerBracketOrder`, `IsBracketLegStatic`. ✓
- Two callsites only — both identified. ✓

**R3 — Safety Analysis**: PASS
- All 4 safety points present: entry orders (Point 1), follower orders (Point 2), double-fire (Point 3), fresh bracket (Point 4). ✓
- `SyncFollowerBracket` price-delta guard (`Math.Abs(newPrice - currentPrice) < tickSize`) confirmed at line 850 (plan cites "line 850"). ✓
- `FindFollowerBracketOrder` null-return path (`if (fo == null) return;`) confirmed at line 846 (plan cites "line 846"). ✓

**R4 — CYC Claim**: OBSERVATION (non-blocking)
- Plan claims CYC=1 for `return (A || B) && C`.
- Actual McCabe CYC: baseline=1, `||`=+1, `&&`=+1 → CYC=3.
- CYC=3 is well within the ≤8 hard limit. No FAIL triggered.
- See Non-Blocking Observations below.

**R5 — Test Plan**: PASS
- All 4 `[Fact]` tests specified: T_B63_01, T_B63_02, T_B63_03, T_B63_04. ✓
- DW-B63-01 (sealed `Order` stub strategy) documented and flagged P1 deferred. ✓
- `internal static` visibility change called out, with `IsExitSignalName` (line 729) cited as prior precedent. ✓
- xUnit-only framework declared (`Assert.True` / `Assert.False`; no NUnit, no MSTest). ✓

**R6 — JS Compliance**: PASS
- **JS-021** (`lock()` ban): `IsWorkingBracket` is a static pure predicate with no shared mutable state. No `lock()` possible. ✓
- **JS-001** (no `throw` in hot path): Method returns `bool`. No exception path. ✓
- **JS-002** (no `return null`): Method returns `bool`. `null` is not returnable. ✓
- **CYC ≤ 8**: Actual CYC=3 (see R4). Within limit. ✓
- **ASCII-only**: No new string literals introduced. Inline comments contain ASCII-only characters. ✓
- **xUnit only**: `[Fact]` attributes, `Assert.True`/`Assert.False`. No `[Test]` or `[TestMethod]`. ✓
- **No `DateTime.Now`**: No temporal references introduced. ✓
- **No FontFamily / hex**: No UI layer touched. ✓
- **No Dispatcher**: Static predicate; no UI thread context. ✓

**R7 — Deferred Items**: PASS
- Plan Section I carries forward all 9 open items from `B59-LaneA/06-deferred-backlog.md`:
  DW-B60-01, DW-B59-02, DW-B58-01, DW-B58-02, DW-B58-03, DW-B54-01,
  PRE-EXISTING-01, PRE-EXISTING-02, PRE-EXISTING-03. ✓
- All items retain their original Priority and Target Block values. ✓
- DW-B63-01 (NT8 `Order` sealed type; xUnit stub strategy) added as new P1 deferred item with resolution options. ✓

**R8 — No Scope Creep**: PASS
- Changed files: `src/PropTraderTools/CopyEngine.cs` (3 lines: comment, access modifier, condition)
  and `tests/PropTraderTools.Tests/CopyEngineTests.cs` (new file, ~50 lines).
- No other methods, files, or concerns added.
- Estimated diff surface (~60 lines total) well within the 10,000-character PR hygiene limit. ✓

---

## Violations (REVIEW_FAIL items only — cite rule ID + exact location)

None.

---

## Non-Blocking Observations

**OBS-01 — CYC claim in Section C/F is CYC=1; correct value is CYC=3**

Location: Section C (proposed code comment `// CYC=1`), Section F compliance table (`IsWorkingBracket CYC = 1`), Section G SCAN-05 (`Expected: CYC = 1`).

The proposed expression `return (A || B) && C` contains two logical operators. Standard McCabe cyclomatic complexity counts each boolean short-circuit operator (`||`, `&&`) as +1 decision point. Baseline CYC=1, plus one `||`, plus one `&&` = **CYC=3**.

**Impact**: None on correctness. CYC=3 satisfies the ≤8 hard limit with 5 units of headroom. The method remains trivially simple.

**Action for engineer**: Update the inline comment from `// CYC=1` to `// CYC=3` in the final implementation and update Section F/G expected value from `1` to `3` in the ticket. This is cosmetic accuracy only and does not alter the fix.

---

## Result

**REVIEW_PASS**
