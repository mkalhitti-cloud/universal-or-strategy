# PTT-COPIER-B3 Ticket T1 Verification

**Verifier:** PTT Verifier
**Date:** 2026-06-23

---

## 7-Scan Results

| Scan | Pattern | Expected | Actual | Result |
|------|---------|----------|--------|--------|
| SCAN-01 | `lock(` in CopyEngine.cs | 0 | 0 | PASS |
| SCAN-02 | `DateTime.Now[^U]` in CopyEngine.cs | 0 | 0 | PASS |
| SCAN-03 | `new CopyEngine` in CopyEngine.cs | 1 | 1 (line 15) | PASS |
| SCAN-06 | `#[0-9A-Fa-f]{6}` in CopyEngine.cs | 0 | 0 | PASS |
| SCAN-07 | `FontFamily` in CopyEngine.cs | 0 | 0 | PASS |
| SCAN-11 | `readonly ConcurrentBag` in CopyEngine.cs | 0 | 0 | PASS |
| SCAN-12 | `_rules = new ConcurrentBag` in CopyEngine.cs | 2 | 2 (lines 21, 103) | PASS |

---

## T1 Check Results (V01–V22)

| ID | Check | Result | Note |
|----|-------|--------|------|
| V01 | `readonly` removed from `_rules` | PASS | Line 21: `private ConcurrentBag<CopyRule> _rules = ...` — no `readonly` |
| V02 | `_rules` field type is still `ConcurrentBag<CopyRule>` | PASS | Line 21: type unchanged |
| V03 | `CopyRule.Enabled` field present as `internal readonly bool Enabled` | PASS | Line 34 |
| V04 | `CopyRule` constructor accepts `bool enabled` parameter (4 params) | PASS | Line 36: `(string instrument, Account master, Account[] followers, bool enabled)` |
| V05 | `CopyRule` constructor assigns `Enabled = enabled;` | PASS | Line 41 |
| V06 | `Create` factory has `bool enabled = true` default | PASS | Line 44: `bool enabled = true` |
| V07 | Gate 2.5 inserted after `if (matchedRule == null) return;` | PASS | Lines 154–155: comment + `if (!matchedRule.Value.Enabled) return;` |
| V08 | Gate 2.5 placed BEFORE Gate 3 (state check) | PASS | Gate 2.5 at lines 154–155; Gate 3 begins at line 158 |
| V09 | `_dailyCapFloor` field present, default = `-500.0` | PASS | Line 22: `private double _dailyCapFloor = -500.0;` |
| V10 | `_dailyCapFloor` placed after `_rules` field | PASS | `_rules` line 21, `_dailyCapFloor` line 22 |
| V11 | `SetDailyCapFloor(double floor)` method present | PASS | Line 97 |
| V12 | `SetDailyCapFloor` placed after `SetEnabled` | PASS | `SetEnabled` lines 90–94; `SetDailyCapFloor` line 97 |
| V13 | `SetRuleEnabled(string instrument, bool enabled)` method present | PASS | Lines 100–111 |
| V14 | `SetRuleEnabled` uses `new List<CopyRule>(_rules)` snapshot | PASS | Line 102: `var snapshot = new List<CopyRule>(_rules);` |
| V15 | `SetRuleEnabled` rebuilds into `new ConcurrentBag<CopyRule>()` | PASS | Line 103: `_rules = new ConcurrentBag<CopyRule>();` |
| V16 | `SetRuleEnabled` assigns `_rules = newBag` (or equivalent) | PASS | Line 103: direct field assignment `_rules = new ConcurrentBag<CopyRule>();` — semantically equivalent to arch plan spec (deviation acknowledged in ticket-1-completion.md) |
| V17 | `SetRuleEnabled` contains zero `lock(` | PASS | SCAN-01 = 0 across entire file |
| V18 | `PassesDailyCapCheck` uses `acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar)` | PASS | Line 363 |
| V19 | `PassesDailyCapCheck` returns `true` when `pnl == double.MinValue` | PASS | Line 364: `if (pnl == double.MinValue) return true;` |
| V20 | `PassesDailyCapCheck` returns `pnl > _dailyCapFloor` | PASS | Line 365: `return pnl > _dailyCapFloor;` |
| V21 | `FindRule` first statement is `if (instrument == null) return null;` | PASS | Line 351 — first statement of method |
| V22 | `CopyRule` struct still uses `private readonly struct` modifier | PASS | Line 29: `private readonly struct CopyRule` |

---

## Summary

- Total checks: 22
- Passed: 22
- Failed: 0

---

## Decision

VERIFY_PASS
