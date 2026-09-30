# B76-LaneA Ticket-2 Verification
**Status**: VERIFY_PASS
**Ticket**: TICKET-B76-2 -- PositionStateChanged dedup + leak fixes (tests only)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-18
**Engineer completion report**: docs/brain/B76-LaneA/ticket-2-completion.md (BUILD_PASS)

---

## Layer 3 Independent Verification

### Code Verification

#### CopyEngine.cs -- _lastHasPos field (lines 187-188)

| Claim | Location | Verified |
|-------|----------|----------|
| `private readonly ConcurrentDictionary<string, int[]> _lastHasPos` | line 187 | PASS |
| `= new ConcurrentDictionary<string, int[]>();` | line 188 | PASS |
| Comment: int[1] box design, Interlocked.Exchange sole writer, JS-021 | lines 181-186 | PASS |

#### CopyEngine.cs -- TryFirePositionState (lines 1418-1444)

| Claim | Location | Verified |
|-------|----------|----------|
| `private void TryFirePositionState(OrderEventArgs e)` -- private instance | line 1418 | PASS |
| `int newVal = hasPos ? 1 : 0;` | line 1437 | PASS |
| `var box = _lastHasPos.GetOrAdd(instr, _ => new int[] { 2 });` | line 1438 | PASS |
| `int prior = System.Threading.Interlocked.Exchange(ref box[0], newVal);` | line 1439 | PASS |
| `if (prior == newVal) return;` -- CAS dedup guard | line 1440 | PASS |

HOTFIX-B76-POSSTATE-DEDUP-01 verified in source. No `lock()` anywhere in method.

#### TradeCopierAddOn.cs -- DoInject stale panel cleanup (lines 373-388)

| Claim | Location | Verified |
|-------|----------|----------|
| `var stalePanel = old as TradeCopierPanel;` | line 379 | PASS |
| `if (stalePanel != null) stalePanel.Detach();` | lines 380-381 | PASS |
| Comment: HOTFIX-B76-POSSTATE-LEAK-01 | line 375 | PASS |

#### TradeCopierWindow.cs -- OnLoaded idempotency (lines 112-117)

| Claim | Verified |
|-------|----------|
| `-=` unsubscribe before `+=` subscribe for StatusUpdate, PositionStateChanged, CopyEnabledChanged | PASS |

**Architecture plan vs implementation note**: Architecture plan (line 61) described `_engine.Unsubscribe()`
as the POSSTATE-LEAK-02 fix. The actual implementation uses inline `-=` before `+=` (equivalent idempotent
re-subscribe pattern). `CopyEngine.Unsubscribe()` exists at line 814 but is not called from `OnLoaded`.
The `-=` before `+=` pattern achieves identical behavior: any N prior subscriptions collapse to exactly 1.
This is an acceptable behavior-equivalent divergence. TICKET-B76-2 tests do not cover `TradeCopierWindow`
and the core dedup fix (POSSTATE-DEDUP-01) is correctly tested. PASS with note.

### Test Verification (B76Tests.cs T_B76_07..T_B76_09)

| Test | Assertion | Ticket Spec Match | Code Present |
|------|-----------|-------------------|--------------|
| T_B76_07 | `_lastHasPos` field exists, type ConcurrentDictionary<string,int[]>, non-null on CopyEngine.Instance | YES | PASS |
| T_B76_08 | TryFirePositionState IL contains Interlocked.Exchange(ref int, int) call token | YES | PASS |
| T_B76_09 | TryFirePositionState private (NonPublic lookup succeeds, Public lookup returns null, IsStatic=false) | YES | PASS |

T_B76_07 correctly accesses `CopyEngine.Instance` (singleton pattern -- live NT8 context will supply).
T_B76_08 resolves `Interlocked.Exchange` overload `(ref int, int)` by exact signature -- correct.
T_B76_09 checks both `IsPrivate` and `!IsStatic` -- correct accessor/instance checks.

### 7-Scan Cross-Check (Layer 3)

All scans run across all 5 B76-modified files. Results consistent with ticket-2-completion.md:

| Scan | Result |
|------|--------|
| SCAN-01 lock() | 0 hits PASS |
| SCAN-02 async void | 0 hits PASS |
| SCAN-03 throw new Exception | 1 pre-existing (TradeCopierWindow.cs:638 ConvertBack) -- not B76 scope PASS |
| SCAN-04 return null (new scope) | 0 hits in B76Tests.cs PASS |
| SCAN-05 non-ASCII (new scope) | 0 hits in B76 diff areas PASS |
| SCAN-06 DateTime.Now | 0 hits PASS |
| SCAN-07 NUnit/MSTest | 0 hits PASS |

---

## Verdict

**VERIFY_PASS**

All claims in ticket-2-completion.md independently confirmed:
- HOTFIX-B76-POSSTATE-DEDUP-01 (_lastHasPos + Interlocked.Exchange CAS) present and correct.
- HOTFIX-B76-POSSTATE-LEAK-01 (stalePanel.Detach()) present and correct.
- HOTFIX-B76-POSSTATE-LEAK-02 (-= before += idempotency) present and correct (behavior-equivalent to plan).
- T_B76_07..T_B76_09 present, correct, and match ticket specification.
- 7 scans: zero new violations.
- JS-021 compliant: Interlocked.Exchange, no lock().
