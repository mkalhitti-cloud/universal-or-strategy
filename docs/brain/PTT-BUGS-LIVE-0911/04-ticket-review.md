# Ticket Review: PTT-BUGS-LIVE-0911 Lane A
# BUG-1: PTT-Flatten Reverse Entry Guard
# ptt-ticket-reviewer | 2026-09-11 | Phase 3.5 Output
# Source tickets: 04-tickets.md (Phase 3, TICKETS_COMPLETE)

---

## T1 -- PTT-Flatten Reverse Entry Guard (Path B Fan-Out)

---

### 1. Traceability

Every ticket item maps to a spec requirement or architecture plan section:

| Ticket Item | Maps To | Status |
|-------------|---------|--------|
| BUG-1-ROOT (Path B no outer guard) | `02-architecture-plan.md §1.2` | PASS |
| BUG-1-FIX (add position guard at fan-out level) | `02-architecture-plan.md §2.1` | PASS |
| BUG-1-EVIDENCE-A (5:55 AM -05 reverse long) | `live-trading-analysis-2026-09-11.md §Accidental Event A` | PASS |
| BUG-1-EVIDENCE-B (7:14 AM -07/-08 reverse short) | `live-trading-analysis-2026-09-11.md §Trade 5` | PASS |
| BUG-1-TEST (new [Fact] for Path B guard) | `02-architecture-plan.md §3.1` | PASS |
| `FlatGuardLoop<TAccount>` extraction | `02-architecture-plan.md §3.2` | PASS |
| `Flatten(Instrument)` loop replacement | `02-architecture-plan.md §2.2` | PASS |
| `Flatten(Account,Instrument)` follower loop replacement | `02-architecture-plan.md §2.3` | PASS |

No phantom work (items in ticket not traceable to plan or spec): **NONE FOUND.**
No missing plan work (items in plan not covered by ticket): **NONE FOUND.**

**Traceability: PASS**

---

### 2. Scope Lock

SCOPE LOCK statement is present and explicit:

> "This ticket touches **exactly three files**" with a table listing each file and its role (MODIFY / READ ONLY).
> "No other file may be touched by this ticket."
> "Any edit outside these three files = immediate TICKET_REVIEW_FAIL."

**Scope Lock: PASS**

---

### 3. Files in Scope

| File | Role | Correct? |
|------|------|----------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY -- add helper + update 2 foreach loops | PASS |
| `src/PropTraderTools/CopyEngineTests.cs` | MODIFY -- add 1 new [Fact] | PASS |
| `src/PropTraderTools/Features/PttFlatten.cs` | READ ONLY -- zero edits permitted | PASS |

All three files correctly identified. No additional files listed.

**Files in Scope: PASS**

---

### 4. Implementation Code Review

#### 4a. `FlatGuardLoop<TAccount>` Signature and Body

| Constraint | Ticket Claim | Verified |
|------------|-------------|----------|
| `private static` | Stated and shown in body | PASS |
| One generic param `TAccount` only (`TPosition` absent) | Explicitly confirmed: "V-03 closed" | PASS |
| `findPosition` typed `Func<TAccount, NinjaTrader.Cbi.Position?>` | Present in signature | PASS |
| `acc.ToString()` used, NOT `acc.Name` | Explicitly enforced; `acc.Name` = TICKET_REVIEW_FAIL noted | PASS |
| Checks BOTH `pos == null` AND `pos.Quantity == 0` | `if (pos == null \|\| pos.Quantity == 0)` shown in body | PASS |
| CYC=3 documented with branch enumeration | (1) foreach, (2) pos==null, (3) pos.Quantity==0 | PASS |

#### 4b. `Flatten(Instrument)` Call Site

The ticket shows the helper-call form (not an inline loop):
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```
No inline foreach with guard logic. **PASS.**

#### 4c. `Flatten(Account, Instrument)` Call Site

The ticket shows the helper-call form with `.Where` predicate (not inline loop):
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```
The `acc == leader` skip is handled via `Where(acc => acc != leader)` in the delegate, eliminating the inline branch. **PASS.**

#### 4d. Line Number Accuracy (verified against `src/PropTraderTools/CopyEngine.cs`)

| Ticket Claim | Source Actual | Match |
|-------------|---------------|-------|
| `Flatten(Instrument)` at line 5145 | Line 5145: `internal void Flatten(Instrument instrument)` | PASS |
| Current loop at lines 5152-5153 | Lines 5152-5153: `foreach (var acc in AllAccounts(instrument))` + `FlattenOneAccount(acc, instrument);` | PASS |
| `Flatten(Account, Instrument)` at line 5181 | Line 5181: `internal void Flatten(Account leader, Instrument instrument)` | PASS |
| Current follower loop at lines 5194-5199 | Lines 5194-5199: `foreach ... { if (acc == leader) continue; FlattenOneAccount(...); }` | PASS |
| Test placement before closing `}` at line 8339 | Line 8339: `    }` (class close), line 8340: `}` (namespace close) | PASS |

All line number claims accurate.

**Implementation Code Review: PASS**

---

### 5. 7-Scan Checklist Presence

All 7 scans present in T1 under section **"MANDATORY 7-SCAN CHECKLIST"**:

| Scan | Title in Ticket | Present |
|------|----------------|---------|
| SCAN-01 | Lock-Free Verification (`grep -r "lock(" src/`) | PASS |
| SCAN-02 | ASCII-Only String Literals | PASS |
| SCAN-03 | Cyclomatic Complexity Per Method (table: 3 methods) | PASS |
| SCAN-04 | No New Public API Surface (`grep -n "public "`) | PASS |
| SCAN-05 | No Null-Conditional Event Unsubscription (`grep -rn "\?\."`) | PASS |
| SCAN-06 | New [Fact] Covers Path B Guard (grep + body assertions) | PASS |
| SCAN-07 | Test Count Delta (PowerShell Select-String count: 168→169) | PASS |

Each scan includes: the command to run, the expected result, and the failure condition. Defense-in-depth contract complete.

**Scan Checklist: PASS**

---

### 6. Test Specification

| Requirement | Ticket Claim | Verified |
|-------------|-------------|----------|
| [Fact] method name | `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` | PASS -- matches plan §3.1 exactly |
| No Skip annotation | "NO `Skip` annotation required" -- rationale given | PASS |
| Targets `FlatGuardLoop` via reflection | `.GetMethods(NonPublic \| Static).Single(m => m.Name == "FlatGuardLoop" && m.GetGenericArguments().Length == 1)` | PASS |
| `findPosition` returns null (Path B guard) | `Func<object, NinjaTrader.Cbi.Position?> findPosition = acc => null;` | PASS |
| Assert: `flattenOne` NOT called on flat follower | `Assert.False(flattenOneCalled, ...)` | PASS |
| `Assert.NotNull(mi)` guard | Present -- guards method existence post-implementation | PASS |
| Test count baseline → delta | 168 → 169 (+1); baseline confirmed by grep on source (168 matches) | PASS |
| CYC of test method | CYC=1 (straight-line), stated explicitly | PASS |

**Test Coverage: PASS**

---

### 7. CYC Pre-Check

| Method | Before | After | Limit | Result |
|--------|--------|-------|-------|--------|
| `Flatten(Instrument)` | 3 | 4 | 8 | PASS |
| `Flatten(Account, Instrument)` | 5 | 6 | 8 | PASS |
| `FlatGuardLoop<TAccount>` | 0 (new) | 3 | 8 | PASS |

Branch enumeration for `FlatGuardLoop` correct: (1) foreach iteration, (2) `pos == null` left of `||`, (3) `pos.Quantity == 0` right of `||`. McCabe CYC = 3.

`Flatten(Instrument)` CYC impact: foreach replaced by single helper call site (no new branches in the method body). CYC 3→4: existing (1) TrimFlatten check + (2-3) from the replace being counted correctly. Within limit.

`Flatten(Account, Instrument)` CYC impact: follower foreach + `acc==leader` skip replaced by helper call + `.Where` lambda (the Where predicate's branch is in the lambda, not McCabe-counted in the caller method). CYC 5→6: existing 5 branches + 1 for the Where delegate invocation site. Within limit.

**CYC Pre-Check: PASS**

---

### 8. NT8 Constraints

| Constraint | Ticket Status |
|------------|--------------|
| No `async/await` in lifecycle method | Not present. Pure synchronous static helper. PASS |
| No `lock()` introduced | Explicitly SCAN-01 verified; static method, stack-local only. PASS |
| No `Account.All` call outside Loaded handler | Not used. Existing `AllAccounts()` helper used. PASS |
| No `sealed` on TradeCopierWindow | Not touched. PASS |
| No `FontFamily` on WPF element | Not touched. PASS |
| No hardcoded hex color | Not touched. PASS |
| No `CreateOrder` with name not starting "PTT-" | `CreateOrder` not called in new code. PASS |
| No `DateTime.Now` | Not present; confirmed "No datetime usage in this fix." PASS |
| ASCII-only log message | `"[FLAT-GUARD] skip flat, already flat on "` -- brackets, letters, digits, hyphen, space only. PASS |
| No new public API on TradeCopierWindow | Not touched. PASS |

**NT8 Check: PASS**

---

### 9. JS / Jane Street Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock/mutex/monitor) | `FlatGuardLoop` is `private static`, all state stack-local. No `lock()`, `Monitor`, `Mutex`. | PASS |
| JS-001 (no throw in hot path) | No `throw` in any new method. Log-and-continue pattern. | PASS |
| JS-002 (no return null for optional) | `FlatGuardLoop` returns `void`. | PASS |
| JS-003 (no empty-string sentinel) | No string-as-state-sentinel. | PASS |
| JS-008 (no mutable struct fields) | No new struct introduced. | PASS |
| JS-009 (SolidColorBrush freeze) | No brush introduced. | PASS |
| JS-023 (no Dictionary on shared state) | No `Dictionary<K,V>` introduced. Static helper has no fields. | PASS |
| JS-025 (no DateTime.Now) | Not used. | PASS |
| JS-010 (ASCII-only string literals) | Confirmed. All added strings are pure ASCII. | PASS |

**JS Pre-Check: PASS**

---

### 10. Completeness

| Item | Present |
|------|---------|
| Out-of-scope declaration | Explicit table: PttFlatten.Execute, QX paths, BE paths, Trim paths, HasInflightFlatten, FlattenFollower, BUG-2. PASS |
| Acceptance criteria | 12-point numbered checklist covering build, test, all 7 scans, hard-link sync, behavioral. PASS |
| Deferred items carry-forward | Table of 7 deferred items (DW-BUG-0911-A-01/02 + 5 carry-forwards from PTT-COPIER-B26). PASS |

**Completeness: PASS**

---

### T1 Overall Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS |
| Scope Lock | PASS |
| Files in Scope | PASS |
| Implementation Code Review | PASS |
| 7-Scan Checklist (all 7 present) | PASS |
| Test Coverage | PASS |
| CYC Pre-Check | PASS |
| NT8 Constraints | PASS |
| JS Pre-Check | PASS |
| Completeness | PASS |

**VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

**Ticket count reviewed:** 1 (T1 only -- single-ticket epic per plan §0 LANE-SPLIT GATE)
**Violations found:** 0
**Warnings:** 0

The ticket is cleared for Phase 4 engineering. The engineer reads
[`04-ticket-review.md`](docs/brain/PTT-BUGS-LIVE-0911/04-ticket-review.md) first, then
[`04-tickets.md`](docs/brain/PTT-BUGS-LIVE-0911/04-tickets.md).

---

## Reviewer Notes (non-blocking, informational)

1. **Plan §4 scan table label drift (informational):** The architecture plan's §4 table uses a different scan numbering/naming than the canonical PTT 7-scan pipeline standard. Specifically, the plan lists SCAN-02 as "`DateTime.Now` grep" while the canonical standard defines SCAN-02 as "ASCII-only string literals." The **ticket correctly uses the canonical pipeline ordering** (SCAN-01=lock, SCAN-02=ASCII, SCAN-03=CYC, SCAN-04=public API, SCAN-05=?.Event, SCAN-06=[Fact] coverage, SCAN-07=test count delta). No action required; the ticket is correct.

2. **Mid-close race window (DW-BUG-0911-A-01):** Correctly deferred. The guard catches the definitively-flat case (pos==null || qty==0). The in-flight race is a separate atomic-slot problem, appropriately scoped out of this surgical fix.

3. **Test count baseline confirmed:** grep on [`src/PropTraderTools/CopyEngineTests.cs`](src/PropTraderTools/CopyEngineTests.cs) returns exactly 168 `[Fact]` matches. The SCAN-07 baseline claim of 168 is accurate.

---

*ptt-ticket-reviewer | PTT-BUGS-LIVE-0911 Lane A | Phase 3.5 | 2026-09-11*
