# PTT-REPAIRS-04-POST-BUG-F -- Ticket Review (Phase 3.5)

**Reviewer**: PTT Ticket Reviewer
**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Tickets file**: docs/brain/PTT-REPAIRS-04-POST-BUG-F/04-tickets.md
**Plan file**: docs/brain/PTT-REPAIRS-04-POST-BUG-F/02-architecture-plan.md
**Plan gate**: REVIEW_PASS (02-plan-review.md line 10 -- confirmed)

---

## Ticket Review: PTT-REPAIRS-04-POST-BUG-F

### T1 -- BUG-F: Clone ATM Per-Instrument Fix (Source Verification)

---

#### A. STEP SPECIFICITY

**PASS**

All 17 steps checked.

STEP-01 through STEP-10 (CopyEngine.cs and TradeCopierPanel.cs verifications):
Each step uses a `~` line-range as a navigation hint and then EXPLICITLY instructs the
engineer to "cite the exact line numbers and text" (STEP-01) or "cite the exact line"
(STEP-02 through STEP-10). For a SOURCE VERIFICATION ticket, the verifier is the one who
determines and records the exact line number -- the `~` hint is correct and the deliverable
requirement (verbatim citation) is present in every step. No step omits the "cite exact line"
instruction. Field names, method signatures, and argument expressions are stated exactly:
`_cloneAtmCacheByInstr`, `_cloneAtmObjectByInstr`, `StringComparer.Ordinal`, full method
signatures with all parameter names, exact argument expressions (`order.Instrument.FullName`,
`cancelledOrder.Instrument.FullName`, `instrKey`).

STEP-11 through STEP-17: all include exact shell commands.
- STEP-11: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }` ✓
- STEP-12: same command against TradeCopierPanel.cs ✓
- STEP-13: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"` ✓
- STEP-14: same command against TradeCopierPanel.cs ✓
- STEP-15: CYC table with branch-count derivation ✓
- STEP-16: `dotnet build src/PropTraderTools/Linting.csproj /nologo 2>&1` ✓
- STEP-17: `fsutil hardlink list src/PropTraderTools/CopyEngine.cs` AND `fsutil hardlink list src/PropTraderTools/TradeCopierPanel.cs` ✓

---

#### B. SCOPE COMPLIANCE

**PASS**

Ticket header (line 14): "SOURCE VERIFICATION ONLY" declared explicitly.
Line 15: "No new .cs edits unless a fix step is found missing from source."
Line 16: "No test updates. Stale T_CLONE_* test update is deferred (see DEFERRED-1 below)."
Files in scope (lines 19-20): `src/PropTraderTools/CopyEngine.cs` AND `src/PropTraderTools/TradeCopierPanel.cs` -- both files present.
STEP-11 through STEP-14 run scans on both files independently.
STEP-17 verifies hard-link integrity for both files.

---

#### C. 7-SCAN CHECKLIST PRESENCE

**PASS**

All 7 scans present in BOTH the step body (STEP-11 through STEP-17) and the formal
"7-SCAN CHECKLIST (engineer contract)" section (lines 291-297).

| # | Scan | Location in ticket | Command/value | Status |
|---|------|--------------------|---------------|--------|
| SCAN-01 | lock() -- CopyEngine.cs | STEP-11 + checklist line 291 | `Select-String ... \block\s*\(` | PRESENT ✓ |
| SCAN-02 | lock() -- TradeCopierPanel.cs | STEP-12 + checklist line 292 | same pattern against TCP.cs | PRESENT ✓ |
| SCAN-03 | non-ASCII -- CopyEngine.cs | STEP-13 + checklist line 293 | `Select-String ... [^\x00-\x7F]` | PRESENT ✓ |
| SCAN-04 | non-ASCII -- TradeCopierPanel.cs | STEP-14 + checklist line 294 | same pattern against TCP.cs | PRESENT ✓ |
| SCAN-05 | CYC spot-check | STEP-15 + checklist line 295 | SetCloneAtmObjectCache=2, GetCloneAtmMode=4, ResolveAtmMode=2 | PRESENT ✓ |
| SCAN-06 | Build | STEP-16 + checklist line 296 | `dotnet build Linting.csproj /nologo` | PRESENT ✓ |
| SCAN-07 | Hard-link | STEP-17 + checklist line 297 | `fsutil hardlink list` both files | PRESENT ✓ |

CYC values in checklist (SetCloneAtmObjectCache=2, GetCloneAtmMode=4, ResolveAtmMode=2) match
architecture plan CYC table (plan lines 157-165): GetCloneAtmMode=4 confirmed. No discrepancy.

---

#### D. DEFERRED ITEMS

**PASS**

DEFERRED-1 (ticket lines 280-281): explicitly names the stale T_CLONE_* tests, cites
approximate lines 4620-4685 in CopyEngineTests.cs, describes the required change (zero-param
to one-param GetCloneAtmMode call), and names the deferred target session ("Option A test
runner session").

Ticket header line 16 states "No test updates" and cross-references DEFERRED-1.

STEP-16 (lines 233-238) further reinforces this: CopyEngineTests.cs errors are explicitly
scoped out of the BUILD_PASS criterion for this ticket. Engineer is told to report them
separately as a note.

No test updates are in scope for Phase 4a. Contract is unambiguous.

---

#### E. TRACEABILITY

**PASS**

Spec requirement citation: "BUG-F" at ticket line 12. ✓

Architecture plan call graph (plan lines 103-132) lists 7 changed call graph nodes.
All are covered by a verification step:

| Plan call graph item | Ticket step |
|----------------------|-------------|
| `_cloneAtmCacheByInstr` field (new) | STEP-01 |
| `_cloneAtmObjectByInstr` field (new) | STEP-01 |
| Old volatile scalars absent | STEP-01 (with Select-String command) |
| `SetCloneAtmCache(string instrFullName, string value)` | STEP-02 |
| `SetCloneAtmObjectCache` null branch body | STEP-03 |
| `GetCloneAtmMode(string instrFullName)` TryGetValue keyed lookup | STEP-04 |
| `ResolveAtmMode` 3-param signature, passes instrFullName | STEP-07 |
| `DispatchToFollower` call site at line 2672 | STEP-05 |
| `ReplaceFollowerCopyOnAtmCancel` call site at line 4404 | STEP-06 |
| `OnCloneModeClick` instrKey assignment | STEP-08 |
| `OnCloneModeClick` SetCloneAtmObjectCache call with instrKey | STEP-09 |
| `OnCloneModeClick` SetCloneAtmCache call with instrKey | STEP-10 |

Zero plan items uncovered. Zero ticket steps are phantom (every step traces to a plan item).

CYC value cross-check -- STEP-04 ticket states CYC=4 for GetCloneAtmMode.
Plan CYC table (plan line 161) states CYC=4 for GetCloneAtmMode. Match. ✓
STEP-15 CYC table states GetCloneAtmMode=4. Match. ✓
No discrepancy between ticket and plan on any CYC value.

---

#### F. JS PRE-CHECK

**PASS**

| Rule | Ticket description | Status |
|------|--------------------|--------|
| JS-021 (no lock()) | All operations described use ConcurrentDictionary atomic ops (TryGetValue, TryRemove, indexer set). No lock() described anywhere in method bodies. JS RULE CONSTRAINTS table (ticket lines 302-311) explicitly lists JS-021 as "No lock()". | PASS |
| JS-013 (CYC <= 8) | Max CYC described = 4 (GetCloneAtmMode, STEP-04, STEP-15). All methods <= 8. | PASS |
| JS-001 (no throw in dispatch) | No throw described in SetCloneAtmCache, SetCloneAtmObjectCache, GetCloneAtmMode, ResolveAtmMode bodies. Ticket JS CONSTRAINTS table cites JS-001 explicitly. | PASS |
| JS-002 (no null return) | GetCloneAtmMode described as returning `new FollowerAtmMode.Inherit()` as final fallback (STEP-04, STEP-07). JS-002 cited in constraints table. | PASS |
| JS-009/025 (ConcurrentDictionary) | Fields described as `ConcurrentDictionary`, not `Dictionary<K,V>`. JS-025 cited in constraints table. | PASS |
| JS-042 (ASCII-only) | STEP-13 and STEP-14 scan for non-ASCII. JS-042 cited in constraints table. | PASS |
| JS-023 (Dispatcher.InvokeAsync) | OnCloneModeClick runs on UI thread; dictionary writes are lock-free. No off-thread UI update described. JS-023 cited as N/A in constraints table with correct rationale. | PASS |

No concurrency violations, type-safety violations, immutability violations, or NT8 constraint
violations described anywhere in the ticket body.

---

#### FILE ROUTING

**PASS**

Files in scope (ticket lines 19-20):
- `src/PropTraderTools/CopyEngine.cs` ✓
- `src/PropTraderTools/TradeCopierPanel.cs` ✓

Both paths resolve to the Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`).
No Director workspace paths referenced for .cs files.

---

### VERDICT: TICKET_REVIEW_PASS

All six adversarial criteria pass. Zero violations found.

---

## Overall: TICKET_REVIEW_PASS

One ticket in scope. All checks pass. Safe to spawn engineer (Phase 4a).

Engineer contract summary:
- 17 verification steps, all with exact field names / signatures / argument expressions to confirm
- Exact shell commands for STEP-11 through STEP-17
- 7-scan checklist present with values matching the architecture plan
- DEFERRED-1 explicitly scoped out with no ambiguity
- BUILD_PASS criterion scoped to CopyEngine.cs and TradeCopierPanel.cs only (CopyEngineTests.cs errors expected and non-blocking)
