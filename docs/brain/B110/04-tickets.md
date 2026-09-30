# B110 Tickets
# DW-B110: Remove CancelQxBracketsForFollowers from Leader Path

**Status**: TICKETS_COMPLETE  
**Epic**: B110  
**Phase**: 3 (Ticket Generation)  
**Author**: ptt-architect  
**Date**: 2026-08-26  
**Source Plan**: docs/brain/B110/02-architecture-plan.md (REVIEW_PASS)

---

# Ticket 1 — B110: Remove CancelQxBracketsForFollowers from PttQuickExit.Execute

## Spec Req IDs

- **DW-B110** (P0 Combo C collision fix — primary target)
- **Ref: DW-B79-03** (PttGlobalQuickExit ExecuteOne guarded cancel — unchanged, remains correct path)
- **Ref: DW-B70-02** (original feature — being removed as REDUNDANT+HARMFUL per plan Section 2)

---

## Problem

The leader path in `PttQuickExit.Execute` calls `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr)` at
L107 **before** the per-follower `_qxCancelInProgress` flag is set for each follower account. When
`QX-ALL` fires, `PttGlobalQuickExit.ExecuteOne` first runs for the leader (with `skipIfFollower=true`),
which triggers `CancelQxBracketsForFollowers` and issues `OnOrderUpdate(Cancelled)` events on every
follower account. At that moment the follower's own `ExecuteOne` has not yet run, so the
`_qxCancelInProgress` flag for that follower is absent; guard (3b) in `TryReplacePttBeBrackets`
evaluates to **FALSE**, and a spurious BE-RETRY fires simultaneously with the pending QX order
submission — producing the Combo C defect (BE-ALL followed by QX-ALL causes collision).
`DW-B79-03` in `PttGlobalQuickExit.ExecuteOne` already performs the same per-follower cancel
correctly (with the flag set), making the leader-path call strictly redundant and harmful.

---

## Method Signatures (context only — signatures are unchanged by this ticket)

```csharp
// src/PropTraderTools/Features/PttQuickExit.cs
internal void Execute(
    Account leader,
    Instrument instr,
    int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true,
    double leaderStop = 0,
    int leaderTargetCount = 0
)
// CYC changes 8 -> 7 only (no parameter changes)

// src/PropTraderTools/CopyEngine.cs
internal void CancelQxBracketsForFollowers(Instrument instr)
// STAYS in CopyEngine.cs -- no signature change, no deletion
```

---

## File Paths in Wave Workspace

```
MODIFY : src/PropTraderTools/Features/PttQuickExit.cs
CREATE : src/PropTraderTools/Tests/B110Tests.cs
NO CHANGE: src/PropTraderTools/CopyEngine.cs
NO CHANGE: src/PropTraderTools/Features/PttGlobalQuickExit.cs
NO CHANGE: src/PropTraderTools/Tests/B68Tests.cs
NO CHANGE: src/PropTraderTools/Tests/B78Tests.cs
NO CHANGE: src/PropTraderTools/Tests/B79Tests.cs
```

---

## Implementation Steps

### Step A — DELETE L100–L107 from `src/PropTraderTools/Features/PttQuickExit.cs`

Verify exact line numbers by reading the file. Based on the plan-review-confirmed source, the block
to delete starts immediately after the `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);`
call at approximately L99 and ends at the `CancelQxBracketsForFollowers` call-site.

Delete these **8 lines** (verbatim content — engineer must verify line numbers before edit):

```csharp
            // B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders.
            // B78 DW-B78-02: ONLY from the leader execution path (skipIfFollower=true).
            // When skipIfFollower=false (follower account), CancelQxBracketsForFollowers would
            // silently erase every previous follower's just-submitted PTT-QX orders, because
            // each follower's Execute call runs on the same synchronous dispatch loop and the
            // sibling PTT-QX orders are in Submitted/Initialized state -- IsQxCancelCandidate matches them.
            if (skipIfFollower)
                CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

After deletion, the line immediately following the `CancelQxBrackets(leader, instr, snapshot)` call
must be the blank line before `// Step 4: compute direction and tick` (or the Step 4 comment itself
if no blank line is present). Do **not** delete any surrounding lines.

### Step B — UPDATE Execute docstring in `src/PropTraderTools/Features/PttQuickExit.cs`

Two sub-changes within the `/// <summary>` block of `Execute`:

**Sub-change B1** — Replace the CYC=8 branch list line (~L28-29):

Replace:
```
/// CYC=8: null/flat guard(1) + follower guard(2) + cancelFollowers guard(3) + snapshotStop guard(4)
///        + isLong(5) + for-loop(6) + stop-submit null check(7) + target-submit null check(8).
```

With:
```
/// CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3)
///        + isLong(4) + for-loop(5) + stop-submit null check(6) + target-submit null check(7).
```

**Sub-change B2** — Delete the two-line B78 DW-B78-02 sentence (~L35-36):

Delete:
```
/// B78 DW-B78-02: CancelQxBracketsForFollowers guarded by skipIfFollower -- prevents sibling
///   follower QX orders from being cancelled by subsequent follower Execute calls.
```

No other lines in the docstring are modified. All remaining `///` lines stay as-is.

### Step C — CREATE `src/PropTraderTools/Tests/B110Tests.cs`

Create a new file with the following complete content (namespace `PropTraderTools`, pattern
matches existing test files such as `B68Tests.cs`):

```csharp
// src/PropTraderTools/Tests/B110Tests.cs
// B110: DW-B110 -- Remove CancelQxBracketsForFollowers from PttQuickExit leader path.
// 2 xUnit [Fact] tests: T_B110_01, T_B110_02.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.

using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B110Tests
    {
        // -------------------------------------------------------------------------
        // T_B110_01: IL token scan -- PttQuickExit.Execute does NOT call
        // CancelQxBracketsForFollowers. Mirrors T_B68_03 pattern on CopyEngine.DispatchCopy.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B110_01_Execute_DoesNotCallCancelQxBracketsForFollowers()
        {
            // Arrange: locate Execute on PttQuickExit
            var executeMi = typeof(PttQuickExit).GetMethod(
                "Execute",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(executeMi);

            // Arrange: locate CancelQxBracketsForFollowers on CopyEngine
            var cancelFollowersMi = typeof(CopyEngine).GetMethod(
                "CancelQxBracketsForFollowers",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(cancelFollowersMi);

            int cancelToken = cancelFollowersMi.MetadataToken;

            // Act: scan Execute IL for CancelQxBracketsForFollowers token
            var body = executeMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            bool foundCancelFollowers = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                // call (0x28) or callvirt (0x6F) opcode
                if (il[i] == 0x28 || il[i] == 0x6F)
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == cancelToken)
                    {
                        foundCancelFollowers = true;
                        break;
                    }
                }
            }

            // Assert: Execute must NOT call CancelQxBracketsForFollowers -- DW-B110 fix
            Assert.False(
                foundCancelFollowers,
                "PttQuickExit.Execute must NOT call CancelQxBracketsForFollowers -- DW-B110 fix"
            );
        }

        // -------------------------------------------------------------------------
        // T_B110_02: IL branch count scan -- PttQuickExit.Execute has exactly 6 branch
        // instructions after the DW-B110 fix, confirming CYC=7 (CYC = branch_count + 1).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B110_02_Execute_CycIs7_BranchCountIs6()
        {
            // Arrange: locate Execute on PttQuickExit
            var executeMi = typeof(PttQuickExit).GetMethod(
                "Execute",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(executeMi);

            // Act: count branch instructions in Execute IL
            var body = executeMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Branch opcodes (short and long forms):
            // brfalse.s=0x2C, brtrue.s=0x2D, br.s=0x2B
            // brfalse=0x39, brtrue=0x3A, br=0x38
            // beq.s=0x2E, bge.s=0x2F, bgt.s=0x30, ble.s=0x31, blt.s=0x32, bne.un.s=0x33
            // beq=0x3B, bge=0x3C, bgt=0x3D, ble=0x3E, blt=0x3F, bne.un=0x40
            // bge.un.s=0x34, bgt.un.s=0x35, ble.un.s=0x36, blt.un.s=0x37
            // bge.un=0x41, bgt.un=0x42, ble.un=0x43, blt.un=0x44
            int branchCount = 0;
            for (int i = 0; i < il.Length; i++)
            {
                byte op = il[i];
                if (
                    op == 0x2B || op == 0x2C || op == 0x2D
                    || // br.s, brfalse.s, brtrue.s
                    op == 0x2E || op == 0x2F || op == 0x30 || op == 0x31 || op == 0x32
                    || op == 0x33
                    || // beq.s..bne.un.s
                    op == 0x34 || op == 0x35 || op == 0x36 || op == 0x37
                    || // bge.un.s..blt.un.s
                    op == 0x38 || op == 0x39 || op == 0x3A
                    || // br, brfalse, brtrue
                    op == 0x3B || op == 0x3C || op == 0x3D || op == 0x3E || op == 0x3F
                    || op == 0x40
                    || // beq..bne.un
                    op == 0x41 || op == 0x42 || op == 0x43 || op == 0x44 // bge.un..blt.un
                )
                {
                    branchCount++;
                }
            }

            // Assert: CYC=7 means branchCount=6 (CYC = branch_count + 1)
            Assert.Equal(
                6,
                branchCount
            );
        }
    }
}
```

---

## xUnit [Fact] Names

```
[Fact] public void T_B110_01_Execute_DoesNotCallCancelQxBracketsForFollowers()
[Fact] public void T_B110_02_Execute_CycIs7_BranchCountIs6()
```

Both in class `B110Tests` (sealed), namespace `PropTraderTools`, file `src/PropTraderTools/Tests/B110Tests.cs`.

---

## JS Rule Constraints (per modified method/file)

| Rule | Scope | Requirement |
|------|-------|-------------|
| **JS-021** | All modified files | No `lock()` — none introduced; deletion removes code only |
| **JS-001** | `PttQuickExit.Execute` | No `throw new XxxException` in hot path — deletion adds no exceptions |
| **JS-002** | `PttQuickExit.Execute` | No `return null` — no new return paths |
| **JS-033** | `B110Tests.cs` | No `async void` — both test methods are synchronous `void` |
| **JS-051** | `B110Tests.cs` | xUnit `[Fact]` only — no NUnit/MSTest |
| **JS-066** | PR diff | Diff < 10k chars — deletion of 8 lines + docstring update ≈ 600 chars; well within limit |
| **JS-080** | `PttQuickExit.Execute` | CYC ≤ 8 — post-fix CYC=7, improves compliance margin |

---

## Combo Regression Map (verify criteria — all four must hold post-fix)

| Combo | Description | Covering Test/Scan | Expected Result |
|-------|-------------|-------------------|-----------------|
| C | BE-ALL -> QX-ALL (copier ON) | T_B110_01 (IL scan: call absent) | PASS -- no CancelQxBracketsForFollowers call from leader path |
| D | QX-ALL -> BE-ALL | T_B68_03 (DispatchCopy clean) | PASS -- DW-B79-03 path unaffected |
| E | QX-ALL direct (no BE brackets) | T_B68_03 + build scan | PASS -- no behaviour change |
| F | QX-ALL -> BE-ALL while in green (B108 path) | T_B68_03 + build scan | PASS -- B108 path unaffected |

Rationale: Combo C is the target defect. Combos D/E/F are non-regression guards.

---

## 7-Scan Checklist (engineer contract — all must reach zero/PASS before BUILD_PASS)

```
SCAN-01 — Build
  Command : dotnet build src/
  Pass    : Zero errors, zero warnings

SCAN-02 — Tests
  Command : dotnet test
  Pass    : All pre-existing tests green AND T_B110_01 green AND T_B110_02 green

SCAN-03 — Lock
  Command : grep -r "lock(" src/PropTraderTools/Features/PttQuickExit.cs
            grep -r "lock(" src/PropTraderTools/Tests/B110Tests.cs
  Pass    : Zero results in both modified files

SCAN-04 — CYC
  Command : python scripts/complexity_audit.py
  Pass    : PttQuickExit.Execute reports complexity = 7

SCAN-05 — ASCII
  Command : grep -P "[^\x00-\x7F]" src/PropTraderTools/Features/PttQuickExit.cs
            grep -P "[^\x00-\x7F]" src/PropTraderTools/Tests/B110Tests.cs
  Pass    : Zero non-ASCII bytes in both modified files

SCAN-06 — Combo C guard
  Verify  : T_B110_01 green (asserts CancelQxBracketsForFollowers token absent from Execute IL)
  Pass    : Assert.False(foundCancelFollowers, ...) passes

SCAN-07 — Non-regression
  Verify  : T_B68_03 still green (DispatchCopy does not call CancelQxBracketsForFollowers)
  Pass    : Existing T_B68_03 in B68Tests.cs passes unchanged
```

---

## Additional Verify Checks (Ph4b — ptt-verifier contract)

```
T8 : grep "CancelQxBrackets(acc, instr)" src/PropTraderTools/Features/PttGlobalQuickExit.cs
     -> Must be PRESENT (DW-B79-03 block intact at approximately L157)

T9 : powershell -File scripts\ptt-sync-and-verify.ps1
     -> Pass condition: 0 MISMATCH lines in output

T10: Agent writes PASS confirmation from ptt-sync-and-verify.ps1 to:
     docs/brain/B110/ticket-1-verification.md
     (Must log: sync run timestamp + "0 MISMATCH" verbatim from script output)
```

---

## Files Written

```
MODIFY : src/PropTraderTools/Features/PttQuickExit.cs
         Change 1: Delete L100-L107 (8-line comment block + if-call)
         Change 2: Update Execute docstring (CYC=8->7, remove B78 sentence, renumber branches)

CREATE : src/PropTraderTools/Tests/B110Tests.cs
         Contains: T_B110_01 (IL token scan) + T_B110_02 (IL branch count)

NO CHANGES:
  src/PropTraderTools/CopyEngine.cs
  src/PropTraderTools/Features/PttGlobalQuickExit.cs
  src/PropTraderTools/Tests/B68Tests.cs
  src/PropTraderTools/Tests/B78Tests.cs
  src/PropTraderTools/Tests/B79Tests.cs
```

---

## Completion Artifact

Engineer must write: `docs/brain/B110/ticket-1-completion.md`

Required content:
1. **7-Scan Results Table** — all 7 scans with zero/PASS result per scan
2. **Build Output** — full `dotnet build` output (last 20 lines minimum)
3. **Test Output** — full `dotnet test` output confirming T_B110_01 and T_B110_02 pass
4. **Exact Lines Deleted** — copy-paste of the 8 deleted lines with their original line numbers
5. **Docstring Diff** — before/after of the two docstring sub-changes (B1 and B2)
6. **ptt-sync-and-verify.ps1 output** — confirming 0 MISMATCH lines (T9)

---

*Tickets generated by ptt-architect from REVIEW_PASS plan. Engineer contract is this file only.*
