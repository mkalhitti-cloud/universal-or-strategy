# B215 Lane C -- Tickets
## Phase 3 -- ptt-architect
## Block: B215-LaneC
## Epic: DW-B209-04 -- Spec HTML Update (B209/B211/B212/B213/B214 Features)
## Date: 2026
## Status: TICKETS_COMPLETE
## Input Plan: docs/brain/B215/02-architecture-plan-lane-c.md (REVIEW_PASS)
## Plan Review: docs/brain/B215/02-plan-review-lane-c.md (REVIEW_PASS, 0 violations)

---

## Rules Catalog Gate

**Gate result (from plan Section 0, confirmed by plan reviewer Check 7)**: PASS

This lane is HTML-only. Zero .cs files produced. P0 C# rules (JS-021 lock ban, JS-033 async void,
JS-001 throw ban, JS-002 return null) are NOT APPLICABLE. The single applicable rule is the
ASCII-only mandate (Jane Street code review category), which is ENFORCED in all new content and
verified by SCAN-7.

**GATE RESULT: PASS -- All work may proceed.**

---

## Ticket Count: 1

| Ticket | Title | File | Status |
|--------|-------|------|--------|
| LC1 | DW-B209-04 -- Spec HTML Update (B209/B211/B212/B213/B214 Features) | `specs/002-trade-copier-spec.html` | TODO |

---

## Ticket LC1

### Title
DW-B209-04 -- Spec HTML Update (B209/B211/B212/B213/B214 Features)

### Spec Requirements Satisfied
- **DW-B209-04** (OPEN in `docs/brain/B215/06-deferred-backlog.md` line 45): "specs/002-trade-copier-spec.html
  B209 block updates. Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before
  production SIM R8."
- LC1 closes DW-B209-04 by adding a new HTML section documenting all 6 feature groups (B209/B211/B212/
  B213/B214 Lane B).

### DW-Card Reference
**None.** LC1 closes DW-B209-04 -- no new DW cards are created by this ticket.

---

### Scope Lock

**SINGLE FILE. ZERO .cs FILES. ZERO TEST FILES. ZERO .csproj FILES.**

The engineer MUST touch ONLY `specs/002-trade-copier-spec.html`. Any modification to a .cs file,
test file, or .csproj file during LC1 execution is a SCOPE VIOLATION and SCAN-1 FAIL.

---

### File to Modify

`specs/002-trade-copier-spec.html`

---

### Method Signatures

N/A -- HTML-only ticket. No C# methods are implemented.

---

### xUnit Tests

N/A -- HTML-only ticket. No test cases are written.

---

### Insertion Location

Insert the new HTML block at exactly this position:

```
Line 44337: (existing)    <!-- end B208 block -->
<--- INSERT HERE (new content begins on the blank line after 44337) --->
Line 44339: (existing)  </body>
Line 44340: (existing) </html>
```

The new block is a `<div class="section" id="section-b209-b214">` element -- a sibling to the B208
section at the same nesting level inside `<body>`. Do NOT modify or delete any existing line.

---

### Full HTML Content to Insert

The engineer MUST insert exactly the following HTML block between line 44337 and line 44339.
Every ASCII-only rule applies. No curly/smart quotes. No Unicode dashes. Use straight quotes and
ASCII hyphen-minus only. HTML entities (&nbsp; &middot; &rarr; &lt; &gt; &amp;) are permitted.

```html

    <!-- == B209-B214 features block ====================================== -->
    <div class="section" id="section-b209-b214">
      <div class="section-label">Blocks B209-B214 &nbsp;&middot;&nbsp; PTT-COPIER-B209-B214 &nbsp;&middot;&nbsp; OCO Guard, GTC Guard, Ask/Bid Buttons, Re-entry Suppression, Multi-Chart Fix, UI Range</div>

      <!-- FG-1: B209 OCO Guard -->
      <div class="card">
        <div class="card-title">FG-1 -- B209 OCO Guard - BuildBeOcoId Collision Fix &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B209)</span></div>
        <p style="color:var(--mid);">Hotfix tag: HOTFIX-BUG-BE-OCO-REUSE</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Problem</h4>
        <p style="color:var(--mid);"><code>BuildBeOcoId</code> used <code>accName.Substring(0, 4)</code> as the OCO prefix component. Both "Sim101" and "Sim102" produced the same prefix "Sim1". With the same <code>seq</code> value, both accounts generated identical OCO IDs (e.g., "PTT-BE-Sim1-00001-0"). NT8 rejected the second <code>CreateOrder</code> call: "The OCO ID cannot be reused". All Sim102 BE bracket orders were rejected -- only Sim101 was protected.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Fix</h4>
        <p style="color:var(--mid);">Changed prefix to <code>accName.Substring(0, 8)</code> (or full name if shorter than 8 chars). "Sim101" -&gt; prefix "Sim101", "Sim102" -&gt; prefix "Sim102". No collision possible between any two distinct account names.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Method Affected</h4>
        <p style="color:var(--mid);"><code>BuildBeOcoId</code> in <code>PttBreakEven.cs</code> (~line 331)</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Minimal Diff</h4>
        <pre style="background:var(--raised);border:1px solid var(--border);border-radius:4px;padding:10px;font-size:12px;overflow-x:auto;margin:8px 0;">BEFORE: string prefix = accName.Length &gt;= 4 ? accName.Substring(0, 4) : accName;
AFTER:  string prefix = accName.Length &gt;= 8 ? accName.Substring(0, 8) : accName;</pre>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">JS Compliance</h4>
        <p style="color:var(--mid);">No lock(). No throw. No return null. CYC unchanged (one-char change in ternary).</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Tests Added</h4>
        <ul style="color:var(--mid);">
          <li>T_OCO_ID_01: Sim101 != Sim102 for same seq/pair</li>
          <li>T_OCO_ID_02: same seq + pair + different acc -&gt; unique OCO ID</li>
          <li>T_OCO_ID_03: short account name (&lt; 8 chars) -&gt; full name used as prefix</li>
        </ul>
      </div>

      <!-- FG-2: B209 GTC Guard -->
      <div class="card">
        <div class="card-title">FG-2 -- B209 GTC Guard - Entry Drag Dedup Fix &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B209)</span></div>
        <p style="color:var(--mid);">Hotfix tag: HOTFIX-ENTRY-DRAG-DEDUP</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Problem</h4>
        <p style="color:var(--mid);"><code>HandleEntryChange</code> called <code>_dedupCache.TryRemove(orderId, out _)</code> after a drag-price update. NT8 then fired a second <code>OnOrderUpdate</code> event for the same leader order (Accepted -&gt; Working state transition). At that point, Gate C's <code>TryGetValue</code> returned false (key was removed), the drag-price delta check was never reached, and the code fell through to <code>DispatchCopy</code>. A second PTT-Copy order was placed on the follower -- doubling follower contracts.</p>
        <p style="color:var(--mid);">Note: HOTFIX-DW-B64-01 had added <code>_dedupCache[orderId] = currentPrice</code> before calling <code>HandleEntryChange</code> to survive the TryRemove, but <code>HandleEntryChange</code> then immediately removed the key again, undoing that fix for the Working-state event.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Fix</h4>
        <p style="color:var(--mid);">Replaced <code>_dedupCache.TryRemove(orderId, out _)</code> with <code>_dedupCache[orderId] = newPrice</code> (upsert, keep key alive). The second Working-state event hits Gate C, finds the stored price, computes <code>Math.Abs(newPrice - newPrice) = 0 &lt; tickSize</code>, and returns without calling <code>DispatchCopy</code>. <code>DispatchCopy</code>'s own <code>IsDedup</code> check also independently blocks it.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Method Affected</h4>
        <p style="color:var(--mid);"><code>HandleEntryChange</code> in <code>CopyEngine.cs</code> (~line 1140)</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Minimal Diff</h4>
        <pre style="background:var(--raised);border:1px solid var(--border);border-radius:4px;padding:10px;font-size:12px;overflow-x:auto;margin:8px 0;">BEFORE: _dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _);
AFTER:  _dedupCache[leaderOrder.OrderId.ToString()] = newPrice;</pre>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">JS Compliance</h4>
        <p style="color:var(--mid);">No lock() (<code>_dedupCache</code> is <code>ConcurrentDictionary</code>, indexer write is lock-free). No throw. No return null. CYC unchanged (one-for-one line replacement).</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Tests Added</h4>
        <ul style="color:var(--mid);">
          <li>T_DRAG_DEDUP_02: Working-state re-entry after drag -- Gate C sees delta=0 -&gt; DispatchCopy NOT called</li>
          <li>T_DRAG_DEDUP_03: orderId key still present in _dedupCache after HandleEntryChange at newPrice</li>
          <li>T_DRAG_DEDUP_04: new follower order on cancel+resubmit path -- new orderId keyed correctly</li>
        </ul>
      </div>

      <!-- FG-3: B211 Ask/Bid/Ask-All/Bid-All Buttons -->
      <div class="card">
        <div class="card-title">FG-3 -- B211 Ask/Bid/Ask-All/Bid-All Buttons &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B214-LaneA)</span></div>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section A: Ask (single account) - HOTFIX-B211-ASK-01</h4>
        <p style="color:var(--mid);"><strong>Problem:</strong> Ask button did nothing -- <code>TryFindWorkingTarget</code> required a PTT- prefix but Clone-mode targets are named <code>Target1</code>..<code>Target9</code> (native NT8 ATM names, no PTT prefix). <code>firstTarget</code> was always null.</p>
        <p style="color:var(--mid);"><strong>Fix:</strong> Widened name filter to also accept names starting with "Target" (1..9), covering native NT8 ATM targets alongside PTT-prefixed ones.</p>
        <p style="color:var(--mid);"><strong>Method affected:</strong> <code>TryFindWorkingTarget</code> in <code>TradeCopierPanel.cs</code> (~line 1462 area)</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section B: Ask-All and Bid-All - HOTFIX-B211-ASKALL-01, HOTFIX-B211-BIDALL-01</h4>
        <p style="color:var(--mid);"><strong>Problem:</strong> Ask-ALL did nothing -- <code>OnAskAllClick</code> routed through <code>PttMoveTargetAll.Execute</code> -&gt; <code>AllAccounts(instrument)</code> -&gt; <code>FindRule(instrument)</code> -&gt; returns null when copier is OFF or no rule loaded. Bid-ALL had the same root cause.</p>
        <p style="color:var(--mid);"><strong>Fix:</strong> Rewrote <code>OnAskAllClick</code> and <code>OnBidAllClick</code> to bypass <code>AllAccounts</code>/<code>FindRule</code> entirely. Direct pattern: resolve <code>_leaderAccount</code> via <code>TryResolveLeaderAccount()</code>, then <code>foreach (var item in _followerItems)</code> with <code>item.IsSelected</code> guard, calling <code>PttMoveTargetOne.Execute</code> per account.</p>
        <p style="color:var(--mid);"><strong>Key design decision:</strong> NOT using <code>AllAccounts</code> or <code>FindRule</code>. Direct <code>_leaderAccount</code> + <code>_followerItems</code> loop is the canonical pattern for Ask-ALL/Bid-ALL. This is correct when copier is OFF because no CopyRule lookup is needed -- the buttons operate on any open position regardless of copier state.</p>
        <p style="color:var(--mid);"><strong>Methods affected:</strong> <code>OnAskAllClick</code>, <code>OnBidAllClick</code> in <code>TradeCopierPanel.cs</code>. JS compliance: No lock(); no throw; no return null; CYC=4 per method; ASCII-only.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section C: Multi-target - HOTFIX-B211-MOVETARGET-01</h4>
        <p style="color:var(--mid);">Ask/Bid buttons only moved the last found target when two working targets existed. <code>TryFindWorkingTarget</code> had last-write semantics (single Order ref).</p>
        <p style="color:var(--mid);"><strong>Fix:</strong> <code>CollectWorkingTargets(acc, instr)</code> returns <code>Order[]</code> via two-pass count+fill (no <code>List&lt;&gt;</code> to avoid NT8 NinjaScript CS0246 import issue). Batch cancel+resubmit all found targets.</p>
      </div>

      <!-- FG-4: B212 Re-entry Suppression Guards -->
      <div class="card">
        <div class="card-title">FG-4 -- B212 Multi-Layer Follower Re-entry Suppression &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B214-LaneA)</span></div>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Root Cause</h4>
        <p style="color:var(--mid);">Copier ON + BE-ALL press: <code>PttBreakEvenSwap</code> cancels native ATM brackets. NT8 ATM detects bracket cancellation -&gt; re-arms a new Entry on the leader. This Entry flows through <code>OnOrderUpdate</code> -&gt; Gate 1 (copy enabled) -&gt; Gate 2 (leader account) -&gt; <code>DispatchCopy</code>. Without guards, DispatchCopy copies the re-armed Entry to all followers = spurious re-entry.</p>
        <p style="color:var(--mid);">Similarly: Flatten-ALL -&gt; PTT-Flatten fills -&gt; NT8 ATM re-arms Entry -&gt; copied to followers. Also: after all BE targets fill (leader position flat) -&gt; NT8 ATM re-arms Entry -&gt; leader and follower re-entries if not guarded.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Guard Layer 1 -- HOTFIX-B212-REENTRY-V3 (BE-ALL + Flatten-ALL window)</h4>
        <ul style="color:var(--mid);">
          <li>Added <code>IsBeSwapInProgress(order.Account?.Name)</code> gate in <code>DispatchCopy</code> -- blocks re-armed Entry during bracket-cancel window (~50-200ms after BE-ALL). <code>IsBeSwapInProgress</code> is <code>ConcurrentDictionary</code> lookup (lock-free, O(1)).</li>
          <li>Added <code>HasActivePttFlatten(order.Account, order.Instrument)</code> gate in <code>DispatchCopy</code> -- scans <code>acc.Orders.ToList()</code> for PTT-Flatten in Submitted/Accepted/Working/PartFilled state. Blocks re-armed Entry while flatten is in-flight.</li>
          <li>Design rationale: leader must always receive its BE bracket (keep all PttBeSwapAll paths intact). Copier must not forward the NT8 ATM re-arm artifact to followers. These are orthogonal concerns -- <code>DispatchCopy</code> is the correct single choke point.</li>
        </ul>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Guard Layer 2 -- HOTFIX-B212-REENTRY-BETARGET V4 (after target fill)</h4>
        <ul style="color:var(--mid);">
          <li><code>_beSwapInProgress</code> clears when PTT-BE-Stop-N reaches Working/Cancelled (~50-200ms). Target fills happen seconds/minutes later. A different sentinel is required.</li>
          <li>Added <code>HasFilledBeTargetFast(order.Account)</code> check in <code>IsSuppressedReentry</code> (third guard). Returns true if <code>_filledBeTargetCount</code> (ConcurrentDictionary, lock-free) is non-zero for the account.</li>
          <li>Cleared via <code>_filledBeTargetCount.TryRemove(leader)</code> in <code>DispatchCopy</code> after all gates pass (confirmed legitimate new entry). This is the correct clear timing -- the re-armed Entry is blocked; the next legitimate new trade clears the counter.</li>
          <li>Six-site audit of all <code>SendCopyWithAtm</code> call sites confirmed. <code>ReplaceFollowerCopyOnAtmCancel</code> was the only site NOT guarded by <code>DispatchCopy</code> -- extracted <code>IsReplaceOnAtmCancelBlocked</code> helper and added <code>HasFilledBeTargetFast(leader)</code> check there (V4).</li>
        </ul>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Guard Layer 3 -- HOTFIX-B212-REENTRY-FOLLOWER-V1/V2 (follower drain race + Lane E)</h4>
        <ul style="color:var(--mid);">
          <li>After BE-All, followers can re-enter via two paths not covered by <code>IsSuppressedReentry</code>: (1) stale drain parked during pre-BE drag (<code>SubmitDrainedEntry</code>), (2) Lane E (<code>IsLaneEEligible</code>) when <code>_laneEFlattenBlocked</code> is not set.</li>
          <li>V1 Fix 1: <code>HasFilledBeTargetFast(follower)</code> guard added in <code>SubmitDrainedEntry</code> after <code>IsBeSwapInProgress</code> check. <code>SubmitDrainedEntry</code> CYC 7-&gt;8 (at ceiling, PASS).</li>
          <li>V1 Fix 2: <code>HasFilledBeTargetFast(follower)</code> guard added in <code>DrainThenDispatch</code> fast-path after <code>IsFlattenInFlightOrRecent</code> check.</li>
          <li>V2 Fix 3 (belt-and-suspenders): <code>HasFilledBeTargetFast(acc)</code> gate (5b) added in <code>IsLaneEEligible</code>. Prerequisite: CYC extraction from 11 to 7 (four helpers extracted: <code>IsLaneEFlattenLatched</code>, <code>IsPositionOrInstrumentNull</code>, <code>IsFollowerSlateNotClosed</code>, <code>IsLaneEBlockedByFlattenOrLeader</code>). After gate (5b) added: CYC=8 (at ceiling, PASS).</li>
          <li>Covers case where followers close via BE-targets only (no FlattenOneAccount, so <code>_laneEFlattenBlocked</code> is NOT set).</li>
        </ul>
      </div>

      <!-- FG-5: B213 Multi-Chart Instrument Fix -->
      <div class="card">
        <div class="card-title">FG-5 -- B213 Multi-Chart Instrument Fix &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B214-LaneA)</span></div>
        <p style="color:var(--mid);">Hotfix tag: HOTFIX-B213-MULTI-CHART-INSTRUMENT-01</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Problem</h4>
        <p style="color:var(--mid);">When two ChartTrader windows are open for different instruments (e.g., MES + MGC), standalone buttons (BE-All, Flatten, Move Target, Quick Exit) on the second window silently did nothing. Copier OFF. Only one window's buttons responded.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Root Cause</h4>
        <p style="color:var(--mid);"><code>TryResolveLeaderAccount()</code> returned null immediately when <code>_accountCombo == null</code>. <code>_accountCombo</code> is set by <code>WireAccountCombo()</code>, called by <code>WireLeaderAccount()</code> in <code>DoInject()</code>. <code>WireLeaderAccount()</code> calls <code>FindAccountComboBox(chartTrader)</code> -- if the second ChartTrader visual tree was not fully laid out at inject time (race condition), <code>FindAccountComboBox</code> returned null AND the <code>FindVisualChildByIndex</code> fallback also returned null -&gt; <code>WireLeaderAccount</code> returned early without calling <code>WireAccountCombo</code> -&gt; <code>_accountCombo</code> stayed null forever.</p>
        <p style="color:var(--mid);">All button handlers use <code>_leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); if (_leaderAccount == null) return;</code>. With <code>_accountCombo</code> null -&gt; <code>TryResolveLeaderAccount()</code> -&gt; null -&gt; silent no-op.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Fix</h4>
        <p style="color:var(--mid);">Extended <code>TryResolveLeaderAccount()</code> with a lazy re-wire path in the <code>_accountCombo == null</code> branch. When <code>_accountCombo</code> is null, attempts to find and wire the Account ComboBox from <code>_currentChart</code> (via <code>FindVisualChild&lt;ChartTrader&gt;</code> + <code>FindAccountComboBox</code>) before proceeding with resolution. Calls <code>WireAccountCombo(combo)</code> if found.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Details</h4>
        <ul style="color:var(--mid);">
          <li><strong>Method affected:</strong> <code>TryResolveLeaderAccount</code> in <code>TradeCopierPanel.cs</code></li>
          <li><strong>CYC delta:</strong> 4 -&gt; 6 (under ceiling of 8, PASS)</li>
          <li><strong>Threading:</strong> UI-thread-only -- called from WPF Click handlers. <code>FindVisualChild</code>, <code>FindAccountComboBox</code>, <code>WireAccountCombo</code> are all UI-thread operations. No threading concern.</li>
          <li><strong>JS compliance:</strong> No lock(); no async void; no heap allocation on hot path (button click = infrequent); CYC=6; ASCII-only.</li>
          <li><strong>SIM confirmed:</strong> Both ChartTrader windows (MES + MGC) buttons fire correctly on their respective instruments with copier OFF.</li>
        </ul>
      </div>

      <!-- FG-6: B214 Lane B UI Changes -->
      <div class="card">
        <div class="card-title">FG-6 -- B214 Lane B UI Changes (Label Casing, Tick Suffix, Buffer Cap) &nbsp;<span class="pill-green">PIPELINE-COMPLETE (B214-LaneB)</span></div>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section A: UI-1 -- Button Label Casing</h4>
        <p style="color:var(--mid);">"ALL" changed to "All" at 7 callsites in <code>TradeCopierPanel.cs</code> (lines 1048, 1059, 1131, 1170, 1187, 1521, 1578). Examples: "Be All", "Ask-All", "Bid-All", "Flatten All", "Quick Exit All". No functional change -- display text only.</p>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section B: UI-2/UI-3 -- Buffer Tick Suffix Display</h4>
        <p style="color:var(--mid);">Buffer numbers now show a "t" tick suffix (e.g., "+5t", "-3t"). Zero buffer shows the label name only (e.g., "BE All" not "BE All +0t"). Zero suppression guard. Implemented in <code>FormatBuffer</code> and <code>FormatGlobalBeBuffer</code> (pure static helpers in <code>TradeCopierPanel.cs</code>).</p>
        <ul style="color:var(--mid);">
          <li><code>FormatBuffer(string name, int ticks)</code>: ticks == 0 -&gt; return name; ticks &gt; 0 -&gt; return <code>name + " +" + ticks + "t"</code>; ticks &lt; 0 -&gt; return <code>name + " " + ticks + "t"</code> (ticks already negative)</li>
          <li><code>FormatGlobalBeBuffer</code>: same pattern applied to global BE buffer display.</li>
        </ul>
        <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-section C: UI-4/UI-5 -- BE All Buffer Cap</h4>
        <ul style="color:var(--mid);">
          <li>BE All buffer ceiling raised from 10 to 50 ticks. Floor raised from -10 to -50 ticks.</li>
          <li><code>IncrementBuffer</code>: gate changed from <code>&lt; 10</code> to <code>&lt; 50</code> in <code>PttGlobalBreakEven.cs</code> (line 18).</li>
          <li><code>DecrementBuffer</code>: gate changed from <code>&gt; -10</code> to <code>&gt; -50</code> in <code>PttGlobalBreakEven.cs</code> (line 25).</li>
          <li><code>InitBeSlider</code>: slider max changed from 10 to 50, min from -10 to -50.</li>
          <li>Allows fine-tuning of BE buffer over a 100-tick range (-50 to +50) instead of 20-tick range.</li>
          <li>JS compliance: No lock(); no throw; no return null; CYC unchanged (value change only); ASCII-only.</li>
        </ul>
      </div>

    </div>
    <!-- end B209-B214 block -->
```

---

### Post-Edit Verification Commands

```powershell
# SCAN-1: scope check -- must show ONLY specs/002-trade-copier-spec.html
git diff --name-only

# SCAN-2: test baseline -- Failed=9 (unchanged from Lane A final state), no regression
dotnet test --no-build

# SCAN-6: tag closure check
# Review new HTML block manually: verify all <div>, <h4>, <p>, <pre>, <ul>, <li> tags are closed

# SCAN-7: ASCII mandate check -- must return 0 matches in new content
grep -P "[\x80-\xFF]" specs/002-trade-copier-spec.html
```

---

### 7-Scan Checklist (Engineer Contract)

The ptt-engineer MUST complete all 7 scans before VERIFY_PASS may be issued.
SCAN-3, SCAN-4, and SCAN-5 are N/A for HTML. Mark them N/A in the verification report.

| Scan | Rule | Command / Check | Pass Criteria |
|------|------|-----------------|---------------|
| SCAN-1 | File scope | `git diff --name-only` | Result MUST show ONLY `specs/002-trade-copier-spec.html`. Any additional file = SCAN-1 FAIL. |
| SCAN-2 | Test baseline | `dotnet test --no-build` | Failed=9 (Lane A final state, unchanged). Zero new failures introduced. Any regression = SCAN-2 FAIL. |
| SCAN-3 | N/A | HTML file -- no C# lock() patterns | Mark N/A in verification report. |
| SCAN-4 | N/A | HTML file -- no async void | Mark N/A in verification report. |
| SCAN-5 | N/A | HTML file -- no return null | Mark N/A in verification report. |
| SCAN-6 | HTML structure | Manual tag closure review | All new `<div>`, `<h4>`, `<p>`, `<pre>`, `<ul>`, `<li>` tags properly closed. No unclosed tag introduced. Any unclosed tag = SCAN-6 FAIL. |
| SCAN-7 | ASCII mandate | `grep -P "[\x80-\xFF]" specs/002-trade-copier-spec.html` | Must return 0 matches in new content. Any non-ASCII byte in new content = SCAN-7 FAIL. Pre-existing content matches (if any) are acceptable. |

---

### Spec Requirements Satisfied

| Requirement ID | Description | Closed by |
|----------------|-------------|-----------|
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates. Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before production SIM R8. | LC1 (this ticket) |

**All 6 FGs documented**:
- FG-1 (B209 OCO Guard) -- HOTFIX-BUG-BE-OCO-REUSE
- FG-2 (B209 GTC Guard) -- HOTFIX-ENTRY-DRAG-DEDUP
- FG-3 (B211 Ask/Bid/Ask-All/Bid-All) -- HOTFIX-B211-ASK-01, HOTFIX-B211-ASKALL-01, HOTFIX-B211-BIDALL-01, HOTFIX-B211-MOVETARGET-01
- FG-4 (B212 Re-entry Suppression) -- HOTFIX-B212-REENTRY-V3, HOTFIX-B212-REENTRY-BETARGET V4, HOTFIX-B212-REENTRY-FOLLOWER-V1/V2
- FG-5 (B213 Multi-Chart Fix) -- HOTFIX-B213-MULTI-CHART-INSTRUMENT-01
- FG-6 (B214 Lane B UI) -- Label Casing, Tick Suffix, Buffer Cap

---

## Execution Order and Success Gate

| Step | Action | Success Criteria |
|------|--------|-----------------|
| 1 | Read `specs/002-trade-copier-spec.html` lines 44335-44340 to confirm insertion point | Lines match plan Section 5.1 exactly |
| 2 | Insert new HTML block after line 44337 (after `<!-- end B208 block -->`) | Block present; no existing lines deleted |
| 3 | Run SCAN-1 | Only `specs/002-trade-copier-spec.html` in diff |
| 4 | Run SCAN-2 | Failed=9, no regression |
| 5 | Run SCAN-6 (manual tag review) | Zero unclosed tags |
| 6 | Run SCAN-7 | Zero non-ASCII bytes in new content |
| 7 | Issue VERIFY_PASS | All 7 scans pass (SCAN-3/4/5 marked N/A) |

**Success gate**: VERIFY_PASS requires SCAN-1, SCAN-2, SCAN-6, and SCAN-7 all PASS, and SCAN-3/4/5 marked N/A. Any FAIL blocks VERIFY_PASS.

---

## TICKETS_COMPLETE
