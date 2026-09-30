# PTT-COPIER-B2 — Ticket 2 Verification

**Ticket:** T2 — TradeCopierWindow.cs  
**Verifier:** Orchestrator (direct scan + read)  
**Date:** 2026-07-06  
**Verdict:** VERIFY_PASS

---

## Scan Results

| Scan | Pattern | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `lock\s*\(` | 0 | ✅ PASS |
| SCAN-02 | Non-ASCII chars | 0 | ✅ PASS |
| SCAN-03 | `FontFamily` | 0 | ✅ PASS |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | 0 | ✅ PASS |
| SCAN-05 | CreateOrder → no calls in Window | N/A | ✅ PASS |
| SCAN-06 | `DateTime\.Now[^U]` | 0 | ✅ PASS |
| SCAN-07 | `lock\s*\(` | 0 | ✅ PASS |
| SCAN-B2-01 | `Subscribe()` count | 2 (OnInitialize + OnDestroyed) | ✅ PASS |
| SCAN-B2-06 | `AddRule` count | 5 occurrences | ✅ PASS |
| SCAN-B2-08 | `"BorderBrush"` unqualified | 0 | ✅ PASS |
| SCAN-B2-09 | bare `catch {` | 0 | ✅ PASS |

## Specific Condition Checks

| # | Condition | Status |
|---|-----------|--------|
| 1 | `_engine.Subscribe()` in OnInitialize after StatusUpdate subscription | ✅ PASS |
| 2 | `_engine.Unsubscribe()` in OnDestroyed after StatusUpdate unsubscription | ✅ PASS |
| 3 | sep1 uses `"NTBrushes.BorderBrush"` | ✅ PASS |
| 4 | sep2 uses `"NTBrushes.BorderBrush"` | ✅ PASS |
| 5 | `catch (Exception) {` instead of bare `catch {` | ✅ PASS |
| 6 | `followerCb.ItemsSource = Account.All` added to BuildRuleRow | ✅ PASS |
| 7 | Apply button added to BuildRuleRow (column 7) | ✅ PASS |
| 8 | `OnRowApply` method added with `_engine.AddRule(...)` call | ✅ PASS |
| 9 | No lock() added | ✅ PASS |
| 10 | No Subscribe() or Unsubscribe() added to TradeCopierPanel | N/A (different file) | ✅ PASS |

## Summary

All 4 DEFECT-1/2B/4/5 fixes applied correctly. Subscribe/Unsubscribe lifecycle owned exclusively by TradeCopierWindow. Both border separators use NT-qualified resource key. Bare catch replaced with typed. Rule wiring wired through OnRowApply → AddRule string overload.

**VERIFY_PASS**
