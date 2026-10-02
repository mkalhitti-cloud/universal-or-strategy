# B183 Architecture Plan
# Re-introduce 16 Symbols Dropped by Megacommit 878a5b33

**Status**: PLAN_COMPLETE  
**Date**: 2026-07-07  
**Author**: ptt-architect (Phase 1)  
**Source of truth**: `git show f01b5155:src/PropTraderTools/CopyEngine.cs`  
**Next phase**: ptt-plan-reviewer -> docs/brain/B183/02-plan-review.md

---

## 1. LANE-SPLIT GATE

**Q1. Are all changes in the same method or within 50 lines of each other?**  
NO. Changes span `CopyEngine.cs` (5,951 lines) across 10+ distinct insertion sites ranging from
line 385 to line 6386, plus `TradeCopierPanel.cs` at separate locations.

**Q2. Does Fix B (TradeCopierPanel BUG3) design depend on Fix A (CopyEngine BUG1+BUG2)?**  
NO. BUG3 is purely visual (button colors) + BE event plumbing in `TradeCopierPanel.cs`. It does
not depend on the slate gate, Lane D, or Lane E changes. The CopyEngine events
`PendingBeArmed`/`GlobalBeAllDisarmed` exist in HEAD CopyEngine.cs; only the panel subscriptions
were dropped.

**Q3. Does each fix have standalone value if the other is blocked?**  
YES. BUG1+BUG2 fixes (CopyEngine) cure cancel+reorder copy failure and ghost re-entry independently.
BUG3 fix (TradeCopierPanel) cures button colour regression and BE arm/disarm visibility independently.

**Q4. Does each fix have an independent SIM verification path?**  
YES. See Section 9 (SIM Gate Criteria). BUG1+BUG2 verified via call-trace analysis. BUG3 verified
via code-inspection of BuildInstrRow + BE event wiring.

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

- **Lane A**: CopyEngine.cs changes (Tickets T1-T5)
- **Lane B**: TradeCopierPanel.cs + non-src (Tickets T6-T7) — may proceed independently of Lane A

---

## 2. Architecture Overview

### Root Cause

Megacommit `878a5b33` (Sep 29) wholesale-replaced both `CopyEngine.cs` (9,329 → 6,404 lines, single
hunk) and `TradeCopierPanel.cs` (3,061 → 2,754 lines, 40 hunks), overwriting changes from commits
`cb474be9` and `f01b5155` (Sep 12). Cherry-pick is not viable due to 12 conflicting hunks from
renamed methods and a completely restructured file. The correct repair is surgical re-introduction
of the verbatim bodies from `f01b5155`.

### Bugs and Their Fixes

| Bug | Root Cause | Fix Mechanism | File |
|-----|-----------|---------------|------|
| BUG1: cancel+reorder not copying | No slate gate — `FindFollowerBracketOrder` returns stale bracket after cancel | Slate gate (Group A) + Lane D dispatch hook (Group C) | CopyEngine.cs |
| BUG2: ghost re-entry after close | No flatten guard — bracket sync fires on already-flat follower | Slate gate (Group A) + Lane E re-arm (Group D) + FlatGuardLoop (Group B) | CopyEngine.cs |
| BUG3: Quick2t/QAll2t colour + BE arm/disarm | BrushInactive used instead of BrushTeal; OnPendingBeArmedDispatch + OnGlobalBeAllDisarmed removed | Visual tuple fix + BE event re-add | TradeCopierPanel.cs |

### Files Changed

| File | Change Type | Tickets |
|------|------------|---------|
| `src/PropTraderTools/CopyEngine.cs` | Additions only (16 symbols + call sites) | T1, T2, T3, T4, T5 |
| `src/PropTraderTools/TradeCopierPanel.cs` | Additions + targeted inline fixes | T7 |
| `scripts/verify_critical_symbols.ps1` | New file | T6 |
| `AGENTS.md` | Protocol hardening additions | T6 |
| `docs/brain/regression-investigation-B179-B180.md` | Status update | T6 |

**Approved Deletion List: NONE. B183 adds code only.**

---

## 3. The 16 Missing Symbols

### Group A: Slate Gate (BUG1 + BUG2 core mechanism)

The slate gate tracks per-follower "ready" state. When a bracket wipe is imminent (cancel or
flatten), the slate is marked `false`, blocking `FindFollowerBracketOrder` from returning stale
brackets. When position goes flat and orders are clean, the slate is re-armed (`true`).

| Item | Symbol | Type | CYC | Ticket |
|------|--------|------|-----|--------|
| A1 | `_followerSlateReady` | Field (ConcurrentDictionary<string,bool>) | — | T1 |
| A1 | `_laneDDispatched` | Field (ConcurrentDictionary<string,byte>) | — | T1 |
| A1 | `_laneEDispatched` | Field (ConcurrentDictionary<string,byte>) | — | T1 |
| A2 | `TryMarkFollowerSlateNotReady` | Method | 2 | T1 |
| A2 | `TrySetFollowerSlateReady` | Method | 4 | T1 |
| A2 | `IsAccountFlat` | Static Method | 3 | T1 |
| A2 | `HasAnyActiveOrders` | Static Method | 5 | T1 |
| A2 | `OnFollowerPositionUpdate` | Event Handler | 3 | T1 |
| A3 | Call sites: `TryMarkFollowerSlateNotReady` + `_laneEDispatched.TryRemove` in both `CancelQxBrackets` overloads + `FlattenOneAccount` | Call sites | 0 | T2 |
| A4 | Gate (1) in `FindFollowerBracketOrder` (Account overload) — expr-body to block-body | Modification | +1 | T2 |
| A5 | `acc.PositionUpdate += OnFollowerPositionUpdate` in `Subscribe()` | Subscription | 0 | T2 |
| A5 | `acc.PositionUpdate -= OnFollowerPositionUpdate` in `Unsubscribe()` | Subscription | 0 | T2 |

### Group B: FlatGuardLoop (BUG2 flatten guard)

Prevents `FlattenOneAccount` from being called on already-flat followers, avoiding position
inversion.

| Item | Symbol | Type | CYC | Ticket |
|------|--------|------|-----|--------|
| B1 | `FlatGuardLoop<TAccount>` | Static Generic Method | 3 | T5 |
| B2 | Call sites in `Flatten(Instrument)` + `Flatten(Account,Instrument)` | Replacement | 0 | T5 |

### Group C: Lane D (ATM dispatch hook, BUG1 dispatch path)

When in Clone+Named-ATM mode, Lane D handles the initial ATM entry dispatch separately from the
`DispatchToFollower` loop, with a dedup gate to prevent double-dispatch.

| Item | Symbol | Type | CYC | Ticket |
|------|--------|------|-----|--------|
| C2 | `TryLaneDHook` | Method | 6 | T3 |
| C2 | `TryLaneDDispatchFollower` | Method | 4 | T3 |
| C3 | Call site: `TryLaneDHook(...)` in `DispatchCopy` | Call site | +0 | T3 |
| C4 | Lane D skip gate in `DispatchToFollower` | Insertion | +1 | T3 |
| C4 | `dispatched` counter + `SetLiveEntryDispatched` in `DispatchCopy` loop | Loop modification | +1 | T3 |

*Note: `_laneDDispatched` field is in A1 (T1). C1 is not a separate item — do not add twice.*

### Group D: Lane E (re-arm ATM after partial QX/BE, BUG2 re-entry)

After a partial Quick Exit or Break Even move, a follower may have a remaining position but no ATM
brackets. Lane E detects this state and re-arms the ATM template.

| Item | Symbol | Type | CYC | Ticket |
|------|--------|------|-----|--------|
| D2 | `IsLaneEEligible` | Method | 8 (AT BUDGET — frozen) | T4 |
| D2 | `TryLaneEHook` | Method | 5 | T4 |
| D2 | `TryLaneEDispatchFollower` | Method | 5 | T4 |
| D2 | `BuildLaneEKey` | Static Method | 2 | T4 |

*Note: `_laneEDispatched` field is in A1 (T1). D1 is not a separate item — do not add twice.*

---

## 4. Compile-Time Dependency Warning

`OnFollowerPositionUpdate` (T1) calls `TryLaneEHook` (T4).  
`CancelQxBrackets` call sites (T2) call `BuildLaneEKey` (T4).

**Resolution**: Lane A engineer must apply T1 through T5 insertions in a single editing session
before the first build. Do not attempt an incremental compile after T1 alone — it will fail
with CS0103 (name `TryLaneEHook` does not exist). The ticket ordering (T1→T2→T3→T4→T5) is
the INSERTION ORDER within the file and the REVIEW ORDER for Ph4b verification, not the commit
order. Apply all 5 tickets' changes, then build once.

---

## 5. Ticket Breakdown

### T1: Slate Gate Fields + Methods (Group A: A1, A2)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug**: BUG1 + BUG2  
**Type**: Additions only

#### Insertion Point 1 — Field declarations (A1)

Anchor: After `_drainOwnedOrderIds` field declaration ends (HEAD line 385), before
`// --- Status event ---` comment (HEAD line 387).

Insert verbatim:
```csharp
        // BUG1+BUG2: Slate-ready gate per follower. Key = acc.Name (ASCII). Value = bool.
        // false: bracket wipe imminent -- FindFollowerBracketOrder returns null (gate closed).
        // true / absent: gate open -- normal bracket search proceeds.
        // ConcurrentDictionary: no lock (JS-021). Indexer write is atomic.
        private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
        // Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // Lane E: re-arm dedup guard. Prevents double-arm on multiple PositionUpdate events for the
        // same post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only). Value: 0 (unused).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. TryRemove: called from CancelQxBrackets.
        // Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

#### Insertion Point 2 — 5 Slate Gate methods (A2)

Anchor: After `BuildQxSnapshot` method body ends (find the closing `}` of `BuildQxSnapshot`),
before the `// CancelQxBrackets` comment (HEAD line 910 area). The block goes between
`BuildQxSnapshot` end and `IsQxCancelCandidate`/`CancelQxBrackets`.

Insert verbatim (5 methods):  
See exact bodies in `docs/brain/B183/00-orchestrator-prompt.md` section A2 (lines 151-233).

**Methods**: `TryMarkFollowerSlateNotReady`, `TrySetFollowerSlateReady`, `IsAccountFlat`,
`HasAnyActiveOrders`, `OnFollowerPositionUpdate`

**CYC budget**: 2, 4, 3, 5, 3 — all within JS-066 (<=8). PASS.

**JS compliance notes (T1)**:
- JS-021: All writes use ConcurrentDictionary indexer/TryAdd — zero lock() usage.
- JS-001: All methods use early returns. Zero throw.
- JS-002: All methods return void or bool. No null return.
- JS-033: OnFollowerPositionUpdate is `private void` (not async). PASS.
- JS-081: All strings are ASCII-only. PASS.

---

### T2: Slate Gate Call Sites + Subscribe/Unsubscribe (Group A: A3, A4, A5)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug**: BUG1 + BUG2  
**Type**: Targeted insertions and one expression-body-to-block-body conversion

**Compile dependency**: Calls `TryMarkFollowerSlateNotReady` (T1), `BuildLaneEKey` (T4),
`OnFollowerPositionUpdate` (T1). Do not build until T1 and T4 are also applied.

#### Insertion Point 1 — CancelQxBrackets 2-param (A3)

Anchor: HEAD line ~918. After null guard `return; // (1)` at line 921, before `var stale = new`.

Insert 2 lines verbatim:
```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

#### Insertion Point 2 — CancelQxBrackets 3-param (A3)

Anchor: HEAD line ~998. After null guard `return; // (1)` at line 1005, before `var stale = new`.

Insert 2 lines verbatim:
```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent (3-param path)
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

#### Insertion Point 3 — FlattenOneAccount (A3)

Anchor: HEAD line ~4677. BEFORE `CancelAllAccountOrders(acc, instrument);` (HEAD line 4709).

Insert 1 line verbatim:
```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
```

#### Insertion Point 4 — FindFollowerBracketOrder Account overload (A4)

Anchor: HEAD lines 3571-3582. Currently an expression-body (`=>`). Convert to block body and
add the slate gate check.

Replace the current expression-body with the block-body form verbatim from spec A4
(see `docs/brain/B183/00-orchestrator-prompt.md` lines 268-289).

**CYC impact**: +1 (from CYC=1 expr-body to CYC=2 block-body). Still within JS-066 budget.

**Note on JS-002**: The gate returns `return null;` for the `Order?` return type. This is the
existing null contract for this method (nullable reference `Order?`). This is NOT a JS-002
violation — the method's return type is explicitly `Order?` (nullable). Document in scan 3.

#### Insertion Points 5 + 6 — Subscribe() and Unsubscribe() (A5)

**Subscribe** (HEAD line 1350): Add after `acc.OrderUpdate += OnOrderUpdate;`:
```csharp
                acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
```

**Unsubscribe** (HEAD line 1356): Add after `acc.OrderUpdate -= OnOrderUpdate;`:
```csharp
                acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
```

**JS compliance notes (T2)**:
- JS-021: TryRemove is lock-free. No lock(). PASS.
- CYC impact on CancelQxBrackets: method-call insertions add CYC+0 (no new branches). PASS.
- CYC impact on FlattenOneAccount: method-call insertion adds CYC+0. HEAD CYC=6. Stays at 6. PASS.

---

### T3: Lane D (Group C)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug**: BUG1 (copy dispatch path in Clone+Named-ATM mode)  
**Type**: Method additions + targeted modifications to existing methods

**Compile dependency**: References `_laneDDispatched` (T1), `SetLiveEntryDispatched`,
`GetCloneAtmMode`/`ResolveAtmMode`, `PassesDailyCapCheck`, `SendCopyWithAtm` (all must exist in HEAD).

#### Pre-insert verification (engineer MUST run before inserting):

```powershell
# Verify required internal methods exist in HEAD
grep -n "SetLiveEntryDispatched" src/PropTraderTools/CopyEngine.cs
grep -n "GetCloneAtmMode\|ResolveAtmMode" src/PropTraderTools/CopyEngine.cs
grep -n "PassesDailyCapCheck" src/PropTraderTools/CopyEngine.cs
grep -n "SendCopyWithAtm" src/PropTraderTools/CopyEngine.cs
```

**RISK**: `GetCloneAtmMode` may be renamed `ResolveAtmMode` in HEAD (HEAD line 2506 shows
`ResolveAtmMode`). If so, use the HEAD method name, not the f01b5155 name.

#### Insertion Point 1 — TryLaneDHook + TryLaneDDispatchFollower (C2)

Anchor: After `DispatchCopy` method ends (HEAD line ~2415), before the next method comment.

Insert 2 methods verbatim from spec C2 (see `docs/brain/B183/00-orchestrator-prompt.md`
lines 394-476). Adapt `GetCloneAtmMode` to match the verified HEAD method name.

**CYC**: TryLaneDHook=6, TryLaneDDispatchFollower=4. Both within JS-066 budget.

#### Insertion Point 2 — TryLaneDHook call in DispatchCopy (C3)

Anchor: HEAD line ~2373. After `int baseQty = ResolveBaseQty(...)`.

Insert 2 lines verbatim:
```csharp
            // Lane C: Lane D wiring point. Unconditional void call -- 0 CYC cost in parent. PTT-BUGS-LIVE-0911.
            TryLaneDHook(order, rule, baseSignal, baseQty);
```

#### Insertion Point 3 — DispatchCopy loop replacement (C4)

Anchor: HEAD lines 2384-2414 (the `int idx = 0; foreach (var acc in rule.FollowerAccounts)` block
through to `_lastLeaderDirection` update, exclusive).

Replace the loop body (lines 2384-2410, the `int idx = 0;` through `idx++;` close) with the
`dispatched` counter version verbatim from spec C4 (see `docs/brain/B183/00-orchestrator-prompt.md`
lines 505-535).

**CYC impact on DispatchCopy**: +1 for `if (dispatched > 0)` check. Engineer must verify HEAD
DispatchCopy CYC before insertion; if currently CYC=7, it becomes CYC=8 (AT BUDGET). If CYC=8,
extract a helper before inserting.

#### Insertion Point 4 — Lane D skip gate in DispatchToFollower (C4)

Anchor: HEAD line ~2506. After `var mode = ResolveAtmMode(rule, acc.Name);`.

Insert 5 lines verbatim:
```csharp
            // Lane D skip gate: if TryLaneDHook already dispatched this (order, follower) pair via ATM,
            // return immediately -- prevents one ATM entry (hook) + one raw entry (loop) double-dispatch.
            // _laneDDispatched.ContainsKey is O(1) lock-free (JS-021). No counter modification -- dedup
            // commit for non-ATM followers in this loop is unaffected.
            string laneD_key = baseSignal.OrderId + "#" + acc.Name;
            if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

**CYC impact on DispatchToFollower**: +1 for the `if ContainsKey` return. HEAD comment says
"CCN<=3" (line 2487). After insertion: CYC=4. Within JS-066 budget.

**JS compliance notes (T3)**:
- JS-021: All ConcurrentDictionary operations (TryAdd, ContainsKey) are lock-free. PASS.
- JS-001: TryLaneDHook uses early returns only. TryLaneDDispatchFollower returns false on failure.
- JS-081: All string keys use "#" as separator (ASCII, cannot appear in NT8 account names). PASS.

---

### T4: Lane E (Group D)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug**: BUG2 (re-entry after partial QX/BE)  
**Type**: Method additions only

**NOTE ON COMPILE ORDER**: Although T4 is listed after T1-T3, its methods (`TryLaneEHook`,
`BuildLaneEKey`) are CALLED from T1 (OnFollowerPositionUpdate) and T2 (CancelQxBrackets). Apply
T4 insertions IN THE SAME SESSION as T1 and T2 before building.

**Compile dependency**: References `_laneEDispatched` (T1), `_followerSlateReady` (T1),
`GetCloneAtmMode`/`ResolveAtmMode`, `PassesDailyCapCheck`, `SendCopyWithAtm` (all verified in T3).

#### Insertion Point — All 4 Lane E methods (D2)

Anchor: After `TryLaneDDispatchFollower` ends (inserted in T3). Insert as a contiguous block.

Insert 4 methods verbatim from spec D2 (see `docs/brain/B183/00-orchestrator-prompt.md`
lines 552-664). Adapt `GetCloneAtmMode` to match the verified HEAD method name.

**Methods**: `IsLaneEEligible`, `TryLaneEHook`, `TryLaneEDispatchFollower`, `BuildLaneEKey`

**CYC budget**:
- `IsLaneEEligible`: CYC=8 — AT JS-066 BUDGET. FROZEN. Any future branch addition = JS-066 violation.
  The `||` operators in the null guard (`pos == null || pos.Instrument == null`) and in the
  TryGetValue check (`!TryGetValue || ready`) are counted as +1 CYC each per JS-013.
- `TryLaneEHook`: CYC=5. PASS.
- `TryLaneEDispatchFollower`: CYC=5. PASS.
- `BuildLaneEKey`: CYC=2. PASS.

**JS compliance notes (T4)**:
- JS-021: All ConcurrentDictionary operations lock-free. PASS.
- JS-002: `BuildLaneEKey` returns `string.Empty` for null instr, never null. PASS.
- JS-013: `||` operators are CYC budget items, all isolated in `IsLaneEEligible` — compound
  guards are deliberately centralised there to keep `TryLaneEHook` CYC low.
- JS-081: `"#LANE-E#"` separator and `"LANE-E#"` prefix are ASCII-only. PASS.

---

### T5: FlatGuardLoop (Group B)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug**: BUG2 (flatten guard)  
**Type**: New static generic method + 2 call site replacements

#### Insertion Point 1 — FlatGuardLoop<TAccount> method (B1)

Anchor: Before `PttDragOrphanPredicates` class (before HEAD line 6386), at end of the
`CopyEngine` class body.

Insert method verbatim from spec B1 (see `docs/brain/B183/00-orchestrator-prompt.md`
lines 334-354).

**CYC**: 3 (foreach + isFlat true + isFlat false). Within JS-066. PASS.

#### Insertion Point 2 — Flatten(Instrument) call site (B2)

Anchor: HEAD lines 4482-4491. Replace:
```csharp
            foreach (var acc in AllAccounts(instrument))
                FlattenOneAccount(acc, instrument);
```
With `FlatGuardLoop(...)` call verbatim from spec B2 (lines 362-366).

#### Insertion Point 3 — Flatten(Account,Instrument) call site (B2)

Anchor: HEAD lines 4518-4537. After `FlattenOneAccount(leader, instrument);`, replace the
follower foreach loop with `FlatGuardLoop(...)` call verbatim from spec B2 (lines 373-378).

**JS compliance notes (T5)**:
- JS-021: Static, no shared mutable state. Pure iteration. PASS.
- JS-001: No throw. NinjaTrader.Code.Output.Process is not a throw. PASS.
- NT8: `Output.Process` is thread-safe (safe from non-UI threads). PASS.

---

### T6: Guardian Script + Protocol Hardening (Non-src)

**Files**: `scripts/verify_critical_symbols.ps1` (create), `AGENTS.md` (append),
`docs/brain/regression-investigation-B179-B180.md` (status update)  
**Type**: Non-src additions

#### scripts/verify_critical_symbols.ps1

Create the file with the exact PowerShell body from spec section "GUARDIAN SCRIPT"
(`docs/brain/B183/00-orchestrator-prompt.md` lines 750-821).

The script:
- Verifies 16 symbols present in CopyEngine.cs
- Verifies 6 BE event symbols present in TradeCopierPanel.cs
- Checks _instr2tBtn.Background = BrushTeal (BUG3 visual fix)
- Detects single-hunk commits on CopyEngine.cs (regression guard)
- Exit 0 = PASS, Exit 1 = FAIL

#### AGENTS.md Protocol Hardening

Append to Section 2 (Architectural Mandates) verbatim from spec section "PROTOCOL HARDENING"
(`docs/brain/B183/00-orchestrator-prompt.md` lines 869-885):

Four new mandates:
1. Ph4b Deletion Audit — required git diff command before VERIFY_PASS
2. Single-Hunk Ban — single-hunk CopyEngine replace = automatic VERIFY_FAIL
3. Guardian Symbol Script — must pass before VERIFY_PASS
4. Commit Size Gate — >300 changed lines requires Director approval

#### docs/brain/regression-investigation-B179-B180.md

Update status section to reflect B183 resolution:
- Add entry: "B183 (2026-07-07): All 16 missing symbols re-introduced. BUG1+BUG2+BUG3 resolved."

---

### T7: TradeCopierPanel.cs — BUG3 Visual + BE Arm/Disarm Events

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Bug**: BUG3  
**Type**: Inline fixes + method additions

**Constraint**: B180-2 approved removals (`ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`,
`UpdateBeVisuals`, `OnBeConnected`) MUST NOT be re-introduced. Check
`docs/brain/B180-orchestrator-prompt.md` approved removal list before restoring any symbol.

#### Sub-scope 7A: Visual Fix (Low Risk)

**Step 1 — BuildInstrRow(): Add Background + Foreground AFTER SetResourceReference**

Anchor: HEAD line 1183 (`BuildInstrRow()`). After `_instr2tBtn.SetResourceReference(...)` at
HEAD line 1193 (before `_instr2tBtn.Click += ...`), add:
```csharp
            _instr2tBtn.Background = BrushTeal;
            _instr2tBtn.Foreground = System.Windows.Media.Brushes.White;
```
After `_instrQAll2tBtn.SetResourceReference(...)` at HEAD line 1204, add:
```csharp
            _instrQAll2tBtn.Background = BrushTeal;
            _instrQAll2tBtn.Foreground = System.Windows.Media.Brushes.White;
```
Source: `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs` lines 1290-1315.

**Step 2 — Specs tuple: Change BrushInactive to BrushTeal for 4 teal buttons**

HEAD lines 1079, 1089, 1099, 1109 all show `BrushInactive` as the Bg field for BE, BE ALL,
Quick, Quick ALL tuples. Change all 4 to `BrushTeal`.

**Step 3 — Specs foreach: Fix btn.Foreground when s.Teal == true**

HEAD line 1149: Change `btn.Foreground = BrushTeal;` to:
```csharp
                    btn.Foreground = System.Windows.Media.Brushes.White;
```

**Step 4 — Specs foreach: Fix btn.Background**

HEAD line 1153: Change `btn.Background = s.Bg;` to:
```csharp
                bool isTeal = s.Teal;
                btn.Background = isTeal ? BrushTeal : BrushInactive; // AFTER style -- explicit brush wins
```

#### Sub-scope 7B: BE Arm/Disarm Events (Medium Risk)

Engineer MUST read `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs` for lines
~1350-1395 and ~1655-1665 to get verbatim bodies.

**Methods to re-add**:
- `OnPendingBeArmedDispatch(string instr, string accountName)` — f01b5155 ~lines 1350-1365
- `OnGlobalBeAllDisarmed()` — f01b5155 ~lines 1366-1370
- `UpdateBeAllVisuals(BeState state)` — f01b5155 ~lines 1373-1395
- `DisarmBeAllOnFlat(bool hasPosition)` — f01b5155 ~lines 1655-1665

**Event wiring**: Add subscriptions in `SubscribeEngineEvents()` or `OnLoaded()`:
```csharp
_engine.PendingBeArmed += OnPendingBeArmedDispatch;
_engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed;
```

**Threading mandate**: Any WPF property set inside `OnPendingBeArmedDispatch` and
`OnGlobalBeAllDisarmed` MUST use `Dispatcher.InvokeAsync`. These fire from CopyEngine on the
NT8 account background thread. Verify f01b5155 bodies include the correct `Dispatcher.InvokeAsync`
wrapping before inserting.

#### Sub-scope 7C: Structural Extraction Methods (Low Functional Risk — Engineer's Discretion)

The following extracted methods exist in f01b5155 but were inlined in HEAD:
- `BuildSingleButtonCluster(...)` (~f01b5155 lines 1215-1260)
- `BuildQuickT3HiddenRow(root)` (~f01b5155 lines 1276-1286)
- `SubscribeEngineEvents()`, `BuildAllAccountsList()`, `RegisterAndInitializeModules()`,
  `WireLeaderOrderHandlers()` (~f01b5155 lines 890-960)

**Decision criteria**: If the 7A inline fixes correctly reproduce the visual behaviour, 7C is
OPTIONAL and may be deferred. If 7B requires `SubscribeEngineEvents()` as the wiring point (it
likely does), that extraction MUST be verified to exist in HEAD or created. Engineer must verify:
```powershell
grep -n "SubscribeEngineEvents" src/PropTraderTools/TradeCopierPanel.cs
```

**Note on BuildBuyToggleButton etc.**: If their logic is correctly inlined in HEAD
`BuildClickTraderRow`, they are acceptable as-is. Do NOT re-extract unless CYC is violated.

**JS compliance notes (T7)**:
- JS-021: No lock(). BE event subscriptions are += / -= operations (thread-safe NT8 pattern). PASS.
- JS-081: No Unicode/emoji in new string literals. PASS.
- Dispatcher.InvokeAsync is the correct pattern for CopyEngine callback → UI update (not `lock`).

---

## 6. NT8 API Risk Items

These must be verified by ptt-engineer BEFORE inserting T3/T4 code:

| Risk | Check Command | Expected Result |
|------|--------------|----------------|
| `GetCloneAtmMode` name | `grep -n "GetCloneAtmMode\|ResolveAtmMode" CopyEngine.cs` | If `ResolveAtmMode` found, use that name in T3/T4 bodies |
| `PassesDailyCapCheck` name | `grep -n "PassesDailyCapCheck\|DailyCapCheck" CopyEngine.cs` | Use exact HEAD name |
| `SetLiveEntryDispatched` name | `grep -n "SetLiveEntryDispatched" CopyEngine.cs` | Must find exactly 1 definition |
| `SendCopyWithAtm` return type | `grep -n "SendCopyWithAtm" CopyEngine.cs` | Confirm `bool` return type (not void) |

If `SendCopyWithAtm` returns void in HEAD, the `bool ok = SendCopyWithAtm(...)` pattern in T3/T4
must be changed to a two-line form: call + assume success. Document any deviation.

---

## 7. Seven-Scan Checklist

Every engineer implementing Lane A (T1-T5) and Lane B (T7) MUST run all 7 scans to zero before
submitting the ticket-N-completion.md.

### Scan 1 — JS-021: lock() ban (P0 CRITICAL)
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
```
**Expected**: 0 results. Any match = P0 violation. STOP. Fix before proceeding.

### Scan 2 — JS-033: async void ban (P0 CRITICAL)
```powershell
grep -rn "async void " src/PropTraderTools/CopyEngine.cs
```
**Expected**: 0 results. Any match = P0 violation. STOP.

### Scan 3 — JS-002: return null documentation
```powershell
grep -rn "return null;" src/PropTraderTools/CopyEngine.cs
```
**Expected**: Document every match. The `FindFollowerBracketOrder` gate (T2/A4) adds one
intentional `return null;` with `Order?` return type — this is compliant with the `Order?`
nullable contract, not a violation. All other `return null;` occurrences should be pre-existing.
New matches from T1-T5 must be zero (no new `return null;` in Group A-D methods).

### Scan 4 — JS-066: CYC per new method (P1 HIGH)
Manual review of each new method's CYC against the documented values:

| Method | Documented CYC | Budget |
|--------|---------------|--------|
| TryMarkFollowerSlateNotReady | 2 | PASS |
| TrySetFollowerSlateReady | 4 | PASS |
| IsAccountFlat | 3 | PASS |
| HasAnyActiveOrders | 5 | PASS |
| OnFollowerPositionUpdate | 3 | PASS |
| FlatGuardLoop<TAccount> | 3 | PASS |
| TryLaneDHook | 6 | PASS |
| TryLaneDDispatchFollower | 4 | PASS |
| IsLaneEEligible | 8 | AT BUDGET — FROZEN |
| TryLaneEHook | 5 | PASS |
| TryLaneEDispatchFollower | 5 | PASS |
| BuildLaneEKey | 2 | PASS |

Also verify `DispatchToFollower` CYC after Lane D gate (+1) stays <= 8.  
Also verify `DispatchCopy` CYC after `if (dispatched > 0)` (+1) stays <= 8.

### Scan 5 — JS-081: ASCII-only check (P1 HIGH)
```powershell
# Check for non-ASCII in new lines (search for common violations)
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs
```
**Expected**: 0 results in lines added by B183 tickets. Pre-existing matches are not B183's
responsibility but must be documented.

### Scan 6 — Guardian Script
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
**Expected**: Exit 0 (PASS). All 16 CopyEngine symbols present. All 6 BE event panel symbols
present. BrushTeal background visual fix confirmed. No single-hunk ban triggered.

### Scan 7 — ptt-sync.ps1
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
**Required**: Run after ALL .cs edits are complete. Re-synchronizes NT8 hard links.
Must complete with no errors before ticket-N-completion.md is submitted.

---

## 8. Constraint Reminders

1. **`TrySetSlateReadyOnDrainFill` is NOT in scope**: This method was deliberately removed in
   `f01b5155` (superseded by Lane E's position-event-based re-arm). Do NOT re-introduce it.

2. **Approved Deletion List: NONE**. B183 is additions-only. ptt-engineer must not delete any
   symbol from either file.

3. **All symbol bodies verbatim from f01b5155**: Do NOT rewrite, refactor, or modernize.
   Copy exact. The bodies have been pre-verified for JS compliance.

4. **B180-2 approved removals in T7**: `ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`,
   `UpdateBeVisuals`, `OnBeConnected` MUST NOT be re-introduced. These were removed in B180-2
   by Director authority.

5. **DispatchCopy `dispatched` counter**: HEAD DispatchCopy loop lacks both the `dispatched`
   counter and the `SetLiveEntryDispatched` call. Both must be re-added as part of T3 (Lane D
   wiring), not as a separate ticket.

6. **Single-hunk ban**: A single-hunk commit to CopyEngine.cs (full-file replace) is an automatic
   VERIFY_FAIL. All T1-T5 changes must be surgical insertions producing multiple hunks.

7. **_followerSlateReady absent key = gate open**: The gate logic treats an absent key as
   gate-open (first trade). `TryGetValue` returns false when key absent, so `!ready` = false =
   gate not closed. This is the correct first-trade behaviour.

---

## 9. SIM Gate Criteria (verbatim from spec)

Ph4b ptt-verifier MUST confirm ALL THREE bugs are architecturally resolved by tracing call paths:

### BUG1 Verification (cancel+reorder not copying)

1. Confirm `TryMarkFollowerSlateNotReady` is called in `CancelQxBrackets` (both overloads)
2. Confirm gate `(1)` exists in `FindFollowerBracketOrder(Account, ...)` overload
3. Confirm `TrySetFollowerSlateReady` is called from `OnFollowerPositionUpdate`
4. Confirm `acc.PositionUpdate += OnFollowerPositionUpdate` is in `Subscribe()`
5. Confirm `TryLaneDHook` is called from `DispatchCopy` after `baseQty`
6. Confirm Lane D skip gate exists in `DispatchToFollower`
7. Trace: cancel -> `CancelQxBrackets` -> `TryMarkFollowerSlateNotReady` (gate=false)
   -> `FindFollowerBracketOrder` returns null (correct)
   -> NT8 PositionUpdate fires flat -> `OnFollowerPositionUpdate` -> `TrySetFollowerSlateReady` (gate=true)
   -> next order -> `DispatchCopy` -> `TryLaneDHook` (if Named-ATM) or `DispatchToFollower` (raw)
   -> copy dispatched (BUG1 FIXED)

### BUG2 Verification (re-entry after close)

1. Confirm `TryMarkFollowerSlateNotReady` is called in `FlattenOneAccount`
2. Confirm `IsLaneEEligible` checks gate is CLOSED (not ready) before re-arming
3. Confirm `TryLaneEHook` is called from `OnFollowerPositionUpdate`
4. Trace: close -> `FlattenOneAccount` -> `TryMarkFollowerSlateNotReady` (gate=false)
   -> `FindFollowerBracketOrder` returns null -> no bracket sync -> no re-entry (BUG2 path 1 FIXED)
5. Trace: partial QX/BE -> position remains -> `OnFollowerPositionUpdate` fires ->
   `TrySetFollowerSlateReady` (not flat, returns early, gate stays false) ->
   `TryLaneEHook` -> `IsLaneEEligible` (gate=false, eligible) -> re-arms ATM bracket (BUG2 FIXED)

### BUG3 Verification (Quick2t / QAll2t colour + BE arm events)

1. Open TradeCopierPanel.cs `BuildInstrRow()` -- confirm:
   - `_instr2tBtn.Background = BrushTeal` is set AFTER `SetResourceReference`
   - `_instr2tBtn.Foreground = System.Windows.Media.Brushes.White` is set (not `BrushTeal`)
   - Same for `_instrQAll2tBtn`
2. Open specs tuple -- confirm BE, BE ALL, Quick, Quick ALL entries have `BrushTeal` as Bg field
3. In the foreach cluster loop -- confirm `btn.Foreground = Brushes.White` (not `BrushTeal`)
   when `s.Teal == true`, and `btn.Background = isTeal ? BrushTeal : BrushInactive`
4. Confirm `OnPendingBeArmedDispatch` exists and is subscribed to `_engine.PendingBeArmed`
5. Confirm `OnGlobalBeAllDisarmed` exists and is subscribed to `_engine.GlobalBeAllDisarmed`
6. Confirm `UpdateBeAllVisuals(BeState.Idle)` is called from `OnGlobalBeAllDisarmed`
7. Confirm `DisarmBeAllOnFlat(hasPosition)` is called from the position-state update path

---

## 10. Data Flow Summary

```
BUG1/BUG2 Resolution Call Chain:
  
  [Leader Cancel/Flatten]
       |
       v
  CancelQxBrackets / FlattenOneAccount
       |-- TryMarkFollowerSlateNotReady(acc) --> _followerSlateReady[acc.Name] = false
       |-- _laneEDispatched.TryRemove(BuildLaneEKey) [QX path only]
       |
       v
  FindFollowerBracketOrder(Account, ...)
       |-- _followerSlateReady.TryGetValue(acc.Name, out ready) && !ready --> return null (GATE CLOSED)
       |
       v
  NT8 fires acc.PositionUpdate
       |
       v
  OnFollowerPositionUpdate
       |-- TrySetFollowerSlateReady(acc) --> IsAccountFlat + !HasAnyActiveOrders --> gate=true [full flat path]
       |-- TryLaneEHook(acc, pos)        --> IsLaneEEligible + SendCopyWithAtm --> gate=true [partial QX path]
       |
       v
  _followerSlateReady[acc.Name] = true  (GATE OPEN)
       |
       v
  [Next Leader Order]
       |
       v
  DispatchCopy
       |-- TryLaneDHook(order, rule, baseSignal, baseQty)  [Clone+Named-ATM path]
       |-- foreach followers --> DispatchToFollower
             |-- _laneDDispatched.ContainsKey(laneD_key) ? return : continue [dedup gate]
             |-- copy dispatched (BUG1 FIXED)
```

---

## 11. Artifacts This Plan Produces

- [x] `docs/brain/B183/02-architecture-plan.md` — THIS FILE (Ph1)

Pending after REVIEW_PASS:
- [ ] `docs/brain/B183/04-tickets.md` (Ph3)
- [ ] `docs/brain/B183/02-plan-review.md` with `REVIEW_PASS: YES` (Ph2)

---

## PLAN_COMPLETE
