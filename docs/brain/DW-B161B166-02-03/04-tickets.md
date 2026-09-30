# Tickets: DW-B161B166-02-03
**Author:** ptt-architect (Phase 3)
**Plan source:** `docs/brain/DW-B161B166-02-03/02-architecture-plan.md` (REVIEW_PASS)
**Plan review:** `docs/brain/DW-B161B166-02-03/02-plan-review.md` (GATE: REVIEW_PASS)
**Target file:** `src/PropTraderTools/CopyEngine.cs`
**Date:** 2025-01

---

## Ticket T1: DW-B161B166-02 -- Rename `IsPttBeStopActiveState` -> `IsPttBeOrderActiveState`

### Spec Requirements Satisfied
- DW-B161B166-02: Eliminate misleading "Stop" qualifier from predicate name used by both Stop and Target order state checks.
- All 5 occurrences of the old name (2 code sites + 2 call sites + 1 additional comment) must be renamed. Zero old-name occurrences may remain in any `.cs` file under `src/PropTraderTools/`.

### File Path
`src/PropTraderTools/CopyEngine.cs`

### Method Signatures (unchanged -- rename only)
```csharp
// BEFORE:
private static bool IsPttBeStopActiveState(OrderState s)
// AFTER:
private static bool IsPttBeOrderActiveState(OrderState s)
```
Method body at L3089 is **NOT modified**:
```csharp
=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;
```

### Sub-Changes

All 6 sub-changes are in `src/PropTraderTools/CopyEngine.cs` only.
Engineer MUST apply ALL 6 changes in a single `apply_diff` call or in sequence without omission.
Line numbers are verified against the current file state.

---

#### T1a -- L3085: Expand header comment (add "Signals 2+4" + rename annotation)

**Exact OLD text (L3085, verbatim from file):**
```
        // B160 DW-B160: State predicate for Signal 2 -- extracted to fix V-01 CYC violation.
```

**Exact NEW text (replace L3085 with 3 lines):**
```
        // B160 DW-B160: State predicate for Signals 2+4 -- extracted to fix V-01 CYC violation.
        //   Renamed IsPttBeStopActiveState -> IsPttBeOrderActiveState (DW-B161B166-02):
        //   predicate is used for both Stop and Target order state checks.
```

**Note:** L3086 (`// CYC=3: base(1) + ...`) and L3087 (`// JS-001: ...`) are **not modified**. The 3-line block above replaces only L3085.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "Signals 2\+4"` -- must return 1 hit at the new L3085 area.

---

#### T1b -- L3088: Rename method definition identifier

**Exact OLD text (L3088, verbatim from file):**
```
        private static bool IsPttBeStopActiveState(OrderState s)
```

**Exact NEW text:**
```
        private static bool IsPttBeOrderActiveState(OrderState s)
```

**Note:** L3089 (method body `=> s == OrderState.Working || ...`) is **NOT modified**.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "private static bool IsPttBeOrderActiveState"` -- must return exactly 1 hit.

---

#### T1c -- L3101: Update comment in `HasActivePttBeExits` header (spec gap, found by architect grep)

**Exact OLD text (L3101, verbatim from file):**
```
        //   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
```

**Exact NEW text:**
```
        //   State check is a single call to IsPttBeOrderActiveState -- no inline || chains.
```

**Note:** This line is inside the block comment header above `HasActivePttBeExits` (L3097-L3103). No other lines in that header block are modified.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "single call to IsPttBeOrderActiveState"` -- must return 1 hit.

---

#### T1d -- L3112: Rename call site in `HasActivePttBeExits` body

**Exact OLD text (L3112, verbatim from file):**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
```

**Exact NEW text:**
```
                if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8)
```

**Note:** The trailing comment `// (8)` and all surrounding whitespace are preserved exactly.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "IsPttBeOrderActiveState\(o\.OrderState\)"` -- must return 2 hits total (L3112 area + L3155 area) after both T1d and T1f are applied.

---

#### T1e -- L3144: Update comment in `HasActivePttBeTargets` header

**Exact OLD text (L3144, verbatim from file):**
```
        //   Reuses IsPttBeStopActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```

**Exact NEW text:**
```
        //   Reuses IsPttBeOrderActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```

**Note:** This line is inside the block comment header above `HasActivePttBeTargets` (L3137-L3146). No other lines in that header block are modified.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "Reuses IsPttBeOrderActiveState"` -- must return 1 hit.

---

#### T1f -- L3155: Rename call site in `HasActivePttBeTargets` body

**Exact OLD text (L3155, verbatim from file):**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

**Exact NEW text:**
```
                if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

**Note:** The trailing comment `// (8) reuse Active-state predicate` and all surrounding whitespace are preserved exactly.

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "IsPttBeOrderActiveState.*reuse Active-state predicate"` -- must return 1 hit.

---

### 7-Scan Checklist (T1)

**Engineer must run all 7 scans after applying T1a-T1f and record results.**

| Scan | Command | Expected Result |
|---|---|---|
| SCAN-01: ASCII scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"` scoped to changed lines (L3085-L3088, L3101, L3112, L3144, L3155) | **ZERO matches** -- all new comment and identifier text is 7-bit ASCII only |
| SCAN-02: Lock scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\("` scoped to changed lines | **ZERO matches** -- no lock() introduced |
| SCAN-03: Unicode scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[\x80-\xFF]"` scoped to changed lines | **ZERO matches** -- no non-printable or extended chars |
| SCAN-04: CYC scan | Static analysis on `IsPttBeOrderActiveState` | **CYC = 3** (base=1, `||`=+1, `||`=+1; body unchanged) |
| SCAN-05: Rename completeness scan | `Select-String -Path "src\PropTraderTools\*.cs" -Recurse -Pattern "IsPttBeStopActiveState"` | **ZERO matches** across all .cs files in `src/PropTraderTools/` |
| SCAN-06: Logic change scan | Read L3089: `=> s == OrderState.Working \|\| s == OrderState.Accepted \|\| s == OrderState.Submitted;` | **UNCHANGED** -- method body at L3089 is byte-for-byte identical to pre-change |
| SCAN-07: NT8 API constraint check | Review T1a-T1f diff | **No NT8 API calls added or modified** -- change is identifier rename + comment text only |

---

### T1 Completion Criteria

All of the following must be true before T1 is marked done:

1. `Select-String -Path "src\PropTraderTools\*.cs" -Recurse -Pattern "IsPttBeStopActiveState"` returns **0 hits**.
2. `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "IsPttBeOrderActiveState"` returns **exactly 6 hits** (L3085 comment, L3088 definition, L3101 comment, L3112 call, L3144 comment, L3155 call).
3. Build of `src/PropTraderTools/` succeeds with **0 errors, 0 new warnings**.
4. All 7 scans pass.
5. No lines other than L3085, L3088, L3101, L3112, L3144, L3155 are modified.

---

## Ticket T2: DW-B161B166-03 -- Add B162-B asymmetry comment to `TryTriggerBeRecovery`

### Spec Requirements Satisfied
- DW-B161B166-03: Document that `"PTT-BE-Stop-"` WITH trailing dash at L5208 is intentionally different from `"PTT-BE-Stop"` WITHOUT trailing dash used in `HasActivePttBeExits` and `StillAtNonBePrice`.
- Prevents future maintainers from incorrectly treating L5208 as a missed B162-B fix.

### File Path
`src/PropTraderTools/CopyEngine.cs`

### Method Signatures (unchanged)
`TryTriggerBeRecovery` -- method body logic is **NOT modified**. Only the comment on L5209 is expanded. The `if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))` guard at L5208 is preserved verbatim.

---

### Sub-Change

#### T2a -- L5208-5209: Expand single-line comment to 6-line asymmetry block

**Exact OLD text (L5208-5209, verbatim from file):**
```
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery
```

**Exact NEW text (L5208-5214, replace the 2-line block with 7 lines):**
```
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery.
                        // B162-B: "PTT-BE-Stop-" WITH dash is intentional here.
                        // Only suffixed follower Stop orders (PTT-BE-Stop-1, PTT-BE-Stop-2)
                        // trigger bracket recovery. Bare "PTT-BE-Stop" (leader atomic stop)
                        // must NOT. HasActivePttBeExits and StillAtNonBePrice use the bare
                        // prefix intentionally -- that asymmetry is correct by design.
```

**Critical notes for engineer:**

1. L5208 (`if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))`) is **NOT modified** -- not a single character changes on this line.
2. The old comment text `// (5) DW-B90: only Stop-* triggers recovery` loses its trailing absence of period and gains a period: `// (5) DW-B90: only Stop-* triggers recovery.` (period added at end).
3. The 5 new continuation comment lines are indented to align under the `//` of the return statement (24 spaces before `//`).
4. All continuation lines use `//` (double-slash), not `/* */` block style.
5. The line immediately after (L5210 pre-change, now L5215 post-change): `if (HasFilledBeTargetFast(o.Account))` is **NOT modified**.
6. All new comment characters are 7-bit ASCII: A-Z, a-z, 0-9, space, `/`, `(`, `)`, `-`, `.`, `*`, `,`, `"`, `+`. No curly quotes. No en-dash or em-dash (`--` double-hyphen is used).

**Verification:** `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "B162-B.*PTT-BE-Stop.*WITH dash is intentional"` -- must return 1 hit in the `TryTriggerBeRecovery` method area (L5208-5215 region).

---

### 7-Scan Checklist (T2)

**Engineer must run all 7 scans after applying T2a and record results.**

| Scan | Command | Expected Result |
|---|---|---|
| SCAN-01: ASCII scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"` scoped to changed lines (L5209-5214) | **ZERO matches** -- all new comment text is 7-bit ASCII only |
| SCAN-02: Lock scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\("` scoped to changed lines | **ZERO matches** -- no lock() introduced |
| SCAN-03: Unicode scan | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[\x80-\xFF]"` scoped to changed lines | **ZERO matches** -- no non-printable or extended chars |
| SCAN-04: CYC scan | Static analysis on `TryTriggerBeRecovery` | **CYC = 6** (unchanged -- comment lines add zero McCabe decision points) |
| SCAN-05: N/A for T2 | (Rename completeness scan not applicable -- no identifier renamed in T2) | N/A |
| SCAN-06: Logic change scan | Read L5208: `if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))` | **UNCHANGED** -- guard line is byte-for-byte identical to pre-change; no logic modified |
| SCAN-07: NT8 API constraint check | Review T2a diff | **No NT8 API calls added or modified** -- change is comment expansion only |

---

### T2 Completion Criteria

All of the following must be true before T2 is marked done:

1. `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "B162-B"` returns at least 1 hit in the `TryTriggerBeRecovery` method area (L5208-5215 region).
2. `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "asymmetry is correct by design"` returns **1 hit**.
3. L5208 (`if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))`) is **unchanged**.
4. Build of `src/PropTraderTools/` succeeds with **0 errors, 0 new warnings**.
5. All 7 scans pass.
6. No lines other than L5209-5214 (new comment continuation lines) are modified.

---

## Ticket Order and Dependency

T1 and T2 are **independent**. They touch non-overlapping regions (~2054 lines apart):
- T1: L3085-L3155
- T2: L5208-5214

Engineer may apply in either order or as a single commit. A single PR covering both is the recommended approach per the architect's lane-split gate note.

---

## PTT Sync (mandatory after any `.cs` modification)

After applying T1 and T2 (in any order), run:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
This re-synchronizes NinjaTrader hard links. **This step is mandatory per AGENTS.md §2 Hard-Link Integrity.**

---

## Summary Table

| Ticket | File | Lines Modified | Type | Logic Change | CYC After | JS Rules |
|---|---|---|---|---|---|---|
| T1 (6 sub-changes) | `src/PropTraderTools/CopyEngine.cs` | L3085, L3088, L3101, L3112, L3144, L3155 | Identifier rename + comment update | **None** | IsPttBeOrderActiveState=3 (unchanged) | JS-042 PASS, JS-021 PASS, JS-001 PASS |
| T2 (1 sub-change) | `src/PropTraderTools/CopyEngine.cs` | L5209-5214 (comment expansion) | Comment expansion | **None** | TryTriggerBeRecovery=6 (unchanged) | JS-042 PASS, JS-021 PASS, JS-001 PASS |

**Total source changes: 7 text substitutions in 1 file. Zero new files. Zero other files.**

TICKETS_COMPLETE
