# PTT-BUGS-LIVE-0911 Lane C — Tickets

**Status:** TICKETS_COMPLETE
**Phase:** 3 (Ticket Generation)
**Author:** ptt-architect
**Plan source:** `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-c.md` (REVIEW_PASS)
**Epic:** PTT-BUGS-LIVE-0911

---

## Ticket LC-T1: Add TryLaneDHook Stub at Lane D Wiring Point in DispatchCopy

### Spec Requirements Addressed

| ID | Requirement |
|---|---|
| Lane C #1 | Confirm `OnOrderUpdate` is the sole, architecturally correct entry for copy dispatch — no Push path is possible or needed before `Account.OrderUpdate` fires. |
| Lane C #2 | Identify the exact Lane D wiring point inside `DispatchCopy` (after `ResolveBaseQty`, before the B119 snapshot block). |
| Lane C #3 | Establish the wiring contract for Lane D by adding an unconditional stub call `TryLaneDHook(order, rule, baseSignal, baseQty)` at the identified wiring point. |
| Lane C #4 | Confirm zero behavior change — `TryLaneDHook` has an empty body; no existing dispatch path is altered. |

---

### Files Touched

| File | Change type | Location |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | Insert 1 call site (1 line) | Line 2630 — between `ResolveBaseQty` (line 2629) and the B119 comment (line 2631) |
| `src/PropTraderTools/CopyEngine.cs` | Insert 1 new private method (8 lines including doc comment) | After the closing brace of `DispatchCopy` — append below the last line of `DispatchCopy` body |
| `src/PropTraderTools/CopyEngineTests.cs` | Insert 1 new `[Fact]` test | Append to `CopyEngineTests` class body |

---

### Implementation Instructions

#### Step 1 — Insert the unconditional call at the wiring point in `DispatchCopy`

Locate this exact block in `src/PropTraderTools/CopyEngine.cs`:

```
// Line 2629
int baseQty = ResolveBaseQty(order.Instrument, baseSignal.Quantity);

// Line 2631 (currently immediately follows line 2629 after the blank line)
// B119: DW-B128 -- snapshot instrument and last direction once before the loop.
```

Insert **one line** in the blank gap between line 2629 and the B119 comment (line 2631).
The result must read exactly:

```csharp
// B9 T1: ATR base qty -- overrides signal qty when ATR enabled, else uses signal qty
int baseQty = ResolveBaseQty(order.Instrument, baseSignal.Quantity);

// Lane C: Lane D wiring point. Unconditional void call -- 0 CYC cost in parent. PTT-BUGS-LIVE-0911.
TryLaneDHook(order, rule, baseSignal, baseQty);

// B119: DW-B128 -- snapshot instrument and last direction once before the loop.
```

**Nothing else in `DispatchCopy` is modified.** The `DispatchCopy` method signature, gate chain, follower loop, dedup commit, and direction tracking are all untouched.

---

#### Step 2 — Add the `TryLaneDHook` private method stub

Locate the closing brace `}` that ends `DispatchCopy` (the method that begins at line 2561).
Immediately after that closing brace, insert the following new private method.
No other method is modified.

```csharp
// Lane C: Lane D wiring stub -- PTT-BUGS-LIVE-0911.
// Called once per confirmed leader entry signal, after all 5 dispatch gates have passed
// and after ResolveBaseQty. Before the B119 instrument/direction snapshot.
// CYC=1 (empty body -- base branch only). JS-001: no throw. JS-021: no lock. ASCII-only.
// Lane D: replace this empty body with the Lane D implementation.
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
{
}
```

---

### Method Signatures

```csharp
// Call site inserted in DispatchCopy (src/PropTraderTools/CopyEngine.cs, after line 2629):
TryLaneDHook(order, rule, baseSignal, baseQty);

// New private method (src/PropTraderTools/CopyEngine.cs, appended after DispatchCopy closing brace):
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
```

Parameter types:
- `order` — `NinjaTrader.Cbi.Order` (the confirmed master entry order, all gates passed)
- `rule` — `PropTraderTools.CopyEngine.CopyRule` (the matched, enabled copy rule)
- `baseSignal` — `PropTraderTools.CopyEngine.CopySignal` (value-type signal, passed by value, already resolved)
- `baseQty` — `int` (ATR-scaled or signal quantity, already resolved by `ResolveBaseQty`)
- Return type: `void`

---

### xUnit Test Names

Append the following single test to the `CopyEngineTests` class in `src/PropTraderTools/CopyEngineTests.cs`.
Follow the existing file conventions: `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`, use `GetMethod` reflection helper, skip attribute matching the pattern in the file.

```csharp
[Fact]
public void TryLaneDHook_MethodExists_IsPrivateVoidWith4Params()
```

**What this test asserts (in order):**

1. `typeof(CopyEngine)` has a method named `TryLaneDHook` discoverable via `BindingFlags.NonPublic | BindingFlags.Instance`.
2. The method is not null (i.e., it exists).
3. `method.ReturnType == typeof(void)`.
4. `method.GetParameters().Length == 4`.

This test does **not** require a running NT8 host — it uses reflection only. Do **not** apply `Skip`.
The reflection helper `GetMethod(string name)` is already defined in the test class (line 24 of the file). Use it.

Full test body to insert:

```csharp
[Fact]
public void TryLaneDHook_MethodExists_IsPrivateVoidWith4Params()
{
    var method = GetMethod("TryLaneDHook");
    Assert.NotNull(method);
    Assert.Equal(typeof(void), method.ReturnType);
    Assert.Equal(4, method.GetParameters().Length);
}
```

---

### NT8 Constraints

| Constraint | Application to this ticket |
|---|---|
| `StartAtmStrategy` is STATIC — `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` | N/A — `TryLaneDHook` stub does not call StartAtmStrategy. |
| `AtmStrategyCreate()` — BANNED in AddOnBase | N/A — stub has empty body. |
| `AtmStrategyChangeStopTarget()` — BANNED in AddOnBase | N/A — stub has empty body. |
| Order name must be `"Entry"` for ATM arming | N/A — stub has empty body, no CreateOrder call. |
| `"PTT-"` prefix on all non-ATM entry order names | N/A — stub has empty body, no CreateOrder call. |
| `DispatchCopy` is on NT8 account background thread | `TryLaneDHook` inherits the same thread. No `Dispatcher.InvokeAsync` needed in an empty stub. Lane D implementor must note the thread context when adding logic. |
| `DispatchCopy` CYC=8 (at limit) | An unconditional void call adds 0 branches. CYC remains 8. Verified: a call statement with no conditional is not a branch in McCabe complexity. |
| `TryLaneDHook` CYC budget | Empty body = CYC=1 (base path only). Confirmed within the ≤8 limit. |

---

### 7-Scan Checklist (Engineer Contract)

The engineer MUST complete every scan and confirm the result before marking this ticket done.
Paste scan output into the PR description.

---

**SCAN-01 — lock() grep**

```
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```

Required result: **zero matches**.
`TryLaneDHook` has an empty body. No lock introduced. If any match is found, the implementation is non-compliant — do not proceed.

---

**SCAN-02 — CYC manual count of new/modified methods**

Methods to count:

| Method | Expected CYC after change |
|---|---|
| `DispatchCopy` | 8 (unchanged — unconditional void call adds 0 branches) |
| `TryLaneDHook` | 1 (empty body — base path only) |

Verify with Lizard or manual McCabe count. Both must be ≤ 8. Document the count in the PR.

---

**SCAN-03 — async/await grep in new/modified methods**

```
grep -n "async\|await" src/PropTraderTools/CopyEngine.cs
```

Scope: `TryLaneDHook` method body and the modified line in `DispatchCopy`.
Required result: **zero matches** in the new code. No async/await may appear in NT8 lifecycle or event-handler descendant methods.

---

**SCAN-04 — non-ASCII bytes**

```
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```

Required result: **zero new matches** in lines added by this ticket (the call site comment and the `TryLaneDHook` method block). If existing non-ASCII bytes are present in unrelated lines, do not fix them (surgical change rule) — confirm none exist in the new lines only.

---

**SCAN-05 — ATM API grep in new code**

```
grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs
```

Scope: new lines added by this ticket only.
Required result: **zero matches**. `TryLaneDHook` stub has an empty body; no ATM API is introduced.

---

**SCAN-06 — dotnet build + dotnet test**

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
dotnet test src/PropTraderTools/CopyEngineTests.cs
```

Required result: **0 errors, 0 failures**.
`TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` must appear in the test run and pass (it is not Skipped — it uses reflection only, no NT8 host required).
All pre-existing Skipped tests must remain Skipped (no regressions to Skip status).

---

**SCAN-07 — Structural verification**

Verify each of the following by opening the file and confirming line content:

| Check | What to verify |
|---|---|
| Call site in `DispatchCopy` | Line immediately after `int baseQty = ResolveBaseQty(...)` is `TryLaneDHook(order, rule, baseSignal, baseQty);` |
| B119 block unchanged | The line `// B119: DW-B128 -- snapshot instrument and last direction once before the loop.` immediately follows the new call site (no lines removed or shifted above it) |
| `TryLaneDHook` definition | Method is `private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)` with an empty body `{ }` |
| `TryLaneDHook` position | Method definition is located after the closing brace of `DispatchCopy`, not inside it |
| No new fields | No new instance or static fields added to `CopyEngine` by this ticket |
| No other method modified | `DispatchToFollower`, `OnOrderUpdate`, `SendCopy`, `SendCopyWithAtm`, and all other existing methods are byte-for-byte identical to pre-ticket source |
| Test file | `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` is present in `CopyEngineTests`, is not Skipped, and references `GetMethod("TryLaneDHook")` |

---

*Return: TICKETS_COMPLETE*
