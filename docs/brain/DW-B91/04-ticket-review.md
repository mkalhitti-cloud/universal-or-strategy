# Ticket Review: DW-B91

**Epic**: DW-B91 -- Entry dedup survivor guard + flat-follower re-entry guard
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-24
**Input artifacts**:
  - `docs/brain/DW-B91/04-tickets.md`
  - `docs/brain/DW-B91/02-architecture-plan.md`
  - `docs/standards/jane-street/RULES_CATALOG.md` (JS-001, JS-002, JS-021, JS-025)
  - `src/PropTraderTools/CopyEngine.cs` (L120-135, L1380-1440, L1882-1910, L2468-2494)

---

## T1 -- DW-B91-A: Entry order dispatch dedup survivor guard

### TC1-01: Spec Req ID Traceability
PASS -- `04-tickets.md` line 16 cites `DW-B91-A: double dispatch on re-submitted orderId after EvictDedup terminal-state eviction`. Maps to plan §2 (root cause DW-B91-A) and plan §3 Fix A.

### TC1-02: File Routing
PASS -- Production changes routed exclusively to `src/PropTraderTools/CopyEngine.cs`. Test file `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` is an additive test-only artifact; does not constitute a second production file.

### TC1-03: New Field Type
PASS -- Field declared at ticket line 37:
```csharp
private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders
    = new ConcurrentDictionary<string, byte>();
```
Matches plan §3 Fix A field declaration exactly.

### TC1-04: IsEntryDispatched Signature + CYC
PASS -- Ticket line 42-43: `// CYC=2: 1 base + 1 if (ContainsKey).` and `private bool IsEntryDispatched(string orderId)`. CYC=2 <= 8. Matches plan §4 signature table.

### TC1-05: DispatchCopy CYC <= 8 After Fix
PASS -- Ticket line 45: `// MODIFIED -- Gate 5 becomes compound OR; orderId local extracted; CYC=8 unchanged.` and line 97: `// Compound OR: single McCabe branch -- DispatchCopy CYC stays at 8.` Unchanged CYC=8. Confirmed against current source (L1381 CYC header: `JS-001: no throw in hot path. JS-021: no lock.`).

### TC1-06: EvictDedup Gains _entryDispatchedOrders.TryRemove
PASS -- Ticket line 49: `// MODIFIED -- add _entryDispatchedOrders.TryRemove after existing _dedupCache.TryRemove; CYC=2 unchanged.` Full replacement body shown at ticket lines 146-153; `_entryDispatchedOrders.TryRemove(orderId, out _)` explicitly inserted. Matches plan §3 Fix A "Modified EvictDedup".

### TC1-07: JS-021 Explicitly Verified
PASS -- Ticket line 188: `| JS-021 | No lock() anywhere | DO: ConcurrentDictionary.ContainsKey, TryAdd, TryRemove (lock-free). DON'T: lock (_entryDispatchedOrders).` Explicit cite of JS-021 in JS Constraints table.

### TC1-08: JS-001 Explicitly Verified
PASS -- Ticket line 190: `| JS-001 | No throw in hot path | DO: return bool; use early-return guard. DON'T: throw new InvalidOperationException(...)` Explicit cite of JS-001 in JS Constraints table.

### TC1-09: 3 xUnit [Fact] Test Names Present
PASS -- Ticket lines 198-200:
```
IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched    (T_B91A_01)
IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse       (T_B91A_02)
IsEntryDispatched_DifferentOrderIds_IndependentTracking        (T_B91A_03)
```
All three names match plan §5 test table. Assertion descriptions present (ticket lines 203-206).

### TC1-10: 7-Scan Checklist Present
PASS -- Ticket lines 212-218:
- SCAN-01: lock() scan present
- SCAN-02: async void scan present
- SCAN-03: CYC scan present (IsEntryDispatched=2, DispatchCopy=8, EvictDedup=2)
- SCAN-04: return null scan present
- SCAN-05: PTT- prefix scan present (N/A marked explicitly)
- SCAN-06: ASCII scan present
- SCAN-07: test presence scan present

All 7 scans enumerated. Engineer contract is complete.

### JS Pre-Check: T1
PASS -- No lock() described. No throw described. No return null described. ConcurrentDictionary<string,byte> is the canonical JS-025 lock-free set pattern. CYC bounds respected across all described methods.

### CYC Pre-Check: T1
PASS -- IsEntryDispatched=2, DispatchCopy=8 (compound `||` = 1 McCabe branch, unchanged), EvictDedup=2. All <= 8.

### NT8 Check: T1
PASS -- No async/await in lifecycle methods. No Account.All outside Loaded. No sealed on window class. No FontFamily. No hex color. No CreateOrder call. No DateTime.Now. No non-ASCII.

### VERDICT: T1
**TICKET_REVIEW_PASS**

---

## T2 -- DW-B91-B: Flat-follower open-position guard in TryDispatchLeaderFlat

### TC2-01: Spec Req ID Traceability
PASS -- `04-tickets.md` line 227 cites `DW-B91-B: spurious flattenOne call on already-flat follower accounts in TryDispatchLeaderFlat`. Maps to plan §2 root cause DW-B91-B and plan §3 Fix B.

### TC2-02: File Routing
PASS -- Production changes routed exclusively to `src/PropTraderTools/CopyEngine.cs` (ticket line 231: `ONLY`). Test addition is to existing test file `CopyEngineB91Tests.cs`; additive test-only modification.

### TC2-03: FlattenFollower Signature + CYC <= 3
PASS -- Ticket lines 243-248:
```
// CYC=3: 1 base + if (acc == null) + if (!hasOpenPosition).
private static void FlattenFollower(
    Account acc, Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
```
CYC=3 <= 8. Matches plan §4 signature table exactly (including `private static`).

### TC2-04: TryDispatchLeaderFlat CYC <= 8 After Extraction
PASS -- Ticket line 252: `// MODIFIED -- foreach body replaced with FlattenFollower call; null guard removed from caller. // Header comment updated: CYC=8->7.` CYC analysis at ticket lines 327-334 shows 5 branch points + 1 loop + 1 base = CYC=6 or 7 depending on compound-`&&` counting. Both interpretations <= 8. Matches plan §3 Fix B CYC analysis.

### TC2-05: FlattenFollower Absorbs Both Guards
PASS -- Ticket lines 321-322 show method body explicitly:
```csharp
if (acc == null) return;                            // (a) null guard (moved from caller)
if (!hasOpenPosition(acc, instrument)) return;     // (b) DW-B91-B: skip already-flat follower
```
Both null guard AND hasOpenPosition guard accounted for. Matches plan §3 Fix B `FlattenFollower` body.

### TC2-06: foreach Body Reduced to Single FlattenFollower Call
PASS -- Ticket lines 274-275:
```csharp
foreach (var acc in rule.FollowerAccounts)
    FlattenFollower(acc, instrument, hasOpenPosition, flattenOne);  // DW-B91-B
```
Ticket line 277 explicitly confirms: `The foreach body is now a single statement with zero branches in the caller.` Matches plan §3 Fix B foreach replacement. Confirmed against source L1901-1905 (current: 3-line body with null guard; ticket removes the guard from the caller entirely).

### TC2-07: JS-021 Verified
PASS -- Ticket line 358: `| JS-021 | No lock() anywhere | DO: FlattenFollower uses only delegate calls (hasOpenPosition(acc, instrument), flattenOne(acc, instrument)) -- no shared mutable state. DON'T: lock (rule.FollowerAccounts).`

### TC2-08: JS-001 Verified
PASS -- Ticket line 360: `| JS-001 | No throw in hot path | DO: early-return guard (if (acc == null) return;). DON'T: throw new ArgumentNullException(nameof(acc)).`

### TC2-09: 3 xUnit [Fact] Test Names Present
PASS -- Ticket lines 368-370:
```
FlattenFollower_NullAccount_DoesNotCallFlattenOne      (T_B91B_01)
FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne   (T_B91B_02)
FlattenFollower_HasOpenPosition_CallsFlattenOne         (T_B91B_03)
```
All three names match plan §5 test table. Assertion descriptions present (ticket lines 372-375).

### TC2-10: 7-Scan Checklist Present
PASS -- Ticket lines 381-387:
- SCAN-01: lock() scan present
- SCAN-02: async void scan present
- SCAN-03: CYC scan present (FlattenFollower=3, TryDispatchLeaderFlat=6 or 7)
- SCAN-04: return null scan present
- SCAN-05: PTT- prefix scan present (N/A marked explicitly)
- SCAN-06: ASCII scan present
- SCAN-07: test presence scan present

All 7 scans enumerated. Engineer contract is complete.

### JS Pre-Check: T2
PASS -- No lock() described. No throw described. No return null described. FlattenFollower is void (no return value at all). TryDispatchLeaderFlat returns bool (false/true, never null). CYC bounds respected across all described methods.

### CYC Pre-Check: T2
PASS -- FlattenFollower=3, TryDispatchLeaderFlat=6 or 7 (both interpretations <= 8). No method described exceeds CYC=8.

### NT8 Check: T2
PASS -- No async/await. No Account.All. No sealed on window. No FontFamily. No hex color. No CreateOrder. No DateTime.Now. No non-ASCII identifiers or literals.

### VERDICT: T2
**TICKET_REVIEW_PASS**

---

## Cross-Ticket Checks

### CTC-01: No Extraneous .cs File References
PASS -- Both tickets reference only `CopyEngine.cs` for production and `CopyEngineB91Tests.cs` for tests. No other .cs file cited or implied.

### CTC-02: No lock() Usage Described Anywhere
PASS -- Both tickets explicitly ban lock() in their JS-021 DO/DON'T tables. No ticket step describes acquiring any lock. All concurrency relies on ConcurrentDictionary atomic operations and delegate injection.

### CTC-03: xUnit Only (No NUnit, No MSTest)
PASS -- TICKET-1 line 208: `xUnit only, no NUnit, no MSTest`. Both tickets use `[Fact]` attribute. Ticket-1 test file structure shows `using Xunit;` only (line 160). No NUnit or MSTest import described.

### CTC-04: No async void Introduced
PASS -- All new methods are synchronous: `private bool IsEntryDispatched(...)`, `private static void FlattenFollower(...)`. `DispatchCopy` and `EvictDedup` modifications are synchronous. No async keyword described anywhere in either ticket.

---

## Traceability Summary

| Plan Item | Covered In Ticket | Status |
|-----------|------------------|--------|
| Plan §3 Fix A: _entryDispatchedOrders field | TICKET-1 Step 1 | COVERED |
| Plan §3 Fix A: IsEntryDispatched helper | TICKET-1 Step 3 | COVERED |
| Plan §3 Fix A: DispatchCopy Gate 5 compound OR | TICKET-1 Step 2 | COVERED |
| Plan §3 Fix A: EvictDedup co-eviction | TICKET-1 Step 4 | COVERED |
| Plan §3 Fix B: FlattenFollower static helper | TICKET-2 Step 3 | COVERED |
| Plan §3 Fix B: TryDispatchLeaderFlat foreach replacement | TICKET-2 Step 1 | COVERED |
| Plan §3 Fix B: TryDispatchLeaderFlat header comment update | TICKET-2 Step 2 | COVERED |
| Plan §5 Tests T_B91A_01-03 | TICKET-1 Step 5 | COVERED |
| Plan §5 Tests T_B91B_01-03 | TICKET-2 Step 4 | COVERED |

No phantom work found (all ticket items trace to plan). No missing plan items (all 9 plan deliverables mapped to a ticket step).

---

## Spec Coverage Summary

| Spec Req | Ticket | Status |
|----------|--------|--------|
| DW-B91-A (entry dedup survivor guard) | TICKET-1 | COVERED (exactly once) |
| DW-B91-B (flat-follower re-entry guard) | TICKET-2 | COVERED (exactly once) |

No uncovered requirements. No duplicate coverage.

---

## Overall: TICKET_REVIEW_PASS

All 24 checks passed (TC1-01 through TC1-10, TC2-01 through TC2-10, CTC-01 through CTC-04).

| Check Group | Result |
|-------------|--------|
| TICKET-1 (10 checks) | 10/10 PASS |
| TICKET-2 (10 checks) | 10/10 PASS |
| Cross-Ticket (4 checks) | 4/4 PASS |
| Traceability (both tickets) | PASS |
| Spec Coverage | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| File Routing | PASS |

**TICKET_REVIEW_PASS** -- Safe to spawn ptt-engineer.
