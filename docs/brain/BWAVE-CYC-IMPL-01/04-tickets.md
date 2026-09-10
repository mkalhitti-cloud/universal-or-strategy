# BWAVE-CYC-IMPL-01 — Implementation Tickets
**Epic:** BWAVE-CYC-IMPL-01
**Phase:** 3 — Ticket Generation
**Author:** ptt-architect
**Input:** docs/brain/BWAVE-CYC-IMPL-01/02-architecture-plan.md (REVIEW_PASS)
**Closes:** DW-09-01 (from PTT-REPAIRS-09-OBFUSC-ATTR deferred backlog)
**Status:** TICKETS_COMPLETE

---

## Overview

70 private helper method stubs inserted into `src/PropTraderTools/CopyEngine.cs`.
5 tickets, each independently buildable and mergeable.
All tests remain `[Fact(Skip = "obfuscation: ...")]` after insertion — skip removal is DW-09-04 (out of scope).

| Ticket | Group | Methods | Test Class | Lines |
|--------|-------|---------|------------|-------|
| T1 | A | 24 (#1–#24) | B79CancelRaceGuardTests | private instance + 1 static |
| T2 | B | 12 (#25–#36) | BwaveCycT1R1BeHelperTests | private instance |
| T3 | C | 5 (#37–#41) | BwaveCycTaR2HelperTests | private instance |
| T4 | D | 24 (#42–#65) | BwaveCycTaR3HelperTests | private instance |
| T5 | E | 5 (#66–#70) | BwaveCycTaR6HelperTests | 3 static + 2 instance |

**Insertion anchor (all tickets):** Before the `private class PendingDispatchDrain` declaration in `CopyEngine.cs`. Search for `private class PendingDispatchDrain` — insert the group block immediately above that line. If a previous group's comment block is already present, insert this group's block immediately before `private class PendingDispatchDrain` (after any previously-inserted groups).

**Indentation:** 8 spaces (2 levels: namespace + class). Matches surrounding CopyEngine code.

---


---

### TICKET 1 — Group A: B79CancelRaceGuard Helpers — 24 Methods

**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group A)
**Epic:** BWAVE-CYC-IMPL-01
**Source Test Class(es):** B79CancelRaceGuardTests
**Source File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Insertion Anchor:** Before `private class PendingDispatchDrain` declaration. Search for the text `private class PendingDispatchDrain` in CopyEngine.cs and insert the Group A block immediately above it.

**Scope Lock:** TICKET 1 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files.

---

#### Methods to Implement (24 total)

All methods are `private instance` on `CopyEngine`, except method #20 which is `private static`.

**Method 1 — TryFireImmediateBeIfAlreadyAtLevel**
- Signature: `private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)
        { return false; }
```

**Method 2 — IsPendingBeTriggerMet**
- Signature: `private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)
        { return false; }
```

**Method 3 — IsEligibleBeTargetOrder**
- Signature: `private bool IsEligibleBeTargetOrder(Order order, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsEligibleBeTargetOrder(Order order, Instrument instr)
        { return false; }
```

**Method 4 — IsNativeAtmTargetOrder**
- Signature: `private bool IsNativeAtmTargetOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsNativeAtmTargetOrder(Order order)
        { return false; }
```

**Method 5 — IsPttBeOrQxTargetOrder**
- Signature: `private bool IsPttBeOrQxTargetOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttBeOrQxTargetOrder(Order order)
        { return false; }
```

**Method 6 — RegisterBeRetryIfNoTargets**
- Signature: `private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)
        { }
```

**Method 7 — RegisterPartialTargetBeRetry**
- Signature: `private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)
        { }
```

**Method 8 — CancelExistingStpDragOrders**
- Signature: `private void CancelExistingStpDragOrders(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelExistingStpDragOrders(Account acc, Instrument instr)
        { }
```

**Method 9 — CancelExistingTgtDragOrders**
- Signature: `private void CancelExistingTgtDragOrders(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelExistingTgtDragOrders(Account acc, Instrument instr)
        { }
```

**Method 10 — SubmitReplacementStopLeg**
- Signature: `private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)
        { }
```

**Method 11 — SubmitReplacementTargetLeg**
- Signature: `private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)
        { }
```

**Method 12 — IsReArmedAtmBracketCleanupRequired**
- Signature: `private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (plan note: uses explicit inline NonPublic|Instance)
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)
        { return false; }
```

**Method 13 — FindMatchingNativeAtmBracket**
- Signature: `private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)
        { return null; }
```

**Method 14 — TryFindRuleAndFollowerIndex**
- Signature: `private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Note: `out` parameter does not affect reflection name lookup. Test asserts `Assert.NotNull(m)` only.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)
        { followerIndex = -1; return false; }
```

**Method 15 — HasActiveQxOrdersForInstrument**
- Signature: `private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)
        { return false; }
```

**Method 16 — SyncAtmFollowerStopBracket**
- Signature: `private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)
        { }
```

**Method 17 — CancelStaleTgtDragOrders**
- Signature: `private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)
        { }
```

**Method 18 — CreateAndSubmitReplacementTarget**
- Signature: `private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)
        { return null; }
```

**Method 19 — HasInFlightFlattenOrder**
- Signature: `private bool HasInFlightFlattenOrder(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasInFlightFlattenOrder(Account acc, Instrument instr)
        { return false; }
```

**Method 20 — IsPositionFlatOrMissing** ⚠️ PRIVATE STATIC
- Signature: `private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Static` ← STATIC, not Instance
- CYC estimate: 1
- Implementation: Returns `true` as the safe sentinel (flat = no position).
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)
        { return true; }
```

**Method 21 — IsLeaderTargetOrder**
- Signature: `private bool IsLeaderTargetOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsLeaderTargetOrder(Order order)
        { return false; }
```

**Method 22 — ResubmitFollowerEntry**
- Signature: `private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Note: `CopyRule` is an inner class of `CopyEngine` — already in scope within CopyEngine.cs.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)
        { }
```

**Method 23 — IsLeaderAccountForInstrument**
- Signature: `private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)
        { return false; }
```

**Method 24 — CancelStaleCascadeTgtDrag**
- Signature: `private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)
        { }
```

---

#### Complete Insertion Block — T1

Insert the following block immediately before the line containing `private class PendingDispatchDrain`:

```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01 Group A: B79CancelRaceGuard helpers (24 methods).
        // All private instance except IsPositionFlatOrMissing (private static).
        // ObfuscationAttribute prevents AgileDotNetRT rename.
        // Stubs: reflection targets only. Production logic in follow-on epic.
        // JS-001: no throw. JS-021: no lock. JS-013: CYC=1 each. ASCII-only. .NET 4.8.
        // ===========================================================================

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsEligibleBeTargetOrder(Order order, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsNativeAtmTargetOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttBeOrQxTargetOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelExistingStpDragOrders(Account acc, Instrument instr)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelExistingTgtDragOrders(Account acc, Instrument instr)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)
        { return null; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)
        { followerIndex = -1; return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)
        { return null; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasInFlightFlattenOrder(Account acc, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)
        { return true; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsLeaderTargetOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)
        { }
```

---

#### V12 DNA Constraints (Ticket 1)

- No `lock()` — zero lock() calls in any Group A stub
- No `throw` — all stubs return false/null/void with no exceptions
- CYC <= 8 — all 24 methods CYC=1
- ASCII-only — all identifiers and string literals are 7-bit ASCII
- No `DateTime.Now` — no date/time access in any stub
- `.NET 4.8` — no switch expressions, no record types, no init accessors, no C# 8+ features
- `ObfuscationAttribute` on every method — confirmed: all 24 carry `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`

---

#### 7-Scan Checklist (Ticket 1)

| Scan | Rule | Check |
|------|------|-------|
| SCAN-01 | No lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-02 | No DateTime.Now | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-03 | ASCII-only | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-04 | No FontFamily | `grep -rn "FontFamily" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-05 | No hex colors | `grep -Prn "#[0-9A-Fa-f]{6}" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-06 | No throw | `grep -rn "\bthrow\b" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-07 | deploy-sync | `powershell -File .\deploy-sync.ps1` — 0 errors after src edit |

---

#### Build Verification (after T1 implementation)

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 Error(s)

dotnet test src/PropTraderTools/ --no-build --filter "FullyQualifiedName~B79CancelRaceGuardTests"
# Expected: all B79CancelRaceGuardTests still Skipped (Skip tags not removed in this epic)

dotnet test src/PropTraderTools/ --no-build
# Expected: Passed=24, Failed=0, Skipped=490, Total=514 (unchanged)
```

**Completion Artifact:** `docs/brain/BWAVE-CYC-IMPL-01/ticket-1-completion.md`

---


---

### TICKET 2 — Group B: T1R1 BE Trigger/Arming Helpers — 12 Methods

**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group B)
**Epic:** BWAVE-CYC-IMPL-01
**Source Test Class(es):** BwaveCycT1R1BeHelperTests
**Source File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Insertion Anchor:** Before `private class PendingDispatchDrain` declaration. Search for `private class PendingDispatchDrain` in CopyEngine.cs and insert the Group B block immediately above it (or after any Group A block already present).

**Scope Lock:** TICKET 2 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files.

---

#### Methods to Implement (12 total)

All 12 methods are `private instance` on `CopyEngine`.

⚠️ **CRITICAL:** `SelectBeRefPriceByDirection` (method #28) is **INVOKED** by tests — it has real logic. All other 11 methods are existence-only stubs. Do not swap the logic implementation or the tests will fail when un-skipped.

**Method 25 — GetMarketBidPrice**
- Signature: `private double GetMarketBidPrice(Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetMarketBidPrice(Instrument instr)
        { return 0.0; }
```

**Method 26 — GetMarketAskPrice**
- Signature: `private double GetMarketAskPrice(Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetMarketAskPrice(Instrument instr)
        { return 0.0; }
```

**Method 27 — GetBeTickSize**
- Signature: `private double GetBeTickSize(Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetBeTickSize(Instrument instr)
        { return 0.0; }
```

**Method 28 — SelectBeRefPriceByDirection** ⚠️ LOGIC REQUIRED (test invokes and asserts result)
- Signature: `private double SelectBeRefPriceByDirection(bool isLong, double bid, double ask)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 4 (three ternary decision points: 3 + 1 = CYC 4)
- Logic verified from test assertions:
  - `(true, 100.25, 100.50)` → 100.25 (long + bid positive → bid)
  - `(true, 0.0, 100.50)` → 100.50 (long + bid zero → ask fallback)
  - `(false, 100.25, 100.50)` → 100.50 (short + ask positive → ask)
  - `(false, 100.25, 0.0)` → 100.25 (short + ask zero → bid fallback)
- Implementation:
```csharp
        // SelectBeRefPriceByDirection: working implementation required -- test invokes and asserts result.
        // Long + bid>0 -> bid. Long + bid==0 -> ask. Short + ask>0 -> ask. Short + ask==0 -> bid.
        // CYC=4 (three ternary operators). JS-013 compliant (<=8).
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double SelectBeRefPriceByDirection(bool isLong, double bid, double ask)
        {
            return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);
        }
```

**Method 29 — FireBeAndNotifyEvent**
- Signature: `private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)
        { }
```

**Method 30 — ShouldFireBeImmediately**
- Signature: `private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)
        { return false; }
```

**Method 31 — CompleteBeArming**
- Signature: `private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)
        { }
```

**Method 32 — TryClaimPendingBeSlot**
- Signature: `private bool TryClaimPendingBeSlot(string accName, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryClaimPendingBeSlot(string accName, Instrument instr)
        { return false; }
```

**Method 33 — GetSlotInstrumentName**
- Signature: `private string GetSlotInstrumentName(string accName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private string GetSlotInstrumentName(string accName)
        { return string.Empty; }
```

**Method 34 — GetSlotAccountName**
- Signature: `private string GetSlotAccountName(string instrName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private string GetSlotAccountName(string instrName)
        { return string.Empty; }
```

**Method 35 — RaisePendingBeFiredEvent**
- Signature: `private void RaisePendingBeFiredEvent(string instrName, string accName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RaisePendingBeFiredEvent(string instrName, string accName)
        { }
```

**Method 36 — SettleAndFirePendingBe**
- Signature: `private void SettleAndFirePendingBe(string accName, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SettleAndFirePendingBe(string accName, Instrument instr)
        { }
```

---

#### Complete Insertion Block — T2

Insert the following block immediately before the line containing `private class PendingDispatchDrain` (or after any Group A block already inserted):

```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01 Group B: T1R1 BE trigger/arming helpers (12 methods).
        // All private instance. ObfuscationAttribute prevents AgileDotNetRT rename.
        // SelectBeRefPriceByDirection has working logic (test invokes it).
        // All others are stubs. JS-001: no throw. JS-021: no lock. ASCII-only. .NET 4.8.
        // ===========================================================================

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetMarketBidPrice(Instrument instr)
        { return 0.0; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetMarketAskPrice(Instrument instr)
        { return 0.0; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double GetBeTickSize(Instrument instr)
        { return 0.0; }

        // SelectBeRefPriceByDirection: working implementation required -- test invokes and asserts result.
        // Long + bid>0 -> bid. Long + bid==0 -> ask. Short + ask>0 -> ask. Short + ask==0 -> bid.
        // CYC=4 (three ternary operators). JS-013 compliant (<=8).
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private double SelectBeRefPriceByDirection(bool isLong, double bid, double ask)
        {
            return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);
        }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryClaimPendingBeSlot(string accName, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private string GetSlotInstrumentName(string accName)
        { return string.Empty; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private string GetSlotAccountName(string instrName)
        { return string.Empty; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void RaisePendingBeFiredEvent(string instrName, string accName)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SettleAndFirePendingBe(string accName, Instrument instr)
        { }
```

---

#### V12 DNA Constraints (Ticket 2)

- No `lock()` — zero lock() calls in any Group B method
- No `throw` — all methods return values or are void with no exceptions
- CYC <= 8 — max CYC=4 (SelectBeRefPriceByDirection); all others CYC=1
- ASCII-only — all identifiers and string literals are 7-bit ASCII
- No `DateTime.Now` — no date/time access in any method
- `.NET 4.8` — ternary expressions used (not switch expressions); no record types
- `ObfuscationAttribute` on every method — confirmed: all 12 carry the attribute

---

#### 7-Scan Checklist (Ticket 2)

| Scan | Rule | Check |
|------|------|-------|
| SCAN-01 | No lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-02 | No DateTime.Now | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-03 | ASCII-only | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-04 | No FontFamily | `grep -rn "FontFamily" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-05 | No hex colors | `grep -Prn "#[0-9A-Fa-f]{6}" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-06 | No throw | `grep -rn "\bthrow\b" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-07 | deploy-sync | `powershell -File .\deploy-sync.ps1` — 0 errors after src edit |

---

#### Build Verification (after T2 implementation)

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 Error(s)

dotnet test src/PropTraderTools/ --no-build --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"
# Expected: all BwaveCycT1R1BeHelperTests still Skipped (Skip tags not removed in this epic)

dotnet test src/PropTraderTools/ --no-build
# Expected: Passed=24, Failed=0, Skipped=490, Total=514 (unchanged)
```

**Completion Artifact:** `docs/brain/BWAVE-CYC-IMPL-01/ticket-2-completion.md`

---


---

### TICKET 3 — Group C: TaR2 Target-Selection Helpers — 5 Methods

**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group C)
**Epic:** BWAVE-CYC-IMPL-01
**Source Test Class(es):** BwaveCycTaR2HelperTests
**Source File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Insertion Anchor:** Before `private class PendingDispatchDrain` declaration. Search for `private class PendingDispatchDrain` in CopyEngine.cs and insert the Group C block immediately above it (or after any previously-inserted group blocks).

**Scope Lock:** TICKET 3 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files.

---

#### Methods to Implement (5 total)

All 5 methods are `private instance` on `CopyEngine`.

**Method 37 — HasValidTargetNameSuffix**
- Signature: `private bool HasValidTargetNameSuffix(string orderName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasValidTargetNameSuffix(string orderName)
        { return false; }
```

**Method 38 — SelectBeTargetList**
- Signature: `private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation: Returns an empty list (not null) so callers can safely iterate.
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)
        { return new System.Collections.Generic.List<Order>(); }
```

**Method 39 — IsBeTargetActiveState**
- Signature: `private bool IsBeTargetActiveState(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetActiveState(Order order)
        { return false; }
```

**Method 40 — IsBeTargetPendingChangeState**
- Signature: `private bool IsBeTargetPendingChangeState(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetPendingChangeState(Order order)
        { return false; }
```

**Method 41 — IsBeTargetSnapshotState**
- Signature: `private bool IsBeTargetSnapshotState(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetSnapshotState(Order order)
        { return false; }
```

---

#### Complete Insertion Block — T3

Insert the following block immediately before the line containing `private class PendingDispatchDrain` (or after any previously-inserted group blocks):

```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01 Group C: TaR2 target-selection helpers (5 methods).
        // All private instance stubs. ObfuscationAttribute prevents AgileDotNetRT rename.
        // JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only. .NET 4.8.
        // ===========================================================================

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool HasValidTargetNameSuffix(string orderName)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)
        { return new System.Collections.Generic.List<Order>(); }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetActiveState(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetPendingChangeState(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeTargetSnapshotState(Order order)
        { return false; }
```

---

#### V12 DNA Constraints (Ticket 3)

- No `lock()` — zero lock() calls in any Group C stub
- No `throw` — all stubs return false or empty list with no exceptions
- CYC <= 8 — all 5 methods CYC=1
- ASCII-only — all identifiers and string literals are 7-bit ASCII
- No `DateTime.Now` — no date/time access in any stub
- `.NET 4.8` — no switch expressions, no record types
- `ObfuscationAttribute` on every method — confirmed: all 5 carry the attribute

---

#### 7-Scan Checklist (Ticket 3)

| Scan | Rule | Check |
|------|------|-------|
| SCAN-01 | No lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-02 | No DateTime.Now | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-03 | ASCII-only | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-04 | No FontFamily | `grep -rn "FontFamily" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-05 | No hex colors | `grep -Prn "#[0-9A-Fa-f]{6}" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-06 | No throw | `grep -rn "\bthrow\b" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-07 | deploy-sync | `powershell -File .\deploy-sync.ps1` — 0 errors after src edit |

---

#### Build Verification (after T3 implementation)

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 Error(s)

dotnet test src/PropTraderTools/ --no-build --filter "FullyQualifiedName~BwaveCycTaR2HelperTests"
# Expected: all BwaveCycTaR2HelperTests still Skipped (Skip tags not removed in this epic)

dotnet test src/PropTraderTools/ --no-build
# Expected: Passed=24, Failed=0, Skipped=490, Total=514 (unchanged)
```

**Completion Artifact:** `docs/brain/BWAVE-CYC-IMPL-01/ticket-3-completion.md`

---


---

### TICKET 4 — Group D: TaR3 Sync/Drag/Bracket Helpers — 24 Methods

**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group D)
**Epic:** BWAVE-CYC-IMPL-01
**Source Test Class(es):** BwaveCycTaR3HelperTests
**Source File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Insertion Anchor:** Before `private class PendingDispatchDrain` declaration. Search for `private class PendingDispatchDrain` in CopyEngine.cs and insert the Group D block immediately above it (or after any previously-inserted group blocks).

**Scope Lock:** TICKET 4 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files.

---

#### Methods to Implement (24 total)

All 24 methods are `private instance` on `CopyEngine`.

Note: `CopyRule` is an inner class of `CopyEngine` — it is in scope within CopyEngine.cs without any using directive. Do NOT add a using directive for CopyRule.

Note on `out` parameters: `TryGetCleanupEntryForFollower` uses `out object entry` and `TryMatchFollowerInRule` uses `out int followerIndex`. The `out` parameter does not affect `GetMethod` name lookup. Tests assert `Assert.NotNull(m)` only. The `object` placeholder type for `entry` will be replaced by the correct internal type in the follow-on logic epic.

**Method 42 — TrySyncAtmBrackets**
- Signature: `private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (explicit inline in test)
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)
        { return false; }
```

**Method 43 — TrySkipTrailingStop**
- Signature: `private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)
        { return false; }
```

**Method 44 — SyncStandardBracket**
- Signature: `private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)
        { }
```

**Method 45 — IsPttTgtDragOrder**
- Signature: `private bool IsPttTgtDragOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttTgtDragOrder(Order order)
        { return false; }
```

**Method 46 — IsAtmTgtOrder**
- Signature: `private bool IsAtmTgtOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsAtmTgtOrder(Order order)
        { return false; }
```

**Method 47 — IsBePendingTargetOrder**
- Signature: `private bool IsBePendingTargetOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBePendingTargetOrder(Order order)
        { return false; }
```

**Method 48 — IsPttBeStopRejected**
- Signature: `private bool IsPttBeStopRejected(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttBeStopRejected(Order order)
        { return false; }
```

**Method 49 — IsPttDragOrderCancellable**
- Signature: `private bool IsPttDragOrderCancellable(Order order, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttDragOrderCancellable(Order order, Instrument instr)
        { return false; }
```

**Method 50 — IsPttQxTargetOrder**
- Signature: `private bool IsPttQxTargetOrder(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttQxTargetOrder(Order order)
        { return false; }
```

**Method 51 — IsNativeAtmBeRetryTarget**
- Signature: `private bool IsNativeAtmBeRetryTarget(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsNativeAtmBeRetryTarget(Order order)
        { return false; }
```

**Method 52 — IsBeRetryEligibleOrderState**
- Signature: `private bool IsBeRetryEligibleOrderState(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeRetryEligibleOrderState(Order order)
        { return false; }
```

**Method 53 — IsBeRetryOrderInvalid**
- Signature: `private bool IsBeRetryOrderInvalid(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeRetryOrderInvalid(Order order)
        { return false; }
```

**Method 54 — IsBeSlotNonTerminal**
- Signature: `private bool IsBeSlotNonTerminal(string accName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeSlotNonTerminal(string accName)
        { return false; }
```

**Method 55 — IsBeFilledWithOpenPosition**
- Signature: `private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)
        { return false; }
```

**Method 56 — IsPttDragOrderName**
- Signature: `private bool IsPttDragOrderName(string orderName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttDragOrderName(string orderName)
        { return false; }
```

**Method 57 — IsDragInstrumentMatch**
- Signature: `private bool IsDragInstrumentMatch(Order order, Instrument instr)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsDragInstrumentMatch(Order order, Instrument instr)
        { return false; }
```

**Method 58 — IsQxTOrderStateValid**
- Signature: `private bool IsQxTOrderStateValid(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsQxTOrderStateValid(Order order)
        { return false; }
```

**Method 59 — IsQxTBracketNameValid**
- Signature: `private bool IsQxTBracketNameValid(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsQxTBracketNameValid(Order order)
        { return false; }
```

**Method 60 — TryGetCleanupEntryForFollower**
- Signature: `private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (explicit inline in test)
- CYC estimate: 1
- Note: `out object entry` is a placeholder type. The follow-on logic epic will replace `object` with the correct internal cleanup-entry type. Do NOT change the method NAME — that is the reflection target.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)
        { entry = null; return false; }
```

**Method 61 — IsCleanupEntryCurrentAndMatching**
- Signature: `private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (explicit inline in test)
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)
        { return false; }
```

**Method 62 — SendAtmCancelReplace**
- Signature: `private void SendAtmCancelReplace(Account acc, Order order, double newPrice)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SendAtmCancelReplace(Account acc, Order order, double newPrice)
        { }
```

**Method 63 — TryMatchFollowerInRule**
- Signature: `private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Note: `out` parameter does not affect `GetMethod` name lookup.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)
        { followerIndex = -1; return false; }
```

**Method 64 — IsBeReplaceTargetValid**
- Signature: `private bool IsBeReplaceTargetValid(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeReplaceTargetValid(Order order)
        { return false; }
```

**Method 65 — TryIncrementBeReplaceAttempt**
- Signature: `private bool TryIncrementBeReplaceAttempt(string accName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance`
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryIncrementBeReplaceAttempt(string accName)
        { return false; }
```

---

#### Complete Insertion Block — T4

Insert the following block immediately before the line containing `private class PendingDispatchDrain` (or after any previously-inserted group blocks):

```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01 Group D: TaR3 sync/drag/bracket and BE-retry helpers (24 methods).
        // All private instance stubs. ObfuscationAttribute prevents AgileDotNetRT rename.
        // JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only. .NET 4.8.
        // ===========================================================================

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttTgtDragOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsAtmTgtOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBePendingTargetOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttBeStopRejected(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttDragOrderCancellable(Order order, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttQxTargetOrder(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsNativeAtmBeRetryTarget(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeRetryEligibleOrderState(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeRetryOrderInvalid(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeSlotNonTerminal(string accName)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsPttDragOrderName(string orderName)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsDragInstrumentMatch(Order order, Instrument instr)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsQxTOrderStateValid(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsQxTBracketNameValid(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)
        { entry = null; return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void SendAtmCancelReplace(Account acc, Order order, double newPrice)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)
        { followerIndex = -1; return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool IsBeReplaceTargetValid(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private bool TryIncrementBeReplaceAttempt(string accName)
        { return false; }
```

---

#### V12 DNA Constraints (Ticket 4)

- No `lock()` — zero lock() calls in any Group D stub
- No `throw` — all stubs return false/null/void with no exceptions
- CYC <= 8 — all 24 methods CYC=1
- ASCII-only — all identifiers and string literals are 7-bit ASCII
- No `DateTime.Now` — no date/time access in any stub
- `.NET 4.8` — no switch expressions, no record types
- `ObfuscationAttribute` on every method — confirmed: all 24 carry the attribute

---

#### 7-Scan Checklist (Ticket 4)

| Scan | Rule | Check |
|------|------|-------|
| SCAN-01 | No lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-02 | No DateTime.Now | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-03 | ASCII-only | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-04 | No FontFamily | `grep -rn "FontFamily" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-05 | No hex colors | `grep -Prn "#[0-9A-Fa-f]{6}" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-06 | No throw | `grep -rn "\bthrow\b" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-07 | deploy-sync | `powershell -File .\deploy-sync.ps1` — 0 errors after src edit |

---

#### Build Verification (after T4 implementation)

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 Error(s)

dotnet test src/PropTraderTools/ --no-build --filter "FullyQualifiedName~BwaveCycTaR3HelperTests"
# Expected: all BwaveCycTaR3HelperTests still Skipped (Skip tags not removed in this epic)

dotnet test src/PropTraderTools/ --no-build
# Expected: Passed=24, Failed=0, Skipped=490, Total=514 (unchanged)
```

**Completion Artifact:** `docs/brain/BWAVE-CYC-IMPL-01/ticket-4-completion.md`

---


---

### TICKET 5 — Group E: TaR6 Static/Instance Predicate Helpers — 5 Methods

**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group E)
**Epic:** BWAVE-CYC-IMPL-01
**Source Test Class(es):** BwaveCycTaR6HelperTests
**Source File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
**Insertion Anchor:** Before `private class PendingDispatchDrain` declaration. Search for `private class PendingDispatchDrain` in CopyEngine.cs and insert the Group E block immediately above it (or after any previously-inserted group blocks).

**Scope Lock:** TICKET 5 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files.

---

#### Methods to Implement (5 total)

⚠️ **MIXED ACCESS — READ CAREFULLY:**
- `IsBracketOrderLiveState` (#66): **private STATIC** — test uses `GetStaticMethod` helper
- `MatchesPttReplacementName` (#67): **private STATIC** — test uses `GetStaticMethod` helper
- `LogHbcDiag` (#68): **private instance** — test uses `GetInstanceMethod` helper
- `ExecuteStopDragOrder` (#69): **private instance** — test uses `GetInstanceMethod` helper
- `IsOrderEventProcessable` (#70): **private STATIC** — test uses `GetStaticMethod` helper

⚠️ **FIXED PARAMETER COUNTS — DO NOT CHANGE:**
- `IsBracketOrderLiveState`: exactly 1 parameter (verified by test assertion)
- `MatchesPttReplacementName`: exactly 3 parameters (verified by test assertion)
- `LogHbcDiag`: exactly 5 parameters (verified by test assertion)
- `ExecuteStopDragOrder`: exactly 5 parameters (verified by test assertion)
- `IsOrderEventProcessable`: exactly 1 parameter (verified by test assertion)

**Method 66 — IsBracketOrderLiveState** ⚠️ PRIVATE STATIC — 1 param
- Signature: `private static bool IsBracketOrderLiveState(Order order)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Static` (test uses GetStaticMethod)
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsBracketOrderLiveState(Order order)
        { return false; }
```

**Method 67 — MatchesPttReplacementName** ⚠️ PRIVATE STATIC — 3 params
- Signature: `private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Static` (test uses GetStaticMethod)
- CYC estimate: 1
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)
        { return false; }
```

**Method 68 — LogHbcDiag** ⚠️ PRIVATE INSTANCE — 5 params
- Signature: `private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (test uses GetInstanceMethod)
- CYC estimate: 1
- Note: Exactly 5 parameters required. `CopyRule` is an inner class of `CopyEngine` — in scope without using directive.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)
        { }
```

**Method 69 — ExecuteStopDragOrder** ⚠️ PRIVATE INSTANCE — 5 params
- Signature: `private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Instance` (test uses GetInstanceMethod)
- CYC estimate: 1
- Note: Exactly 5 parameters required. `CopyRule` is an inner class of `CopyEngine` — in scope without using directive.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)
        { }
```

**Method 70 — IsOrderEventProcessable** ⚠️ PRIVATE STATIC — 1 param
- Signature: `private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)`
- ObfuscationAttribute: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Access source: `BindingFlags.NonPublic | BindingFlags.Static` (test uses GetStaticMethod)
- CYC estimate: 1
- Note: Use the fully-qualified type `NinjaTrader.Cbi.OrderEventArgs`. This type is in scope via the existing `using NinjaTrader.Cbi;` directive at the top of CopyEngine.cs — but using the fully-qualified form is safe either way.
- Implementation:
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)
        { return false; }
```

---

#### Complete Insertion Block — T5

Insert the following block immediately before the line containing `private class PendingDispatchDrain` (or after any previously-inserted group blocks):

```csharp
        // ===========================================================================
        // BWAVE-CYC-IMPL-01 Group E: TaR6 static predicate and instance helpers (5 methods).
        // IsBracketOrderLiveState, MatchesPttReplacementName, IsOrderEventProcessable: private static.
        // LogHbcDiag, ExecuteStopDragOrder: private instance.
        // ObfuscationAttribute prevents AgileDotNetRT rename.
        // JS-001: no throw. JS-021: no lock. JS-013: CYC=1. ASCII-only. .NET 4.8.
        // ===========================================================================

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsBracketOrderLiveState(Order order)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)
        { return false; }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)
        { }

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)
        { return false; }
```

---

#### V12 DNA Constraints (Ticket 5)

- No `lock()` — zero lock() calls in any Group E stub
- No `throw` — all stubs return false or are void with no exceptions
- CYC <= 8 — all 5 methods CYC=1
- ASCII-only — all identifiers and string literals are 7-bit ASCII
- No `DateTime.Now` — no date/time access in any stub
- `.NET 4.8` — no switch expressions, no record types
- `ObfuscationAttribute` on every method — confirmed: all 5 carry the attribute
- Static vs Instance correctly assigned — 3 static + 2 instance (see binding flags above)

---

#### 7-Scan Checklist (Ticket 5)

| Scan | Rule | Check |
|------|------|-------|
| SCAN-01 | No lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-02 | No DateTime.Now | `grep -rn "DateTime.Now" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-03 | ASCII-only | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-04 | No FontFamily | `grep -rn "FontFamily" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-05 | No hex colors | `grep -Prn "#[0-9A-Fa-f]{6}" src/PropTraderTools/CopyEngine.cs` — 0 matches in added methods |
| SCAN-06 | No throw | `grep -rn "\bthrow\b" src/PropTraderTools/CopyEngine.cs` — 0 new matches in added methods |
| SCAN-07 | deploy-sync | `powershell -File .\deploy-sync.ps1` — 0 errors after src edit |

---

#### Build Verification (after T5 implementation)

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 Error(s)

dotnet test src/PropTraderTools/ --no-build --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"
# Expected: all BwaveCycTaR6HelperTests still Skipped (Skip tags not removed in this epic)

dotnet test src/PropTraderTools/ --no-build
# Expected: Passed=24, Failed=0, Skipped=490, Total=514 (unchanged)
```

**Completion Artifact:** `docs/brain/BWAVE-CYC-IMPL-01/ticket-5-completion.md`

---

## Out-of-Scope Reminders (DO NOT implement in this epic)

| Item | Reason |
|------|--------|
| Remove any `[Fact(Skip = "obfuscation: ...")]` annotation | DW-09-04 — separate epic |
| Fix `LogBeSlotEviction` binding flags in BwaveCycTaR3HelperTests | DW-09-02 — separate epic |
| Fix `GetSenderAccountName` binding flags in BwaveCycTaR2HelperTests | DW-09-03 — separate epic |
| Add second `LogBeSlotEviction` method | FORBIDDEN — already exists at L1779 as private static |
| Implement production logic for any stub | BWAVE-CYC-LOGIC — follow-on engineering epic |
| Modify any existing method in CopyEngine.cs | FORBIDDEN — pure insertion only |
| Create any new .cs files | FORBIDDEN — not part of this epic |

---

## TICKETS_COMPLETE

**Total methods:** 70 across 5 tickets
**All methods:** Insertion-only into `src/PropTraderTools/CopyEngine.cs`
**All stubs compile:** .NET 4.8 compatible, no C# 8+ features
**All methods carry ObfuscationAttribute:** 70 of 70
**Static methods (4):** IsPositionFlatOrMissing (T1#20), IsBracketOrderLiveState (T5#66), MatchesPttReplacementName (T5#67), IsOrderEventProcessable (T5#70)
**Logic method (1):** SelectBeRefPriceByDirection (T2#28) — CYC=4, test invokes and asserts
**All other 65 methods:** CYC=1 sentinel stubs
**Test baseline unchanged:** Passed=24, Failed=0, Skipped=490, Total=514
