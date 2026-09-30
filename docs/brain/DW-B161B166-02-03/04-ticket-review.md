# Ticket Review: DW-B161B166-02-03
**Reviewer:** ptt-ticket-reviewer (Phase 3.5)
**Tickets file:** `docs/brain/DW-B161B166-02-03/04-tickets.md`
**Plan file:** `docs/brain/DW-B161B166-02-03/02-architecture-plan.md` (REVIEW_PASS)
**Source verified:** `src/PropTraderTools/CopyEngine.cs`
**Date:** 2025-01

---

## Ticket T1 -- DW-B161B166-02: Rename `IsPttBeStopActiveState` -> `IsPttBeOrderActiveState`

### Traceability

All 6 sub-changes (T1a-T1f) map directly to plan sub-changes 02-A through 02-F. No phantom work present. No plan work absent from tickets.

| Sub-Change | Plan ID | Spec Req | Verdict |
|---|---|---|---|
| T1a (L3085) | 02-A | DW-B161B166-02 | MAPPED |
| T1b (L3088) | 02-B | DW-B161B166-02 | MAPPED |
| T1c (L3101) | 02-C | DW-B161B166-02 (spec gap, plan-approved) | MAPPED |
| T1d (L3112) | 02-D | DW-B161B166-02 | MAPPED |
| T1e (L3144) | 02-E | DW-B161B166-02 | MAPPED |
| T1f (L3155) | 02-F | DW-B161B166-02 | MAPPED |

**Traceability: PASS**

---

### Source Line Verification (character-for-character)

#### T1a -- L3085

**Ticket OLD text:**
```
        // B160 DW-B160: State predicate for Signal 2 -- extracted to fix V-01 CYC violation.
```

**Actual CopyEngine.cs L3085:**
```
        // B160 DW-B160: State predicate for Signal 2 -- extracted to fix V-01 CYC violation.
```

**Match:** EXACT. Character-for-character identical including leading 8 spaces.

**Ticket NEW text (3-line replacement):**
```
        // B160 DW-B160: State predicate for Signals 2+4 -- extracted to fix V-01 CYC violation.
        //   Renamed IsPttBeStopActiveState -> IsPttBeOrderActiveState (DW-B161B166-02):
        //   predicate is used for both Stop and Target order state checks.
```
- Valid C# comment syntax: `//` double-slash throughout. ✓
- ASCII-only: A-Z, a-z, 0-9, space, `/`, `+`, `-`, `>`, `.`, `(`, `)`, `:`. No curly quotes, no Unicode. ✓
- Note confirms L3086 (`// CYC=3:`) and L3087 (`// JS-001:`) are NOT modified. ✓
- Method body at L3089 NOT in this sub-change. ✓

**Line number accurate:** L3085 confirmed by source read. ✓

**T1a Verdict: PASS**

---

#### T1b -- L3088

**Ticket OLD text:**
```
        private static bool IsPttBeStopActiveState(OrderState s)
```

**Actual CopyEngine.cs L3088:**
```
        private static bool IsPttBeStopActiveState(OrderState s)
```

**Match:** EXACT. Character-for-character identical including leading 8 spaces.

**Ticket NEW text:**
```
        private static bool IsPttBeOrderActiveState(OrderState s)
```
- Valid C# method signature syntax. ✓
- ASCII-only identifier. ✓
- Body at L3089 (`=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;`) explicitly noted as NOT modified. ✓
- Verified: L3089 is NOT included in this sub-change. ✓

**Line number accurate:** L3088 confirmed by source read. ✓

**T1b Verdict: PASS**

---

#### T1c -- L3101

**Ticket OLD text:**
```
        //   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
```

**Actual CopyEngine.cs L3101:**
```
        //   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
```

**Match:** EXACT. Character-for-character identical including leading 8 spaces and 3-space indent after `//`.

**Ticket NEW text:**
```
        //   State check is a single call to IsPttBeOrderActiveState -- no inline || chains.
```
- Valid C# comment syntax. ✓
- ASCII-only. ✓
- Only the identifier name changes; all surrounding whitespace and punctuation preserved. ✓

**Line number accurate:** L3101 confirmed by source read. ✓

**T1c Verdict: PASS**

---

#### T1d -- L3112

**Ticket OLD text:**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
```

**Actual CopyEngine.cs L3112:**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8)
```

**Match:** EXACT. Character-for-character identical including leading 16 spaces and trailing `    // (8)` (4 spaces before comment).

**Ticket NEW text:**
```
                if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8)
```
- Valid C# syntax. ✓
- ASCII-only. ✓
- Trailing comment `// (8)` and all whitespace preserved exactly. ✓

**Line number accurate:** L3112 confirmed by source read. ✓

**T1d Verdict: PASS**

---

#### T1e -- L3144

**Ticket OLD text:**
```
        //   Reuses IsPttBeStopActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```

**Actual CopyEngine.cs L3144:**
```
        //   Reuses IsPttBeStopActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```

**Match:** EXACT. Character-for-character identical including leading 8 spaces and 3-space indent after `//`.

**Ticket NEW text:**
```
        //   Reuses IsPttBeOrderActiveState (Working/Accepted/Submitted) -- correct for Target orders too.
```
- Valid C# comment syntax. ✓
- ASCII-only. ✓
- Only the identifier name changes; all surrounding whitespace and punctuation preserved. ✓

**Line number accurate:** L3144 confirmed by source read. ✓

**T1e Verdict: PASS**

---

#### T1f -- L3155

**Ticket OLD text:**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

**Actual CopyEngine.cs L3155:**
```
                if (IsPttBeStopActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```

**Match:** EXACT. Character-for-character identical including leading 16 spaces and trailing `    // (8) reuse Active-state predicate`.

**Ticket NEW text:**
```
                if (IsPttBeOrderActiveState(o.OrderState)) return true;    // (8) reuse Active-state predicate
```
- Valid C# syntax. ✓
- ASCII-only. ✓
- Trailing comment `// (8) reuse Active-state predicate` and all whitespace preserved exactly. ✓

**Line number accurate:** L3155 confirmed by source read. ✓

**T1f Verdict: PASS**

---

### JS Pre-Check (T1)

| Rule | Check | Result |
|---|---|---|
| JS-001: no throw in hot path | No `throw` added in any sub-change | PASS |
| JS-021: no lock() | No `lock(` added in any sub-change | PASS |
| JS-042: ASCII-only | All new comment and identifier text is 7-bit ASCII (verified char-by-char above) | PASS |
| JS-002: no null return | Method body unchanged; no new null returns | PASS |
| JS-003: no magic string for state | No discriminated-state strings added | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check (T1)

`IsPttBeOrderActiveState` body is NOT modified. Body remains:
```csharp
=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;
```
CYC = 3 (base=1 + `||`=+1 + `||`=+1). No method at risk of CYC > 8.

**CYC Pre-Check: PASS**

---

### NT8 Check (T1)

- No async/await in lifecycle method. ✓
- No Account.All call. ✓
- No CreateOrder call. ✓
- No DateTime.Now. ✓
- No FontFamily. ✓
- No hardcoded #RRGGBB hex. ✓
- No sealed keyword on TradeCopierWindow. ✓
- Change is identifier rename + comment text only.

**NT8 Check: PASS**

---

### Test Coverage (T1)

T1 is a pure rename + comment change with zero logic change. No new methods are introduced. No new behavior is added. All 6 sub-changes are text substitutions only (identifier rename and comment updates). The plan and plan-review confirm existing tests pass unchanged with no test modifications required. No [Fact] test stubs are required for a text-only rename with no new method signatures.

**Test Coverage: PASS** (no new methods introduced; existing tests cover renamed predicate unchanged)

---

### Scan Checklist (T1)

Ticket contains the full 7-scan checklist (SCAN-01 through SCAN-07) at section "7-Scan Checklist (T1)":

| Scan | Present | Command Provided | Expected Result Defined |
|---|---|---|---|
| SCAN-01: ASCII scan | ✓ | ✓ | ✓ |
| SCAN-02: Lock scan | ✓ | ✓ | ✓ |
| SCAN-03: Unicode scan | ✓ | ✓ | ✓ |
| SCAN-04: CYC scan | ✓ | ✓ | ✓ |
| SCAN-05: Rename completeness scan | ✓ | ✓ | ✓ |
| SCAN-06: Logic change scan | ✓ | ✓ | ✓ |
| SCAN-07: NT8 API constraint check | ✓ | ✓ | ✓ |

**Scan Checklist: PASS** (all 7 scans present with commands and expected results)

---

### File Routing (T1)

All sub-changes target `src/PropTraderTools/CopyEngine.cs`. This is the Wave workspace path. No Director workspace paths used for .cs files.

**File Routing: PASS**

---

### T1 VERDICT: TICKET_REVIEW_PASS

---

## Ticket T2 -- DW-B161B166-03: Add B162-B asymmetry comment to `TryTriggerBeRecovery`

### Traceability

T2a maps directly to plan sub-change 03-A. No phantom work. No plan work absent.

| Sub-Change | Plan ID | Spec Req | Verdict |
|---|---|---|---|
| T2a (L5208-5209) | 03-A | DW-B161B166-03 | MAPPED |

**Traceability: PASS**

---

### Source Line Verification (character-for-character)

#### T2a -- L5208-5209

**Ticket OLD text:**
```
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery
```

**Actual CopyEngine.cs L5208-5209:**
```
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery
```

**Match:** EXACT. Both lines character-for-character identical:
- L5208: 12 leading spaces + `if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))` ✓
- L5209: 16 leading spaces + `return; // (5) DW-B90: only Stop-* triggers recovery` (NO period at end) ✓

**Ticket NEW text:**
```
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
                return; // (5) DW-B90: only Stop-* triggers recovery.
                        // B162-B: "PTT-BE-Stop-" WITH dash is intentional here.
                        // Only suffixed follower Stop orders (PTT-BE-Stop-1, PTT-BE-Stop-2)
                        // trigger bracket recovery. Bare "PTT-BE-Stop" (leader atomic stop)
                        // must NOT. HasActivePttBeExits and StillAtNonBePrice use the bare
                        // prefix intentionally -- that asymmetry is correct by design.
```

**Guard line L5208 check:** The `if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))` line is **IDENTICAL** between OLD and NEW. Not a single character changes. ✓

**"PTT-BE-Stop-" string literal check:** L5208 guard is preserved verbatim in NEW text. The string `"PTT-BE-Stop-"` (with trailing dash) is NOT modified by this sub-change. ✓

**T2a only modifies the comment, not the guard condition line:** Confirmed. NEW L5208 = OLD L5208 exactly. Only L5209 gains a period and 5 continuation comment lines are appended. ✓

**Period addition note:** Ticket note #2 explicitly documents that old text `// (5) DW-B90: only Stop-* triggers recovery` (no period) becomes `// (5) DW-B90: only Stop-* triggers recovery.` (period added). This is the only change to L5209 itself; the 5 new lines are additive. ✓

**Continuation comment indentation:** 5 new lines indented 24 spaces before `//` to align under the `//` of the return statement. All use `//` double-slash, not `/* */`. ✓

**ASCII compliance of NEW text:** Characters used: A-Z, a-z, 0-9, space, `/`, `(`, `)`, `-`, `.`, `*`, `,`, `"`, `+`. All 7-bit ASCII (0x20-0x7E). No curly quotes, no en-dash (U+2013), no em-dash (U+2014). Double hyphen `--` used throughout. ✓

**Line number accurate:** L5208-5209 confirmed by source read. ✓

**Body method check:** No method body logic is included in T2a. The sub-change boundary is L5208-5209 (OLD) and does not touch the subsequent logic lines (L5210+: `if (HasFilledBeTargetFast(o.Account))`). ✓

**T2a Verdict: PASS**

---

### JS Pre-Check (T2)

| Rule | Check | Result |
|---|---|---|
| JS-001: no throw in hot path | No `throw` added | PASS |
| JS-021: no lock() | No `lock(` added | PASS |
| JS-042: ASCII-only | All new comment text is 7-bit ASCII (verified char-by-char above) | PASS |
| JS-002: no null return | No new code, comment-only change | PASS |
| JS-003: no magic string for state | No discriminated-state strings added | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check (T2)

`TryTriggerBeRecovery` CYC is unchanged at 6. Comment lines contain no McCabe decision points (`if`, `while`, `for`, `||`, `&&`, `?`). CYC = 6, well below the CYC > 8 threshold.

**CYC Pre-Check: PASS**

---

### NT8 Check (T2)

- No async/await. ✓
- No Account.All. ✓
- No CreateOrder. ✓
- No DateTime.Now. ✓
- No FontFamily. ✓
- No hardcoded #RRGGBB hex. ✓
- No sealed on TradeCopierWindow. ✓
- Change is comment expansion only.

**NT8 Check: PASS**

---

### Test Coverage (T2)

T2a is a pure comment expansion with zero logic change. No new methods are introduced. No new behavior is added. No [Fact] test stubs are required. Existing tests pass unchanged.

**Test Coverage: PASS** (no new methods introduced; comment-only change requires no test additions)

---

### Scan Checklist (T2)

Ticket contains the full 7-scan checklist (SCAN-01 through SCAN-07) at section "7-Scan Checklist (T2)":

| Scan | Present | Command Provided | Expected Result Defined |
|---|---|---|---|
| SCAN-01: ASCII scan | ✓ | ✓ | ✓ |
| SCAN-02: Lock scan | ✓ | ✓ | ✓ |
| SCAN-03: Unicode scan | ✓ | ✓ | ✓ |
| SCAN-04: CYC scan | ✓ | ✓ | ✓ |
| SCAN-05: N/A for T2 | ✓ (explicitly marked N/A with rationale) | N/A | N/A |
| SCAN-06: Logic change scan | ✓ | ✓ | ✓ |
| SCAN-07: NT8 API constraint check | ✓ | ✓ | ✓ |

Note: SCAN-05 is correctly marked N/A for T2 (no identifier renamed in T2). All 7 scan slots are present and accounted for.

**Scan Checklist: PASS** (all 7 scans present; SCAN-05 correctly noted as N/A with explicit rationale)

---

### File Routing (T2)

T2a targets `src/PropTraderTools/CopyEngine.cs`. Wave workspace path. No Director workspace paths used for .cs files.

**File Routing: PASS**

---

### T2 VERDICT: TICKET_REVIEW_PASS

---

## Special Mandatory Checks (per reviewer instructions)

### 1. Method body at L3089 not included in any sub-change

T1b changes only L3088 (method definition signature line). L3089 (`=> s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;`) is explicitly noted as NOT modified in the T1b description. No sub-change includes L3089 in its OLD or NEW text blocks.

**PASS**

### 2. "PTT-BE-Stop-" string literal at L5208 not modified in T2a NEW text

T2a OLD text includes L5208 verbatim. T2a NEW text includes L5208 verbatim and IDENTICAL to the OLD. The guard condition `if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))` is byte-for-byte unchanged between OLD and NEW.

**PASS**

### 3. T2a only modifies the comment, not the guard condition line

Confirmed: L5208 in NEW text = L5208 in OLD text = L5208 in source file. Zero character difference on the guard line.

**PASS**

### 4. No non-ASCII characters in any NEW text

Verified for all 7 sub-changes (T1a-T1f, T2a). All new comment and identifier text uses only characters in the 0x20-0x7E 7-bit ASCII range. No curly quotes, no Unicode dashes, no extended characters.

**PASS**

### 5. 7-scan checklist present in each ticket

T1: SCAN-01 through SCAN-07 all present. ✓
T2: SCAN-01 through SCAN-07 all present (SCAN-05 explicitly N/A). ✓

**PASS**

---

## Spec Coverage Summary

| Spec Requirement | Covered By | Verdict |
|---|---|---|
| DW-B161B166-02: All 5 old-name occurrences renamed | T1a (L3085 comment), T1b (L3088 definition), T1c (L3101 comment), T1d (L3112 call), T1e (L3144 comment), T1f (L3155 call) | PASS (6 sub-changes cover all 5 occurrences + 1 new comment mentioning old name) |
| DW-B161B166-02: Zero logic change | T1b note: body L3089 unchanged | PASS |
| DW-B161B166-03: Asymmetry comment added at L5208 | T2a | PASS |
| DW-B161B166-03: Guard line L5208 unchanged | T2a note: L5208 verbatim | PASS |
| No other files modified | Tickets scope: CopyEngine.cs only | PASS |

No uncovered requirements. No duplicate coverage.

---

## Overall Review

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|---|---|---|---|---|---|---|---|---|
| T1 (T1a-T1f) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T2 (T2a) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

All sub-changes (T1a, T1b, T1c, T1d, T1e, T1f, T2a) verified character-for-character against `src/PropTraderTools/CopyEngine.cs`. All OLD text matches the source file exactly. All NEW text is valid C# (comments or identifiers), ASCII-only, with no logic changes, no method body modifications, and no guard condition alterations.

GATE: TICKET_REVIEW_PASS
