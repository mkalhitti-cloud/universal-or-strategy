# B112 Tickets

**Status**: TICKETS_COMPLETE
**Phase**: 3 (Ticket Generation)
**Date**: 2026-08-26
**Author**: ptt-architect
**Plan reviewed**: docs/brain/B112/02-plan-review.md — REVIEW_PASS (Cycle 2, final)
**Source verified**: src/PropTraderTools/CopyEngine.cs L3307-3352 (character-for-character before writing)

---

## Ticket T1 — CountLeaderTargets DW-B116 Fix

### Metadata

| Property | Value |
|----------|-------|
| **Block** | B112 |
| **Ticket** | T1 (only ticket in this block) |
| **Defects closed** | DW-B116 (P1), DW-B113 (P0 side-effect), DW-B114 (P1 side-effect track-only) |
| **File** | `src/PropTraderTools/CopyEngine.cs` |
| **Method** | `private int CountLeaderTargets(Instrument instrument)` |
| **Lines** | L3307 (header comment) – L3352 (closing brace) |
| **Current CYC** | 4 (project convention) / 6 (McCabe) |
| **Required CYC after fix** | 4 (project convention) / 6 (McCabe) — UNCHANGED |

---

### Spec Requirements Satisfied

| Defect ID | Priority | Description | Resolution |
|-----------|----------|-------------|------------|
| **DW-B116** | P1 | `CountLeaderTargets` returns 5 for a 3-target ATM when stale residue orders are present | Fixed by Changes 1 + 2 + 3 below |
| **DW-B113** | P0 | Bracketless position after BE-retry cap exhaustion | Resolved as DW-B116 side-effect — no additional code change |
| **DW-B114** | P1 | `_beReplaceAttempts` double-increment | Track-only — resolves as DW-B116 side-effect; no change to increment site |

---

### Changes Required

Apply **all four changes inside `CountLeaderTargets` only**, in the order listed.
No other method is modified. No new files are modified other than the new test file.

---

#### CHANGE 1 — Narrow `isTarget` predicate (L3332-3347)

Remove the `PTT-QX-T*` OR branch (L3341-3345) and the `PTT-BE-Target-*` OR branch (L3346).
Retain only the native `Target1..9` check (L3335-3340). The outer `!string.IsNullOrEmpty`
guard is inlined into a flat conjunction — no parenthesised OR groups remain.

**BEFORE (L3332-3347 — verified from source):**

```csharp
                bool isTarget =
                    !string.IsNullOrEmpty(o.Name)
                    && (
                        (
                            o.Name.Length >= 7
                            && o.Name.StartsWith("Target", StringComparison.Ordinal)
                            && char.IsDigit(o.Name[6])
                            && o.Name[6] != '0'
                        )
                        || (
                            o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                            && o.Name.Length > 8
                            && char.IsDigit(o.Name[8])
                        )
                        || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                    );
```

**AFTER:**

```csharp
                bool isTarget =
                    !string.IsNullOrEmpty(o.Name)
                    && o.Name.Length >= 7
                    && o.Name.StartsWith("Target", StringComparison.Ordinal)
                    && char.IsDigit(o.Name[6])
                    && o.Name[6] != '0';
```

**JS constraints**: ASCII-only strings. No throw (JS-001). No lock (JS-021). No return null (JS-002).
No new branches — CYC unchanged.

---

#### CHANGE 2 — Narrow `stateOk` to Working only (L3325-3328)

Remove the `|| o.OrderState == OrderState.Accepted` and `|| o.OrderState == OrderState.Submitted`
OR terms. `stateOk` becomes a single equality check.

**BEFORE (L3325-3328 — verified from source):**

```csharp
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted;
```

**AFTER:**

```csharp
                bool stateOk = o.OrderState == OrderState.Working;
```

**JS constraints**: No new branch added (JS-021/001/002). CYC unchanged.
No lock. No throw.

---

#### CHANGE 3 — Cap return at `Math.Min(count, 3)` (L3351)

Replace the bare `return count` with a hard cap of 3.
A standard ATM has at most 3 target slots (Target1, Target2, Target3).
This defensive cap ensures that even if future predicates widen,
the return value cannot exceed the valid range.

**BEFORE (L3351 — verified from source):**

```csharp
            return count;
```

**AFTER:**

```csharp
            return Math.Min(count, 3);
```

**JS constraints**: `Math.Min` is a pure expression — no branch added, CYC unchanged.
No allocation. No throw (JS-001). No lock (JS-021). Returns `int` (JS-002 intact).

---

#### CHANGE 4 — Update method header comment (L3307-3311)

Replace the existing header comment block to reflect the Working-only filter,
the native Target1..9 restriction, the Math.Min cap, and the DW-B116 fix reference.
Comment change only — no executable code modified.

**BEFORE (L3307-3311 — verified from source):**

```csharp
        // CountLeaderTargets: CYC=4. Returns the number of Working/Accepted/Submitted target
        // limit orders on the leader account for the given instrument. Used by MoveStopToBreakEven
        // to detect partial-target visibility on follower accounts (DW-B79-07).
        // Matches the same name filter as Step A's isAtmTarget predicate.
        // JS-021: no lock. JS-001: no throw. JS-002: returns int (never negative).
```

**AFTER:**

```csharp
        // CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders
        // (Target1..Target9, digit 1-9, no PTT- prefix) on the leader account for the given
        // instrument. Working-only (DW-B116: Accepted/Submitted removed -- transitional states
        // cause overcount). Capped at Math.Min(count,3) -- standard ATM max 3 targets.
        // Used by MoveStopToBreakEven to detect partial-target visibility on followers (DW-B79-07).
        // DW-B116 fix: removed PTT-QX-T* and PTT-BE-Target-* from isTarget predicate.
        // JS-021: no lock. JS-001: no throw. JS-002: returns int (never negative). ASCII-only.
```

**JS constraints**: ASCII-only. Comment change only — no executable code introduced.

---

### CYC Verification (engineer must confirm after implementing)

The AFTER code retains all 6 decision points that exist in the BEFORE code.
Changes 1 and 2 *remove* OR terms from existing boolean expressions (not branches).
Change 3 substitutes one pure expression for another. Change 4 is comment-only.
No `if`, `else if`, `while`, `for`, ternary, or `??` operator is added.

| # | Decision Point | Code Location | Counted in project CYC |
|---|---------------|---------------|----------------------|
| 1 | `if (rule == null) return 0` | L3315-3316 | YES |
| 2 | `if (leader == null) return 0` | L3318-3319 | YES |
| 3 | `foreach (Order o in leader.Orders)` | L3321 | YES |
| 4 | `if (o == null) continue` | L3323 | NO (null-guard pre-condition) |
| 5 | `if (!stateOk \|\| !instrOk \|\| ...)` | L3330 | NO (filter pre-condition) |
| 6 | `if (isTarget) count++` | L3348-3349 | YES |

**CYC = 4 (project convention, unchanged). McCabe full count = 6 (unchanged).**

Engineer must manually count branches in the AFTER code and confirm this table
is still accurate before committing.

---

### Jane Street Pre-Flight Checklist (engineer runs before commit)

- [ ] `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — must return 0 results inside `CountLeaderTargets` region (JS-021)
- [ ] `grep -n "async void" src/PropTraderTools/CopyEngine.cs` — count must be unchanged from baseline; 0 new `async void` in `CountLeaderTargets` (JS-033)
- [ ] `grep -n "return null" src/PropTraderTools/CopyEngine.cs` — `CountLeaderTargets` returns `int`; no null return possible (JS-002)
- [ ] `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` — 0 non-ASCII characters in modified region (ASCII mandate)
- [ ] Manual branch count of AFTER code confirms CYC = 4 (project convention) — table above matches
- [ ] Read `docs/standards/jane-street/RULES_CATALOG.md` JS-021, JS-001, JS-002, JS-033 before editing

---

### Test File to Create

**File**: `src/PropTraderTools/Tests/B112Tests.cs`
**Framework**: xUnit ONLY. No NUnit. No MSTest.
**Async**: None — all 5 tests are synchronous `[Fact]` methods (JS-033: no `async void`).

> **Implementation note**: `CountLeaderTargets` is `private`. Tests must either:
> (a) use a test-seam subclass that exposes the method as `internal protected`, or
> (b) use `PrivateObject` / reflection to invoke via `MethodInfo.Invoke`, or
> (c) test through the public `MoveStopToBreakEven` trigger path with a fake `leader.Orders` stub.
> The engineer chooses the approach consistent with the existing test infrastructure
> in `src/PropTraderTools/Tests/`. The test names and assertions below are the contract;
> the access mechanism is the engineer's implementation detail.

---

#### T_B112_01 — `CountLeaderTargets_Returns3_WhenLeaderHas3WorkingNativeTargets`

**Purpose**: Nominal path — 3 Working native ATM targets → method returns 3.

**Arrange**:
- Build a fake `leader.Orders` collection containing exactly 3 `Order` objects:
  - `{ Name = "Target1", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }`
  - `{ Name = "Target2", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }`
  - `{ Name = "Target3", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }`
- Configure the SUT so `FindRule(instrument)` returns a rule whose `MasterAccount.Orders` is this collection.

**Act**: Call `CountLeaderTargets(testInstrument)`.

**Assert**: `result == 3`

---

#### T_B112_02 — `CountLeaderTargets_ExcludesPttBeTargetResidues`

**Purpose**: CHANGE 1 — `PTT-BE-Target-*` orders are NOT counted.

**Arrange**:
- Build `leader.Orders` with:
  - 3 Working native targets (`Target1`, `Target2`, `Target3`) — `OrderState.Working`, `OrderType.Limit`, matching instrument.
  - 2 stale PTT-BE residue orders: `{ Name = "PTT-BE-Target-4", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }` and `{ Name = "PTT-BE-Target-5", ... }`.

**Act**: Call `CountLeaderTargets(testInstrument)`.

**Assert**: `result == 3` (not 5)

---

#### T_B112_03 — `CountLeaderTargets_ExcludesPttQxTResidues`

**Purpose**: CHANGE 1 — `PTT-QX-T*` orders are NOT counted.

**Arrange**:
- Build `leader.Orders` with:
  - 3 Working native targets (`Target1`, `Target2`, `Target3`) — `OrderState.Working`, `OrderType.Limit`, matching instrument.
  - 2 stale Quick-Exit orders: `{ Name = "PTT-QX-T1", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }` and `{ Name = "PTT-QX-T2", ... }`.

**Act**: Call `CountLeaderTargets(testInstrument)`.

**Assert**: `result == 3` (not 5)

---

#### T_B112_04 — `CountLeaderTargets_CapsAt3_WhenMoreThan3NativeTargets`

**Purpose**: CHANGE 3 — `Math.Min(count, 3)` hard cap fires when native count > 3.

**Arrange**:
- Build `leader.Orders` with 5 Working native targets (`Target1` through `Target5`),
  each `OrderState.Working`, `OrderType.Limit`, matching instrument.
  (Simulates a 5-target ATM or residue from a wider ATM config.)

**Act**: Call `CountLeaderTargets(testInstrument)`.

**Assert**: `result == 3` (Math.Min cap applied)

---

#### T_B112_05 — `CountLeaderTargets_ExcludesAcceptedAndSubmittedNativeTargets`

**Purpose**: CHANGE 2 — `OrderState.Accepted` and `OrderState.Submitted` orders are NOT counted.

**Arrange**:
- Build `leader.Orders` with:
  - 1 Working native target: `{ Name = "Target1", OrderState = OrderState.Working, OrderType = OrderType.Limit, Instrument.FullName = <testInstrument.FullName> }`.
  - 2 native targets in `Accepted` state: `{ Name = "Target2", OrderState = OrderState.Accepted, ... }`, `{ Name = "Target3", OrderState = OrderState.Accepted, ... }`.
  - 2 native targets in `Submitted` state: `{ Name = "Target4", OrderState = OrderState.Submitted, ... }`, `{ Name = "Target5", OrderState = OrderState.Submitted, ... }`.

**Act**: Call `CountLeaderTargets(testInstrument)`.

**Assert**: `result == 1` (only the Working target counts; Accepted + Submitted excluded; cap not reached)

---

### 7-Scan Checklist (SCAN-01 through SCAN-07) — ENGINEER CONTRACT

The engineer MUST run and record the result of every scan before reporting T1 complete.
All scans must PASS. A failing scan is a blocker — do not commit until resolved.

---

#### SCAN-01 — No `lock()` in modified region

**Command**:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | Select-String "3[0-3][0-9][0-9]"
```
**Pass criterion**: 0 results in the L3307-L3352 region.

---

#### SCAN-02 — No `async void` introduced

**Command**:
```powershell
grep -n "async void" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**: Count of `async void` occurrences is unchanged from baseline.
`CountLeaderTargets` is `private int` — no `async void` is possible in this method.

---

#### SCAN-03 — No `return null` introduced

**Command**:
```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**: `CountLeaderTargets` returns `int`; no `return null` is possible.
Count must be unchanged from baseline.

---

#### SCAN-04 — ASCII-only strings and comments in modified region

**Command**:
```powershell
$lines = Get-Content src/PropTraderTools/CopyEngine.cs
$lines[3306..3351] | ForEach-Object { if ($_ -match '[^\x00-\x7F]') { $_ } }
```
**Pass criterion**: 0 lines with non-ASCII characters in L3307-L3352.

---

#### SCAN-05 — CYC = 4 (project convention) verified manually

**Evidence required**: Engineer manually counts decision points in AFTER code
and confirms the 6-row table in the CYC Verification section above is still accurate.

**Pass criterion**:
- Exactly 4 decision points count toward project CYC (rows marked YES in table).
- No new `if`, `else if`, ternary, `??`, `while`, or `for` introduced.
- McCabe count = 6 (stable).

---

#### SCAN-06 — Only `CountLeaderTargets` modified in `CopyEngine.cs`

**Command (file scope)**:
```powershell
git diff --name-only src/PropTraderTools/CopyEngine.cs
```
Must show only `src/PropTraderTools/CopyEngine.cs`.

**Command (line scope — must show only lines within L3307-L3352)**:
```powershell
git diff src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Where-Object { $_ -notmatch "^\+\+\+" }
```
All `+` lines must be within the L3307-L3352 region. Any `+` line outside
L3307-L3352 is a scope violation — revert and fix.

**Pass criterion**: 0 changed lines outside L3307-L3352 in `CopyEngine.cs`.

---

#### SCAN-07 — `ptt-sync-and-verify.ps1` passes 0 MISMATCH

**Command**:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
**Pass criterion**: Output contains 0 lines matching `MISMATCH`.
After passing, the Director must press **F5** in NinjaTrader 8 to confirm
compilation succeeds with 0 errors.

---

### Completion Artifact

After all 4 changes are implemented, all 5 tests pass, and all 7 scans pass,
engineer writes:

**File**: `docs/brain/B112/ticket-1-completion.md`

Minimum content:
- Date and engineer identifier
- Confirmation that CHANGE 1-4 were applied exactly as specified
- SCAN-01 through SCAN-07 results (PASS for each)
- xUnit test run output (5/5 passing)
- `ptt-sync-and-verify.ps1` output (0 MISMATCH)
- F5 NT8 compilation result (0 errors)

---

## Files Modified by This Block

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `CountLeaderTargets` L3307-L3352 only (4 surgical changes) |
| `src/PropTraderTools/Tests/B112Tests.cs` | NEW — 5 xUnit `[Fact]` tests |

## Files NOT Modified

All files not listed above are **out of scope** for B112. No exceptions.

| Files | Reason |
|-------|--------|
| All other methods in `CopyEngine.cs` | Zero callers broken; signature unchanged |
| `MoveStopToBreakEven` | Receives corrected `int` from `CountLeaderTargets` — no logic change needed |
| `SnapshotBeTargets` | Separate concern (DW-B107/B108) |
| `TryReplacePttBeBrackets` | Not in scope |
| All other `.cs` files in `src/PropTraderTools/` | Not touched |
| `CopyEngineTests.cs` | Pre-existing test infrastructure issues tracked separately (DW-PTT-BE-FIX-03) |
