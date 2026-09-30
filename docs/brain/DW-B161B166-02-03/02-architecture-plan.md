# Architecture Plan: DW-B161B166-02-03
**Epic:** DW-B161B166-02-03
**Source file:** `src/PropTraderTools/CopyEngine.cs`
**Phase:** 1 (Architecture)
**Date:** 2025-01

---

## LANE-SPLIT GATE

| Question | Answer | Evidence |
|---|---|---|
| Q1. Same method or within 50 lines? | **NO** | DW-B161B166-02 touches L3085-3155; DW-B161B166-03 touches L5208-5209. ~2054 lines apart, different methods. |
| Q2. Fix B design depends on Fix A final design? | **NO** | DW-B161B166-03 is a standalone comment about TryTriggerBeRecovery prefix asymmetry. No dependency on the rename. |
| Q3. Each fix has standalone value if the other is blocked? | **YES** | 02 = clarifies predicate naming. 03 = documents intentional asymmetry. Independent value each. |
| Q4. Each fix has an independent SIM verification path? | **YES** | 02: grep IsPttBeStopActiveState = 0 hits post-change. 03: grep B162-B in TryTriggerBeRecovery area. Both build-verifiable independently. |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

> Operational note: Both changes are trivially small (rename + comment). Running as a single pipeline ticket is acceptable for efficiency. The gate approves lanes; the engineer may implement as one PR.

---

## Summary

### DW-B161B166-02: Rename `IsPttBeStopActiveState` -> `IsPttBeOrderActiveState`

The private static predicate `IsPttBeStopActiveState` is used as the active-state guard
for both Stop orders (`HasActivePttBeExits`) and Target orders (`HasActivePttBeTargets`).
The "Stop" in the name is misleading because it implies the predicate only applies to Stop
orders. Renaming to `IsPttBeOrderActiveState` makes the scope clear.

**Zero logic change.** Method body at L3089 is untouched:
```
=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;
```

### DW-B161B166-03: Add asymmetry comment to `TryTriggerBeRecovery` at L5208

`TryTriggerBeRecovery` uses `"PTT-BE-Stop-"` (WITH trailing dash) at L5208, while
`HasActivePttBeExits` (L3110) and `StillAtNonBePrice` use `"PTT-BE-Stop"` (WITHOUT
trailing dash, per B162-B fix). A future maintainer may incorrectly flag L5208 as a
missed B162-B fix. The comment expansion documents that the asymmetry is intentional by design.

**Zero logic change.** The `if (!o.Name.StartsWith(...))` guard at L5208 is untouched.

---

## Grep Evidence

### (a) Occurrences of `IsPttBeStopActiveState` in `CopyEngine.cs`

Grep command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "IsPttBeStopActiveState"`

```
LineNumber  Line
----------  ----
3088        private static bool IsPttBeStopActiveState(OrderState s)
3101        //   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
3112        if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
3144        //   Reuses IsPttBeStopActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
3155        if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

**ACTUAL COUNT: 5 occurrences** (2 code + 2 call sites + 1 additional comment at L3101).

> **SPEC DISCREPANCY**: The spec stated "exactly 3 occurrences" counting only the definition
> and 2 call sites. Grep reveals 5 total: 3 code occurrences + 2 comment-only lines (L3101 and L3144).
> The spec explicitly listed L3144 as a sub-change but omitted L3101.
> **This plan adds L3101 as sub-change 02-C** to ensure the old name is fully eliminated.

### (b) Occurrences in other `.cs` files under `src/PropTraderTools/`

Grep command: `Get-ChildItem -Path "src\PropTraderTools\" -Filter "*.cs" -Recurse | ForEach-Object { ... }`

**Result: ALL 5 occurrences are in `CopyEngine.cs` only. No other .cs file references `IsPttBeStopActiveState`.**

Confirmed private static scope: symbol is inaccessible outside `CopyEngine.cs`. Safe to rename.

---

## CYC Analysis

### (c) `IsPttBeOrderActiveState` after rename -- CYC = 3 (unchanged)

Method body (L3089, unchanged):
```csharp
=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;
```

McCabe count:
- base = 1
- `||` (first) = +1
- `||` (second) = +1
- **CYC = 3**

The rename touches only the identifier at L3088. The body at L3089 is not modified.
CYC is structurally unchanged.

### (e) `TryTriggerBeRecovery` CYC after DW-B161B166-03 -- CYC = 6 (unchanged)

The change at L5209 expands a single-line comment into a multi-line comment block.
Comments contain no decision points. McCabe's algorithm counts only: `if`, `while`, `for`,
`case`, `||` / `&&` in conditional expressions, ternary `?`.

Adding comment lines adds **zero** to McCabe CYC. `TryTriggerBeRecovery` CYC remains at 6.

---

## ASCII Compliance

### (d) DW-B161B166-03 comment text -- ASCII-only check

New comment text (char-by-char scan):
```
// (5) DW-B90: only Stop-* triggers recovery.
// B162-B: "PTT-BE-Stop-" WITH dash is intentional here.
// Only suffixed follower Stop orders (PTT-BE-Stop-1, PTT-BE-Stop-2)
// trigger bracket recovery. Bare "PTT-BE-Stop" (leader atomic stop)
// must NOT. HasActivePttBeExits and StillAtNonBePrice use the bare
// prefix intentionally -- that asymmetry is correct by design.
```

Characters used: A-Z, a-z, 0-9, space, `/`, `(`, `)`, `-`, `.`, `*`, `,`, `"`, `+`.
All are 7-bit ASCII (0x20-0x7E range).
No curly quotes (U+2018/U+2019/U+201C/U+201D).
No en-dash (U+2013) or em-dash (U+2014) -- double hyphen `--` used instead.
No Unicode identifiers.

**JS-042 PASS: all new comment text is ASCII-only.**

### (f) DW-B161B166-02 new comment text -- ASCII-only check

New header comment at L3085:
```
// B160 DW-B160: State predicate for Signals 2+4 -- extracted to fix V-01 CYC violation.
//   Renamed IsPttBeStopActiveState -> IsPttBeOrderActiveState (DW-B161B166-02):
//   predicate is used for both Stop and Target order state checks.
```

All characters: A-Z, a-z, 0-9, space, `/`, `+`, `-`, `>`, `.`, `(`, `)`, `:`.
All 7-bit ASCII. No Unicode.

**JS-042 PASS.**

---

## Complete Sub-Change List (7 sub-changes, confirmed line numbers)

All changes are in `src/PropTraderTools/CopyEngine.cs` only.

### DW-B161B166-02 (6 sub-changes)

| ID | Line | Type | Description |
|---|---|---|---|
| 02-A | L3085 | Comment | Expand header: "Signal 2" -> "Signals 2+4", add rename note |
| 02-B | L3088 | Identifier | Method definition rename |
| 02-C | L3101 | Comment | Update comment in HasActivePttBeExits header (**spec gap -- found by grep**) |
| 02-D | L3112 | Call site | Rename call in HasActivePttBeExits body |
| 02-E | L3144 | Comment | Update comment in HasActivePttBeTargets header |
| 02-F | L3155 | Call site | Rename call in HasActivePttBeTargets body |

#### 02-A: L3085 -- header comment (before L3086)

```
OLD:
        // B160 DW-B160: State predicate for Signal 2 -- extracted to fix V-01 CYC violation.

NEW:
        // B160 DW-B160: State predicate for Signals 2+4 -- extracted to fix V-01 CYC violation.
        //   Renamed IsPttBeStopActiveState -> IsPttBeOrderActiveState (DW-B161B166-02):
        //   predicate is used for both Stop and Target order state checks.
```

#### 02-B: L3088 -- method definition

```
OLD: private static bool IsPttBeStopActiveState(OrderState s)
NEW: private static bool IsPttBeOrderActiveState(OrderState s)
```

#### 02-C: L3101 -- comment in HasActivePttBeExits header (SPEC GAP)

```
OLD: //   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
NEW: //   State check is a single call to IsPttBeOrderActiveState -- no inline || chains.
```

#### 02-D: L3112 -- call site in HasActivePttBeExits

```
OLD: if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
NEW: if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8)
```

#### 02-E: L3144 -- comment in HasActivePttBeTargets header

```
OLD: //   Reuses IsPttBeStopActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
NEW: //   Reuses IsPttBeOrderActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```

#### 02-F: L3155 -- call site in HasActivePttBeTargets

```
OLD: if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
NEW: if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

---

### DW-B161B166-03 (1 sub-change)

| ID | Line | Type | Description |
|---|---|---|---|
| 03-A | L5208-5209 | Comment | Expand single-line comment to multi-line asymmetry explanation |

#### 03-A: L5208-5209 -- comment in TryTriggerBeRecovery

```
OLD:
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery

NEW:
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery.
                        // B162-B: "PTT-BE-Stop-" WITH dash is intentional here.
                        // Only suffixed follower Stop orders (PTT-BE-Stop-1, PTT-BE-Stop-2)
                        // trigger bracket recovery. Bare "PTT-BE-Stop" (leader atomic stop)
                        // must NOT. HasActivePttBeExits and StillAtNonBePrice use the bare
                        // prefix intentionally -- that asymmetry is correct by design.
```

---

## Jane Street Rule Checklist

| Rule | DW-B161B166-02 | DW-B161B166-03 |
|---|---|---|
| JS-001: no throw in dispatch path | PASS (no throw added) | PASS (no code added) |
| JS-021: no lock() | PASS (no lock added) | PASS (no lock added) |
| JS-042: ASCII-only identifiers and strings | PASS (see ASCII section) | PASS (see ASCII section) |
| CYC <= 8 per method | PASS (IsPttBeOrderActiveState=3, no new methods) | PASS (TryTriggerBeRecovery=6, unchanged) |
| No DateTime.Now | N/A | N/A |
| No FontFamily | N/A | N/A |
| No hardcoded hex colors | N/A | N/A |

---

## NT8 API Usage

Neither change introduces any NT8 API calls. No `Account`, `Order`, `CreateOrder`,
`Dispatcher.InvokeAsync`, or any other NT8 type is added or modified.

---

## Threading Model

Not applicable. Both changes are text-only (identifier rename + comment expansion).
No state, no concurrency, no Dispatcher usage added.

---

## Risk Assessment

| Factor | DW-B161B166-02 | DW-B161B166-03 |
|---|---|---|
| Logic change | None | None |
| Runtime behavior change | None | None |
| CYC change | None (CYC=3 preserved) | None (CYC=6 preserved) |
| Scope of change | 1 file, 6 text substitutions | 1 file, 1 comment expansion |
| Cross-file impact | None (private static, single file) | None (comment only) |
| Build risk | Compile-time check: rename = catch-all | No compilation impact |
| Test impact | No behavior change -- existing tests pass unchanged | No behavior change |

**Overall risk: VERY LOW.** These are documentation and naming corrections with no runtime effect.

The compiler will flag any missed occurrence of the old name at compile time, making
the rename inherently safe. The comment change has no compilation impact.

---

## SIM Verification Paths

### DW-B161B166-02 verification

1. Build `src/PropTraderTools/` -- must succeed with 0 errors.
2. `grep IsPttBeStopActiveState CopyEngine.cs` -- must return **0 hits**.
3. `grep IsPttBeOrderActiveState CopyEngine.cs` -- must return **5 hits**:
   - L3085 (comment), L3088 (definition), L3101 (comment), L3112 (call), L3144 (comment), L3155 (call).

   > Wait: that is 6 hits. Sub-changes 02-A and 02-C add the name to two comment locations that
   > did not previously have `IsPttBeOrderActiveState`. Let me recount:
   >
   > After changes, `IsPttBeOrderActiveState` appears at:
   > - L3085 (new rename note in comment): 1 occurrence
   > - L3088 (definition): 1 occurrence
   > - L3101 (updated comment): 1 occurrence
   > - L3112 (call site): 1 occurrence
   > - L3144 (updated comment): 1 occurrence
   > - L3155 (call site): 1 occurrence
   > **Total: 6 hits for IsPttBeOrderActiveState.**
   >
   > `IsPttBeStopActiveState` will appear 0 times.

4. `grep IsPttBeStopActiveState src\PropTraderTools\*.cs` -- must return **0 hits** across all files.

### DW-B161B166-03 verification

1. Build `src/PropTraderTools/` -- must succeed with 0 errors (comment-only change).
2. Read L5208-5215 in `CopyEngine.cs` -- confirm multi-line comment with `B162-B` present.
3. Confirm CYC of `TryTriggerBeRecovery` is still reported as 6 by static analysis.

---

## Files to Modify

| File | Changes |
|---|---|
| `src/PropTraderTools/CopyEngine.cs` | 7 text substitutions (6 for DW-B161B166-02, 1 for DW-B161B166-03) |

No new files. No other files.

---

PLAN_READY
