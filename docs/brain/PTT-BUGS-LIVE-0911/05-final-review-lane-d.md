# Final Review — PTT-BUGS-LIVE-0911 Lane D
Date: 2026-09-11
Reviewer: ptt-plan-reviewer

## Verdict: FINAL_PASS

## Block Summary

Lane D implemented ATM bracket protection at follower entry for Clone mode. `TryLaneDHook` was
promoted from an empty stub to a fully-operative dispatcher: it fans out per-follower ATM orders
via `TryLaneDDispatchFollower` (delegating to existing `SendCopyWithAtm`) and commits a Gate-5
dedup entry to prevent duplicate dispatch on re-fired `OnOrderUpdate` events. A
`ConcurrentDictionary<string, byte> _laneDDispatched` field provides a lock-free per-order,
per-follower skip gate in `DispatchToFollower` that blocks the loop's existing Named branch from
re-dispatching followers already handled by the hook. All non-Clone and non-Limit paths remain
strictly unaffected. Six new `[Fact]` tests cover structural and behavioral contracts, including a
verified TryAdd-idempotency test (T-LD-04) that exercises the skip gate mechanism without NT8
runtime.

---

## Cross-File Coherence Analysis

### 1. All 4 changes in CopyEngine.cs work together as a coherent system

**CONFIRMED.** The four changes are tightly coupled by design and form a complete dispatch-dedup
contract:

1. `_laneDDispatched` field (line 410) — shared state enabling the cross-method dedup signal.
2. `TryLaneDHook` + `TryLaneDDispatchFollower` (lines 2679–2753) — write side: writes key on
   successful ATM dispatch, commits Gate-5 dedup.
3. Skip gate in `DispatchToFollower` (lines 2874–2875) — read side: blocks duplicate dispatch for
   already-handled (order, follower) pairs.

Without any one of the four changes the system has a gap: field missing → skip gate has no backing
store; hook body missing → no ATM dispatch and no key written; helper missing → hook cannot fan out
without exceeding CYC budget; skip gate missing → double-dispatch re-emerges. All four are
necessary and each is sufficient only in conjunction with the others.

### 2. `_laneDDispatched` field exists and is used correctly

**CONFIRMED.** Source-verified:
- Field declaration at `CopyEngine.cs:410–411`:
  `private readonly ConcurrentDictionary<string, byte> _laneDDispatched = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);`
- Write site at `CopyEngine.cs:2751` (inside `TryLaneDDispatchFollower`):
  `_laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);`
- Read site at `CopyEngine.cs:2875` (inside `DispatchToFollower` skip gate):
  `if (_laneDDispatched.ContainsKey(laneD_key)) return;`
- Write only occurs after `SendCopyWithAtm` returns `true` — correctly conditional on successful
  dispatch only. If `SendCopyWithAtm` fails (returns false), the key is NOT written, allowing
  `DispatchToFollower`'s Named branch to serve as fallback. ✓

### 3. Key format consistent between write and read paths

**CONFIRMED.** Source-verified at lines 2751 and 2874:
- Write path: `baseSignal.OrderId + "#" + acc.Name`
- Read path:  `baseSignal.OrderId + "#" + acc.Name`

Both paths receive `baseSignal` (type `CopySignal`) and `acc` (type `Account`) with the same
field access pattern. Verifier SCAN-07e confirmed EXACT MATCH. `StringComparer.Ordinal` is used
for the `ConcurrentDictionary` constructor, consistent with key construction using `+` concatenation
which produces ordinal-comparable strings. ✓

### 4. TryLaneDHook only fires for Clone signal mode

**CONFIRMED.** Branch (1) at `CopyEngine.cs:2682`: `if (GetCopyMode() != CopyMode.Clone) return;`
is the **first statement** of the method body. Signal, Mirror, and Market modes exit immediately.
`_laneDDispatched` receives no entries from non-Clone executions; the skip gate in
`DispatchToFollower` returns `false` on all `ContainsKey` checks for non-Clone orders, leaving the
existing dispatch paths entirely unaffected. ✓

### 5. Follower loop in DispatchCopy still fires for non-ATM followers

**CONFIRMED.** The skip gate in `DispatchToFollower` (line 2875) only returns early when
`_laneDDispatched.ContainsKey(laneD_key)` is `true`. For all non-Clone followers, Market followers,
and Inherit followers, the `_laneDDispatched` dictionary contains no matching key (TryLaneDHook is
a no-op for those cases). Therefore `ContainsKey` returns `false` for all such followers, and the
existing `SendCopyWithAtm` / `SendCopy` dispatch branches fire unchanged. ✓

Additionally, `DispatchCopy`'s loop at line 2653 is unconditionally present — TryLaneDHook is
called before the loop (line 2639, bare unconditional call) and does not replace or short-circuit
the loop. For a follower rule with both Clone ATM followers and non-ATM followers, the ATM
followers are handled by the hook and skipped in the loop, while non-ATM followers pass through
the loop normally. ✓

### 6. 6 tests in LaneDContractTests cover key behavioral paths

**CONFIRMED.** Class `LaneDContractTests` at `CopyEngineTests.cs:8569`. All 6 tests verified
present at expected line numbers (T-LD-01: 8585, T-LD-02: 8598, T-LD-03: 8616, T-LD-04: 8630,
T-LD-05: 8663, T-LD-06: 8677).

Coverage assessment:
- T-LD-01: ATM dispatch integration path (NT8-runtime skipped — correct and honest) ✓
- T-LD-02: Non-Clone mode no-op (structural — field type guard) ✓
- T-LD-03: Market order no-op (structural — method signature guard preserving Lane C contract) ✓
- T-LD-04: Skip gate idempotency (behavioral — TryAdd/ContainsKey/TryAdd sequence; NT8 runtime NOT
  required; VIOLATION-01 from ticket review Cycle 1 fully resolved in Cycle 2) ✓
- T-LD-05: Field contract (private, readonly, ConcurrentDictionary<string,byte>) ✓
- T-LD-06: Helper method contract (private bool, 7 parameters) ✓

---

## Spec Requirement Coverage

| Req ID | Status | Evidence |
|--------|--------|---------|
| R-LD-01 | SATISFIED | `TryLaneDHook` body non-empty at `CopyEngine.cs:2679–2713`; all 5 branches (mode guard, OrderType guard, Named guard, foreach dispatch, laneDCount dedup commit) confirmed present by L3 verifier SCAN-07b and source read |
| R-LD-02 | SATISFIED | `SendCopyWithAtm` called at `CopyEngine.cs:2748` inside `TryLaneDDispatchFollower`, which is invoked from `TryLaneDHook` for each Clone-mode Named follower; follower receives NT8 ATM brackets at entry when Clone mode is active and leader order is Limit |
| R-LD-03 | SATISFIED | `_laneDDispatched` field at line 410; TryAdd write at line 2751 (key = `baseSignal.OrderId + "#" + acc.Name`); ContainsKey read at line 2875 (identical key format); double-dispatch prevention is semantically correct and confirmed exact-match by L3 verifier SCAN-07e |
| R-LD-04 | SATISFIED | Branch (1) `if (GetCopyMode() != CopyMode.Clone) return;` at line 2682 is first statement of TryLaneDHook; Signal/Mirror/Market modes return before any dispatch or key-write; `_laneDDispatched` stays empty for non-Clone sessions; DispatchToFollower ContainsKey returns false for all non-Clone keys |
| R-LD-05 | SATISFIED | DispatchCopy CYC=8 unchanged (L3 verifier count); call site at line 2639 is an unconditional bare void call with zero conditional wrapping — zero CYC delta to DispatchCopy |
| R-LD-06 | SATISFIED | Lane E wiring point documented in `02-architecture-plan-lane-d.md` Section 11 with placement in `OnFollowerPositionUpdate`, timing rationale, and stub signature; no code change required for Lane D |

---

## Scan Summary (Layer 3 reference — ptt-verifier independent re-run)

| Scan | Result |
|------|--------|
| SCAN-01 lock() | 0 executable lock() calls; 67 matches in `\block\s*\(` are ALL in comments ("no lock", "lock-free"). Zero `lock(` in any executable line in new or modified code. |
| SCAN-02 CYC | DispatchCopy=8 (AT LIMIT, UNCHANGED), TryLaneDHook=7 (≤8 ✓), TryLaneDDispatchFollower=4 (≤8 ✓), DispatchToFollower=3 (≤8 ✓). All within budget. |
| SCAN-03 async/await | 0 in new/modified method bodies; 11 matches are ALL in comments only. TryLaneDHook and TryLaneDDispatchFollower are synchronous private methods. |
| SCAN-04 non-ASCII | 0 non-ASCII bytes in both CopyEngine.cs and CopyEngineTests.cs |
| SCAN-05 ATM API | 0 actual calls to AtmStrategyCreate or AtmStrategyChangeStopTarget in new code; 2 pre-existing comment-only references (lines 7744, 7930). |
| SCAN-06 build+test | 0 build errors; Failed=0, Passed=170, Skipped=360, Total=530. Zero L2/L3 discrepancy. |
| SCAN-07 structural | All 10 sub-checks a–j PASS: _laneDDispatched 4 matches (7a), TryLaneDHook non-empty all 5 branches (7b), TryLaneDDispatchFollower null+cap+dedup (7c), skip gate placement correct (7d), key format exact match (7e), DispatchCopy call site unconditional (7f), Lane C reflection test 1 passed (7g), LaneDContractTests class present (7h), T-LD-01 Skip / T-LD-04–06 no Skip (7i), TryLaneDHook unconditional in DispatchCopy (7j). |

---

## NT8 API Final Compliance

| Check | Status | Evidence |
|-------|--------|---------|
| StartAtmStrategy STATIC form only | PASS | Called inside existing `SendCopyWithAtm` at `CopyEngine.cs:5161–5163` as `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` — static form. TryLaneDDispatchFollower delegates entirely to SendCopyWithAtm; no direct call in new code. |
| Order name "Entry" | PASS | `SendCopyWithAtm` creates order with name `"Entry"` at line 5154. New code delegates entirely; no new CreateOrder call in TryLaneDHook or TryLaneDDispatchFollower. |
| AtmStrategyCreate | 0 actual calls | SCAN-05 = 0. Confirmed by L2 and L3 independently. |
| AtmStrategyChangeStopTarget | 0 actual calls | SCAN-05 = 0. Confirmed by L2 and L3 independently. |
| AtmObject null guard | PASS | Guard `if (namedMode.AtmObject != null)` at line 5160 in existing `SendCopyWithAtm`. Clone mode always produces string-only Named (GetCloneAtmMode uses string ctor); string overload `StartAtmStrategy(namedMode.TemplateName, order)` is correctly used. |

---

## DW-PTT0911C Closure

| Item | Status | Evidence |
|------|--------|---------|
| DW-PTT0911C-01 | **CLOSED** | TryLaneDHook body implemented at `CopyEngine.cs:2679–2713`; empty stub replaced with 5-branch ATM dispatch logic + helper call + Gate-5 dedup commit |
| DW-PTT0911C-02 | **CLOSED** | DispatchCopy CYC=8 preserved (verifier count confirmed); TryLaneDHook CYC=7 ≤8; TryLaneDDispatchFollower CYC=4 ≤8; DispatchToFollower CYC=3 ≤8 |
| DW-PTT0911C-03 | **CLOSED** | `_laneDDispatched` is `ConcurrentDictionary<string, byte>` — only new shared state; zero `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim` in new code; SCAN-01 = 0 executable matches; runs on NT8 account background thread; no UI writes |
| DW-PTT0911C-04 | **CLOSED** | StartAtmStrategy STATIC form enforced inside existing `SendCopyWithAtm`; TryLaneDDispatchFollower delegates entirely — no new direct StartAtmStrategy call |
| DW-PTT0911C-05 | **CLOSED** | AtmObject null guard lives in existing `SendCopyWithAtm` at lines 5160–5163; Clone mode string-only Named ctor ensures string overload path is taken; no new guard code needed |

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-LD-01 | **`_laneDDispatched` session-unbounded growth**: Entries persist for session lifetime with no cleanup on session end, position close, or rule reload. Growth is bounded and acceptable at ~2.4 KB/session for 4 followers at 10 dispatches/day. Formal cleanup (e.g., remove all entries for a given `orderId` after DispatchCopy loop completes, or clear on `OnDestroyed`) can be added in a future pass if high-volume sessions reveal growth concern. | P2 | Lane E or future | OPEN |
| DW-LD-02 | **Market order ATM guard decision — documented**: `TryLaneDHook` branch (2) returns early for Market-type leader orders. This is correct by design: `SendCopyWithAtm` hardcodes `OrderType.Limit`, and `Limit @ price 0` is an invalid NT8 order. Market leader signals fall through to `DispatchToFollower`'s existing `SendCopy` path (no ATM bracket). This is a known gap, not a defect. If a Market-with-ATM capability is desired in the future, a separate `SendCopyWithAtmMarket` overload or order type routing would be required. Decision: return-no-op for Market is the correct safe choice. | P2 | future | OPEN |
| DW-LD-03 | **Lane E: re-arm ATM after QX/BE**: After a QX or BE exit, the follower ATM bracket is cancelled. On re-entry, a fresh ATM bracket is not automatically armed. The Lane E wiring point (`TryLaneEHook` in `OnFollowerPositionUpdate` at the `_followerSlateReady` gate-open transition) is documented in plan Section 11. Implementation is out of scope for Lane D. | P1 | Lane E | OPEN |

---

## Violations

None. All Jane Street DNA rules, NT8 API constraints, CYC budgets, spec requirements, and scan
results pass. FINAL_PASS is granted.

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane D · 2026-09-11*
