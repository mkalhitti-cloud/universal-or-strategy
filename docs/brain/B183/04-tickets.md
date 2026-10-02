# B183 Implementation Tickets
# Re-introduce 16 Symbols Dropped by Megacommit 878a5b33

**Status**: TICKETS_COMPLETE  
**Date**: 2026-07-07  
**Author**: ptt-architect (Phase 3)  
**Plan source**: `docs/brain/B183/02-architecture-plan.md` (REVIEW_PASS: YES)  
**Spec source**: `docs/brain/B183/00-orchestrator-prompt.md`  
**Next phase**: ptt-ticket-reviewer -> `docs/brain/B183/04-ticket-review.md`

---

## Compile-Order Mandate (Lane A: T1-T5)

**ALL T1-T5 changes MUST be applied in a single editing session before the first build.**  
`OnFollowerPositionUpdate` (T1) calls `TryLaneEHook` (T4).  
`CancelQxBrackets` call sites (T2) call `BuildLaneEKey` (T4).  
Attempting an incremental build after T1 alone will produce `CS0103: The name 'TryLaneEHook' does not exist`.  
Apply T1 → T2 → T3 → T4 → T5 insertions first, then build once.

---

## Global Constraint — Constraint 8 (P0, Non-Negotiable)

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY for all edits to `CopyEngine.cs`
> and `TradeCopierPanel.cs`. `write_file` is BANNED. A full-file replace produces a single-hunk
> diff which is the exact regression mechanism of `878a5b33`. Any commit where
> `(git show HEAD -- CopyEngine.cs | Select-String "^@@" | Measure-Object).Count -eq 1`
> is an instant VERIFY_FAIL regardless of content. ptt-verifier MUST check this as Gate 1.

---

## Ph4b Verifier Gate Reference (all 5 gates, mandatory for every .cs ticket)

Run for **every ticket** that touches `CopyEngine.cs` or `TradeCopierPanel.cs` before `VERIFY_PASS`:

**Gate 1 — write_file ban check**
```powershell
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@" | Measure-Object).Count
```
Expected: **Count > 1**. Count = 1 → **VERIFY_FAIL: full-file replace, Constraint 8 violated.**

**Gate 2 — Deletion audit**
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs |
    Select-String "^-\s+(private|internal|public)" |
    ForEach-Object { $_.Line.Trim() }
```
For B183 all tickets are add-only → expected: zero deleted symbol declarations.
Any deleted symbol not in the ticket's Approved Deletions table → **VERIFY_FAIL: unauthorised deletion.**

**Gate 3 — Commit size**
```powershell
(git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-" | Measure-Object).Count
(git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Measure-Object).Count
```
If either exceeds 300 without Director approval in the ticket → **VERIFY_FAIL: commit size gate.**  
Director pre-approval granted in `00-orchestrator-prompt.md` for B183 add-only scope.

**Gate 4 — Guardian script**
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Expected: exit 0. If exit 1 → **VERIFY_FAIL.**  
Note: script does not exist until Ticket 6 completes. For Tickets 1-5, run the symbol checks
manually using the `$requiredEngine` list from the script definition in the spec.

**Gate 5 — ptt-sync**
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Must complete with 0 errors before VERIFY_PASS.

---

## 7-Scan Checklist (applies to ALL tickets touching CopyEngine.cs)

Engineer MUST run all 7 scans to zero before submitting `ticket-N-completion.md`:

- **Scan 1**: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results (JS-021 P0)
- **Scan 2**: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results (JS-033 P0)
- **Scan 3**: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; the `FindFollowerBracketOrder` gate (T2 A4) adds one intentional `return null;` with `Order?` return type — compliant with nullable contract. Zero NEW `return null;` from T1/T3/T4/T5 methods.
- **Scan 4**: CYC ≤ 8 for every new method (see per-ticket CYC table)
- **Scan 5**: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in lines added by B183 (JS-081)
- **Scan 6**: Guardian script `powershell -File .\scripts\verify_critical_symbols.ps1` → exit 0 (manual symbol check for T1-T5 before T6 creates the script)
- **Scan 7**: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

---

# T1 — Slate Gate: Fields + Methods

**Ticket ID**: T1  
**Title**: Slate Gate Fields + Methods (Group A: A1, A2)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bugs fixed**: BUG1 + BUG2  
**Type**: Additions only  
**Depends on**: Nothing (first ticket)  
**Spec reference**: `00-orchestrator-prompt.md` sections A1 and A2

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `CopyEngine.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

### Pre-Insert Verification

Run before inserting. Confirm exact line numbers match.

```powershell
# Verify field anchor: _drainOwnedOrderIds must be at line 385
grep -n "_drainOwnedOrderIds" src/PropTraderTools/CopyEngine.cs
# Expected: line 384 (definition) and line 385 (new ConcurrentDictionary). Confirm field ends at 385.

# Verify comment anchor after insertion point
grep -n "Status event" src/PropTraderTools/CopyEngine.cs
# Expected: line 387 "// --- Status event ---". Insertion goes between line 385 and 387.

# Verify BuildQxSnapshot exists and find its closing brace
grep -n "BuildQxSnapshot" src/PropTraderTools/CopyEngine.cs
# Expected: one definition. Scroll to the closing } to find the A2 insertion anchor.

# Verify CancelQxBrackets is the method immediately after the insertion zone
grep -n "CancelQxBrackets" src/PropTraderTools/CopyEngine.cs
# Expected: first overload at ~line 918.

# Verify IsQxCancelCandidate comment is after BuildQxSnapshot
grep -n "IsQxCancelCandidate" src/PropTraderTools/CopyEngine.cs
# Expected: comment line ~910.
```

---

### Insertion 1 — Field Declarations (A1)

**Anchor**: After `_drainOwnedOrderIds` field ends (HEAD line 385), before `// --- Status event ---` (HEAD line 387).

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

**apply_diff search target** (exact): lines 385-387 in HEAD — the blank line after the field + the `// --- Status event ---` comment.

---

### Insertion 2 — Five Slate Gate Methods (A2)

**Anchor**: After `BuildQxSnapshot` method body ends (find its closing `}`), before the `// CancelQxBrackets` comment block (HEAD line ~910). Insert as a contiguous new block.

Insert verbatim (5 methods):

```csharp
        // BUG1+BUG2: Mark follower slate as not-ready. Called before any bracket wipe.
        // Conditions: acc non-null + IsFollowerAccount. Early returns prevent false negatives.
        // CYC<=2: acc null(1) + IsFollowerAccount(2). JS-021: ConcurrentDictionary indexer is lock-free.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryMarkFollowerSlateNotReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            _followerSlateReady[acc.Name] = false;
        }

        // BUG2: Mark follower slate as ready after position confirmed flat + no active orders.
        // Called from OnFollowerPositionUpdate.
        // Conditions: acc non-null + IsFollowerAccount + IsAccountFlat + !HasAnyActiveOrders.
        // Only writes ready=true when ALL conditions pass. Early-returns prevent false positives.
        // CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
        // JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySetFollowerSlateReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            if (!IsAccountFlat(acc))            // (3)
                return;
            if (HasAnyActiveOrders(acc))        // (4)
                return;
            _followerSlateReady[acc.Name] = true;
        }

        // BUG2: Returns true when acc has no open positions across all instruments.
        // Used by TrySetFollowerSlateReady to confirm flat before marking slate ready.
        // NT8: acc.Positions is available in AddOnBase. Each Position.Quantity > 0 = open.
        // CYC<=3: acc null(1) + foreach(2) + Quantity>0(3). JS-021: no lock (read-only enumeration).
        // JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool IsAccountFlat(Account acc)
        {
            if (acc == null)                    // (1)
                return true;
            foreach (var pos in acc.Positions)  // (2)
                if (pos.Quantity > 0)           // (3)
                    return false;
            return true;
        }

        // BUG2: Returns true when acc has any Working/Submitted/Accepted orders remaining.
        // Used by TrySetFollowerSlateReady to confirm no residual active orders before ready.
        // NT8: acc.Orders.ToList() snapshot -- standard pattern in CopyEngine (line 4898).
        // CYC<=5: acc null(1) + foreach(2) + Working(3) + Submitted(4) + Accepted(5).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool HasAnyActiveOrders(Account acc)
        {
            if (acc == null)                    // (1)
                return false;
            foreach (var o in acc.Orders.ToList())  // (2)
            {
                if (o.OrderState == OrderState.Working    // (3)
                    || o.OrderState == OrderState.Submitted  // (4)
                    || o.OrderState == OrderState.Accepted)  // (5)
                    return true;
            }
            return false;
        }

        // BUG2: NT8 account.PositionUpdate event handler.
        // Fires on NT8 account background thread (same context as OnOrderUpdate).
        // When position goes flat and no active orders remain, marks slate ready for next trade.
        // NT8: PositionEventArgs.Position.Account confirmed from TradeCopierPanel.cs line 2109.
        // No UI operations -- ConcurrentDictionary write is thread-safe, no Dispatcher needed.
        // CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
        private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
        {
            if (e?.Position?.Account == null)           // (1)
                return;
            if (!IsFollowerAccount(e.Position.Account)) // (2)
                return;
            TrySetFollowerSlateReady(e.Position.Account);
            TryLaneEHook(e.Position.Account, e.Position); // Lane E: re-arm ATM after partial QX/BE
        }
```

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

---

### 7-Scan Checklist (T1)

- Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 2: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 3: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; T1 adds 0 new `return null;`
- Scan 4: CYC for new T1 methods: `TryMarkFollowerSlateNotReady`=2, `TrySetFollowerSlateReady`=4, `IsAccountFlat`=3, `HasAnyActiveOrders`=5, `OnFollowerPositionUpdate`=3. All ≤ 8. PASS.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in new lines
- Scan 6: Manual check — confirm `_followerSlateReady`, `_laneDDispatched`, `_laneEDispatched`, `TryMarkFollowerSlateNotReady`, `TrySetFollowerSlateReady`, `IsAccountFlat`, `HasAnyActiveOrders`, `OnFollowerPositionUpdate` all present in file
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T1)

Run all 5 gates documented in the Global section above. Confirm Gate 1 hunk count > 1. Gate 2 zero deletions. Gate 3 deletion count ≈ 0 for add-only. Gate 4 manual symbol check (script not yet created). Gate 5 ptt-sync.

### BUILD_PASS Criteria (T1)

1. All 3 field declarations present in CopyEngine.cs (grep confirms `_followerSlateReady`, `_laneDDispatched`, `_laneEDispatched`)
2. All 5 methods present in CopyEngine.cs (grep confirms each method name)
3. File compiles — note: compile will FAIL with CS0103 until T2 through T4 are also applied in the same session (cross-forward references to `TryLaneEHook`, `BuildLaneEKey`)
4. All 7 scans pass (zero violations)
5. All 5 Ph4b verifier gates pass

---

# T2 — Slate Gate: Call Sites + Subscribe/Unsubscribe

**Ticket ID**: T2  
**Title**: Slate Gate Call Sites + Subscribe/Unsubscribe (Group A: A3, A4, A5)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bugs fixed**: BUG1 + BUG2  
**Type**: Targeted insertions + one expression-body-to-block-body conversion  
**Depends on**: T1 (fields `_followerSlateReady`, `_laneEDispatched` and methods `TryMarkFollowerSlateNotReady`, `OnFollowerPositionUpdate` must be present), T4 (`BuildLaneEKey` must be present before build)  
**Spec reference**: `00-orchestrator-prompt.md` sections A3, A4, A5

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `CopyEngine.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

### Pre-Insert Verification

```powershell
# Confirm CancelQxBrackets 2-param null guard location
grep -n "CancelQxBrackets" src/PropTraderTools/CopyEngine.cs
# Expected: first overload signature at ~line 918; null guard "return; // (1)" at ~line 921

# Confirm "var stale = new" line immediately after null guard in 2-param overload
grep -n "var stale = new" src/PropTraderTools/CopyEngine.cs
# Expected: line ~922 (2-param) and line ~1006 (3-param)

# Confirm CancelQxBrackets 3-param null guard
# 3-param overload: signature at ~line 998, null guard "return; // (1)" at ~line 1004-1005

# Confirm FlattenOneAccount and CancelAllAccountOrders line
grep -n "CancelAllAccountOrders" src/PropTraderTools/CopyEngine.cs
# Expected: line ~4709 in FlattenOneAccount body. Insertion goes BEFORE this line.

# Confirm FindFollowerBracketOrder Account overload is still expression-body
grep -n "FindFollowerBracketOrder" src/PropTraderTools/CopyEngine.cs
grep -n "=>" src/PropTraderTools/CopyEngine.cs | grep -A2 -B2 "FindFollowerBracketOrder"
# Expected: lines 3571-3582, expression-body using =>. If already converted, stop -- do not double-insert.

# Confirm Subscribe() and Unsubscribe() locations
grep -n "Subscribe\|Unsubscribe" src/PropTraderTools/CopyEngine.cs
# Expected: Subscribe at ~line 1350, Unsubscribe at ~line 1356.
# Confirm only "acc.OrderUpdate += OnOrderUpdate" is present (no PositionUpdate yet).
```

---

### Insertion 1 — CancelQxBrackets 2-param call sites (A3)

**Anchor**: HEAD line ~921. After `return; // (1)` in the 2-param overload, BEFORE `var stale = new System.Collections.Generic.List<Order>();`.

Insert 2 lines verbatim:

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

CYC impact on `CancelQxBrackets` 2-param: +0 (method calls, no new branches). HEAD CYC=6 stays at 6.

---

### Insertion 2 — CancelQxBrackets 3-param call sites (A3)

**Anchor**: HEAD line ~1004-1005. After `return; // (1)` in the 3-param overload, BEFORE `var stale = new System.Collections.Generic.List<Order>();`.

Insert 2 lines verbatim:

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent (3-param path)
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

CYC impact on `CancelQxBrackets` 3-param: +0. HEAD CYC=7 stays at 7.

---

### Insertion 3 — FlattenOneAccount call site (A3)

**Anchor**: HEAD line ~4709. Insert BEFORE `CancelAllAccountOrders(acc, instrument);`.

Insert 1 line verbatim:

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
```

CYC impact on `FlattenOneAccount`: +0. HEAD CYC=6 stays at 6.

---

### Insertion 4 — FindFollowerBracketOrder Account overload: expr-body to block-body + gate (A4)

**Anchor**: HEAD lines 3571-3582. The entire expression-body form. Replace with block-body form.

**SEARCH** (exact HEAD content lines 3571-3582):

```csharp
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        ) =>
            FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
```

**REPLACE** with verbatim block-body form (A4):

```csharp
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            // BUG1+BUG2: slate gate -- if bracket wipe was signalled, block bracket lookup.
            // Absent key (first trade): gate open. Key=false: gate closed. Key=true: gate open.
            // CYC: +1 (from 1 to 2). JS-021: ConcurrentDictionary.TryGetValue is lock-free. ASCII-only.
            bool ready;
            if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) // (1)
                return null;
            return FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
        }
```

**CYC impact**: +1 (CYC=1 expr-body → CYC=2 block-body). Within JS-066 budget.

**JS-002 note**: The `return null;` on the gate path is compliant — this method's return type is explicitly `Order?` (nullable). Scan 3 must document this instance as compliant.

---

### Insertion 5 — Subscribe() PositionUpdate wiring (A5)

**Anchor**: HEAD line 1353. After `acc.OrderUpdate += OnOrderUpdate;`, before the closing `}` of the foreach.

**SEARCH** (exact):
```csharp
        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
                acc.OrderUpdate += OnOrderUpdate;
        }
```

**REPLACE** with:
```csharp
        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate += OnOrderUpdate;
                acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
            }
        }
```

---

### Insertion 6 — Unsubscribe() PositionUpdate unwiring (A5)

**Anchor**: HEAD line 1359. After `acc.OrderUpdate -= OnOrderUpdate;`.

**SEARCH** (exact):
```csharp
        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
                acc.OrderUpdate -= OnOrderUpdate;
        }
```

**REPLACE** with:
```csharp
        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate -= OnOrderUpdate;
                acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
            }
        }
```

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

Note: The expression-body-to-block-body conversion of `FindFollowerBracketOrder` is a form transformation, not a deletion. The overload is preserved; only its syntax changes. This is NOT listed as a deletion.

---

### 7-Scan Checklist (T2)

- Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 2: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 3: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; T2 adds exactly 1 new `return null;` in `FindFollowerBracketOrder` (A4 gate) — compliant with `Order?` nullable contract. No other new instances.
- Scan 4: CYC for modified methods: `FindFollowerBracketOrder` (Account overload) was CYC=1, now CYC=2. `CancelQxBrackets` 2-param unchanged at CYC=6. `CancelQxBrackets` 3-param unchanged at CYC=7. All ≤ 8. PASS.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in new lines
- Scan 6: Manual check — confirm `PositionUpdate` wiring in `Subscribe` and `Unsubscribe`; confirm gate `TryGetValue` in `FindFollowerBracketOrder`
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T2)

All 5 gates as documented in Global section. Gate 2: confirm zero deleted symbol declarations (the expr-body conversion modifies lines, does not delete a symbol declaration).

### BUILD_PASS Criteria (T2)

1. Both `CancelQxBrackets` overloads contain `TryMarkFollowerSlateNotReady` + `_laneEDispatched.TryRemove` (grep confirms)
2. `FlattenOneAccount` contains `TryMarkFollowerSlateNotReady` call before `CancelAllAccountOrders`
3. `FindFollowerBracketOrder` Account overload is now block-body with `_followerSlateReady` gate
4. `Subscribe()` contains `acc.PositionUpdate += OnFollowerPositionUpdate;`
5. `Unsubscribe()` contains `acc.PositionUpdate -= OnFollowerPositionUpdate;`
6. All 7 scans pass
7. All 5 Ph4b verifier gates pass
8. Build PASS (only after T1-T5 all applied in same session)

---

# T3 — Lane D

**Ticket ID**: T3  
**Title**: Lane D — ATM Dispatch Hook (Group C: C2, C3, C4)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug fixed**: BUG1 (copy dispatch path in Clone+Named-ATM mode)  
**Type**: Method additions + targeted modifications to existing methods  
**Depends on**: T1 (`_laneDDispatched` field must be present)  
**Spec reference**: `00-orchestrator-prompt.md` sections C2, C3, C4

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `CopyEngine.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

### Pre-Insert Verification (MANDATORY — run before any insertion)

```powershell
# Confirm SetLiveEntryDispatched exists in HEAD
grep -n "SetLiveEntryDispatched" src/PropTraderTools/CopyEngine.cs
# Expected: at least 1 result (definition). If 0 results: STOP. Report to orchestrator.

# Confirm method name: GetCloneAtmMode or ResolveAtmMode?
grep -n "GetCloneAtmMode\|ResolveAtmMode" src/PropTraderTools/CopyEngine.cs
# Expected: ResolveAtmMode (HEAD confirmed at line 2506). Use "ResolveAtmMode" NOT "GetCloneAtmMode".
# If GetCloneAtmMode found instead: use that name in the inserted code.

# Confirm PassesDailyCapCheck exists
grep -n "PassesDailyCapCheck" src/PropTraderTools/CopyEngine.cs
# Expected: at least 1 result. If 0 results: STOP. Verify correct method name.

# Confirm SendCopyWithAtm exists and returns bool
grep -n "SendCopyWithAtm" src/PropTraderTools/CopyEngine.cs
# Expected: multiple results. Confirm the definition shows "bool SendCopyWithAtm" or "private bool".
# If return type is void: the "bool ok = SendCopyWithAtm(...)" pattern must be changed to
# two-line form (call + assume success). Document any deviation in ticket-3-completion.md.

# Confirm DispatchCopy ends at ~line 2415 (the closing } before the next method comment)
grep -n "DispatchCopy" src/PropTraderTools/CopyEngine.cs
# Then view lines 2410-2420 to confirm insertion point after line 2415.

# Confirm int baseQty = ResolveBaseQty line number
grep -n "ResolveBaseQty" src/PropTraderTools/CopyEngine.cs
# Expected: line ~2373 in DispatchCopy. TryLaneDHook call inserts after this line.

# Confirm the DispatchCopy loop starts at ~line 2384-2386
grep -n "int idx = 0" src/PropTraderTools/CopyEngine.cs
# Expected: line 2385 in DispatchCopy. The loop body replacement applies from here.

# Confirm DispatchToFollower and the ResolveAtmMode call inside it
grep -n "var mode = ResolveAtmMode" src/PropTraderTools/CopyEngine.cs
# Expected: line ~2506 in DispatchToFollower body. Lane D gate inserts after this line.

# Verify DispatchCopy current CYC before modification
# Count branches manually: each if/foreach/while/ternary/|| adds 1.
# If current CYC >= 8: STOP. Extract a helper before adding "if (dispatched > 0)".
# Report CYC count in ticket-3-completion.md.
```

---

### Insertion 1 — TryLaneDHook + TryLaneDDispatchFollower methods (C2)

**Anchor**: After `DispatchCopy` method ends (HEAD line ~2415 closing `}`), before the next method comment (`// TB-T4 Gate 4 predicate`).

**IMPORTANT**: In the inserted method bodies below, `GetCloneAtmMode` must be replaced with the verified HEAD method name (`ResolveAtmMode` per pre-insert verification above).

Insert 2 methods verbatim:

```csharp
        // Lane D: ATM dispatch hook. Called once per confirmed leader entry signal after all 5
        // DispatchCopy gates. Handles Clone-mode Named followers only; all other modes are no-ops.
        // CYC=6: (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) dispatched in loop, (5) laneDCount>0.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        // JS-023: no UI update here -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread.
        private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
        {
            // Branch (1): non-Clone mode -> no-op for Signal/Mirror/Market
            if (GetCopyMode() != CopyMode.Clone)
                return;

            // Branch (2): Market leader order -> skip ATM dispatch (SendCopyWithAtm hardcodes Limit;
            // Limit @ price 0 is invalid). Market followers fall through to DispatchToFollower SendCopy.
            if (order.OrderType != OrderType.Limit)
                return;

            // Branch (3): resolve ATM mode; Inherit returned when _cloneAtmCache is empty -> no-op
            FollowerAtmMode cloneAtmMode = ResolveAtmMode(rule, order.Instrument.FullName);
            if (!(cloneAtmMode is FollowerAtmMode.Named namedMode))
                return;

            // Fan-out: dispatch each follower via ATM
            int laneDCount = 0;
            int idx = 0;
            foreach (Account acc in rule.FollowerAccounts)
            {
                // Branch (4): track dispatched count
                if (TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty))
                    laneDCount++;
                idx++;
            }

            // Branch (5): commit Gate-5 dedup entry so a second OnOrderUpdate for the same fill
            // cannot re-enter TryLaneDHook (SetLiveEntryDispatched is thread-safe by existing design).
            if (laneDCount > 0)
            {
                string instrKey = order.Instrument.FullName + "|" + order.OrderAction.ToString();
                SetLiveEntryDispatched(instrKey, order.OrderId.ToString());
            }
        }

        // Lane D: per-follower ATM dispatch helper. Keeps TryLaneDHook CYC low.
        // CYC=4: (A) null acc guard, (B) daily cap check, (C) SendCopyWithAtm result.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneDHook caller).
        private bool TryLaneDDispatchFollower(
            Account acc,
            CopyRule rule,
            int idx,
            Order order,
            FollowerAtmMode.Named namedMode,
            CopySignal baseSignal,
            int baseQty
        )
        {
            // Branch (A): null guard
            if (acc == null)
                return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc))
                return false;

            int mult = GetMultiplier(rule, idx);
            CopySignal scaledSig = CopySignal.Create(
                baseSignal.Action,
                baseSignal.Type,   // always Limit (TryLaneDHook guards order.OrderType above)
                baseQty * mult,
                baseSignal.LimitPrice,
                baseSignal.OrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
            if (ok)
                // Key: orderId + "#" + accName. Separator "#" is ASCII and cannot appear in NT8 account names.
                _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
            return ok;
        }
```

> **METHOD NAME WARNING**: `GetCloneAtmMode` in the spec is the f01b5155 name. HEAD uses `ResolveAtmMode`. The pre-insert grep MUST confirm the name before copying this block. Replace `GetCloneAtmMode(rule, order.Instrument.FullName)` with `ResolveAtmMode(rule, order.Instrument.FullName)` (or the confirmed HEAD name) at the marked call site.

---

### Insertion 2 — TryLaneDHook call site in DispatchCopy (C3)

**Anchor**: HEAD line ~2373-2374. After `int baseQty = ResolveBaseQty(order.Instrument, baseSignal.Quantity);`, before the `// B119: DW-B128` comment.

Insert 2 lines verbatim:

```csharp
            // Lane C: Lane D wiring point. Unconditional void call -- 0 CYC cost in parent. PTT-BUGS-LIVE-0911.
            TryLaneDHook(order, rule, baseSignal, baseQty);
```

CYC impact on `DispatchCopy`: +0 (void method call, no branch).

---

### Insertion 3 — DispatchCopy loop replacement with `dispatched` counter (C4)

**Anchor**: HEAD lines 2384-2410. Replace the loop body (from `int idx = 0;` through the final `idx++;` and closing `}` of the foreach) with the `dispatched` counter version.

**SEARCH** (exact HEAD lines 2384-2410):

```csharp
            // B8 T1: index-tracking loop applies per-follower multiplier
            int idx = 0;
            foreach (var acc in rule.FollowerAccounts)
            {
                if (ShouldSkipFollowerDispatch(acc))
                {
                    idx++;
                    continue;
                }

                if (
                    ShouldSkipForReversalGuard(
                        acc,
                        instr,
                        currentAction,
                        lastAction,
                        hasLastDirection
                    )
                )
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
            }
```

**REPLACE** with:

```csharp
            // B8 T1: index-tracking loop applies per-follower multiplier
            int dispatched = 0;
            int idx = 0;
            foreach (var acc in rule.FollowerAccounts)
            {
                if (ShouldSkipFollowerDispatch(acc))
                {
                    idx++;
                    continue;
                }

                if (
                    ShouldSkipForReversalGuard(
                        acc,
                        instr,
                        currentAction,
                        lastAction,
                        hasLastDirection
                    )
                )
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
                dispatched++;
            }
            if (dispatched > 0)
                SetLiveEntryDispatched(instrKey, orderId);
```

**CYC impact on DispatchCopy**: +1 for `if (dispatched > 0)`. Engineer MUST report current CYC of `DispatchCopy` in `ticket-3-completion.md`. If CYC was already 8, extract a helper before adding this branch.

---

### Insertion 4 — Lane D skip gate in DispatchToFollower (C4)

**Anchor**: HEAD line ~2506. After `var mode = ResolveAtmMode(rule, acc.Name);`, before the `NinjaTrader.Code.Output.Process(` call.

Insert 6 lines verbatim:

```csharp
            // Lane D skip gate: if TryLaneDHook already dispatched this (order, follower) pair via ATM,
            // return immediately -- prevents one ATM entry (hook) + one raw entry (loop) double-dispatch.
            // _laneDDispatched.ContainsKey is O(1) lock-free (JS-021). No counter modification -- dedup
            // commit for non-ATM followers in this loop is unaffected.
            string laneD_key = baseSignal.OrderId + "#" + acc.Name;
            if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

CYC impact on `DispatchToFollower`: +1 for the `if ContainsKey` return. HEAD `DispatchToFollower` CYC was noted as CCN<=3 (comment line 2487). After insertion: CYC=4. Within JS-066 budget.

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

Note: The loop body replacement in Insertion 3 modifies lines but does not delete any method or property declaration.

---

### 7-Scan Checklist (T3)

- Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 2: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 3: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; T3 adds 0 new `return null;`
- Scan 4: CYC: `TryLaneDHook`=6, `TryLaneDDispatchFollower`=4. `DispatchCopy` CYC: report measured value (must be ≤ 8). `DispatchToFollower` CYC: was CCN<=3, now CYC=4. All ≤ 8. PASS.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in new lines. Note: `"#"` separator and `"|"` are ASCII-only (0x23, 0x7C). PASS.
- Scan 6: Manual check — confirm `TryLaneDHook`, `_laneDDispatched`, `TryLaneDDispatchFollower`, `SetLiveEntryDispatched` call in loop, Lane D skip gate all present
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T3)

All 5 gates. Confirm Gate 1 hunk count > 1 (4 apply_diff hunks expected: after line 2415, after line 2373, replace loop block, after line 2506). Gate 2 zero deletions. Gate 3 add count may be ~80 lines (T3 two methods + insertions).

### BUILD_PASS Criteria (T3)

1. `TryLaneDHook` method present in CopyEngine.cs
2. `TryLaneDDispatchFollower` method present in CopyEngine.cs
3. `TryLaneDHook(order, rule, baseSignal, baseQty)` call present in `DispatchCopy` after `baseQty` line
4. `dispatched` counter and `SetLiveEntryDispatched` call present in `DispatchCopy` loop
5. Lane D skip gate (`laneD_key` + `ContainsKey` return) present in `DispatchToFollower`
6. `DispatchCopy` CYC ≤ 8 (reported in completion.md)
7. All 7 scans pass
8. All 5 Ph4b verifier gates pass
9. Build PASS (only after T1-T5 all applied in same session)

---

# T4 — Lane E

**Ticket ID**: T4  
**Title**: Lane E — Re-arm ATM After Partial QX/BE (Group D: D2)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug fixed**: BUG2 (re-entry after partial Quick Exit / Break Even)  
**Type**: Method additions only  
**Depends on**: T1 (`_laneEDispatched`, `_followerSlateReady` fields must be present), T3 (`TryLaneDDispatchFollower` insertion point used as anchor for block placement)  
**Spec reference**: `00-orchestrator-prompt.md` section D2

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `CopyEngine.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

> **COMPILE ORDER NOTE**: Although T4 is listed after T1-T3, its methods (`TryLaneEHook`, `BuildLaneEKey`) are CALLED from T1 (`OnFollowerPositionUpdate`) and T2 (`CancelQxBrackets`). Apply T4 insertions IN THE SAME SESSION as T1 and T2 before building.

---

### Pre-Insert Verification

```powershell
# Confirm TryLaneDDispatchFollower was inserted by T3 (needed as insertion anchor)
grep -n "TryLaneDDispatchFollower" src/PropTraderTools/CopyEngine.cs
# Expected: method definition inserted by T3. Find the closing } of this method.
# T4 block inserts immediately after.

# Confirm GetCloneAtmMode vs ResolveAtmMode name (same check as T3)
grep -n "GetCloneAtmMode\|ResolveAtmMode" src/PropTraderTools/CopyEngine.cs
# Use the same name confirmed in T3.

# Confirm PassesDailyCapCheck and SendCopyWithAtm (same checks as T3)
grep -n "PassesDailyCapCheck" src/PropTraderTools/CopyEngine.cs
grep -n "SendCopyWithAtm" src/PropTraderTools/CopyEngine.cs
```

---

### Insertion — All 4 Lane E Methods (D2)

**Anchor**: After `TryLaneDDispatchFollower` ends (its closing `}`). Insert as a contiguous block.

**IMPORTANT**: `GetCloneAtmMode` in spec bodies must be replaced with the verified HEAD method name (`ResolveAtmMode` per T3 pre-insert verification). Two locations in this block use it.

Insert 4 methods verbatim:

```csharp
        // Lane E: eligibility predicate for TryLaneEHook. Absorbs all compound boolean guards so
        // TryLaneEHook contains zero || or && operators (JS-013 compliance).
        // CYC=8: entry(1) + if(1) + if+||(2) + if(1) + if+||(2) + if(1). Exactly at budget.
        // JS-001: no throw. JS-021: no lock. JS-002: returns bool, never null.
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook).
        private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // (1): non-Clone mode -> not eligible (Signal/Mirror/Market no-op)
            if (GetCopyMode() != CopyMode.Clone)
                return false;

            // (2): null guard -- || is +1 CYC per JS-013, isolated here intentionally
            if (pos == null || pos.Instrument == null)
                return false;

            // (3): no remaining position -> not a re-arm event (flat or zero-qty update)
            if (pos.Quantity <= 0)
                return false;

            // (4): gate must be closed (false) -- confirms post-QX/BE state.
            // Absent key (first session entry) treated as ready -> returns false -> no-op. Correct.
            // || is +1 CYC per JS-013, isolated here intentionally.
            bool ready;
            if (!_followerSlateReady.TryGetValue(acc.Name, out ready) || ready)
                return false;

            // (5): ATM template must be Named -- Inherit/unconfigured = no-op
            if (!(ResolveAtmMode(null, pos.Instrument.FullName) is FollowerAtmMode.Named))
                return false;

            return true;
        }

        // Lane E: re-arm ATM hook. Called from OnFollowerPositionUpdate after TrySetFollowerSlateReady.
        // Fires when follower has a remaining position (post-partial QX/BE) with gate closed and no ATM brackets.
        // CYC=5: (1)IsLaneEEligible gate,(2)dedup check,(3)ok gate-open. All compound guards in IsLaneEEligible.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        // JS-023: no UI update -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread (same as OnFollowerPositionUpdate).
        private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // Branch (1): eligibility gate -- all compound guards delegated to IsLaneEEligible
            if (!IsLaneEEligible(acc, pos))
                return;

            // Resolve ATM template and dedup key (assignments only, +0 CYC)
            string instrFullName = pos.Instrument.FullName;
            FollowerAtmMode.Named namedMode =
                ResolveAtmMode(null, instrFullName) as FollowerAtmMode.Named;
            string key = acc.Name + "#LANE-E#" + instrFullName;

            // Branch (2): dedup check -- only re-arm once per account+instrument slot
            if (_laneEDispatched.ContainsKey(key))
                return;

            // Dispatch: re-arm ATM bracket for the remaining position
            bool ok = TryLaneEDispatchFollower(acc, pos.Instrument, pos, namedMode, key);

            // Branch (3): gate open -- ATM is now armed; bracket sync is safe to proceed
            if (ok)
                _followerSlateReady[acc.Name] = true;
        }

        // Lane E: per-follower ATM re-arm helper. Keeps TryLaneEHook CYC low.
        // CYC=5: (A) acc null guard, (B) daily cap check, ?: action ternary, (C) SendCopyWithAtm result.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook caller).
        private bool TryLaneEDispatchFollower(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            NinjaTrader.Cbi.Position pos,
            FollowerAtmMode.Named namedMode,
            string dedupKey
        )
        {
            // Branch (A): null guard
            if (acc == null)
                return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc))
                return false;

            // Build signal from follower's remaining position
            OrderAction action = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long
                ? OrderAction.Buy
                : OrderAction.SellShort;
            string syntheticOrderId = "LANE-E#" + acc.Name + "#" + instr.FullName;
            CopySignal sig = CopySignal.Create(
                action,
                OrderType.Limit,
                pos.Quantity,       // remaining quantity (already multiplier-scaled from original entry)
                pos.AveragePrice,   // ATM bracket anchored at original entry price
                syntheticOrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, instr, in sig, namedMode);
            if (ok)
                _laneEDispatched.TryAdd(dedupKey, 0);
            return ok;
        }

        // Lane E: dedup key builder. Isolates the instr null-guard so CancelQxBrackets callers
        // incur +0 CYC from the null check. Called from both CancelQxBrackets overloads.
        // CYC=2: entry(1) + ternary null guard(1). ASCII-only. No lock (JS-021). static: no state.
        private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
        }
```

> **CYC FREEZE WARNING for `IsLaneEEligible`**: CYC=8 is AT THE JS-066 BUDGET. Any future branch addition to this method is a JS-066 violation. This method is FROZEN. Document in `ticket-4-completion.md`.

> **`ResolveAtmMode` call signature note**: The spec uses `GetCloneAtmMode(instrFullName)` (one argument). HEAD's `ResolveAtmMode` takes `(rule, accName)`. Verify the correct HEAD signature:  
> `grep -n "ResolveAtmMode" src/PropTraderTools/CopyEngine.cs`  
> Then adapt the 3 call sites in this block to match. If the signature differs from the spec, document the adaptation in `ticket-4-completion.md`. Do NOT introduce a compilation error.

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

---

### 7-Scan Checklist (T4)

- Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 2: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 3: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; T4 adds 0 new `return null;`
- Scan 4: CYC: `IsLaneEEligible`=8 (AT BUDGET, FROZEN), `TryLaneEHook`=5, `TryLaneEDispatchFollower`=5, `BuildLaneEKey`=2. All ≤ 8. PASS.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in new lines. Note: `"#LANE-E#"` and `"LANE-E#"` are ASCII-only. PASS.
- Scan 6: Manual check — confirm `IsLaneEEligible`, `TryLaneEHook`, `TryLaneEDispatchFollower`, `BuildLaneEKey` all present
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T4)

All 5 gates. Gate 1: hunk count > 1 (single contiguous insertion, but must use apply_diff — count ≥ 1; Lane A engineer applies T1-T5 together so actual commit may bundle all hunks).

### BUILD_PASS Criteria (T4)

1. All 4 Lane E methods present: `IsLaneEEligible`, `TryLaneEHook`, `TryLaneEDispatchFollower`, `BuildLaneEKey`
2. `IsLaneEEligible` CYC=8 documented as AT BUDGET in `ticket-4-completion.md`
3. `ResolveAtmMode` call sites adapted to match verified HEAD signature (documented if changed from spec)
4. All 7 scans pass
5. All 5 Ph4b verifier gates pass
6. Build PASS (only after T1-T5 all applied in same session)

---

# T5 — FlatGuardLoop

**Ticket ID**: T5  
**Title**: FlatGuardLoop (Group B: B1, B2)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Bug fixed**: BUG2 (flatten guard — prevents FlattenOneAccount on already-flat followers)  
**Type**: New static generic method + 2 call site replacements  
**Depends on**: Nothing in isolation (static method + call site changes are independent), but must be applied in same session as T1-T4 for build to succeed  
**Spec reference**: `00-orchestrator-prompt.md` sections B1, B2

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `CopyEngine.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

### Pre-Insert Verification

```powershell
# Confirm PttDragOrphanPredicates class location (B1 insertion anchor)
grep -n "PttDragOrphanPredicates" src/PropTraderTools/CopyEngine.cs
# Expected: line 6392 (class definition). Insertion goes BEFORE this line (before the class).
# HEAD confirmed: PttDragOrphanPredicates at line 6392, preceded by closing } of CopyEngine at 6384.

# Confirm Flatten(Instrument) foreach target (B2 first replacement)
grep -n "Flatten" src/PropTraderTools/CopyEngine.cs | head -20
# Expected: Flatten(Instrument) at line 4482. Foreach "FlattenOneAccount" at line 4489-4490.

# Confirm Flatten(Account,Instrument) follower foreach target (B2 second replacement)
# Expected: Flatten(Account,Instrument) at line 4518. Follower foreach at lines 4531-4536.

# Confirm FindPosition exists
grep -n "FindPosition" src/PropTraderTools/CopyEngine.cs
# Expected: at least 1 definition. Used in the FlatGuardLoop lambda in call sites.
```

---

### Insertion 1 — FlatGuardLoop<TAccount> method (B1)

**Anchor**: Before `PttDragOrphanPredicates` class (before HEAD line 6386 — the blank line before the `// B180:` comment). Insert at the end of the `CopyEngine` class body, after the last method, before the closing `}`.

Insert verbatim:

```csharp
        // BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
        // CYC=3: (1) foreach, (2) isFlat true-branch (skip), (3) isFlat false-branch (flattenOne).
        // JS-021: no lock (static, pure iteration). JS-001: no throw. ASCII-only.
        private static void FlatGuardLoop<TAccount>(
            Func<IEnumerable<TAccount>> getAccounts,
            Func<TAccount, bool> isFlat,
            Action<TAccount> flattenOne)
        {
            foreach (var acc in getAccounts())
            {
                if (isFlat(acc))
                {
                    NinjaTrader.Code.Output.Process(
                        "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    continue;
                }
                flattenOne(acc);
            }
        }
```

---

### Insertion 2 — Flatten(Instrument) call site replacement (B2)

**Anchor**: HEAD lines 4489-4490. Replace the foreach body inside `Flatten(Instrument)`.

**SEARCH** (exact):
```csharp
            foreach (var acc in AllAccounts(instrument))
                FlattenOneAccount(acc, instrument);
```

**REPLACE** with:
```csharp
            FlatGuardLoop(
                () => AllAccounts(instrument),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
```

CYC impact on `Flatten(Instrument)`: 0 (foreach replaced by delegate-passing call with same logical structure; FlatGuardLoop contains its own CYC).

---

### Insertion 3 — Flatten(Account,Instrument) follower foreach replacement (B2)

**Anchor**: HEAD lines 4531-4536. After `FlattenOneAccount(leader, instrument);` (HEAD line 4530), replace the follower foreach loop.

**SEARCH** (exact):
```csharp
            foreach (var acc in AllAccounts(instrument))
            {
                if (acc == leader)
                    continue;
                FlattenOneAccount(acc, instrument);
            }
```

**REPLACE** with:
```csharp
            FlatGuardLoop(
                () => AllAccounts(instrument).Where(acc => acc != leader),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
```

CYC impact on `Flatten(Account,Instrument)`: -1 (removes the `acc == leader` branch from the foreach; equivalent guard moved into the `Where` predicate, which is allocation-aware but correct for correctness purposes).

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

Note: The foreach loop bodies replaced in B2 are not symbol declarations (no `private/internal/public` method removed). These are call site modifications within existing methods.

---

### 7-Scan Checklist (T5)

- Scan 1: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 2: `grep -n "async void " src/PropTraderTools/CopyEngine.cs` → 0 results
- Scan 3: `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` → document all; T5 adds 0 new `return null;`
- Scan 4: CYC: `FlatGuardLoop<TAccount>`=3. `Flatten(Instrument)` and `Flatten(Account,Instrument)` CYC unchanged or reduced. All ≤ 8. PASS.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` → 0 results in new lines. `[FLAT-GUARD]` string is ASCII-only. PASS.
- Scan 6: Manual check — confirm `FlatGuardLoop`, `FlatGuardLoop` call sites in both `Flatten` overloads present
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T5)

All 5 gates. Gate 2: confirm the foreach removals are NOT symbol-level deletions (no method declarations removed).

### BUILD_PASS Criteria (T5)

1. `FlatGuardLoop<TAccount>` method present before `PttDragOrphanPredicates` class
2. `Flatten(Instrument)` uses `FlatGuardLoop(...)` call (not bare foreach)
3. `Flatten(Account,Instrument)` uses `FlatGuardLoop(...)` for follower iteration (not bare foreach)
4. All 7 scans pass
5. All 5 Ph4b verifier gates pass
6. Build PASS (with T1-T5 all applied in same session)

---

# T6 — Guardian Script + Protocol Hardening

**Ticket ID**: T6  
**Title**: Guardian Script + Protocol Hardening (Non-src)  
**Files**:
- `scripts/verify_critical_symbols.ps1` (create new file)
- `AGENTS.md` (append to Section 2)
- `docs/brain/regression-investigation-B179-B180.md` (status update)

**Type**: Non-src additions only  
**Depends on**: Nothing (independent of Lane A)  
**Spec reference**: `00-orchestrator-prompt.md` sections "GUARDIAN SCRIPT" and "PROTOCOL HARDENING"

---

> **write_file ban does NOT apply to this ticket** — no `.cs` files are touched.
> `write_file` is permitted for `scripts/verify_critical_symbols.ps1` (new file).
> `apply_diff` or `insert_content` is preferred for `AGENTS.md` and the regression doc (existing files).

---

### Task 1 — Create `scripts/verify_critical_symbols.ps1`

Create the file with the exact content from spec section "GUARDIAN SCRIPT". The script:
- Verifies 16 `$requiredEngine_B183` symbols present in `CopyEngine.cs`
- Verifies 12 `$requiredEngine_permanent` symbols present in `CopyEngine.cs`
- Verifies 6 `$requiredPanel` symbols present in `TradeCopierPanel.cs`
- Checks `_instr2tBtn.Background = BrushTeal` (BUG3 visual fix confirmation)
- Detects single-hunk commits on `CopyEngine.cs` (regression guard)
- Performs deletion audit (warns on deleted symbol declarations)
- Performs commit size gate (warns on >300 added/deleted lines)
- Exit 0 = PASS, Exit 1 = FAIL

Full script content verbatim from `docs/brain/B183/00-orchestrator-prompt.md` lines 776-886.

---

### Task 2 — Update `AGENTS.md` Section 2

Append to Section 2 (Architectural Mandates) the 4 protocol hardening items verbatim from spec section "PROTOCOL HARDENING":

```
- **Ph4b Deletion Audit (MANDATORY)**: Before VERIFY_PASS, engineer MUST run:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"`
  Every deleted symbol must have an authorising DW-card in the ticket. Zero tolerance.

- **Single-Hunk Ban**: `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` means
  a full-file replace was committed. This is ALWAYS a VERIFY_FAIL. Requires Director re-review.

- **Guardian Symbol Script**: After every .cs edit involving CopyEngine.cs, run:
  `powershell -File .\scripts\verify_critical_symbols.ps1`
  PASS required before VERIFY_PASS may be issued.

- **Commit Size Gate**: >300 changed lines on CopyEngine.cs requires Director explicit approval
  before Ph4b may issue VERIFY_PASS.
```

---

### Task 3 — Update `docs/brain/regression-investigation-B179-B180.md`

Append the following resolution status entry:

```
## B183 Resolution (2026-07-07)

All 16 missing symbols re-introduced surgically from f01b5155 source-of-truth.

- **BUG1 (cancel+reorder not copying)**: RESOLVED. Slate gate (TryMarkFollowerSlateNotReady +
  FindFollowerBracketOrder gate) + Lane D dispatch hook (TryLaneDHook) re-introduced.
- **BUG2 (re-entry after close)**: RESOLVED. FlatGuardLoop + Lane E re-arm (TryLaneEHook +
  IsLaneEEligible) + OnFollowerPositionUpdate PositionUpdate wiring re-introduced.
- **BUG3 (Quick2t/QAll2t colour + BE arm/disarm)**: RESOLVED. BrushTeal visual fix +
  OnPendingBeArmedDispatch/OnGlobalBeAllDisarmed/UpdateBeAllVisuals/DisarmBeAllOnFlat re-introduced.

Root cause confirmed: 878a5b33 megacommit was a single-hunk full-file replace that overwrote
f01b5155 (Sep 12) changes. Protocol hardening added to AGENTS.md to prevent recurrence:
single-hunk ban, deletion audit, guardian script, commit size gate.
```

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

---

### 7-Scan Checklist (T6)

Scans 1-5 and 7 do not apply (no `.cs` file edited). Run only:

- Scan 6: `powershell -File .\scripts\verify_critical_symbols.ps1` → exit 0 (this is the first time the script can be tested; it will FAIL until T1-T5 are applied and built)

Additional verification for T6 itself:
- Confirm `scripts/verify_critical_symbols.ps1` exists and is UTF-8 no-BOM
- Confirm AGENTS.md contains all 4 hardening bullet points
- Confirm `docs/brain/regression-investigation-B179-B180.md` contains B183 resolution entry

### BUILD_PASS Criteria (T6)

1. `scripts/verify_critical_symbols.ps1` exists and is syntactically valid PowerShell
2. Script exits 0 when all 16+12 symbols are present in `CopyEngine.cs` and all 6 in `TradeCopierPanel.cs` (after T1-T7 complete)
3. AGENTS.md contains 4 protocol hardening items
4. `docs/brain/regression-investigation-B179-B180.md` updated with B183 resolution status

---

# T7 — TradeCopierPanel.cs: BUG3 Visual + BE Arm/Disarm Events

**Ticket ID**: T7  
**Title**: TradeCopierPanel.cs — BUG3 Visual Fix + BE Arm/Disarm Event Re-add  
**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Bug fixed**: BUG3 (Quick2t/QAll2t colour regression + BE arm/disarm broken)  
**Type**: Inline visual fixes + method additions + event wiring  
**Depends on**: Independent of T1-T6 (Lane B)  
**Spec reference**: `00-orchestrator-prompt.md` sections "Ticket 7" sub-scopes 7A, 7B, 7C

---

> **ENGINEER INSTRUCTION (Constraint 8)**: Use `apply_diff` ONLY. `write_file` is BANNED on
> `TradeCopierPanel.cs`. A single-hunk commit = instant VERIFY_FAIL (Gate 1).

---

> **B180-2 CONSTRAINT (HARD STOP)**: The following symbols were approved for removal in B180-2
> by Director authority. They MUST NOT be re-introduced in T7:
> - `ResetBeStateOnFlat`
> - `ExecuteBeIdle`
> - `ExecuteBeArmed`
> - `UpdateBeVisuals`
> - `OnBeConnected`
>
> Before inserting any symbol from f01b5155, verify the symbol name is not in this list.
> If any symbol from the f01b5155 bodies matches a B180-2 removal: STOP. Report to orchestrator.

---

### Pre-Insert Verification

```powershell
# Confirm BuildInstrRow location and _instr2tBtn.SetResourceReference lines
grep -n "BuildInstrRow\|_instr2tBtn\|_instrQAll2tBtn" src/PropTraderTools/TradeCopierPanel.cs | head -30
# Expected: BuildInstrRow defined at ~line 1183; SetResourceReference calls at ~lines 1193 and 1204.

# Confirm specs tuple locations for BE, BE ALL, Quick, Quick ALL
grep -n "BrushInactive\|BrushTeal" src/PropTraderTools/TradeCopierPanel.cs | head -30
# Expected: lines 1079, 1089, 1099, 1109 show BrushInactive for 4 teal-button tuple entries.
# Confirm line 1149 has "btn.Foreground = BrushTeal" (to be changed to Brushes.White)
# Confirm line 1153 has "btn.Background = s.Bg" (to be changed to isTeal ternary)

# Confirm SubscribeEngineEvents exists in HEAD or determine correct wiring location
grep -n "SubscribeEngineEvents\|OnLoaded\|PendingBeArmed\|GlobalBeAllDisarmed" src/PropTraderTools/TradeCopierPanel.cs
# If SubscribeEngineEvents exists: use it as the wiring point for 7B event subscriptions.
# If not: use OnLoaded or the existing engine event subscription block.

# Confirm B180-2 removed symbols are NOT present in HEAD (sanity check)
grep -n "ResetBeStateOnFlat\|ExecuteBeIdle\|ExecuteBeArmed\|UpdateBeVisuals\|OnBeConnected" src/PropTraderTools/TradeCopierPanel.cs
# Expected: 0 results. If any found: do NOT re-introduce. These are B180-2 removals.

# Confirm PendingBeArmed and GlobalBeAllDisarmed events are accessible from CopyEngine
grep -n "PendingBeArmed\|GlobalBeAllDisarmed" src/PropTraderTools/CopyEngine.cs
# Expected: event declarations present. If absent: report to orchestrator -- cannot wire without source events.
```

---

### Sub-scope 7A — Visual Fix (Low Risk)

**Source of truth**: `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs` lines 1290-1315

#### Step 7A-1: BuildInstrRow — _instr2tBtn Background + Foreground

**Anchor**: After `_instr2tBtn.SetResourceReference(...)` (HEAD line ~1193), before `_instr2tBtn.Click +=`.

Insert 2 lines:
```csharp
            _instr2tBtn.Background = BrushTeal;
            _instr2tBtn.Foreground = System.Windows.Media.Brushes.White;
```

#### Step 7A-2: BuildInstrRow — _instrQAll2tBtn Background + Foreground

**Anchor**: After `_instrQAll2tBtn.SetResourceReference(...)` (HEAD line ~1204), before `_instrQAll2tBtn.Click +=`.

Insert 2 lines:
```csharp
            _instrQAll2tBtn.Background = BrushTeal;
            _instrQAll2tBtn.Foreground = System.Windows.Media.Brushes.White;
```

#### Step 7A-3: Specs tuple — 4 BrushInactive → BrushTeal changes

**Anchor**: HEAD lines 1079, 1089, 1099, 1109 (BE, BE ALL, Quick, Quick ALL tuple `Bg` fields).

For each of the 4 tuples, change `BrushInactive` to `BrushTeal` in the `Bg` field.

Engineer must verify the exact tuple line with:
```powershell
grep -n "BrushInactive" src/PropTraderTools/TradeCopierPanel.cs | head -20
```
Apply 4 separate `apply_diff` hunks — one per tuple line.

#### Step 7A-4: Specs foreach — Fix btn.Foreground when s.Teal == true

**Anchor**: HEAD line ~1149. Change `btn.Foreground = BrushTeal;` to:
```csharp
                    btn.Foreground = System.Windows.Media.Brushes.White;
```

#### Step 7A-5: Specs foreach — Fix btn.Background

**Anchor**: HEAD line ~1153. Change `btn.Background = s.Bg;` to:
```csharp
                bool isTeal = s.Teal;
                btn.Background = isTeal ? BrushTeal : BrushInactive; // AFTER style -- explicit brush wins
```

---

### Sub-scope 7B — BE Arm/Disarm Events (Medium Risk)

**Source of truth**: `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs` lines ~1350-1395 and ~1655-1665

Engineer MUST run:
```powershell
git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs | sed -n '1350,1395p'
git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs | sed -n '1655,1665p'
```
to obtain the verbatim method bodies before inserting.

#### Methods to re-add (verbatim from f01b5155):

1. `OnPendingBeArmedDispatch(string instr, string accountName)` — f01b5155 ~lines 1350-1365
2. `OnGlobalBeAllDisarmed()` — f01b5155 ~lines 1366-1370
3. `UpdateBeAllVisuals(BeState state)` — f01b5155 ~lines 1373-1395
4. `DisarmBeAllOnFlat(bool hasPosition)` — f01b5155 ~lines 1655-1665

#### Threading mandate (JS-023):

Any WPF property set inside `OnPendingBeArmedDispatch` and `OnGlobalBeAllDisarmed` MUST use `Dispatcher.InvokeAsync`. These fire from `CopyEngine` on the NT8 account background thread.

Verify before inserting: confirm f01b5155 bodies include `Dispatcher.InvokeAsync` wrapping. If they do not, add it. Do NOT use `lock()` or `Dispatcher.Invoke` (synchronous).

#### Event wiring in SubscribeEngineEvents() / OnLoaded():

```csharp
_engine.PendingBeArmed += OnPendingBeArmedDispatch;
_engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed;
```

And corresponding unsubscribe in the appropriate teardown method.

---

### Sub-scope 7C — Structural Extractions (Engineer's Discretion)

7C is OPTIONAL if 7A correctly fixes the inline loop. Engineer may defer if:
- The 7A visual fix reproduces the correct BrushTeal behaviour
- The 7B BE events are correctly wired

If `SubscribeEngineEvents()` is required as the 7B wiring point but does not exist in HEAD, it MUST be verified or created as part of 7B (not deferred).

Optional methods from f01b5155 that may be re-added if needed:
- `BuildSingleButtonCluster(...)` (~f01b5155 lines 1215-1260)
- `BuildQuickT3HiddenRow(root)` (~f01b5155 lines 1276-1286)
- `SubscribeEngineEvents()`, `BuildAllAccountsList()`, `RegisterAndInitializeModules()`, `WireLeaderOrderHandlers()` (~f01b5155 lines 890-960)

Note on `BuildBuyToggleButton`, `BuildSellToggleButton`, `BuildArmButton`, `BuildClickTraderCancelButton`: if their logic is correctly inlined in HEAD `BuildClickTraderRow`, do NOT re-extract. Only re-add if CYC of `BuildClickTraderRow` violates JS-066 (>8).

---

### Approved Deletions

| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |

---

### 7-Scan Checklist (T7)

- Scan 1: `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` → 0 results in new lines (JS-021 P0)
- Scan 2: `grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs` → 0 results in new lines (JS-033 P0)
- Scan 3: `grep -rn "return null;" src/PropTraderTools/TradeCopierPanel.cs` → document all; T7 should add 0 new `return null;`
- Scan 4: CYC for re-added methods: `OnPendingBeArmedDispatch`, `OnGlobalBeAllDisarmed`, `UpdateBeAllVisuals`, `DisarmBeAllOnFlat` — each must be ≤ 8. Verify and report in `ticket-7-completion.md`.
- Scan 5: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` → 0 results in new lines (JS-081)
- Scan 6: `powershell -File .\scripts\verify_critical_symbols.ps1` → exit 0 (after T6 creates script). Confirms `OnPendingBeArmedDispatch`, `OnGlobalBeAllDisarmed`, `UpdateBeAllVisuals`, `DisarmBeAllOnFlat`, `PendingBeArmed`, `GlobalBeAllDisarmed` all present. Also confirms `_instr2tBtn.Background = BrushTeal`.
- Scan 7: `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

### Ph4b Verifier Gates (T7)

**Gate 1** (write_file ban):
```powershell
(git show HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object).Count
```
Expected: Count > 1. (7A has ~7 separate hunks; 7B has method insertions + event wiring.)

**Gate 2** (deletion audit):
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs |
    Select-String "^-\s+(private|internal|public)" |
    ForEach-Object { $_.Line.Trim() }
```
For B183: expected zero deleted symbol declarations. ANY deletion must be explicitly listed in Approved Deletions with a DW-card. B180-2 removals (`ResetBeStateOnFlat` etc.) must NOT reappear.

**Gates 3, 4, 5**: Same as documented in Global section (substitute `TradeCopierPanel.cs` for `CopyEngine.cs` in Gate 3 command).

### BUILD_PASS Criteria (T7)

1. `_instr2tBtn.Background = BrushTeal` and `_instr2tBtn.Foreground = System.Windows.Media.Brushes.White` present after `SetResourceReference` in `BuildInstrRow`
2. Same for `_instrQAll2tBtn`
3. All 4 teal-button tuple entries have `BrushTeal` as Bg field (not `BrushInactive`)
4. Specs foreach uses `btn.Foreground = System.Windows.Media.Brushes.White` (not `BrushTeal`) when `s.Teal == true`
5. Specs foreach uses `isTeal ? BrushTeal : BrushInactive` for `btn.Background`
6. `OnPendingBeArmedDispatch`, `OnGlobalBeAllDisarmed`, `UpdateBeAllVisuals`, `DisarmBeAllOnFlat` all present in file
7. `_engine.PendingBeArmed += OnPendingBeArmedDispatch` and `_engine.GlobalBeAllDisarmed += OnGlobalBeAllDisarmed` wired
8. All BE event handler bodies use `Dispatcher.InvokeAsync` for any WPF property set
9. B180-2 removals (`ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected`) NOT present
10. Guardian script exits 0 (all 6 panel symbols confirmed)
11. All 7 scans pass
12. All 5 Ph4b verifier gates pass
13. `powershell -File .\scripts\ptt-sync.ps1` → 0 errors

---

## Ticket Summary

| Ticket | Title | File | Type | Bugs | Depends On |
|--------|-------|------|------|------|-----------|
| T1 | Slate Gate Fields + Methods | CopyEngine.cs | Add only | BUG1+BUG2 | None |
| T2 | Slate Gate Call Sites + Sub/Unsub | CopyEngine.cs | Insert + convert | BUG1+BUG2 | T1, T4 (for build) |
| T3 | Lane D | CopyEngine.cs | Add + modify | BUG1 | T1 |
| T4 | Lane E | CopyEngine.cs | Add only | BUG2 | T1, T3 (anchor) |
| T5 | FlatGuardLoop | CopyEngine.cs | Add + replace | BUG2 | None (same session) |
| T6 | Guardian Script + Protocol | scripts/ + AGENTS.md + docs/ | Non-src | N/A | None |
| T7 | TradeCopierPanel BUG3 | TradeCopierPanel.cs | Add + inline fix | BUG3 | None (Lane B) |

**Apply order for Lane A build**: T1 → T2 → T3 → T4 → T5 in a single editing session, then one build.  
**Lane B (T6, T7)**: May proceed independently in parallel with Lane A.

---

## TICKETS_COMPLETE
