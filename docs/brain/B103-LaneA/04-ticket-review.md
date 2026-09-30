# Ticket Review — B103-LaneA

## Review Date: 2026-08-10
## Reviewer: ptt-ticket-reviewer
## Source tickets: docs/brain/B103-LaneA/04-tickets.md
## Source plan: docs/brain/B103-LaneA/02-architecture-plan.md (REVIEW_PASS cycle 2)

---

## Checklist Results

---

### A. Traceability

**Ticket 1 (DW-B102)**

| Item | Result |
|------|--------|
| DW item reference | PASS — metadata correctly cites DW-B102 |
| File routing | PASS — `src/PropTraderTools/CopyEngine.cs` specified |
| BEFORE Change 1A matches source | PASS — L3868-3871 in tickets matches source exactly: `// -- B6: Persistence field ---`, blank line, `private volatile bool _persistenceLoaded = false;`, blank line |
| BEFORE Change 1B matches source | PASS — L4084-4086 in tickets matches source exactly: `if (_persistenceLoaded) / return; / _persistenceLoaded = true;` |
| BEFORE Change 1C matches source | PASS — L4075-4081 in tickets matches source exactly: 7-line `<summary>` block verified word-for-word against CopyEngine.cs |
| AFTER blocks traceable to plan §2.2 | PASS — all three changes (1A delete, 1B replace, 1C doc comment update) map to plan section 2.2 |
| WARN — doc comment AFTER text wording | The ticket's Change 1C AFTER text differs editorially from plan §2.2 Change 1C proposed text (different line breaks and phrasing), though the semantic content is identical: idempotent, UI-thread-only, CYC=4. Not a code-logic change. Logged as WARN only; does not block. |

**Ticket 2 (DW-B103)**

| Item | Result |
|------|--------|
| DW item reference | PASS — metadata correctly cites DW-B103 |
| File routing | PASS — `src/PropTraderTools/CopyEngine.cs` specified |
| BEFORE Change 2A matches source | PASS — L1506-1523 in tickets matches source exactly: 4-line block comment (CYC=4, Returns true, HOTFIX-B63, JS-021/001) + 14-line method body verified against CopyEngine.cs |
| AFTER block traceable to plan §3.2 | PASS — new guard `order.Name != null && (StartsWith("PTT-QX-") \|\| StartsWith("PTT-BE-"))` returning `false` maps exactly to plan section 3.2 |

**Traceability verdict: PASS**

---

### B. 7-Scan Checklist Presence

**Ticket 1** — 7 scans present and verified:

| Scan | Command / Method | Expected | Present |
|------|-----------------|----------|---------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 new matches | ✓ |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new matches | ✓ |
| SCAN-03 | Visual ASCII check on new string literals | 0 non-ASCII | ✓ |
| SCAN-04 | `grep -n "_persistenceLoaded" src/PropTraderTools/CopyEngine.cs` | 0 results | ✓ |
| SCAN-05 | `grep -n "_rules = new ConcurrentBag"` inside `LoadRules()` | 1 match | ✓ |
| SCAN-06 | Manual CYC count for `LoadRules()` | CYC = 4 ≤ 8 | ✓ |
| SCAN-07 | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH | ✓ |

**Ticket 2** — 7 scans present and verified:

| Scan | Command / Method | Expected | Present |
|------|-----------------|----------|---------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` in changed region | 0 matches | ✓ |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` in changed region | 0 matches | ✓ |
| SCAN-03 | Visual ASCII check on new string literals | 0 non-ASCII | ✓ |
| SCAN-04 | `grep -n "PTT-QX-" src/PropTraderTools/CopyEngine.cs` | 1 match in `TryCancelFollowerEntries` | ✓ |
| SCAN-05 | `grep -n "StringComparison.Ordinal"` in change region | 2 matches | ✓ |
| SCAN-06 | Manual CYC count for `TryCancelFollowerEntries()` | CYC = 6 ≤ 8 | ✓ |
| SCAN-07 | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH | ✓ |

Note: The plan's aggregate SCAN table (§6) numbers the sync script differently than per-ticket numbering. Per-ticket scans are customized — each ticket's SCAN-06 is the CYC check for that ticket's method; SCAN-07 is the sync gate in both tickets. All 7 distinct checks are present per ticket. Defense-in-depth contract intact.

**7-Scan Checklist verdict: PASS**

---

### C. JS P0 Rule Pre-Check

**JS-021 (no `lock()` — P0 CRITICAL)**

Ticket 1 AFTER blocks: No `lock(` in any new or replaced line. Block comment explicitly annotates `JS-021: no lock`. PASS.
Ticket 2 AFTER blocks: No `lock(` in any new or replaced line. Block comment explicitly annotates `JS-021: no lock`. PASS.

**JS-001 (no `throw new Exception` — P0 CRITICAL)**

Ticket 1 AFTER: Catch block swallows — `catch (Exception) { }`. No `throw new`. PASS.
Ticket 2 AFTER: No exception handling introduced. PASS.
Block comments in both tickets explicitly annotate `JS-001: no throw`. PASS.

**JS-002 (no `return null` — P0 CRITICAL)**

Ticket 1: No nullable return introduced. `LoadRules()` is `void`. PASS.
Ticket 2: Returns `false` (bool), never `null`. PASS.

**ASCII-only**

Ticket 1: New string literal `"DW-B102: idempotent clear -- each caller gets a fresh read"` — all ASCII; double-dash `--` is ASCII hyphen-minus (0x2D), not em-dash. Doc comment text verified ASCII. PASS.
Ticket 2: New string literals `"PTT-QX-"`, `"PTT-BE-"`, comment `"DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets"` — all ASCII. PASS.

**CYC ≤ 8**

Ticket 1: `LoadRules()` CYC = 4 (`File.Exists` + `try/catch` + `null-check` + `foreach`). ≤ 8. PASS.
Ticket 2: `TryCancelFollowerEntries()` CYC = 6 (`OrderState` + `IsAtmBracketName` + `name-null` + `OR-branch` + `foreach` + `acc-null`). ≤ 8. PASS.

**Immutability (JS-008/009)**

Ticket 1: `_rules = new ConcurrentBag<CopyRule>()` — ConcurrentBag is a lock-free concurrent collection, not a mutable Dictionary. Pattern already used at L1052, L1090, L1107, L2584 in the same file. PASS.

**JS P0 Rule Pre-Check verdict: PASS**

---

### D. NT8 Constraints

| Constraint | Check | Result |
|------------|-------|--------|
| `order.Name` is valid NT8 Order property | Confirmed: already used at L1514 and L3202-3203 in same file (plan §8) | PASS |
| `StringComparison.Ordinal` for prefix matching | Appropriate for ASCII order-name identifiers; matches NT8 naming conventions | PASS |
| `ConcurrentBag<CopyRule>` reassignment on UI thread | `_rules` field is NOT readonly (per source L178 comment "// Change 1: removed readonly"); reassignment in `LoadRules()` is UI-thread-only per NT8 `OnLoaded` guarantee (plan §7) | PASS |
| No `async/await` in lifecycle method | Not introduced | PASS |
| No `Account.All` outside Loaded handler | Not introduced | PASS |
| No `sealed` on `TradeCopierWindow` | Not introduced | PASS |
| No `FontFamily` set | Not introduced | PASS |
| No hardcoded hex color | Not introduced | PASS |
| No `CreateOrder` without `PTT-` prefix | Not introduced | PASS |
| No `DateTime.Now` | Not introduced | PASS |

**NT8 Constraints verdict: PASS**

---

### E. Completeness

**Ticket 1 — DW-B102**

All plan §2.2 deliverables are present in ticket acceptance criteria:
- Field deletion (Change 1A): ✓ — acceptance criterion verifies `grep _persistenceLoaded = 0`
- Guard replacement (Change 1B): ✓ — acceptance criterion verifies first statement is `_rules = new ConcurrentBag<CopyRule>()`; no old guard lines remain
- Doc comment update (Change 1C): ✓ — acceptance criterion verifies CYC line updated, old `No-op if... has already been loaded` text removed
- `_rules` field unchanged: ✓ — explicitly stated
- `// -- B6/B8: Serialization DTO classes ---` comment untouched: ✓ — explicitly stated
- Sync gate: ✓ — `ptt-sync-and-verify.ps1` 0 MISMATCH criterion present

**Ticket 2 — DW-B103**

All plan §3.2 deliverables are present in ticket acceptance criteria:
- `PTT-QX-` guard: ✓
- `PTT-BE-` guard: ✓
- Guard returns `false` (not `true`): ✓ — explicitly called out
- `StringComparison.Ordinal` on both `StartsWith` calls: ✓
- Guard placement (after `IsAtmBracketName`, before `foreach`): ✓
- Block comment updated (CYC=6, DW-B103 annotation): ✓

Protected regions explicitly called out in ticket (table + acceptance criteria):
- `IsBracketLeg()` at L3198-3205: ✓ UNTOUCHED — verified source matches (B29 intentional design: PTT- excluded)
- `CancelOneAccount()` at L2915-2939: ✓ UNTOUCHED — verified source at L2915-2939 is unrelated user-initiated cancel path
- `IsAtmBracketName()` at ~L669-682: ✓ UNTOUCHED — B63 hotfix guard

**Completeness verdict: PASS**

---

### F. Test Coverage

Neither ticket creates a new method. Both tickets modify existing methods:
- `LoadRules()` (existing `public void`) — behavioral modification of existing public method
- `TryCancelFollowerEntries()` (existing `private bool`) — behavioral modification of existing private method

Per roleDefinition: "Every **new** method described in the ticket must have a [Fact] test specified." No new methods are introduced. The roleDefinition's [Fact] requirement is scoped to **new** methods only.

`TryCancelFollowerEntries()` is `private` — not directly testable with [Fact].
`LoadRules()` is `public` but is an NT8 UI-thread-only callback (called from `OnLoaded` handlers). Testing would require NT8 mock infrastructure not present in the current test suite.

Note: Neither ticket explicitly states "no new tests required" with a rationale. This is an omission but not a blocking violation — the roleDefinition threshold is unmet (no new methods). Recommending the architect add a `### Test Coverage` note to each ticket for future clarity.

**Test Coverage verdict: PASS** (with minor documentation recommendation)

---

### G. Ticket Application Order

The tickets include an explicit "Application Order" section at the end:

> Apply **Ticket 2 first** (lower line numbers: L1506-1523), then **Ticket 1** (higher line numbers: L3868-3871 and L4075-4112). This preserves line offsets during sequential editing. Either order is safe since ranges do not overlap — applying low-lines first is a defensive convention only.

This correctly directs the engineer to apply Ticket 2 (L1506-1523) before Ticket 1 (L3868+ and L4075+), eliminating any risk of stale line numbers from a high-line edit shifting low-line content. The note that ranges do not overlap and either order is technically safe is accurate and gives the engineer correct context.

**Application Order verdict: PASS**

---

## Violations Found

None.

The single editorial observation (Change 1C doc comment wording in ticket differs from plan §2.2 proposed text) is a non-blocking WARN: the semantic content and all material facts (idempotent, UI-thread-only, CYC=4) are preserved. The code-level AFTER blocks are unaffected.

---

## Decision

### TICKET_REVIEW_PASS — proceed to Ph4a (ptt-engineer)

All checks pass:

| Check | Ticket 1 | Ticket 2 |
|-------|----------|----------|
| A. Traceability | PASS | PASS |
| B. 7-Scan Checklist (SCAN-01..07) | PASS | PASS |
| C. JS P0 Rules (lock/throw/null/ASCII/CYC) | PASS | PASS |
| D. NT8 Constraints | PASS | PASS |
| E. Completeness | PASS | PASS |
| F. Test Coverage | PASS | PASS |
| G. Application Order | PASS | PASS |
| **Overall** | **PASS** | **PASS** |

The engineer may proceed with Ticket 2 first (L1506-1523), then Ticket 1 (L3868-3871, L4075-4112), in `src/PropTraderTools/CopyEngine.cs`. Both BEFORE blocks have been verified against live source. All 7 scans are defined per ticket. No P0 violations are introduced. Protected regions are explicitly identified and must remain untouched.
