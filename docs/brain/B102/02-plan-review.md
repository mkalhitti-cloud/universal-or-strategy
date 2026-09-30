# B102 Plan Review

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Plan file**: `docs/brain/B102/02-architecture-plan.md`
**Block**: B102
**Defects**: DW-B100 (XmlSerializer private-type silent failure), DW-B101 (Cancelled eviction gap)

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|---|---|---|
| DW-B100: Fix XmlSerializer private-type reflection failure | YES | §1, §3 (Changes 1 & 2) |
| DW-B100: Explain silent failure mechanism (catch swallow) | YES | §1 "Why the Bug Was Silent" |
| DW-B100: Explain LoadRules early-return chain | YES | §1 "Why LoadRules Always Returned Early" |
| DW-B101: Fix Cancelled order stale _entryDispatchedOrders entry | YES | §2, §3 (Change 3) |
| DW-B101: Identify correct fix site (EvictDedup) | YES | §2 "Why EvictDedup Is the Correct Fix Site" |
| Only CopyEngine.cs modified | YES | §6 Files Touched |
| TradeCopierPanel.cs NOT modified | YES | §6, plan header |
| All changes CYC ≤ 8 | YES | §4 CYC Impact |
| JS-DNA rules satisfied | YES | §5 JS-DNA Compliance |
| xUnit [Fact] tests for both defects | YES | §7 Test Plan |

---

## Check Results

### CHECK 1 — MINIMAL FIX

**Verdict: PASS**

The plan specifies exactly 3 changes to exactly 1 file (`CopyEngine.cs`):
- **Change 1**: `private sealed class CopyRuleDto` → `internal sealed class CopyRuleDto` at L3872 (one-word change).
- **Change 2**: `private sealed class CopyRulesContainer` → `internal sealed class CopyRulesContainer` at L3893 (one-word change).
- **Change 3**: One new `if`-branch in `EvictDedup` body + comment update at the `EvictDedup` comment block (§3).

No other changes are proposed. No method signatures are modified. `TradeCopierPanel.cs` is explicitly excluded (§6: "NOT TOUCHED").

---

### CHECK 2 — NO SCOPE CREEP

**Verdict: PASS**

None of the following are proposed in the plan:
- Removing the `catch(Exception)` swallows in `SaveRules`/`LoadRules` — plan acknowledges them for root-cause accuracy only; does not touch them.
- Changing method signatures — zero signature changes anywhere.
- Adding new public methods — none proposed.
- Touching `TradeCopierPanel.cs` — plan header and §6 both confirm explicitly NOT touched.
- Modifying the `_persistenceLoaded` guard — not mentioned as a change target.

---

### CHECK 3 — P0 JS VIOLATIONS

**Verdict: PASS**

The proposed changes introduce zero P0 violations:

| Pattern | Check | Result |
|---|---|---|
| `lock()` | `ConcurrentDictionary.Clear()` is interlocked (lock-free); no `lock()` added anywhere | PASS — JS-021 satisfied |
| `throw new XxxException` in hot path | No `throw` statement proposed | PASS — JS-001 satisfied |
| `return null` | No `return null` proposed | PASS — JS-002 satisfied |
| `async void` (non-event-handler) | No async methods added or modified | PASS — JS-033 satisfied |

The plan explicitly documents the lock-free rationale for `ConcurrentDictionary.Clear()` at §2 "Why ConcurrentDictionary.Clear() Is Lock-Free".

---

### CHECK 4 — ROOT CAUSE ACCURACY

**Verdict: PASS**

All five root cause points are correctly identified and attributed:

| Claim | Plan Citation | Accurate? |
|---|---|---|
| DW-B100: `private sealed class` → XmlSerializer reflection failure | §1 "Root Cause" — "private nested types are invisible to reflection from outside the declaring type's own methods" | YES |
| DW-B100: `catch(Exception){}` swallow makes SaveRules silent | §1 "Why the Bug Was Silent" — "swallowed without logging. The caller sees no error." | YES |
| DW-B100: LoadRules early-returns because file never written (File.Exists=false) | §1 "Why LoadRules Always Returned Early" — "_persistenceLoaded=true, File.Exists=false → early return" | YES |
| DW-B101: `TryEvictFollowerBeSlot` L1394 guard early-returns on Cancelled | §2 "Root Cause" — `isFilled=false, isRejected=false → EARLY RETURN` with inline code trace | YES |
| DW-B101: `EvictDedup` is correct fix site — already owns Cancelled for `_dedupCache` | §2 "Why EvictDedup Is the Correct Fix Site" — "already whitelists Cancelled alongside Filled and Rejected" | YES |

No root cause is misattributed. The Rithmic orderId-recycle failure scenario (§2) provides complete end-to-end tracing.

---

### CHECK 5 — CYC IMPACT

**Verdict: PASS**

| Method | Before | After | Delta | Limit | Plan States |
|---|---|---|---|---|---|
| DW-B100 changes (class decl) | n/a | n/a | 0 | n/a | "access-modifier only" — delta=0 |
| `EvictDedup` (DW-B101) | 2 | 3 | +1 | 8 | CYC 2→3, PASS |

From §4: "DW-B100: CYC delta = 0." and "DW-B101: EvictDedup CYC 2 → 3. One new `if` branch. Well within the <= 8 ceiling." Both statements are correct.

---

### CHECK 6 — TEST PLAN

**Verdict: PASS**

Exactly 5 xUnit `[Fact]` tests are specified in §7. No NUnit/MSTest framework references exist anywhere in the plan.

| Test ID | Framework | Covers |
|---|---|---|
| `T_B100_01_SaveRules_WritesFile` | xUnit `[Fact]` | DW-B100: SaveRules writes file after access-modifier fix |
| `T_B100_02_LoadRules_RestoresState` | xUnit `[Fact]` | DW-B100: LoadRules restores `_isCopyEnabled` + rule count |
| `T_B100_03_LoadRules_MissingFile_IsNoop` | xUnit `[Fact]` | DW-B100: Missing file path → no exception, empty state |
| `T_B101_01_EvictDedup_Cancelled_ClearsEntryDispatched` | xUnit `[Fact]` | DW-B101: Cancelled evicts `_entryDispatchedOrders` |
| `T_B101_02_EvictDedup_Filled_DoesNotClearOtherEntries` | xUnit `[Fact]` | DW-B101: Filled does NOT over-clear (boundary condition) |

Count = 5. T_B100_01 through T_B101_02 — all present. Full coverage of both defects including the Filled non-clear boundary condition.

---

### CHECK 7 — FILES TOUCHED

**Verdict: PASS**

§6 (Files Touched) explicitly lists:
- `src/PropTraderTools/CopyEngine.cs` — MODIFIED (3 surgical changes)
- `src/PropTraderTools/TradeCopierPanel.cs` — **NOT TOUCHED** (OnLoaded wiring correct as of e06bce7b)

"Maximum 1 file modified: CONFIRMED." stated explicitly. No other files appear anywhere in the plan as modification targets.

---

## Summary Verdict

| Check | Result |
|---|---|
| CHECK 1 — Minimal Fix (exactly 3 changes, 1 file) | **PASS** |
| CHECK 2 — No Scope Creep | **PASS** |
| CHECK 3 — Zero P0 JS Violations | **PASS** |
| CHECK 4 — Root Cause Accuracy | **PASS** |
| CHECK 5 — CYC Impact Numbers | **PASS** |
| CHECK 6 — Test Plan (5 xUnit [Fact] tests) | **PASS** |
| CHECK 7 — Files Touched (CopyEngine.cs only) | **PASS** |

---

## REVIEW_PASS
