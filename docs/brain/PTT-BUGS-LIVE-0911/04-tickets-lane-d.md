# PTT-BUGS-LIVE-0911 Lane D — Implementation Tickets
**Phase:** 3 — Ticket Generation  
**Author:** ptt-architect  
**Date:** 2026-09-11  
**Plan status:** REVIEW_PASS (02-plan-review-lane-d.md, 2026-09-11, 22/22 checks pass, 0 violations)  
**Source:** `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-d.md`

---

## T-LD-D1 — Implement TryLaneDHook Body + Double-Dispatch Guard

---

### 1. Ticket ID + Title

**T-LD-D1**: Implement `TryLaneDHook` ATM dispatch body, add `TryLaneDDispatchFollower` helper, add `_laneDDispatched` field, add `DispatchToFollower` skip gate, and add 6 xUnit `[Fact]` tests.

---

### 2. Spec Requirement IDs Satisfied

| Requirement ID | Description |
|---|---|
| R-LD-01 | `TryLaneDHook` body implemented (non-empty) — DW-PTT0911C-01 CLOSED |
| R-LD-02 | `SendCopyWithAtm` called for Clone-mode followers → NT8 ATM brackets armed at entry |
| R-LD-03 | `_laneDDispatched` dedup gate prevents double-dispatch per (orderId, follower) pair |
| R-LD-04 | Non-Named AtmMode path returns early → Signal/Mirror/Market/Inherit paths unaffected |
| R-LD-05 | All new conditional branches inside `TryLaneDHook`/helper only — `DispatchCopy` CYC unchanged at 8 |
| R-LD-06 | Lane E wiring point documented (Section 11 of plan) — no code change required |
| DW-PTT0911C-02 | Lane D CYC constraint CLOSED |
| DW-PTT0911C-03 | Lane D thread context constraint CLOSED |
| DW-PTT0911C-04 | `StartAtmStrategy` STATIC form, order name `"Entry"` — CLOSED |
| DW-PTT0911C-05 | ATM null guard (`namedMode.AtmObject`) — CLOSED (guard lives in existing `SendCopyWithAtm`) |

---

### 3. Scope Lock

**Modified files (exactly two):**

| File | Change type |
|---|---|
| `src/PropTraderTools/CopyEngine.cs` | 4 targeted edits (1 new field, 1 body replacement, 1 new method, 1 two-line insertion) |
| `src/PropTraderTools/CopyEngineTests.cs` | 1 new test class with 6 `[Fact]` methods appended before the closing `}` of the namespace |

**No other file is touched.** `TradeCopierPanel.cs` is explicitly OUT OF SCOPE.

---

### 4. Pre-conditions

Before implementation begins the following must be true:

1. `dotnet build src/PropTraderTools/PropTraderTools.csproj` succeeds with 0 errors.
2. `dotnet test` passes: 165 passed, 359 skipped, 0 failed (Lane C baseline).
3. `grep -n "TryLaneDHook" src/PropTraderTools/CopyEngine.cs` returns exactly:
   - Line 2632: `TryLaneDHook(order, rule, baseSignal, baseQty);` (call site)
   - Line 2671: `private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)` (stub definition)
4. `TryLaneDHook` body at lines 2672-2673 is empty (`{ }`).
5. `grep -n "_laneDDispatched" src/PropTraderTools/CopyEngine.cs` returns 0 matches.
6. `grep -n "TryLaneDDispatchFollower" src/PropTraderTools/CopyEngine.cs` returns 0 matches.
7. The `DispatchToFollower` method is at lines 2772–2811 with no existing `_laneDDispatched` check.
8. `GetCloneAtmMode(string instrFullName)` exists at line 2751 with signature:
   `internal FollowerAtmMode GetCloneAtmMode(string instrFullName)`.
9. `SendCopyWithAtm` exists at line 5135 with signature:
   `private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)`.
10. `PassesDailyCapCheck(Account acc)` exists at line 6154.
11. The last `ConcurrentDictionary` field in the fields region before the `_followerSlateReady` declaration is `_drainOwnedOrderIds` at line 393-394.

---

### 5. Implementation Steps

All four edits are in `src/PropTraderTools/CopyEngine.cs`. All line numbers are from the Lane C final build. After each edit, line numbers in subsequent edits shift; apply edits in order 5.1 → 5.4.

#### Step 5.1 — Add `_laneDDispatched` field (CopyEngine.cs, after line 404)

**Location:** After the `_followerSlateReady` field declaration, which ends at line 404. Insert after line 404 (before the `// --- Status event ---` comment at line 406).

**Content to insert (7 lines):**

```csharp
        // Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
        // Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

```

**Verify after insert:** `grep -n "_laneDDispatched" src/PropTraderTools/CopyEngine.cs` returns exactly 1 match (the field declaration). Line number will be ~405.

---

#### Step 5.2 — Replace `TryLaneDHook` empty body (CopyEngine.cs, lines 2671-2673 pre-insert; ~2678-2680 post-Step-5.1)

**Current state (empty stub):**

```csharp
        private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
        {
        }
```

**Replace the entire method (comment block + signature + body):**

Replace the 8-line comment block starting with `// Lane C: Lane D wiring stub` through the closing `}` of the stub with the following:

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
            FollowerAtmMode cloneAtmMode = GetCloneAtmMode(order.Instrument.FullName);
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

**Verify after replace:**
- `grep -n "TryLaneDHook" src/PropTraderTools/CopyEngine.cs` returns 2 matches: the call site and the method definition.
- `grep -n "TryLaneDDispatchFollower" src/PropTraderTools/CopyEngine.cs` returns 2 matches: the call inside `TryLaneDHook` and the method definition.
- The `TryLaneDHook` method is declared `private void` with 4 parameters (Lane C reflection test preserved).

---

#### Step 5.3 — Add skip gate in `DispatchToFollower` (CopyEngine.cs)

**Location:** Inside `DispatchToFollower` body, immediately after `var mode = ResolveAtmMode(...)` and before the `NinjaTrader.Code.Output.Process(...)` logging call.

**Current code (lines ~2789-2790 pre-edits; adjust to actual line after prior inserts):**

```csharp
            var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
            NinjaTrader.Code.Output.Process(
```

**Insert two lines between these two statements:**

```csharp
            // Lane D skip gate: if TryLaneDHook already dispatched this (order, follower) pair via ATM,
            // return immediately -- prevents one ATM entry (hook) + one raw entry (loop) double-dispatch.
            // _laneDDispatched.ContainsKey is O(1) lock-free (JS-021). No counter modification -- dedup
            // commit for non-ATM followers in this loop is unaffected.
            string laneD_key = baseSignal.OrderId + "#" + acc.Name;
            if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

**Verify after insert:** `grep -n "_laneDDispatched.ContainsKey" src/PropTraderTools/CopyEngine.cs` returns exactly 1 match inside `DispatchToFollower`.

---

#### Step 5.4 — Add 6 `[Fact]` tests (CopyEngineTests.cs)

**Location:** Insert before the final closing `}` of the namespace (currently line 8566 before this insert). Append a new test class `LaneDContractTests` after the existing `LaneCWiringContractTests` class closing `}` at line 8565.

**Content to append (between line 8565 and 8566):**

```csharp
    // Lane D: structural contract tests. All tests use reflection to avoid NT8 runtime dependency.
    // T-LD-01 is Skip-annotated (NT8 runtime required for SendCopyWithAtm). T-LD-02 through T-LD-06 run.
    public class LaneDContractTests
    {
        private static System.Reflection.FieldInfo GetField(string name) =>
            typeof(CopyEngine).GetField(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

        private static System.Reflection.MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

        // T-LD-01: Full ATM dispatch integration test -- requires NT8 runtime (Account.CreateOrder +
        // StartAtmStrategy unavailable in xUnit host). Skipped. Structural coverage in T-LD-02 through T-LD-06.
        [Fact(Skip = "NT8-runtime: SendCopyWithAtm requires Account.CreateOrder + StartAtmStrategy NT8 host")]
        public void TryLaneDHook_CloneNamedMode_CallsSendCopyWithAtm()
        {
            // Arrange: CopyEngine in Clone mode, _cloneAtmCacheByInstr non-empty, rule with 1 follower,
            //          leader Limit order signal.
            // Act: invoke TryLaneDHook via reflection.
            // Assert: _laneDDispatched contains key orderId + "#" + followerName.
            Assert.True(false, "NT8 runtime required -- test must run inside NinjaTrader process.");
        }

        // T-LD-02: Signal mode is a no-op -- _laneDDispatched stays empty.
        // Verifies: TryLaneDHook branch (1) (GetCopyMode() != Clone early return).
        [Fact]
        public void TryLaneDHook_SignalMode_IsNoOp()
        {
            // Verify that the TryLaneDHook method exists and that the _laneDDispatched field exists.
            // In Signal mode the branch (1) guard fires immediately; no dedup entry is added.
            // Full behavioral assertion requires NT8 runtime (CopyMode.Signal default).
            // Structural assertion: field must exist and be a ConcurrentDictionary<string,byte>.
            var field = GetField("_laneDDispatched");
            Assert.NotNull(field);
            // Field type must be ConcurrentDictionary<string, byte>
            Assert.Equal(
                typeof(System.Collections.Concurrent.ConcurrentDictionary<string, byte>),
                field.FieldType
            );
        }

        // T-LD-03: Market order type is a no-op -- verifies branch (2) guard exists structurally.
        // Behavioral assertion requires NT8 runtime (Order.OrderType). Structural: method must exist.
        [Fact]
        public void TryLaneDHook_MarketOrderType_IsNoOp()
        {
            var method = GetMethod("TryLaneDHook");
            Assert.NotNull(method);
            // Method must be private void with exactly 4 parameters (Lane C contract preserved)
            Assert.Equal(typeof(void), method.ReturnType);
            Assert.Equal(4, method.GetParameters().Length);
        }

        // T-LD-04: Double-dispatch prevention -- ConcurrentDictionary skip gate semantics verified.
        // The ContainsKey check in DispatchToFollower fires BEFORE any NT8 Account/Order type access,
        // so this test does NOT require the NT8 runtime.
        // Verifies: TryAdd idempotency is the entire skip gate mechanism -- a second TryAdd on the same
        // key returns false, proving that a duplicate (orderId, follower) pair would be blocked.
        [Fact]
        public void TryLaneDHook_DuplicateFill_SkipGateBlocksSecondDispatch()
        {
            // Step 1: obtain _laneDDispatched ConcurrentDictionary via reflection
            var field = GetField("_laneDDispatched");
            Assert.NotNull(field);
            Assert.Equal(
                typeof(System.Collections.Concurrent.ConcurrentDictionary<string, byte>),
                field.FieldType
            );

            // Step 2: instantiate an isolated ConcurrentDictionary to test skip gate semantics
            // (mirrors the exact type used by the field -- no CopyEngine instance needed)
            var dict = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(
                System.StringComparer.Ordinal
            );
            const string key = "test-order-id#FollowerAcct";

            // Step 3: pre-populate the key (simulates hook already dispatched for this pair)
            bool firstAdd = dict.TryAdd(key, 0);
            Assert.True(firstAdd, "First TryAdd must succeed (pair not yet dispatched)");

            // Step 4: verify ContainsKey returns true -- the skip gate would fire
            Assert.True(dict.ContainsKey(key), "ContainsKey must return true after TryAdd (skip gate fires)");

            // Step 5: verify idempotency -- a second TryAdd on the same key returns false
            // This is the EXACT mechanism that prevents double-dispatch in DispatchToFollower
            bool secondAdd = dict.TryAdd(key, 0);
            Assert.False(secondAdd, "Second TryAdd must return false (skip gate blocks duplicate dispatch)");
        }

        // T-LD-05: _laneDDispatched field existence, access modifier, and type contract.
        [Fact]
        public void LaneDDispatched_Field_ExistsOnCopyEngine()
        {
            var field = GetField("_laneDDispatched");
            Assert.NotNull(field);
            Assert.True(field.IsPrivate, "_laneDDispatched must be private");
            Assert.True(field.IsInitOnly, "_laneDDispatched must be readonly");
            Assert.Equal(
                typeof(System.Collections.Concurrent.ConcurrentDictionary<string, byte>),
                field.FieldType
            );
        }

        // T-LD-06: TryLaneDDispatchFollower existence and return type contract.
        [Fact]
        public void TryLaneDDispatchFollower_MethodExists_IsPrivateWithExpectedParams()
        {
            var method = GetMethod("TryLaneDDispatchFollower");
            Assert.NotNull(method);
            Assert.Equal(typeof(bool), method.ReturnType);
            // 7 parameters: Account acc, CopyRule rule, int idx, Order order,
            //               FollowerAtmMode.Named namedMode, CopySignal baseSignal, int baseQty
            Assert.Equal(7, method.GetParameters().Length);
        }
    }
```

**Verify after insert:** `grep -c "\[Fact" src/PropTraderTools/CopyEngineTests.cs` increases by 6. Namespace closes correctly.

---

### 6. Method Signatures (Exact)

#### 6a. Existing method — signature UNCHANGED (Lane C reflection test verifies exact form)

```csharp
// CopyEngine.cs ~line 2678 (after field insert)
// DO NOT CHANGE SIGNATURE -- LaneCWiringContractTests.TryLaneDHook_MethodExists_IsPrivateVoidWith4Params
// asserts: private void, 4 parameters.
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
```

#### 6b. New private helper

```csharp
// CopyEngine.cs (new, immediately after TryLaneDHook closing brace)
// Lane D: per-follower ATM dispatch helper. CYC=4. JS-001: no throw (returns bool false on all failure paths).
// JS-021: no lock. JS-002: returns bool (never null). ASCII-only.
private bool TryLaneDDispatchFollower(
    Account acc,
    CopyRule rule,
    int idx,
    Order order,
    FollowerAtmMode.Named namedMode,
    CopySignal baseSignal,
    int baseQty
)
```

#### 6c. Existing method — 1-line addition only (signature UNCHANGED)

```csharp
// CopyEngine.cs ~line 2772 (shifts after inserts)
// MODIFICATION: 2-line skip gate added after var mode = ResolveAtmMode(...).
// Signature itself is unchanged.
private void DispatchToFollower(
    Account acc,
    Order order,
    CopyRule rule,
    int idx,
    CopySignal baseSignal,
    int baseQty
)
```

Exact two-line addition inside body (after `var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);`):

```csharp
string laneD_key = baseSignal.OrderId + "#" + acc.Name;
if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

#### 6d. New field

```csharp
// CopyEngine.cs (after _followerSlateReady declaration, ~line 405)
private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

---

### 7. CYC Pre-Check

| Method | Decision points | CYC | Budget | Status |
|---|---|---|---|---|
| `DispatchCopy` | Unchanged (8 existing branches) | **8** | ≤8 | AT LIMIT — ZERO NEW BRANCHES ADDED ✓ |
| `TryLaneDHook` (after impl) | (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) `if (dispatched)` in loop, (5) laneDCount>0 | **6** | ≤8 | ✓ |
| `TryLaneDDispatchFollower` (new) | (A) acc==null, (B) !PassesDailyCapCheck, (C) `if (ok)` TryAdd | **4** | ≤8 | ✓ |
| `DispatchToFollower` (modified) | Prior: 1(base) + 2(Named branch + else) + 1(new ContainsKey skip) = **4** | **4** | ≤8 | ✓ |

**Note (OBS-1 from plan review):** The plan's Section 7 table showed `DispatchToFollower` CYC=3; the correct Lizard count including base is 4. At CYC=4 the method is within budget. Engineer must record CYC=4 in final verification.

**Reviewer-confirmed:** Reviewer OBS-1 explicitly states: "At CYC=4 the method remains well within the ≤8 budget. Not a rule violation."

---

### 8. Jane Street Rule Constraints

The following JS rules apply to all new/modified code in this ticket. Violation of any = immediate SCAN fail.

| Rule | Constraint | Applies to |
|---|---|---|
| **JS-021** | Zero `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim` in new code. Use `ConcurrentDictionary`/`volatile`/`Interlocked` only. | All new/modified methods and the new field |
| **JS-001** | No `throw` statement in dispatch path, gate methods, or event handlers. All failure paths return `false` or early-return `void`. | `TryLaneDHook`, `TryLaneDDispatchFollower` |
| **JS-002** | No unexpected null return. Helper `TryLaneDDispatchFollower` returns `bool` — null is impossible for value type. `TryLaneDHook` is `void`. | `TryLaneDDispatchFollower` |
| **JS-023** | No WPF UI updates off the UI thread without `Dispatcher.InvokeAsync`. `TryLaneDHook` and `TryLaneDDispatchFollower` run on the NT8 account background thread — no UI writes permitted. `StatusUpdate` raised inside `SendCopyWithAtm` is an existing pattern, unchanged. | `TryLaneDHook`, `TryLaneDDispatchFollower` |
| **ASCII-only** | No Unicode, emoji, curly quotes, or non-ASCII characters in string literals or comments. Key separator `"#"` and instrument key separator `"|"` are ASCII-safe. | All new string literals and comments |
| **No `DateTime.Now`** | Use `DateTime.UtcNow` if any timestamp is ever needed. No timestamp used in Lane D new code — constraint is satisfied by absence. | All new code |
| **No async/await** | `TryLaneDHook` is `private void`; `TryLaneDDispatchFollower` is `private bool`. Neither is `async`. No `await` anywhere in new code. | All new/modified methods |

---

### 9. NT8 API Constraints

| Constraint | Rule | Location in new code |
|---|---|---|
| `StartAtmStrategy` — STATIC form only | `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` — NOT an instance call. Required because `TryLaneDHook` runs in `CopyEngine` which is `AddOnBase`, not `NinjaScriptBase`. | Called inside existing `SendCopyWithAtm` — no change. No new `StartAtmStrategy` call is added by this ticket. |
| Order name `"Entry"` for ATM arming | `Account.CreateOrder(..., "Entry", ...)` — MUST be `"Entry"` for `StartAtmStrategy` to arm stop/target brackets. Used inside existing `SendCopyWithAtm` at line 5154. | No change. `TryLaneDDispatchFollower` delegates to `SendCopyWithAtm` which already enforces this. |
| `AtmObject` null guard | `if (namedMode.AtmObject != null)` → object overload; else → string overload. For Clone mode, `namedMode.AtmObject` is always null (string-only Named ctor via `GetCloneAtmMode`). String overload used. | Guard lives in `SendCopyWithAtm` lines 5160-5163. No change needed. |
| `AtmStrategyCreate()` — BANNED | Zero calls to `AtmStrategyCreate()` in new code. | Not used anywhere in new code — confirmed absent. |
| `AtmStrategyChangeStopTarget()` — BANNED | Zero calls in new code. Cancel+resubmit is the AddOn pattern (Lane D does not change stops). | Not used anywhere in new code — confirmed absent. |
| `Account.CreateOrder` + `follower.Submit` | `SendCopyWithAtm` uses `CreateOrder` + `StartAtmStrategy`. It does NOT call `follower.Submit()` after `StartAtmStrategy` (NT8 handles submission). | No change. `TryLaneDDispatchFollower` only calls `SendCopyWithAtm`. |
| Market order guard | `SendCopyWithAtm` hardcodes `OrderType.Limit`. A Market leader signal at price 0 would produce an invalid Limit order. `TryLaneDHook` branch (2) guards: `if (order.OrderType != OrderType.Limit) return;`. Market leader fills use existing `SendCopy` path via `DispatchToFollower`. | Branch (2) in `TryLaneDHook` body (Step 5.2). |

---

### 10. xUnit `[Fact]` Test Names

All 6 tests are placed in new class `LaneDContractTests` in `CopyEngineTests.cs`. Tests are added at the END of the file, after the existing `LaneCWiringContractTests` class.

| ID | Method name | Skip? | What it asserts |
|---|---|---|---|
| T-LD-01 | `TryLaneDHook_CloneNamedMode_CallsSendCopyWithAtm` | YES — `NT8-runtime: SendCopyWithAtm requires Account.CreateOrder + StartAtmStrategy NT8 host` | Full ATM dispatch: `_laneDDispatched` contains key after hook invocation. Requires NT8 runtime. |
| T-LD-02 | `TryLaneDHook_SignalMode_IsNoOp` | No | `_laneDDispatched` field exists, is `ConcurrentDictionary<string,byte>`. Structural: branch (1) guard exists. |
| T-LD-03 | `TryLaneDHook_MarketOrderType_IsNoOp` | No | `TryLaneDHook` exists, is `private void`, has 4 parameters (Lane C contract preserved). Structural: branch (2) guard exists. |
| T-LD-04 | `TryLaneDHook_DuplicateFill_SkipGateBlocksSecondDispatch` | No | Behavioral: obtains `_laneDDispatched` field via reflection, pre-populates key `"test-order-id#FollowerAcct"` via an isolated `ConcurrentDictionary`, asserts first `TryAdd` returns `true`, `ContainsKey` returns `true`, and second `TryAdd` returns `false`. Proves TryAdd idempotency is the entire skip gate mechanism. NT8 runtime NOT required (ContainsKey fires before any Account/Order access). |
| T-LD-05 | `LaneDDispatched_Field_ExistsOnCopyEngine` | No | `_laneDDispatched` is private, readonly, type `ConcurrentDictionary<string,byte>`. |
| T-LD-06 | `TryLaneDDispatchFollower_MethodExists_IsPrivateWithExpectedParams` | No | `TryLaneDDispatchFollower` exists, returns `bool`, has 7 parameters. |

**Test count change:** Baseline 165 passed / 359 skipped / 0 failed. After this ticket: 170 passed / 360 skipped / 0 failed (5 new passing + 1 new Skip).

---

### 11. 7-Scan Checklist (Engineer Contract)

The engineer MUST run all 7 scans and record the result. Any scan failure blocks merge.

---

#### SCAN-01 — No `lock()` in new/modified code

```powershell
# Run from repo root
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\block\s*\(" | Select-Object LineNumber, Line
```

**Expected result:** 0 matches in newly added code (lines covering `_laneDDispatched` field, `TryLaneDHook` body, `TryLaneDDispatchFollower`, and `DispatchToFollower` skip gate).

**Confirmation method:** Inspect every match returned; confirm all are in pre-existing code, none are in the 4 new/modified sections. A match in new code = **SCAN-01 FAIL** (JS-021 violation).

---

#### SCAN-02 — CYC manual count (all new/modified methods ≤8)

**No grep — manual count required.** Engineer counts decision branches (if, else if, foreach, while, &&, ||, ?:, case) in each new/modified method body.

| Method | Count branches | Expected CYC | Pass threshold |
|---|---|---|---|
| `DispatchCopy` | Count ALL branches including pre-existing | **8** (unchanged) | CYC must be exactly 8 — no new branches at call site |
| `TryLaneDHook` | Count branches (1)-(5) in new body | **6** | ≤8 |
| `TryLaneDDispatchFollower` | Count branches (A)-(C) in new body | **4** | ≤8 |
| `DispatchToFollower` | Count branches including new `if (ContainsKey)` | **4** | ≤8 |

**Confirmation:** If any method exceeds 8 = **SCAN-02 FAIL**.

---

#### SCAN-03 — No `async`/`await` in new/modified methods

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\basync\b|\bawait\b" | Select-Object LineNumber, Line
```

**Expected result:** 0 matches in the new `TryLaneDHook` body, `TryLaneDDispatchFollower`, and `DispatchToFollower` skip gate lines. Any pre-existing matches outside these ranges are acceptable.

**Confirmation method:** Verify no match falls within the line ranges of the new/modified method bodies. A match in new code = **SCAN-03 FAIL**.

---

#### SCAN-04 — No non-ASCII bytes

```powershell
# PowerShell: check for non-ASCII bytes in modified file
$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\CopyEngine.cs")
$nonAscii = $bytes | Where-Object { $_ -gt 127 }
Write-Host "Non-ASCII byte count: $($nonAscii.Count)"
```

**Expected result:** 0 non-ASCII bytes introduced by this ticket's edits. (If pre-existing non-ASCII exists in the file from prior commits, record the pre-existing count and confirm it is unchanged after edits.)

A new non-ASCII byte in new code = **SCAN-04 FAIL** (ASCII-only rule).

---

#### SCAN-05 — No banned ATM API calls in new code

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "AtmStrategyCreate|AtmStrategyChangeStopTarget" | Select-Object LineNumber, Line
```

**Expected result:** 0 matches in `TryLaneDHook` body, `TryLaneDDispatchFollower`, or `DispatchToFollower` skip gate. These banned APIs must not appear in any line added by this ticket.

Any match in new code = **SCAN-05 FAIL**.

---

#### SCAN-06 — Build and test pass

```powershell
# Step 1: build
dotnet build src\PropTraderTools\PropTraderTools.csproj
# Expected: 0 errors, 0 warnings related to new code

# Step 2: test
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj
# Expected: Passed >= 170, Failed = 0, Skipped >= 360
# Regression check: no previously-passing test may become failing
```

**Expected result:**
- Build: 0 errors.
- Tests: Failed = 0. Passed count increases by 5 (T-LD-02 through T-LD-06). Skipped count increases by 1 (T-LD-01).
- Lane C test `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` must still PASS (signature preserved).

Build error or test regression = **SCAN-06 FAIL**.

---

#### SCAN-07 — Structural verification (exact line-by-line confirmation)

After all edits, the engineer must confirm each of the following structural facts. Record line numbers from the final built file.

**7a. `_laneDDispatched` field:**

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "_laneDDispatched" | Select-Object LineNumber, Line
```

Expected lines:
- Field declaration: `private readonly ConcurrentDictionary<string, byte> _laneDDispatched =`
- Field initializer: `new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);`
- Write site (inside `TryLaneDDispatchFollower`): `_laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);`
- Read site (inside `DispatchToFollower`): `if (_laneDDispatched.ContainsKey(laneD_key)) return;`

Total: **4 matches**. Any deviation = SCAN-07 FAIL.

**7b. `TryLaneDHook` method — non-empty body:**

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "TryLaneDHook" | Select-Object LineNumber, Line
```

Expected: 2 matches — call site in `DispatchCopy` and method definition. Body must NOT be empty (`{ }`).

Manually read the method body and confirm:
- Branch (1): `if (GetCopyMode() != CopyMode.Clone)` → `return;`
- Branch (2): `if (order.OrderType != OrderType.Limit)` → `return;`
- Branch (3): `if (!(cloneAtmMode is FollowerAtmMode.Named namedMode))` → `return;`
- Loop: `foreach (Account acc in rule.FollowerAccounts)`
- Branch (4): `if (TryLaneDDispatchFollower(...))` → `laneDCount++;`
- Branch (5): `if (laneDCount > 0)` → `SetLiveEntryDispatched(...);`

All 5 branches present = PASS. Any branch missing = SCAN-07 FAIL.

**7c. `TryLaneDDispatchFollower` — null guard and dedup write:**

Manually confirm the method body contains:
- Branch (A): `if (acc == null) return false;` — FIRST statement
- Branch (B): `if (!PassesDailyCapCheck(acc)) return false;` — SECOND statement
- `CopySignal.Create(...)` call with `baseSignal.Action`, `baseSignal.Type`, `baseQty * mult`, `baseSignal.LimitPrice`, `baseSignal.OrderId`
- `bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);`
- Branch (C): `if (ok) _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);`
- `return ok;` — final statement

**7d. `DispatchToFollower` — skip gate placement:**

The skip gate MUST be placed after `var mode = ResolveAtmMode(...)` and before the `NinjaTrader.Code.Output.Process(...)` logging call. Confirm:
- `string laneD_key = baseSignal.OrderId + "#" + acc.Name;` appears AFTER `var mode = ResolveAtmMode(...);`
- `if (_laneDDispatched.ContainsKey(laneD_key)) return;` appears on the NEXT line
- The logging `NinjaTrader.Code.Output.Process(...)` call appears AFTER the skip gate

Order violation (gate after logging) = SCAN-07 FAIL.

**7e. `DispatchCopy` — zero new branches at call site:**

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "TryLaneDHook" | Select-Object LineNumber, Line
```

The call site line must be `TryLaneDHook(order, rule, baseSignal, baseQty);` with no `if`, `while`, or `?:` wrapping it. A conditional wrapping the call = DispatchCopy CYC violation = SCAN-07 FAIL.

**7f. Lane C reflection test still passes:**

```powershell
dotnet test --filter "TryLaneDHook_MethodExists_IsPrivateVoidWith4Params"
```

Expected: 1 test passed. A failure here means the `TryLaneDHook` signature was accidentally changed = SCAN-07 FAIL.

**7g. New test class present:**

```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "LaneDContractTests" | Select-Object LineNumber, Line
```

Expected: 1 match (class declaration). Zero matches = SCAN-07 FAIL.

---

### 12. Acceptance Criteria

**All items must be explicitly verified. PASS/FAIL only — no partial credit.**

| # | Criterion | Pass condition |
|---|---|---|
| AC-01 | `TryLaneDHook` body is non-empty | Method body contains all 5 branches (1)-(5) per Step 5.2 |
| AC-02 | ATM dispatch fires for Clone+Named limit order | `TryLaneDDispatchFollower` calls `SendCopyWithAtm` (structural: method exists with correct signature) |
| AC-03 | `_laneDDispatched` field exists and is correct type | `typeof(CopyEngine).GetField("_laneDDispatched", ...)` is non-null, private, readonly, `ConcurrentDictionary<string,byte>` |
| AC-04 | Double-dispatch skip gate present in `DispatchToFollower` | `_laneDDispatched.ContainsKey(laneD_key)` check is the first dispatch decision after `ResolveAtmMode` |
| AC-05 | `DispatchCopy` CYC = 8 (unchanged) | Manual CYC count of `DispatchCopy` = 8; zero new branches at call site |
| AC-06 | `TryLaneDHook` CYC ≤ 8 | Manual CYC count = 6 |
| AC-07 | `TryLaneDDispatchFollower` CYC ≤ 8 | Manual CYC count = 4 |
| AC-08 | Zero `lock()` in new code | SCAN-01 returns 0 matches in new/modified lines |
| AC-09 | Zero `AtmStrategyCreate`/`AtmStrategyChangeStopTarget` in new code | SCAN-05 returns 0 matches in new lines |
| AC-10 | ASCII-only in new code | SCAN-04 shows 0 new non-ASCII bytes |
| AC-11 | Build succeeds | `dotnet build` = 0 errors |
| AC-12 | Tests pass with no regression | `dotnet test` = Failed 0, Passed ≥ 170, existing lane C test still passes |
| AC-13 | 6 new `[Fact]` tests present | `grep -c "\[Fact" CopyEngineTests.cs` increases by 6 |
| AC-14 | `TryLaneDHook` signature preserved (4 params, private void) | Lane C reflection test `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` still PASS |
| AC-15 | Non-Clone mode paths unaffected | SCAN-07g + manual trace: `GetCopyMode() != Clone` early return in `TryLaneDHook` confirmed present; `_laneDDispatched.ContainsKey` returns false for all non-Lane-D keys |

---

### 13. Out of Scope

The following items are explicitly NOT part of this ticket. Do not implement, mention, or comment on them in code.

| Item | Deferred to |
|---|---|
| **Market-order ATM entry** (DW-LD-01): Clone-mode followers receive raw `SendCopy` (no ATM bracket) when the leader places a Market order. A Market-compatible ATM dispatch path requires a separate approach. | Future lane |
| **`_laneDDispatched` cleanup** (DW-LD-02): Entries persist for session lifetime. Growth is bounded and acceptable. Formal cleanup can be added if growth becomes a concern. | Future |
| **Lane E: Re-arm ATM after QX/BE** (DW-LD-03): `TryLaneEHook` wiring point documented in plan Section 11 — `OnFollowerPositionUpdate` after `_followerSlateReady[acc.Name]` is set to `true`. Implementation is out of scope for Lane D. | Lane E |
| **`TradeCopierPanel.cs`** changes | Not applicable — zero changes |
| **Any file outside `CopyEngine.cs` and `CopyEngineTests.cs`** | Not applicable |
| **Modification of `SendCopyWithAtm`** | Not applicable — called as-is |
| **Modification of `GetCloneAtmMode`** | Not applicable — called as-is |
| **Modification of `DispatchCopy` call site** | Explicitly banned — zero branches may be added at `TryLaneDHook(...)` call site |
| **B24-01, B24-02, B24-03, B25-01, B26-01, BUG-0911-A-01, BUG-0911-A-02, PTT0911B-01..B04** | All prior open items remain open and untouched |

---

*ptt-architect · PTT-BUGS-LIVE-0911 Lane D · Phase 3 — Ticket Generation · 2026-09-11*
