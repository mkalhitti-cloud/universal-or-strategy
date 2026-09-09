# PTT-REPAIRS-02 Ticket 1 Completion Report
**Engineer**: ptt-engineer (Phase 4a)
**Ticket**: PTT-REPAIRS-02-T1
**Date**: 2026-09-07
**Status**: BUILD_PASS

---

## 1. Summary of Changes Made

### Production Fix: `src/PropTraderTools/CopyEngine.cs`

**Change 1 — Header comment update (line 5738)**

Updated the CYC comment to reflect CYC=6 (was 5, one new decision branch added).

**Change 2 — EvictDedup Filled block (lines 5762-5769)**

Replaced the `Filled` branch body: previously only removed `_entryInstrKeyByOrderId[orderId]`
(discarding the retrieved instrKey). Now mirrors the Cancelled branch by also calling
`_liveEntryInstruments.TryRemove(filledInstrKey, out _)` when the TryRemove succeeds.

Exact lines modified: **5738, 5764-5768** (header comment + Filled block body).
Cancelled branch (lines 5758-5759) and `_entryDispatchedOrders` guard (line 5755) are UNTOUCHED.

### Test Addition: `src/PropTraderTools/CopyEngineTests.cs`

Appended one new `[Fact]` after the T_R6 closing brace (before class/namespace closing braces),
at line 7780. Test name: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`.

---

## 2. Before/After Diff of EvictDedup Filled Block

### BEFORE (lines 5738 and 5762-5768):

```csharp
// DW-B142-MGC-02: CYC=5: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4).

if (state == OrderState.Filled)
{
    // DW-B142-MGC-02: clean up companion map (lazy).
    // Do NOT remove _liveEntryInstruments key -- trade is live.
    // PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.
    _entryInstrKeyByOrderId.TryRemove(orderId, out _);
}
```

### AFTER (lines 5738 and 5762-5769):

```csharp
// PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5).

if (state == OrderState.Filled)
{
    // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
    // Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
    // MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
```

**Lines deleted**: old comments at 5764-5766, old single TryRemove at 5767.
**Lines added**: 3 new comments + if-guard TryRemove pattern (5 lines total).
**Net change**: same block size (5 content lines), different behavior.

---

## 3. Full Text of New [Fact] Test Added

```csharp
        // PTT-REPAIRS-02 T1: verify _liveEntryInstruments is cleared on Filled,
        // allowing a second DispatchCopy for the same instrKey to pass Gate 5 check (a).
        // Uses InternalsVisibleTo seams declared at CopyEngine.cs:46.
        // No NT8 type construction required -- seams operate on string keys and OrderState enum.
        [Fact]
        public void IsLiveEntryBlocked_ClearsOnFill_AllowsReentry()
        {
            const string instrKey = "MGC DEC26|Sell";
            const string orderId1 = "PTTR02-orderId-1";
            const string orderId2 = "PTTR02-orderId-2";
            const double limitPrice = 0.0;

            // Pre-condition: clear any residual state from other tests
            _engine.ClearLiveEntryForInstrument_ForTest("MGC DEC26");

            // Step 1: First dispatch -- Gate 5 should pass (instrKey not yet set)
            bool blocked1 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId1, limitPrice);
            Assert.False(blocked1); // first dispatch must proceed

            // Step 2: instrKey is now set in _liveEntryInstruments
            Assert.True(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

            // Step 3: Leader order fills -- EvictDedup must clear instrKey (the fix)
            _engine.EvictDedup_ForTest(orderId1, NinjaTrader.Cbi.OrderState.Filled);

            // Step 4: instrKey must be cleared from _liveEntryInstruments after the fill
            Assert.False(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

            // Step 5: Second dispatch for same instrKey -- Gate 5 check (a) must pass
            bool blocked2 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId2, limitPrice);
            Assert.False(blocked2); // second dispatch must proceed (was blocked before fix)
        }
```

---

## 4. Results of All 7 Scans

### SCAN-01: lock() scan in modified region (lines 5735-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' | Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 matches — **PASS**

### SCAN-02: DateTime.Now scan in modified region (lines 5735-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'DateTime\.Now' | Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 matches — **PASS**

### SCAN-03: return null scan in modified region (lines 5710-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'return null' | Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 matches — **PASS** (EvictDedup is void; IsLiveEntryBlocked returns bool)

### SCAN-04: async void scan in modified region (lines 5710-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'async void' | Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 matches — **PASS**

### SCAN-05: Non-ASCII character scan in modified region (lines 5735-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' | Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 non-ASCII chars — **PASS**

### SCAN-06: ?.Event -= scan in modified region (lines 5710-5775)
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\?\.\w+\s*-=' | Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }`
**Output**: (no output)
**Result**: 0 matches — **PASS**

### SCAN-07: CYC count on EvictDedup (manual)
**Method**: `internal void EvictDedup(string orderId, OrderState state)` — line 5740
**Branch count**:
| # | Branch |
|---|--------|
| 1 | `if (state != Filled && state != Cancelled && state != Rejected)` — terminal guard |
| 2 | `if (state == OrderState.Cancelled)` — Cancelled arm |
| 3 | `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))` — Cancelled instrKey lookup |
| 4 | `if (state == OrderState.Filled)` — Filled arm |
| 5 | `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))` — NEW Filled instrKey lookup |

Base(1) + 5 decision points = **CYC=6** — within budget <=7 — **PASS**
`IsLiveEntryBlocked` CYC=4 (unchanged) — within budget <=5 — **PASS**

---

## 5. Hard-Link Sync Result

**Command**: `powershell -File .\deploy-sync.ps1`
**Result**: SYNC COMPLETE — all hard links synchronized to NT8.
- ASCII GATE PASS
- DIFF GUARD PASS (diff size 9691 chars, within limit)
- SOVEREIGN AUDIT PASS
- All source files linked successfully.

---

## 6. Build Result

**Command**: `powershell -File .\scripts\build_readiness.ps1`
**CopyEngine.cs errors**: NONE — no errors in CopyEngine.cs or CopyEngineTests.cs.
**Pre-existing errors**: Linting.csproj contains pre-existing NT8 assembly reference errors in
V12_002.*.cs strategy files (HashSet<>, TcpListener, Timer, etc.). These are pre-existing
infrastructure issues unrelated to this ticket's changes and were present before this edit.
**No new errors introduced by PTT-REPAIRS-02-T1.**

---

## 7. [Fact] Count Verification

**Before this ticket**: 474 `[Fact]` methods
**After this ticket**: 475 `[Fact]` methods
**Delta**: +1 (exactly one new test added)

**Verification command**: `Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object`
**Result**: 475

Note: The ticket document states 300 before / 301 after. The actual file count was 474 before
this ticket (the file has grown since the plan was written). The delta of +1 is correct and
matches the requirement.

---

## 8. Self-Attestation

**All 7 scans run to zero. Scope limited to Ticket 1 only.**

- SCAN-01 (lock): 0 matches in modified region. PASS.
- SCAN-02 (DateTime.Now): 0 matches in modified region. PASS.
- SCAN-03 (return null): 0 matches in modified region. PASS.
- SCAN-04 (async void): 0 matches in modified region. PASS.
- SCAN-05 (non-ASCII): 0 non-ASCII chars in modified lines. PASS.
- SCAN-06 (?.Event -=): 0 matches in modified region. PASS.
- SCAN-07 (CYC): EvictDedup CYC=6 (<=7 budget PASS), IsLiveEntryBlocked CYC=4 (<=5 budget PASS).

No changes made to any file outside the two files listed in Section C of the ticket.
No changes made to the Cancelled branch (lines 5758-5759).
No changes made to `_entryDispatchedOrders` (line 5755).
No changes made to `IsLiveEntryBlocked` or `ClearLiveEntryForInstrument`.

---

*ptt-engineer · PTT-REPAIRS-02-T1 · 2026-09-07*
