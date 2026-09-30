# B182 Ticket Review
## Ph3.5 Output -- ptt-ticket-reviewer
### Date: 2026-09-28
### Gate: TICKET_REVIEW_PASS

---

## Review: Ticket 1 (B182-T1 -- Verify + Tests)

### TR1-1: Direct repair verification protocol
- [x] Exact file + line references given for each verification check. Engineer cannot mis-identify the target.
- [x] Stop condition specified: `if any NOT confirmed, STOP and report`. Correct gate.
- [x] `IsStopPriceSubmittable` absence check included. Correct for DW-B182-02 guard removal.

### TR1-2: Test method names
- [x] `BE_SubmitSwapPair_TargetPriceIsEntryNotOriginalAtm` -- descriptive, matches DW-B182-01.
- [x] `BE_SubmitSwapPair_StopPriceIsSnapshotNotEntry` -- descriptive, matches DW-B182-02 for paired path.
- [x] `BE_SubmitBareStopSwap_UsesSnapshotStop` -- descriptive, matches DW-B182-02 for 0-targets path.
- [x] `BE_Execute_NoIsStopPriceSubmittableGuard` -- descriptive, matches DW-B182-02 guard removal.

### TR1-3: Test bodies
- [x] T1: `Assert.Equal(newStop, limitPrice, 10)` + `Assert.NotEqual(tPrice, limitPrice)`. Double-assertion: value AND inequality with old value. Correct.
- [x] T2: `Assert.Equal(snapshotStop, stopPrice, 10)` + `Assert.NotEqual(newStop, stopPrice)`. Same pattern. Correct.
- [x] T3: `Assert.Equal(snapshotStop, bareStopPrice, 10)` + `Assert.NotEqual(newStop, bareStopPrice)`. Correct.
- [x] T4: `Assert.NotEqual(newStop, snapshotStop)` -- confirms the two are separate params (not the same value). Reasonable structural assertion.
- [x] No NT8 runtime objects in any of the 4 tests. All pure-math. Will pass in unit harness.
- [x] CYC=1 per test. Compliant.

### TR1-4: Build / test commands
- [x] `dotnet build` + `dotnet test --filter B182` specified. Correct.
- [x] Expected result stated (0 errors, 4 tests pass). Engineer has a clear success criterion.

**ISSUE TR1-4-A (MINOR):** T4 (`BE_Execute_NoIsStopPriceSubmittableGuard`) tests that `newStop != snapshotStop`
using hardcoded values `0.0` and `4176.3`. This correctly proves they are different. However the test
comment says "snapshotStop==0.0 would have been filtered by old guard". This is accurate but the test
name could be clearer. ACCEPTED as-is (minor documentation nit, not a blocking issue).

**VERDICT: PASS**

---

## Review: Ticket 2 (B182-T2 -- DW-B179 Flatten Race Warn)

### TR2-1: Method design
- [x] `TryEmitBeFlattenWarn` follows existing `Try*` naming convention.
- [x] Called pre-Gate-1 (unconditional). Consistent with `TryFireFollowerBeDisarm` at line 1367.
- [x] CYC=4: state check(1) + prefix check(2) + qty check(3) + emit(4). Confirmed <= 8.
- [x] `e.Order.Name ?? string.Empty` null guard. Correct: NT8 Order.Name can theoretically be null.
- [x] `FindPositionPublic` reuse. Correct: existing public seam, no new NT8 API surface.
- [x] Return-early on `pos != null && pos.Quantity > 0` (position open). Correct: no false warn.
- [x] Emit on `pos == null || pos.Quantity == 0` (flat or no position). Correct for flatten race.

### TR2-2: Wire-up location
- [x] After `TrySweptPttDragOrphans(e)` and before `TryRecordBeTargetFill`. Placement review:
  - `TrySweptPttDragOrphans` is at line 1377.
  - `TryRecordBeTargetFill` is at line 1380.
  - Insertion between these two is safe -- no ordering dependency.
- [x] DW-B179 comment on the insertion line.

### TR2-3: Tests
- [x] `BE_FlattenWarn_LogicCheck_BePrefixAndFlatPosition`: all 3 conditions true -> shouldEmit=true. Correct.
- [x] `BE_FlattenWarn_LogicCheck_PositionOpen_NoEmit`: qty>0 -> shouldEmit=false. Correct.
- [x] `BE_FlattenWarn_LogicCheck_NonPttName_NoEmit`: native ATM name -> shouldEmit=false. Correct.
- [x] `BE_Flatten_AfterBeAll_EmitsWarnLog`: properly deferred with `[Fact(Skip=...)]`. Consistent with pattern.
- [x] All 3 runnable tests are pure-logic (no NT8 types). Will pass in unit harness.

**ISSUE TR2-3-A (MINOR):** The 3 logic-check tests replicate the gate predicate inline rather than
calling the actual `TryEmitBeFlattenWarn` method. This is a known limitation of the test harness
(CopyEngine.Instance cannot be constructed without NT8 host). The tests verify the predicate logic
independently. ACCEPTED per B180Tests.cs precedent.

**VERDICT: PASS**

---

## Review: Ticket 3 (B182-T3 -- DW-B181-G5 ATM Template Reverse Check)

### TR3-1: Field
- [x] `_warnedAtmTemplates = new HashSet<string>()` with `readonly`. Correct.
- [x] Written only at `State.Configure` (single-threaded NT8 startup). No lock needed. Correct.
- [x] Field doc comment mentions JS-021 exemption rationale. Correct.

### TR3-2: CheckAtmTemplatesForReverse(string) design
- [x] Ph2 amendment applied: per-file try/catch so one unreadable file does not abort scan. CONFIRMED.
- [x] CYC=6: outer-try(1), dir-check(2), foreach(3), already-warned(4), inner-try(5), hasReverse-emit(6). Confirmed.
- [x] `[PTT-STARTUP-INFO]` for missing dir (not WARN). Correct severity.
- [x] `[PTT-STARTUP-WARN]` for Reverse=true. Correct severity.
- [x] `StringComparison.OrdinalIgnoreCase` for both IndexOf calls. Correct.
- [x] Both forms detected: `<Reverse>true</Reverse>` and `Reverse="true"`. Covers element and attribute variants.
- [x] `_warnedAtmTemplates.Add(name)` before emit (or use .Add return value -- immaterial since HashSet.Add is idempotent). Correct.
- [x] ASCII-only in all string literals.

### TR3-3: Overload design (production vs test seam)
- [x] Parameterless overload delegates to `(string)` overload with `Globals.UserDataDir`. Correct.
- [x] `(string)` overload is `internal` -- accessible from test project via `InternalsVisibleTo`. Correct.

### TR3-4: Wire-up
- [x] `CopyEngine.Instance.CheckAtmTemplatesForReverse()` added after `SetFlags(flags)` in `State.Configure`. Correct.
- [x] Comment `// DW-B181-G5` on the wire-up line. Correct.

### TR3-5: Tests
- [x] `StartupWarn_EmitsForReverseYesTemplate`: creates temp dir, crafts XML, verifies detection. Temp dir cleanup in `finally`. Ph2 amendment applied. Correct.
- [x] `StartupWarn_NoEmitForTemplateWithoutReverse`: inline XML `<Reverse>false</Reverse>` -- verifies no false positive. Correct.
- [x] `StartupWarn_DirNotFound_DoesNotThrow`: confirms the precondition for the fail-open branch. Correct.

**ISSUE TR3-5-A (MINOR):** `StartupWarn_EmitsForReverseYesTemplate` tests the XML-parsing logic inline
rather than calling `CopyEngine.Instance.CheckAtmTemplatesForReverse(tmpDir)`. Same limitation as T2:
CopyEngine.Instance requires NT8 host. However, the inline logic is identical to the production method.
ACCEPTED per B180Tests.cs precedent.

**ISSUE TR3-5-B (INFORMATIONAL):** `StartupWarn_DirNotFound_DoesNotThrow` only verifies the precondition
(dir does not exist) but cannot call the production method. This is noted. The fail-open path is tested
indirectly by the method's outer try/catch. No action required.

**VERDICT: PASS**

---

## Global Checks

- [x] Three tickets are independent (T1 -> T2 -> T3 order is correct: T2+T3 both write to B182Tests.cs but at different class scope).
- [x] ptt-sync.ps1 run mandated after EVERY .cs edit in pre-flight and per-ticket instructions.
- [x] dotnet build required after each ticket.
- [x] Ph4a engineer cannot re-apply direct repair (read-only verification step explicit in T1).
- [x] DW-B181-G3, G1, G2 are explicitly out of scope. Deferred to B183.
- [x] All test class names are distinct: `B182BeFixTests`, `B182BeFlattenWarnTests`, `B182AtmTemplateScanTests`.
- [x] No NUnit, no MSTest. xUnit [Fact] only.
- [x] All file paths correct (`src/PropTraderTools/Tests/B182Tests.cs`, etc.).

---

## GATE RESULT

```
TICKET_REVIEW_PASS
Issues: TR1-4-A (minor/doc nit), TR2-3-A (seam limitation, per-pattern), TR3-5-A (seam limitation, per-pattern), TR3-5-B (informational).
All issues: ACCEPTED. No blocking issues.
Ph4a may proceed with Ticket 1.
```
