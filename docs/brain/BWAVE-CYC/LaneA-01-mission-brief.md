# BWAVE-CYC Lane-A — Mission Brief
## PTT-COPIER | CopyEngine BE/ATM/Bracket Complexity Reduction

**Stage**: 1 of 4 (ptt-orchestrator → ptt-architect handoff)
**Wave workspace**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Epic**: BWAVE-CYC Lane-A
**Baseline commit**: 596ebf41
**Date**: pre-B144

---

## 1. Context and Goal

CopyEngine.cs has a Code Health score of **1.41 / 10** (CodeScene baseline) due to widespread
high-complexity methods in the BE, ATM, bracket, and sync clusters. The Jane Street strict standard
requires **CYC <= 8** for every production method. Lane-A targets **21 methods across 8 tickets**
in the line ranges 875–1100 and 2279–5520, reducing all to CYC <= 8 without any behaviour change.

Target Code Health after Lane-A: **>= 4.0 / 10** (meaningful improvement toward final 7.0 wave goal).

---

## 2. Constraints (non-negotiable)

- **Zero behaviour change.** No logic changes, no reordering, no early returns added or removed.
- **Private helpers only.** No new public or internal surface may be created.
- **Each helper CCN <= 4.** Leave headroom for future feature growth.
- **Each parent after extraction CCN <= 8.** Jane Street strict standard.
- **Helper names are semantic** — they describe the decision slice, not their position.
- **One new `[Fact]` test per extracted helper** added to `CopyEngineTests.cs`.
- **Build must pass after every ticket** before moving to the next.
- **JS Rules in scope**: JS-021 (no lock()), JS-002 (no return null), JS-033 (no async void).
- **Read `docs/standards/NT8_COMPILER_RULES.md`** before any .cs edit.

---

## 3. CodeScene Baseline (Lane-A methods only)

| Method | Line | CS warning |
|--------|------|------------|
| CancelQxBrackets (overload 1) | 875 | Complex Method (cc=16) |
| BuildQxSnapshot | 916 | Complex Method (cc=14) |
| CancelQxBrackets (overload 2) | 955 | Complex Method (cc=19) |
| CancelAllAccountOrders | 1013 | Complex Method (cc=14) |
| SyncFollowerBracket | 2279 | Complex Method (cc=16) |
| ResubmitTargetAfterCascade | 2588 | [No direct CS entry — Lizard CCN=13] |
| ResubmitOneCollateralLeg | 2701 | Complex Method (cc=15) + Large Method (79 LoC) |
| SyncAtmFollowerTarget | 2869 | Complex Method (cc=15) + Large Method (71 LoC) |
| HandleEntryChange | 3366 | Complex Method (cc=15) |
| TryFirePositionState | 3451 | Complex Method (cc=10) |
| ReplaceFollowerCopyOnAtmCancel | 3548 | Bumpy Road (2 bumps) + Complex Method (cc=16) |
| TryReplacePttBeBrackets | 3644 | Complex Method (cc=12) |
| TryCleanupReArmedAtmBracket | 3727 | Complex Method (cc=20) + Complex Conditional (10 exprs) |
| FlattenOneAccount | 4303 | Complex Method (cc=16) |
| AllAccounts | 4705 | Complex Method (cc=13) |
| CountLeaderTargets | 4904 | Complex Method (cc=16) |
| SnapshotBeTargets | 4938 | Complex Method (cc=28) |
| MoveStopToBreakEven | 4993 | Bumpy Road (3 bumps) + Complex Method (cc=14) + Large Method (82 LoC) |
| ArmPendingBe | 5308 | Complex Method (cc=17) |
| OnPendingBeAccountUpdate | 5480 | Complex Method (cc=19) |

**AllAccounts (L4705, CCN=9)** — only 1 over the hard CYC=8 limit; included in T8.

---

## 4. Ticket Roster (8 tickets — ptt-architect to expand into full ticket specs)

### T1 — Highest Severity Pair (CCN 32 + 27)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| OnPendingBeAccountUpdate | L5480–5520 | 32 | 19 |
| ArmPendingBe | L5308–5364 | 27 | 17 |

Goal: Both methods <= CCN 8 after extraction. Focus areas:
- OnPendingBeAccountUpdate: large conditional dispatch; extract per-account-state handlers
- ArmPendingBe: guard chain + state mutation; extract pre-arm guard and arm-state applier

### T2 — BE Target Snapshot + Stop-to-BE (CCN 24 + 18, Bumpy Road)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| SnapshotBeTargets | L4938–4981 | 24 | 28 (CS higher) |
| MoveStopToBreakEven | L4993–5133 | 18 | 14 + Bumpy Road (3 bumps) + Large |

Goal: SnapshotBeTargets <= 8; MoveStopToBreakEven <= 8.
Focus areas:
- SnapshotBeTargets: CS cc=28 indicates many conditional expressions; extract IsBeTargetStale() guard method
- MoveStopToBreakEven: Bumpy Road = nested loop/if depth; flatten each bump into a named helper

### T3 — Collateral Resubmit (CCN 25, Large Method)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| ResubmitOneCollateralLeg | L2701–2785 | 25 | 15 + Large (79 LoC) |

Goal: CCN <= 8 after extraction. Large Method means multiple distinct logical phases;
split each phase (validation, order cancel, order resubmit, cleanup) into private helpers.

### T4 — ATM Cleanup Pair (CCN 23 + 18)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| TryCleanupReArmedAtmBracket | L3727–3793 | 23 | 20 + Complex Conditional (10 exprs) |
| ReplaceFollowerCopyOnAtmCancel | L3548–3601 | 18 | 16 + Bumpy Road (2 bumps) |

Goal: Both <= CCN 8.
Focus areas:
- TryCleanupReArmedAtmBracket: the 10-expression Complex Conditional MUST become `IsReArmedAtmBracketCleanupRequired()`
- ReplaceFollowerCopyOnAtmCancel: 2 Bumpy Road bumps = 2 nested if-blocks; each becomes a named helper

### T5 — ATM/Bracket Sync (CCN 21 + 20) — DW-B143-POSSTATE-CYC8 P0
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| SyncAtmFollowerTarget | L2869–2953 | 21 | 15 + Large (71 LoC) |
| SyncFollowerBracket | L2279–2373 | 20 | 16 |

Goal: Both <= CCN 8. P0 deferred-work item — must be resolved this wave.

### T6 — Flatten + BE Replace + Target Count (CCN 19 + 14 + 13)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| FlattenOneAccount | L4303–4372 | 19 | 16 + Code Duplication cluster |
| TryReplacePttBeBrackets | L3644–3715 | 14 | 12 |
| CountLeaderTargets | L4904–4931 | 13 | 16 |

Goal: All 3 <= CCN 8.
Note: FlattenOneAccount has a Code Duplication cluster — extract the shared pattern into a single helper.

### T7 — HandleEntry + PositionState + ResubmitTarget (CCN 13 + 13 + 13) — DW-B143-POSSTATE-CYC8 P0
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| HandleEntryChange | L3366–3426 | 13 | 15 |
| TryFirePositionState | L3451–3499 | 13 | 10 |
| ResubmitTargetAfterCascade | L2588–2649 | 13 | — |

Goal: All 3 <= CCN 8. Two of three are DW-B143-POSSTATE-CYC8 P0 deferred-work items.

### T8 — QX Bracket Cancel Cluster + AllAccounts (CCN 16 + 14 + 12 + 11 + 9)
| Method | Lines | Lizard CCN | CS cc |
|--------|-------|-----------|-------|
| CancelQxBrackets (overload 2) | L955–1004 | 16 | 19 |
| CancelQxBrackets (overload 1) | L875–905 | 14 | 16 |
| CancelAllAccountOrders | L1013–1043 | 12 | 14 |
| BuildQxSnapshot | L916–944 | 11 | 14 |
| AllAccounts | L4705–4752 | 9 | 13 |

Goal: All 5 <= CCN 8.
Note: AllAccounts CCN=9 is only 1 over the limit; a minimal extraction is sufficient.

---

## 5. Deferred Work Items Resolved by Lane-A

| DW Item | Methods | Ticket |
|---------|---------|--------|
| DW-B143-POSSTATE-CYC8 P0 | SyncAtmFollowerTarget, SyncFollowerBracket | T5 |
| DW-B143-POSSTATE-CYC8 P0 | TryFirePositionState, HandleEntryChange | T7 |

These items were blocked pending complexity reduction. Lane-A closes them.

---

## 6. Mandatory Scans (all 7 — ptt-verifier runs all before VERIFY_PASS)

| Scan | Command | Target |
|------|---------|--------|
| SCAN-01 | `Select-String "lock(" src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-04 | `Select-String "throw new " src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-05a | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | 0 warnings for all T1–T8 methods |
| SCAN-05b | `$env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta` | Code Health does NOT decrease; no new Complex/Large/Bumpy |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test` | 370 pass, 22 pre-existing IL-reflection failures (ACCEPT), 0 new failures |

**Known baseline failures (not regressions)**: 22 IL-reflection failures in archive/v12-reference linting DLL, pre-existing since B87.
ptt-verifier MUST state: "22 pre-existing IL-reflection failures — accepted, baseline confirmed."

---

## 7. Post-Scan Mandatory Step

After all 7 scans pass:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```
(NT8 hard-link sync — mandatory after every .cs change; omitting causes CS0246 compilation errors.)

---

## 8. FINAL_PASS Criteria

- ptt-verifier VERIFY_PASS reported
- All 21 target methods: CCN <= 8 confirmed by `lizard`
- `cs delta`: Code Health score improved vs pre-Lane-A HEAD
- New `[Fact]` tests: minimum 1 per extracted helper, all passing
- No new `lock()`, no new `async void`, no new `return null`
- Hard-link sync complete (`verify_links.ps1 -Fix` run)
- `docs/brain/BWAVE-CYC/LaneA-04-verify-report.md` written

---

## 9. Output Artifact Chain

| Stage | Agent | Output |
|-------|-------|--------|
| 1 | ptt-orchestrator | `LaneA-01-mission-brief.md` ← this document |
| 2 | ptt-architect | `LaneA-02-architect-plan.md` |
| 3 | ptt-engineer | `LaneA-03-engineer-report.md` |
| 4 | ptt-verifier | `LaneA-04-verify-report.md` |

---

**STAGE 1 COMPLETE — handing off to ptt-architect.**
