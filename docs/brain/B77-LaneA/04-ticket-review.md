# B77-LaneA Ticket Review

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Epic**: B77-LaneA — ATM Template Name Fix Test Coverage
**Input**: docs/brain/B77-LaneA/04-tickets.md
**Plan**: docs/brain/B77-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Plan review**: docs/brain/B77-LaneA/02-plan-review.md (REVIEW_PASS confirmed 2026-08-10)
**Review date**: 2026-08-10

---

## Verdict: TICKET_REVIEW_PASS

---

## Ticket T1 — xUnit Tests for GetLeaderAtmTemplateName (B77 fallback-1 repair)

### 1. Traceability: PASS

Every test ID maps to a specific branch in the architecture plan §3 Test Matrix and the stated
spec requirement IDs (REPAIR-01, HOTFIX-B77-01). No phantom work. No missing plan items.

| Test ID | Branch mapped | Plan §3 row | Spec req |
|---------|--------------|-------------|----------|
| T_B77_TPL_01 | Branch 1 (null chart → string.Empty) | Row 1 | REPAIR-01 |
| T_B77_TPL_02 | Branch 2 (ct==null, no ChartTrader) | Row 2 | REPAIR-01 |
| T_B77_TPL_03 | Branches 3→4→5→fallback-1(sel==null)→fallback-2(null) | Row 3 | REPAIR-01 |
| T_B77_TPL_04 | Branch 6 — IL proof: SelectedAtmStrategy getter NOT called | Row 4 | HOTFIX-B77-01 |
| T_B77_TPL_05 | Branch 6 — null-safe ?? proof via IL scan + reflection proxy | Row 5 | HOTFIX-B77-01 |

### 2. All 5 Test IDs Present: PASS

T_B77_TPL_01 ✅  T_B77_TPL_02 ✅  T_B77_TPL_03 ✅  T_B77_TPL_04 ✅  T_B77_TPL_05 ✅

All 5 `[Fact]` attributes present (3 runnable, 2 skip skeletons). Summary table on ticket line 222
confirms count is exactly 5 with correct NT8-host and runnable classification.

### 3. Scenarios Unambiguous: PASS

All scenarios are specified with sufficient precision for a zero-ambiguity implementation:

- **T_B77_TPL_01**: Exact reflection boilerplate (BindingFlags + Invoke call), both assertions
  (`Assert.Equal(string.Empty, result)` and `Assert.NotNull(result)`) fully spelled out. CYC=1.
- **T_B77_TPL_02**: Skip skeleton with descriptive reason string; body is one-liner. No guessing
  required.
- **T_B77_TPL_03**: Skip skeleton; branch chain (3→4→5→fallback-1→fallback-2) is described in
  the scenario note. Sufficient for intent documentation.
- **T_B77_TPL_04**: Step-by-step IL scan with opcode literal (`0x6F`), token extraction arithmetic,
  helper invocation, and assertion message. Option A/B split contract is explicit: "T_B77_TPL_04
  must appear in method name(s)". Private helper `IlContainsCallvirt` is fully specified with
  signature and body.
- **T_B77_TPL_05**: Dual-assertion structure unambiguous — part 1 is the exact T_B77_TPL_01
  reflection boilerplate; part 2 is the `0x72` (ldstr) IL scan loop with `module.ResolveString`
  and `foundStringEmpty` flag — every line of the loop body is provided.

### 4. NT8 Constraints: PASS

- **T_B77_TPL_02**: `[Fact(Skip="NT8-HOST-REQUIRED: FindVisualChild<ChartTrader>(currentChart)
  requires live WPF visual tree")]` — correctly marked. ✅
- **T_B77_TPL_03**: `[Fact(Skip="NT8-HOST-REQUIRED: requires live ChartTrader with
  AtmStrategy.Name==\"AtmStrategy\" and no AtmStrategySelector in visual tree")]` — correctly
  marked. ✅
- **T_B77_TPL_01, T_B77_TPL_04, T_B77_TPL_05**: Explicitly stated "NT8 host: NOT required". ✅
- `typeof(AtmStrategySelector)` in T_B77_TPL_04 is reflection-only (MetadataToken lookup) —
  no live NT8 visual tree required. ✅
- No `async/await` in lifecycle methods, no `Account.All` outside Loaded handler, no `sealed` on
  a window class, no `DateTime.Now`, no hardcoded hex color, no `CreateOrder` usage — all
  correctly absent from a pure test file. ✅

### 5. JS-DNA Pre-Check: PASS

7-scan checklist (ticket lines 238–244) is present, file-scoped to the exact new file, and covers
all 7 required scans:

| Scan | Rule | Ticket entry | Status |
|------|------|-------------|--------|
| SCAN-01 | JS-021 `lock(` | grep `lock(` → 0 matches | ✅ |
| SCAN-02 | JS-001 `throw new` | grep `throw new` → 0 matches | ✅ |
| SCAN-03 | JS-002 `return null` | grep `return null` → 0 matches | ✅ |
| SCAN-04 | JS-033 `async void` | grep `async void` → 0 matches | ✅ |
| SCAN-05 | ASCII-only | grep `[^\x00-\x7F]` → 0 matches | ✅ |
| SCAN-06 | CYC <= 8 | All methods and helper stated ≤ CYC 3 | ✅ |
| SCAN-07 | Build | `dotnet build PropTraderTools.csproj` → 0 errors | ✅ |

No pre-code violations detectable from ticket descriptions:
- No `lock()` described anywhere in the test implementation. ✅
- No `throw new XxxException` in hot path — xUnit `Assert.*` is test-framework exception, not
  application hot-path. ✅
- No `return null` — `IlContainsCallvirt` returns `bool`; all test methods return `void`. ✅
- No `Dictionary<K,V>` for shared mutable state. ✅
- No `async void` non-event-handler. ✅
- No mutable fields on structs. ✅
- No `SolidColorBrush` without `.Freeze()`. ✅

### 6. CYC Pre-Check: PASS

All methods are within CYC ≤ 8 (Jane Street strict threshold):

| Method | CYC stated | Within threshold? |
|--------|-----------|-------------------|
| T_B77_TPL_01 | 1 | ✅ |
| T_B77_TPL_02 | 1 (skeleton) | ✅ |
| T_B77_TPL_03 | 1 (skeleton) | ✅ |
| T_B77_TPL_04 | 2..3 | ✅ |
| T_B77_TPL_05 | 3 | ✅ |
| `IlContainsCallvirt` helper | 3 (loop + outer-if + inner-if) | ✅ |

No method approaches CYC 8. No split required.

### 7. Completeness: PASS

All engineer-contract elements are present:

- **File path**: `src/PropTraderTools/TradeCopierPanelB77Tests.cs` — exact. ✅
- **Namespace**: `PropTraderTools` — stated. ✅
- **Class declaration**: `public sealed class TradeCopierPanelB77Tests` — stated. ✅
- **Using directives**: `System`, `System.Reflection`, `Xunit` — all listed. ✅
- **Project**: `src/PropTraderTools/PropTraderTools.csproj` — stated. ✅
- **All 5 method signatures**: provided verbatim with `[Fact]` or `[Fact(Skip=...)]` attributes. ✅
- **Private helper**: `IlContainsCallvirt(byte[] il, int targetToken)` — signature and full body
  provided. ✅
- **Spec requirement IDs**: REPAIR-01 and HOTFIX-B77-01 — both cited. ✅
- **Acceptance criteria**: listed (5 items, including build and scan gate). ✅

### 8. Scope: PASS

Ticket explicitly and repeatedly states `TradeCopierPanel.cs` will NOT be modified:

- Line 14: `**File to modify**: NONE (TradeCopierPanel.cs repair already applied in commit ff5944ee)`
- Line 29: `Do NOT modify TradeCopierPanel.cs.`
- Acceptance criteria last bullet: `No modifications to TradeCopierPanel.cs or any other .cs file outside the new test file`

No other `.cs` file is named as a modification target. Scope boundary is tight and unambiguous. ✅

### File Routing: PASS

- New file path `src/PropTraderTools/TradeCopierPanelB77Tests.cs` resolves to
  `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` — Wave workspace. ✅
- No Director workspace path (`universal-or-strategy-director`) referenced for any `.cs` file. ✅

---

## Notes

**NOTE (non-blocking)**: T_B77_TPL_05 re-exercises the branch-1 null-chart path via reflection
invoke (same invocation as T_B77_TPL_01), then adds the IL scan for `string.Empty` literal as
the branch-6 `??` proof. The engineer should keep these two assertion blocks clearly commented
within the single method body — the ticket description does so explicitly. No action needed;
this is informational.

**NOTE (non-blocking)**: T_B77_TPL_04 Option A/Option B flexibility is explicitly permitted by
the ticket. Either implementation satisfies the contract provided `T_B77_TPL_04` appears in the
method name(s). The verifier (Phase 4b) should accept either form.

**NOTE (non-blocking)**: The plan review (02-plan-review.md) previously noted that
`TradeCopierPanelB77Tests` uses `sealed` while `B75Tests` is non-sealed. The ticket preserves
the `sealed` choice consistent with the B76 pattern and the plan reviewer's PASS. Confirmed
acceptable.

---

## Violation Log

**No violations found.** Zero JS-XXX rule citations required. All 8 checks PASS.

---

RESULT: TICKET_REVIEW_PASS
