# Ticket 3 Verification -- OnOrderUpdate (Advisory)

## Scope: TICKET 3 ONLY

**Epic**: WAVE1-LANE-A
**Ticket**: T3 (A-09)
**Target method**: `OnOrderUpdate` in `src/PropTraderTools/CopyEngine.cs`
**Verifier**: ptt-verifier (independent)
**Date**: 2026-09-07
**Verdict**: VERIFY_PASS

---

## Advisory Tier Note

`OnOrderUpdate` had CCN=8 before this extraction -- fully compliant with JS-080.
This extraction was executed per the SCOPE LOCK directive.
The extraction is an advisory improvement (CCN 8->5), not a compliance requirement.

---

## Independent CCN Scan

Verifier ran: `lizard src/PropTraderTools/CopyEngine.cs --csv`

| Method | Engineer Reported | Verifier Measured | Match? |
|--------|-------------------|-------------------|--------|
| `TryResolveEnabledRule` | 5 | **5** | YES |
| `OnOrderUpdate` | 5 | **5** | YES |

lizard output:
- `TrimSignal::TryResolveEnabledRule@1503-1523` -- CCN=5 (lines 1503-1523)
- `TrimSignal::OnOrderUpdate@1526-1614` -- CCN=5 (lines 1526-1614)

Note: Engineer comment on line 1502 states "CCN=4" but lizard measured 5.
This is an off-by-one comment error (base=1 counted differently), not a code issue.
Both methods are well within the JS-080 CCN <= 8 limit. No violation.

---

## Independent 7-Scan Results

| Scan | Check | Engineer Reported | Verifier Measured | Match? | Result |
|------|-------|-------------------|-------------------|--------|--------|
| SCAN-01 | `lock(` (non-comment) in file | 0 hits | **0 hits** | YES | PASS |
| SCAN-02 | `async void` (non-comment) in file | 0 hits | **0 hits** | YES | PASS |
| SCAN-03 | `return null;` in TryResolveEnabledRule/OnOrderUpdate | 0 hits | **0 hits** | YES | PASS |
| SCAN-04 | CCN <= 8 for affected methods | both=5 | **both=5** | YES | PASS |
| SCAN-05 | `CreateOrder` in TryResolveEnabledRule/OnOrderUpdate | N/A / 0 hits | **0 hits** | YES | PASS |
| SCAN-06 | Non-ASCII chars in TryResolveEnabledRule lines | 0 hits | **0 hits** | YES | PASS |
| SCAN-07 | `public.*TryResolveEnabledRule` visibility leak | 0 hits | **0 hits** | YES | PASS |

All 7 scans: verifier results match engineer self-report. No discrepancies.

---

## Implementation Correctness

| Check | Result |
|-------|--------|
| `TryResolveEnabledRule` declared `private bool` | PASS (line 1503) |
| Parameter signature: `(Order order, out CopyRule rule)` | PASS (line 1503) |
| Gate 1 is `if (!_isCopyEnabled)` FIRST | PASS (line 1505) |
| Gate 2 is `matchedRule == null` SECOND | PASS (line 1511) |
| Gate 3 is `if (!matchedRule.Value.Enabled)` THIRD | PASS (line 1516) |
| `rule = default; return false;` on all three early exits | PASS (lines 1507-1508, 1513-1514, 1518-1519) |
| `rule = matchedRule.Value; return true;` happy path | PASS (lines 1521-1522) |
| `OnOrderUpdate` calls `TryResolveEnabledRule` at gate position | PASS (line 1579) |
| `TryCancelFollowerEntries` call unchanged in `OnOrderUpdate` | PASS (line 1590) |
| `TryDispatchLeaderFlat` call unchanged in `OnOrderUpdate` | PASS (lines 1594-1605) |
| `TryHandleDrag` call unchanged in `OnOrderUpdate` | PASS (line 1609) |
| `DispatchCopy` call unchanged in `OnOrderUpdate` | PASS (line 1613) |

---

## Gate Ordering Verified

**YES** -- gate order is correctness-critical and correctly preserved:
1. `!_isCopyEnabled` (line 1505) -- enabled check must come first
2. `matchedRule == null` (line 1511) -- null check must come before `.Value` dereference
3. `!matchedRule.Value.Enabled` (line 1516) -- requires non-null guarantee from Gate 2

---

## matchedRule.Value Cleanup Verified

**YES** -- no `.Value` references remain inside `OnOrderUpdate` body (lines 1526-1614).

Full file scan for `matchedRule.Value`:
- Line 1516: inside `TryResolveEnabledRule` -- correct (local `CopyRule?` nullable dereference)
- Line 1521: inside `TryResolveEnabledRule` -- correct (assignment to out param)
- Line 4294: in `TryReplaceOnAtmCancel` (unrelated method, outside T3 scope)
- Line 4305: in `TryReplaceOnAtmCancel` (unrelated method, outside T3 scope)

`OnOrderUpdate` (1526-1614) uses `matchedRule` as plain `CopyRule` (unwrapped by `out`). Clean.

---

## Test Verification

**166 passing / 0 failing / 3 skipped (Total: 169)**

Test run command: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`

4 new [Fact] tests confirmed present in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`:

| Test | Gate Tested | Present? |
|------|-------------|----------|
| `TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled` | Gate 1: `!_isCopyEnabled` | YES (line 831) |
| `TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule` | Gate 2: `matchedRule == null` | YES (line 841) |
| `TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled` | Gate 3: `!rule.Enabled` | YES (line 854) |
| `TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass` | All 3 gates pass | YES (line 866) |

All 4 T3 tests pass. Test count increased from 162 to 166 (net +4). Zero failures.

---

## Discrepancies Between Engineer Report and Verifier Scans

| Item | Discrepancy | Impact |
|------|-------------|--------|
| CCN comment on line 1502 says "CCN=4" | lizard measures CCN=5 | Comment only, no code impact |

One comment discrepancy found: the inline comment `// CCN=4: base(1) + enabled(1) + matchedRule null(1) + matchedRule.Enabled(1)` on line 1502 undercounts by 1. lizard reports CCN=5 (base=1 + 3 decision points + 1 `return false` path). This is a documentation error only -- the actual measured CCN=5 is within JS-080 threshold. No code change required.

---

## VERIFY_PASS