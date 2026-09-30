# B102 Deferred Backlog

## Block: B102
## Date: 2026-08-11
## Pipeline Status: FINAL_PASS

---

### DW-B102-DEFER-01: Pre-existing build errors in test files

- **Severity**: P2
- **Files**: CopyEngineTests.cs, B76Tests.cs, B43Tests.cs, B68Tests.cs, B71Tests.cs, Tests/B43Tests.cs
- **Root cause**: Multiple pre-existing compilation errors (CS0246 CopyRule not found, CS0234 missing assembly refs, CS0117 missing method, CS8400 language version mismatch, CS0433 ambiguous Globals reference, CS7036 constructor arg count mismatches, CS0272 set accessor inaccessible, CS1061 missing extension method, CS0122 inaccessible singleton constructor) confirmed present on commit `e06bce7b` BEFORE B102 changes via `git stash; dotnet build; git stash pop` test. Not caused by B102.
- **Action**: Separate dedicated ticket/PR to resolve pre-existing test compilation errors. See `DW-CSPROJ-BUILD-DEBT-01` in `docs/brain/NO-PIPELINE-REPAIRS.md` for full inventory.
- **Status**: OPEN

---

### DW-B102-DEFER-02: xUnit tests T_B100_01..T_B101_02 not yet implemented

- **Severity**: P2
- **Scope**: 5 test methods specified in TICKET-B102-1 test plan:
  - `T_B100_01_SaveRules_WritesFile`
  - `T_B100_02_LoadRules_RestoresState`
  - `T_B100_03_LoadRules_MissingFile_IsNoop`
  - `T_B101_01_EvictDedup_Cancelled_ClearsEntryDispatched`
  - `T_B101_02_EvictDedup_Filled_DoesNotClearOtherEntries`
- **Blocked by**: DW-B102-DEFER-01 — test project does not compile, [Fact] methods cannot be added until the pre-existing build errors are resolved.
- **Action**: Future ticket to implement as actual xUnit `[Fact]` methods in CopyEngineTests.cs once DW-B102-DEFER-01 is resolved. Test stubs are fully specified in `docs/brain/B102/02-architecture-plan.md` Section 7.
- **Status**: OPEN (blocked on DW-B102-DEFER-01)

---

### DW-B102-DEFER-03: catch(Exception) swallow pattern in SaveRules/LoadRules

- **Severity**: P3 (known, intentional design)
- **Files**: CopyEngine.cs L4066 (SaveRules), L4105 (LoadRules)
- **Root cause**: Intentional — bare `catch (Exception) { }` wrapping the entire method body ensures NT8 does not receive an unhandled exception during shutdown or panel reload. This is an accepted NT8 AddOn pattern. The swallow was the root cause of DW-B100 remaining silent for so long; however, removing it is out of scope for B102 per the FORBIDDEN section of TICKET-B102-1 ("Do NOT remove catch(Exception) swallows").
- **Action**: No change required. Document as intentional. Review only if user reports persistent data loss after the DW-B100 fix is verified in live trading. If logging is later desired, add a single `Output.Process("[PTT-RULES] SaveRules/LoadRules exception: " + ex.Message)` line inside the catch — do NOT remove the catch.
- **Status**: OPEN (intentional — no action planned)

---

### DW-B102-DEFER-04: Stale CYC header comment in EvictDedup

- **Severity**: P3
- **File**: CopyEngine.cs L3100
- **Detail**: Header comment reads `// CYC=2` but post-B102 EvictDedup is CYC=3. The CYC count in the comment was not updated when Change 3 added the Cancelled branch. No correctness impact. Updating this comment was not in the scope of TICKET-B102-1.
- **Action**: Future pass — change `// CYC=2` to `// CYC=3` at L3100. One-line change, safe to batch with any future CopyEngine.cs surgical edit.
- **Status**: OPEN
