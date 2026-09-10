# BWAVE-CYC-IMPL-01 — Final Review (Phase 5)

**Epic:** BWAVE-CYC-IMPL-01
**Phase:** 5 — Final Cross-File Coherence Review
**Reviewer:** ptt-plan-reviewer
**Source:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace — READ ONLY)
**Spec Closed:** DW-09-01 (from PTT-REPAIRS-09-OBFUSC-ATTR/06-deferred-backlog.md)

---

## VERDICT: FINAL_PASS

All checks pass. Zero Jane Street DNA violations. All 70 methods present, attributed, and correctly typed. Test baseline preserved. Deferred work documented in Section K and 06-deferred-backlog.md.

---

## Check 1 — Method Count: PASS

All 70 helper methods confirmed present in `src/PropTraderTools/CopyEngine.cs`.

### Group A — B79CancelRaceGuard Helpers (24 methods, lines 7885–7979)

Block header: lines 7877–7883 (BWAVE-CYC-IMPL-01 Group A comment block)

| # | Method | Line | Access | ObfuscAttr Line | Return | CYC |
|---|--------|------|--------|-----------------|--------|-----|
| 1 | `TryFireImmediateBeIfAlreadyAtLevel` | 7886 | private instance | 7885 | bool | 1 |
| 2 | `IsPendingBeTriggerMet` | 7890 | private instance | 7889 | bool | 1 |
| 3 | `IsEligibleBeTargetOrder` | 7894 | private instance | 7893 | bool | 1 |
| 4 | `IsNativeAtmTargetOrder` | 7898 | private instance | 7897 | bool | 1 |
| 5 | `IsPttBeOrQxTargetOrder` | 7902 | private instance | 7901 | bool | 1 |
| 6 | `RegisterBeRetryIfNoTargets` | 7906 | private instance | 7905 | void | 1 |
| 7 | `RegisterPartialTargetBeRetry` | 7910 | private instance | 7909 | void | 1 |
| 8 | `CancelExistingStpDragOrders` | 7914 | private instance | 7913 | void | 1 |
| 9 | `CancelExistingTgtDragOrders` | 7918 | private instance | 7917 | void | 1 |
| 10 | `SubmitReplacementStopLeg` | 7922 | private instance | 7921 | void | 1 |
| 11 | `SubmitReplacementTargetLeg` | 7926 | private instance | 7925 | void | 1 |
| 12 | `IsReArmedAtmBracketCleanupRequired` | 7930 | private instance | 7929 | bool | 1 |
| 13 | `FindMatchingNativeAtmBracket` | 7934 | private instance | 7933 | Order (null) | 1 |
| 14 | `TryFindRuleAndFollowerIndex` | 7938 | private instance | 7937 | bool (out -1) | 1 |
| 15 | `HasActiveQxOrdersForInstrument` | 7942 | private instance | 7941 | bool | 1 |
| 16 | `SyncAtmFollowerStopBracket` | 7946 | private instance | 7945 | void | 1 |
| 17 | `CancelStaleTgtDragOrders` | 7950 | private instance | 7949 | void | 1 |
| 18 | `CreateAndSubmitReplacementTarget` | 7954 | private instance | 7953 | Order (null) | 1 |
| 19 | `HasInFlightFlattenOrder` | 7958 | private instance | 7957 | bool | 1 |
| 20 | `IsPositionFlatOrMissing` | 7962 | **private static** | 7961 | bool (true) | 1 |
| 21 | `IsLeaderTargetOrder` | 7966 | private instance | 7965 | bool | 1 |
| 22 | `ResubmitFollowerEntry` | 7970 | private instance | 7969 | void | 1 |
| 23 | `IsLeaderAccountForInstrument` | 7974 | private instance | 7973 | bool | 1 |
| 24 | `CancelStaleCascadeTgtDrag` | 7978 | private instance | 7977 | void | 1 |

**Group A: 24/24 present. All ObfuscationAttribute lines confirmed. PASS.**

---

### Group B — T1R1 BE Trigger/Arming Helpers (12 methods, lines 7989–8040)

Block header: lines 7982–7987

| # | Method | Line | Access | ObfuscAttr Line | Return | CYC |
|---|--------|------|--------|-----------------|--------|-----|
| 25 | `GetMarketBidPrice` | 7990 | private instance | 7989 | double (0.0) | 1 |
| 26 | `GetMarketAskPrice` | 7994 | private instance | 7993 | double (0.0) | 1 |
| 27 | `GetBeTickSize` | 7998 | private instance | 7997 | double (0.0) | 1 |
| 28 | `SelectBeRefPriceByDirection` | 8005 | private instance | 8004 | double (ternary) | **4** |
| 29 | `FireBeAndNotifyEvent` | 8011 | private instance | 8010 | void | 1 |
| 30 | `ShouldFireBeImmediately` | 8015 | private instance | 8014 | bool | 1 |
| 31 | `CompleteBeArming` | 8019 | private instance | 8018 | void | 1 |
| 32 | `TryClaimPendingBeSlot` | 8023 | private instance | 8022 | bool | 1 |
| 33 | `GetSlotInstrumentName` | 8027 | private instance | 8026 | string.Empty | 1 |
| 34 | `GetSlotAccountName` | 8031 | private instance | 8030 | string.Empty | 1 |
| 35 | `RaisePendingBeFiredEvent` | 8035 | private instance | 8034 | void | 1 |
| 36 | `SettleAndFirePendingBe` | 8039 | private instance | 8038 | void | 1 |

**SelectBeRefPriceByDirection logic verified:** `return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);` — CYC=4, compliant with JS-013 (<=8).
**Group B: 12/12 present. All ObfuscationAttribute lines confirmed. PASS.**

---

### Group C — TaR2 Target-Selection Helpers (5 methods, lines 8048–8066)

Block header: lines 8042–8046

| # | Method | Line | Access | ObfuscAttr Line | Return | CYC |
|---|--------|------|--------|-----------------|--------|-----|
| 37 | `HasValidTargetNameSuffix` | 8049 | private instance | 8048 | bool | 1 |
| 38 | `SelectBeTargetList` | 8053 | private instance | 8052 | IList<Order> (empty) | 1 |
| 39 | `IsBeTargetActiveState` | 8057 | private instance | 8056 | bool | 1 |
| 40 | `IsBeTargetPendingChangeState` | 8061 | private instance | 8060 | bool | 1 |
| 41 | `IsBeTargetSnapshotState` | 8065 | private instance | 8064 | bool | 1 |

**SelectBeTargetList returns non-null empty list (not null) — JS-002 compliant.**
**Group C: 5/5 present. All ObfuscationAttribute lines confirmed. PASS.**

---

### Group D — TaR3 Sync/Drag/Bracket Helpers (24 methods, lines 8075–8169)

Block header: lines 8069–8073

| # | Method | Line | Access | ObfuscAttr Line | Return | CYC |
|---|--------|------|--------|-----------------|--------|-----|
| 42 | `TrySyncAtmBrackets` | 8076 | private instance | 8075 | bool | 1 |
| 43 | `TrySkipTrailingStop` | 8080 | private instance | 8079 | bool | 1 |
| 44 | `SyncStandardBracket` | 8084 | private instance | 8083 | void | 1 |
| 45 | `IsPttTgtDragOrder` | 8088 | private instance | 8087 | bool | 1 |
| 46 | `IsAtmTgtOrder` | 8092 | private instance | 8091 | bool | 1 |
| 47 | `IsBePendingTargetOrder` | 8096 | private instance | 8095 | bool | 1 |
| 48 | `IsPttBeStopRejected` | 8100 | private instance | 8099 | bool | 1 |
| 49 | `IsPttDragOrderCancellable` | 8104 | private instance | 8103 | bool | 1 |
| 50 | `IsPttQxTargetOrder` | 8108 | private instance | 8107 | bool | 1 |
| 51 | `IsNativeAtmBeRetryTarget` | 8112 | private instance | 8111 | bool | 1 |
| 52 | `IsBeRetryEligibleOrderState` | 8116 | private instance | 8115 | bool | 1 |
| 53 | `IsBeRetryOrderInvalid` | 8120 | private instance | 8119 | bool | 1 |
| 54 | `IsBeSlotNonTerminal` | 8124 | private instance | 8123 | bool | 1 |
| 55 | `IsBeFilledWithOpenPosition` | 8128 | private instance | 8127 | bool | 1 |
| 56 | `IsPttDragOrderName` | 8132 | private instance | 8131 | bool | 1 |
| 57 | `IsDragInstrumentMatch` | 8136 | private instance | 8135 | bool | 1 |
| 58 | `IsQxTOrderStateValid` | 8140 | private instance | 8139 | bool | 1 |
| 59 | `IsQxTBracketNameValid` | 8144 | private instance | 8143 | bool | 1 |
| 60 | `TryGetCleanupEntryForFollower` | 8148 | private instance | 8147 | bool (out null) | 1 |
| 61 | `IsCleanupEntryCurrentAndMatching` | 8152 | private instance | 8151 | bool | 1 |
| 62 | `SendAtmCancelReplace` | 8156 | private instance | 8155 | void | 1 |
| 63 | `TryMatchFollowerInRule` | 8160 | private instance | 8159 | bool (out -1) | 1 |
| 64 | `IsBeReplaceTargetValid` | 8164 | private instance | 8163 | bool | 1 |
| 65 | `TryIncrementBeReplaceAttempt` | 8168 | private instance | 8167 | bool | 1 |

**Out-parameter methods: `TryGetCleanupEntryForFollower` (entry=null), `TryMatchFollowerInRule` (followerIndex=-1) — both assign out before return per .NET 4.8 requirement. PASS.**
**Group D: 24/24 present. All ObfuscationAttribute lines confirmed. PASS.**

---

### Group E — TaR6 Static/Instance Predicate Helpers (5 methods, lines 8181–8199)

Block header: lines 8173–8179

| # | Method | Line | Access | ObfuscAttr Line | Return | CYC |
|---|--------|------|--------|-----------------|--------|-----|
| 66 | `IsBracketOrderLiveState` | 8182 | **private static** | 8181 | bool | 1 |
| 67 | `MatchesPttReplacementName` | 8186 | **private static** | 8185 | bool | 1 |
| 68 | `LogHbcDiag` | 8190 | private instance | 8189 | void | 1 |
| 69 | `ExecuteStopDragOrder` | 8194 | private instance | 8193 | void | 1 |
| 70 | `IsOrderEventProcessable` | 8198 | **private static** | 8197 | bool | 1 |

**Static vs instance assignment matches test binding flags exactly (plan §BwaveCycTaR6HelperTests). PASS.**
**Group E: 5/5 present. All ObfuscationAttribute lines confirmed. PASS.**

**TOTAL: 70/70 methods confirmed present. 70/70 ObfuscationAttribute decorations confirmed. PASS.**

---

## Check 2 — ObfuscationAttribute: PASS

grep scan of `src/PropTraderTools/CopyEngine.cs` for `ObfuscationAttribute` in lines 7877–8199:
- Group A: 24 decorator lines at L7885, 7889, 7893, 7897, 7901, 7905, 7909, 7913, 7917, 7921, 7925, 7929, 7933, 7937, 7941, 7945, 7949, 7953, 7957, 7961, 7965, 7969, 7973, 7977
- Group B: 12 decorator lines at L7989, 7993, 7997, 8004, 8010, 8014, 8018, 8022, 8026, 8030, 8034, 8038
- Group C: 5 decorator lines at L8048, 8052, 8056, 8060, 8064
- Group D: 24 decorator lines at L8075, 8079, 8083, 8087, 8091, 8095, 8099, 8103, 8107, 8111, 8115, 8119, 8123, 8127, 8131, 8135, 8139, 8143, 8147, 8151, 8155, 8159, 8163, 8167
- Group E: 5 decorator lines at L8181, 8185, 8189, 8193, 8197

**Count: 24+12+5+24+5 = 70. Every method has `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on the line immediately above its signature. PASS.**

---

## Check 3 — Forbidden Patterns: PASS

### SCAN-01: lock() statements
grep for `^\s+lock\s*\(` across `src/PropTraderTools/CopyEngine.cs`: **0 matches**.
All 71 lock-related file matches are in comment text (e.g. `// no lock()`). Zero actual `lock(` statements anywhere in file. **PASS.**

### SCAN-02: throw statements
grep for `^\s+throw\s` across `src/PropTraderTools/CopyEngine.cs`: **0 matches**.
All throw-related file matches are in comment text (e.g. `// JS-001: no throw`). Zero actual `throw` statements anywhere in file. **PASS.**

### SCAN-03: DateTime.Now
grep for `DateTime\.Now[^U]` across `src/PropTraderTools/CopyEngine.cs`: **7 matches — ALL in comments** (e.g. `// No DateTime.Now`). Zero actual `DateTime.Now` calls. **PASS.**

---

## Check 4 — CYC Compliance: PASS

All 70 new methods:
- 69 methods: CYC=1 (single-expression bodies: `{ return false; }`, `{ }`, `{ return null; }`, `{ return 0.0; }`, `{ return string.Empty; }`, `{ return new List<Order>(); }`, `{ return true; }`, `{ entry = null; return false; }`, `{ followerIndex = -1; return false; }`)
- 1 method: `SelectBeRefPriceByDirection` (#28) — CYC=4 (two nested ternaries). Verified: CYC=1(base)+1(outer ?)+1(inner bid>0 ?)+1(inner ask>0 ?) = 4. **Compliant: 4 <= 8 (JS-013).**

**Max CYC across all 70 methods: 4. All <= 8 threshold. JS-013 PASS.**

---

## Check 5 — Test Baseline: PASS

All 5 ticket engineers and independent verifiers confirmed:
- `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj` → **0 Error(s)**
- `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build` → **Failed=0, Passed=24, Skipped=490, Total=514**

Most recent independent confirmation: T5 verification report (ptt-verifier, 2025-07-18).

No Skipped tests were un-skipped (DW-09-04 is deferred). No previously-passing tests regressed. **PASS.**

---

## Check 6 — Deferred Work Status: PASS

| Item | Expected Status | Confirmed |
|------|-----------------|-----------|
| DW-09-01 | CLOSED by this epic | YES — all 70 methods implemented with ObfuscationAttribute |
| DW-09-02 | Still OPEN | YES — LogBeSlotEviction binding flags NOT fixed (out of scope) |
| DW-09-03 | Still OPEN | YES — GetSenderAccountName binding flags NOT fixed (out of scope) |
| DW-09-04 | Still OPEN | YES — no [Fact(Skip)] annotations removed (out of scope) |

**No scope leak. All deferred items respected. PASS.**

---

## Check 7 — Cross-File Coherence: PASS

### JS DNA Rule Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-001: No throw | grep `^\s+throw\s` → 0 matches in file | PASS |
| JS-021: No lock() | grep `^\s+lock\s*\(` → 0 matches in file | PASS |
| JS-023: No Monitor/Mutex | Not present in new code regions | PASS |
| JS-002: Null return discipline | `FindMatchingNativeAtmBracket`, `CreateAndSubmitReplacementTarget` return `null` (Order is reference type — permitted null return for stub). `SelectBeTargetList` returns non-null empty list (IList<Order> — non-null required). | PASS |
| JS-003: No magic string for discriminated state | No state-discriminating strings in any stub | PASS |
| JS-008: SolidColorBrush Freeze | No WPF brushes in new code | PASS |
| JS-009: No plain Dictionary for shared state | No Dictionary usage in stubs | PASS |
| JS-010: No public constructor on singleton | No constructor added | PASS |
| JS-013: CYC <= 8 | Max CYC=4 across all 70 methods | PASS |

### NT8 Hard Constraints

| Constraint | Check | Result |
|------------|-------|--------|
| No async/await in lifecycle methods | No async/await in stubs | PASS |
| No sealed TradeCopierWindow | No sealed added | PASS |
| No FontFamily override | grep: 3 comment-only matches, 0 code | PASS |
| No #RRGGBB hex color | grep: 0 matches | PASS |
| No CreateOrder without PTT- prefix | No CreateOrder calls in any stub | PASS |
| No DateTime.Now | grep `DateTime\.Now[^U]`: 7 comment-only matches, 0 code | PASS |
| .NET 4.8 compliance | No switch expressions, records, init accessors | PASS |

### Namespace / Class Membership

All 70 methods are members of class `CopyEngine` (between line 91 and the closing brace). Insertion block is at lines 7877–8199, inside the `CopyEngine` class body, outside any inner class. `PendingDispatchDrain` inner class starts at line 8208 — after all 70 methods. No method was accidentally placed inside an inner class. **PASS.**

### Binding Flag Alignment

| Method | Test BindingFlags | Implemented As | Match |
|--------|------------------|----------------|-------|
| All Group A except #20 | NonPublic\|Instance | private instance | PASS |
| #20 IsPositionFlatOrMissing | NonPublic\|Static | private static | PASS |
| All Group B | NonPublic\|Instance | private instance | PASS |
| All Group C | NonPublic\|Instance | private instance | PASS |
| All Group D | NonPublic\|Instance | private instance | PASS |
| #66 IsBracketOrderLiveState | NonPublic\|Static | private static | PASS |
| #67 MatchesPttReplacementName | NonPublic\|Static | private static | PASS |
| #68 LogHbcDiag | NonPublic\|Instance | private instance | PASS |
| #69 ExecuteStopDragOrder | NonPublic\|Instance | private instance | PASS |
| #70 IsOrderEventProcessable | NonPublic\|Static | private static | PASS |

**Total static methods: 4 (#20, #66, #67, #70). Total instance methods: 66. All binding flags aligned. PASS.**

### 7 Scans Aggregate (across all 5 tickets, src/PropTraderTools/ scope)

All 5 ticket engineers and 5 independent verifiers ran all 7 scans. No discrepancies found between Layer 2 (engineer) and Layer 3 (verifier) results on any ticket for any scan.

| Scan | All 5 Tickets | Aggregate Result |
|------|---------------|------------------|
| SCAN-01: lock() | 0 code violations per ticket | 0 across src/PropTraderTools/ |
| SCAN-02: DateTime.Now | 0 code violations per ticket | 0 across src/PropTraderTools/ |
| SCAN-03: ASCII-only | 0 non-ASCII per ticket | 0 across src/PropTraderTools/ |
| SCAN-04: FontFamily | 0 code violations per ticket | 0 across src/PropTraderTools/ |
| SCAN-05: hex color #RRGGBB | 0 per ticket | 0 across src/PropTraderTools/ |
| SCAN-06: throw | 0 code violations per ticket (comment-only hits) | 0 across src/PropTraderTools/ |
| SCAN-07: deploy-sync + build | All tickets: 0 errors, sync complete | 0 errors |

**All 7 scans zero across src/PropTraderTools/. PASS.**

---

## Spec Coverage Matrix

| Requirement | Addressed | Plan Section | Status |
|-------------|-----------|--------------|--------|
| DW-09-01: Implement 70 missing helper methods with ObfuscationAttribute | YES — 70/70 inserted | §Method Inventory | CLOSED |
| Group A: 24 methods (B79CancelRaceGuard) | YES — lines 7885–7979 | §Group A | PASS |
| Group B: 12 methods (T1R1 BE helpers) with SelectBeRefPriceByDirection logic | YES — lines 7989–8040 | §Group B | PASS |
| Group C: 5 methods (TaR2 target-selection) | YES — lines 8048–8066 | §Group C | PASS |
| Group D: 24 methods (TaR3 sync/drag/bracket) | YES — lines 8075–8169 | §Group D | PASS |
| Group E: 5 methods (TaR6 static/instance predicates) | YES — lines 8181–8199 | §Group E | PASS |
| IsPositionFlatOrMissing must be private static | YES — line 7962 | §B79Cancel analysis | PASS |
| IsBracketOrderLiveState, MatchesPttReplacementName, IsOrderEventProcessable: private static | YES — lines 8182, 8186, 8198 | §TaR6 analysis | PASS |
| No [Fact(Skip)] removed (DW-09-04) | YES — CopyEngineTests.cs unmodified across all 5 tickets | Scope Lock | PASS |
| No new .cs files | YES — only CopyEngine.cs modified | Scope Lock | PASS |
| No existing methods modified | YES — pure insertion across all 5 tickets | Scope Lock | PASS |
| Test baseline: Failed=0, Passed=24, Skipped=490, Total=514 | YES — confirmed by all 5 verifiers | §7-Scan SCAN-07 | PASS |
| DW-09-02: LogBeSlotEviction binding flags NOT fixed (out of scope) | Correctly deferred | §Deferred Work | OPEN |
| DW-09-03: GetSenderAccountName binding flags NOT fixed (out of scope) | Correctly deferred | §Deferred Work | OPEN |
| DW-09-04: Skip removal deferred | Correctly deferred | §Deferred Work | OPEN |
| DW-B7-01: 5th B79 TypeInit test (carried from B7 block) | Still open | Prior backlog | OPEN |
| BWAVE-CYC-LOGIC: Production logic for all 70 stubs | Correctly deferred | §Deferred Work | OPEN |

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BWAVE-01 | BWAVE-CYC-LOGIC: Implement actual production logic for all 70 stubs (Groups A–E). All 70 methods are currently reflection-target stubs returning false/null/void. Follow-on engineering epic required. Prerequisite: DW-09-04 (skip removal) to confirm each stub is tested. | P1 | BWAVE-CYC-LOGIC epic | OPEN |
| DW-09-02 | Fix `LogBeSlotEviction` binding flags in BwaveCycTaR3HelperTests: change NonPublic\|Instance → NonPublic\|Static. Remove obfuscation Skip from 2 affected tests. ObfuscationAttribute already present at L1778. | P2 | After BWAVE-CYC-IMPL-01 | OPEN |
| DW-09-03 | Fix `GetSenderAccountName` binding flags in BwaveCycTaR2HelperTests: change NonPublic\|Instance → NonPublic\|Static. Remove obfuscation Skip from 1 affected test. ObfuscationAttribute already present at L6969. | P2 | After BWAVE-CYC-IMPL-01 | OPEN |
| DW-09-04 | Remove all 137 obfuscation-skip annotations after each method is implemented with production logic, binding flags verified, and ObfuscationAttribute confirmed. Requires DW-09-02 + DW-09-03 complete first. | P2 | After DW-09-02/03 | OPEN |
| DW-B7-01 | B79CancelRaceGuardTests: identify and skip the 5th TypeInit-triggering test (carried from PTT-REPAIRS-07-NT8-BULK-SKIP). Run dotnet test with NT8 host to identify the remaining failure by exception message and apply [Fact(Skip="NT8-runtime: ...")] to that test only. | P1 | Next repair block | OPEN |

---

## Summary

| Check | Result |
|-------|--------|
| 1. Method Count (70/70, Groups A–E) | PASS |
| 2. ObfuscationAttribute (70/70) | PASS |
| 3. Forbidden Patterns (lock/throw/DateTime.Now) | PASS |
| 4. CYC Compliance (max=4, all <=8) | PASS |
| 5. Test Baseline (F=0, P=24, S=490, T=514) | PASS |
| 6. Deferred Work Status (DW-09-01 closed; 02/03/04 open) | PASS |
| 7. Cross-File Coherence (JS DNA + NT8 + scans) | PASS |
| 8. Section K present | PASS |
| 06-deferred-backlog.md written | PASS |

**FINAL_PASS**
