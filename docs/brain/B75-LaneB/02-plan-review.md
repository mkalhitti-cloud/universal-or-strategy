# B75-LaneB Plan Review

**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (Plan Gate)  
**Date**: 2026-08-17  
**Plan reviewed**: `docs/brain/B75-LaneB/02-architecture-plan.md`  
**Source cross-checked**:
- `src/PropTraderTools/TradeCopierPanel.cs` (OnLoaded ~616, OnCloneModeClick ~1582, GetLeaderAtmTemplateName ~2218)
- `src/PropTraderTools/CopyEngine.cs` (field ~120, SetCloneAtmObjectCache ~443, GetSavedFollowerNames ~479)

---

## Section A — Spec Completeness

| Requirement | Addressed in Plan? | Plan Section |
|-------------|-------------------|--------------|
| HOTFIX-B66-ATM-TPL: root cause + fix approach + affected method + file | YES | §1 table + §3 |
| HOTFIX-B66-ATM-OBJ (panel-side): root cause + fix approach + affected methods + files | YES | §1 table + §2 |
| HOTFIX-B67-CHECKBOX-RESTORE: root cause + fix approach + affected methods + files | YES | §1 table + §4 |
| Two-cache design documented (string + object, both volatile) | YES | §2 |
| Three-tier fallback for GetLeaderAtmTemplateName documented | YES | §3 fallback table |
| OnLoaded restore sequence (7 steps) documented | YES | §4 step sequence |
| Guard conditions rationale documented | YES | §4 "Guard Conditions Rationale" |
| Data flow diagrams for both fixes | YES | §8 |
| Component list with roles | YES | §9 |

**Verdict: PASS** — all three hotfixes fully specified with root cause, fix approach, affected method, and file.

---

## Section B — JS-DNA P0 Compliance

### B.1 JS-021: No `lock()`

Source grep across all four hotfix methods:

| Method | File | `lock(` found? | Plan claim |
|--------|------|----------------|------------|
| `GetLeaderAtmTemplateName` | TradeCopierPanel.cs:2218 | NO | 0 new — CORRECT |
| `OnCloneModeClick` | TradeCopierPanel.cs:1582 | NO | 0 new — CORRECT |
| `OnLoaded` restore block | TradeCopierPanel.cs:642-654 | NO | 0 new — CORRECT |
| `GetSavedFollowerNames` | CopyEngine.cs:479 | NO | 0 new — CORRECT |
| `SetCloneAtmObjectCache` | CopyEngine.cs:443 | NO | 0 new — CORRECT |

**Verdict: PASS** — no `lock()` anywhere in B75-LaneB hotfix methods (JS-021 satisfied).

### B.2 JS-001: No `throw new` in hotfix methods

Source inspection:

- `GetLeaderAtmTemplateName` (lines 2218-2238): `catch { return string.Empty; }` — no `throw`. PASS.
- `OnCloneModeClick` (lines 1582-1596): no throw. PASS.
- `OnLoaded` restore block (lines 642-654): no throw. PASS.
- `GetSavedFollowerNames` (lines 479-489): no throw. PASS.
- `SetCloneAtmObjectCache` (lines 443-447): no throw. PASS.

**Verdict: PASS** — JS-001 satisfied.

### B.3 JS-002: No `return null` — GetLeaderAtmTemplateName

Source inspection of `GetLeaderAtmTemplateName` (lines 2218-2238):

- Line 2220: `return string.Empty` (null guard)
- Line 2224: `return string.Empty` (null guard)
- Line 2228: `return ct.AtmStrategy.Name ?? string.Empty` (primary path)
- Line 2232: `return sel.SelectedAtmStrategy.Name ?? string.Empty` (fallback-1)
- Line 2235: `return atmCb?.SelectedItem as string ?? string.Empty` (fallback-2)
- Line 2237: `catch { return string.Empty; }` (exception path)

Every code path returns `string.Empty` or a non-null string. No `return null`.

`GetSavedFollowerNames` returns `new HashSet<string>()` on empty — not null.

**Verdict: PASS** — JS-002 satisfied. Plan correctly documents "Never returns null" contract.

### B.4 JS-033: No `async void`

- `OnLoaded` (line 616): `private void OnLoaded(...)` — synchronous. PASS.
- `OnCloneModeClick` (line 1582): `private void OnCloneModeClick(...)` — synchronous. PASS.
- `GetLeaderAtmTemplateName` (line 2218): `internal static string ...` — synchronous. PASS.
- `GetSavedFollowerNames` (line 479): `internal HashSet<string> ...` — synchronous. PASS.
- `SetCloneAtmObjectCache` (line 443): `internal void ...` — synchronous. PASS.

**Verdict: PASS** — JS-033 satisfied.

### B.5 ASCII / Non-ASCII Source Characters

The plan (§5, "Pre-existing ASCII Arrows") claims the Unicode arrows at lines 1044-1107 are pre-existing
B73-LaneB code, not introduced by B75-LaneB.

**Source findings**:

1. Lines 984–1127: The "arrows" are `\u25B2` / `\u25BC` C# Unicode escape sequences. These are six-ASCII-character escape sequences in source text — the source file bytes are ASCII-safe. No literal non-ASCII bytes exist at these locations.
2. Line 1177: A literal em dash (`—`) exists in a comment (`MES tick = $1.25, MGC tick = $0.10, MCL tick = $0.01 — storing raw ticks`). This is a pre-existing non-ASCII character in a comment, not in any B75-LaneB hotfix method. B75-LaneB does not touch `FormatQuickAllBuffer`.

**Assessment**: The plan's description of "Unicode arrow characters at lines 1044-1107" is slightly imprecise — they are ASCII-safe escape sequences, not raw non-ASCII bytes. The plan does correctly conclude these are not B75-LaneB introductions. The actual pre-existing literal non-ASCII byte (em dash, line 1177) is also outside B75-LaneB scope and the plan's conclusion (P0 ASCII scan for B75-LaneB is unaffected) is correct.

**Verdict: PASS** — no non-ASCII characters introduced by B75-LaneB hotfix methods. Plan's ASCII assessment is substantively correct.

---

## Section C — CYC Assessment

### C.1 GetLeaderAtmTemplateName

**Plan claims**: CYC = 5 (or 4 not counting catch).

**Source count** (lines 2218-2238):
1. `if (currentChart == null) return` — Branch 1
2. `if (ct == null) return` — Branch 2
3. `if (ct.AtmStrategy != null)` — Branch 3
4. `if (sel?.SelectedAtmStrategy != null)` — Branch 4
5. `catch {}` — Branch 5

CYC = 5. Below limit of 8.

**Verdict: PASS** — plan CYC claim correct. PASS.

### C.2 OnCloneModeClick

**Plan claims**: CYC = 2.

**Source count** (lines 1582-1596):
1. `if (_currentChart != null)` — Branch 1
Base: 1

CYC = 2. Below limit of 8.

**Verdict: PASS** — plan CYC claim correct. PASS.

### C.3 OnLoaded restore block (additive CYC)

**Plan claims**: +4 additive branches. Plan section 7 and section 4 both correctly document:
- Branch A: `if (_instrument != null && _leaderAccount != null)` (+1)
- Branch B: `if (saved.Count > 0)` (+1)
- Branch C: `foreach (_followerItems)` (+1)
- Branch D: `if (item.Account != null && saved.Contains(...))` (+1)

**Source** (lines 642-654): Confirms all four branches exist as described.

**NOTE — source comment discrepancy** (informational, not a plan violation):  
The source code comment at line 640 states `// CYC cost: +0 (straight-line, no branch beyond the foreach)`.  
This comment is factually incorrect — the restore block has 4 branches. The plan correctly identifies +4.  
This is a source-comment inaccuracy, NOT introduced by B75-LaneB planning. The plan is correct.  
The engineer who executes the tickets should correct the line-640 comment.

**Verdict: PASS** — plan CYC assessment for OnLoaded is correct (+4). Plan supersedes the incorrect source comment.

### C.4 GetSavedFollowerNames

**Plan claims**: CYC = 4+1 base = 5 (with notation that source says CYC=2 counting only two foreach loops).

**Source count** (lines 479-489):
1. `foreach (var rule in _rules)` — Branch 1
2. `if (rule.Instrument != instrument || ...) continue` — Branch 2 (conditional branch on `continue`)
3. `foreach (var f in rule.FollowerAccounts)` — Branch 3
4. `if (f?.Name != null) result.Add(f.Name)` — Branch 4
Base: 1

CYC = 5. The source comment at line 478 says `CYC=2: foreach rules(1) + foreach followers(2)` — this undercounts by omitting the `if`-continue filter and the inner null-guard branch. The plan's comment "Either counting: well below 8" correctly resolves the discrepancy — regardless of methodology, CYC ≤ 5, well below 8.

**Verdict: PASS** — no CYC violation. Plan handles the counting ambiguity appropriately.

---

## Section D — Test Coverage

**Required test IDs**: T_B66TPL_01..05, T_B66OBJ_P01..P02, T_B67_01..03 (10 total)

| Test ID | Present in Plan? | Description Match? |
|---------|-----------------|-------------------|
| T_B66TPL_01 | YES (§6) | null chart → `string.Empty`, no throw |
| T_B66TPL_02 | YES (§6) | no ChartTrader child → `string.Empty`, no throw |
| T_B66TPL_03 | YES (§6) | primary path: `AtmStrategy.Name = "MES $200 SL6"` → `"MES $200 SL6"` |
| T_B66TPL_04 | YES (§6) | fallback-1 path: `AtmStrategySelector.Name = "ATM1"` → `"ATM1"` |
| T_B66TPL_05 | YES (§6) | all paths null → `string.Empty` (not throw, not null) |
| T_B66OBJ_P01 | YES (§6) | `SetCloneAtmObjectCache(nonNull)` → `GetCloneAtmMode()` returns `Named` with `AtmObject != null` |
| T_B66OBJ_P02 | YES (§6) | `SetCloneAtmObjectCache(null)` → no throw; `GetCloneAtmMode()` returns `Inherit` |
| T_B67_01 | YES (§6) | matching rule → HashSet with both follower names |
| T_B67_02 | YES (§6) | no matching rule → empty HashSet (not null, not throw) |
| T_B67_03 | YES (§6) | OnLoaded restore: matching items `IsSelected=true`, non-matching remain `false` |

All 10 test IDs present. Test descriptions correctly mirror hotfix requirements. Framework
correctly specified as xUnit `[Fact]` per JS-051..065.

**Verdict: PASS** — all 10 required tests documented with correct assertions.

---

## Section E — Rules Catalog Spot-Check

Confirming the plan does NOT propose or implicitly permit:

| Forbidden Pattern | Present in Plan? | Assessment |
|------------------|-----------------|------------|
| `lock()` usage | NO | Plan explicitly prohibits — §2 ("No lock is needed. This satisfies JS-021") |
| New `return null` paths | NO | Plan explicitly states "Never returns null" for all three methods |
| Non-ASCII characters in introduced code | NO | Plan scopes to ASCII-safe; pre-existing em dash at line 1177 is outside hotfix methods |
| `async void` handler | NO | All four handlers are synchronous void |
| `throw new` in hotfix paths | NO | GetLeaderAtmTemplateName uses `catch { return string.Empty; }` — no re-throw |
| `Dictionary<K,V>` for thread-touched state | NO | `ConcurrentBag<CopyRule>` (pre-existing), `HashSet<string>` (local, single-thread) |
| `volatile` misuse (no lock substitution for compound operations) | NO | Both volatile fields are single-writer/single-reader, no compound check-then-act |

**volatile reference semantics verified**: `_cloneAtmObject` at CopyEngine.cs:120 is `volatile NinjaTrader.NinjaScript.AtmStrategy`. Single writer (UI thread via `SetCloneAtmObjectCache`), single reader (dispatch thread via `GetCloneAtmMode`). No compound operation — only plain write and plain read. CLR 4.0+ guarantees atomicity of reference reads/writes. The plan's JS-021 compliance argument (§2) is correct.

**Verdict: PASS** — no forbidden patterns proposed or implicitly permitted.

---

## Violations Summary

| Rule ID | Description | Location in Plan | Finding |
|---------|-------------|-----------------|---------|
| (none) | — | — | Zero violations found |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| B66-ATM-TPL root cause documented | YES | §1, §3 |
| B66-ATM-TPL fix approach documented | YES | §3 fallback table |
| B66-ATM-OBJ root cause documented | YES | §1, §2 |
| B66-ATM-OBJ fix approach (object overload) documented | YES | §2 "Why object wins over string" |
| B66-ATM-OBJ two-cache design (string + object) documented | YES | §2 fields block |
| B67-CHECKBOX-RESTORE root cause documented | YES | §1 |
| B67-CHECKBOX-RESTORE fix approach documented | YES | §4 |
| OnLoaded 7-step sequence documented | YES | §4 step sequence |
| Three-tier fallback for GetLeaderAtmTemplateName | YES | §3 |
| P0 gate for all hotfix methods | YES | §5 |
| CYC pre-check for all hotfix methods | YES | §7 |
| 10 test IDs with assertions | YES | §6 |
| Component list with files and roles | YES | §9 |
| Data flow diagrams | YES | §8 |

---

## Informational Notes (Non-Blocking)

1. **Source comment at line 640 is incorrect** (`CYC cost: +0`). The restore block has 4 branches. The plan correctly identifies +4. The engineer executing the ticket should update the source comment to match the plan.
2. **Source comment at line 478 undercounts CYC** (`CYC=2`). Actual CYC is 5. Non-blocking — plan correctly notes the discrepancy and correctly concludes below-8.
3. **Plan's "lines 1044-1107" range** for pre-existing arrows is slightly narrow — `\u25B2`/`\u25BC` escape sequences appear from lines 984 to 2558 throughout the file. These are all ASCII-safe escape sequences. The plan's conclusion (not introduced by B75-LaneB, not a P0 scan concern) is correct.

---

## Overall Gate

**All five sections PASS. Zero rule violations found. Spec fully covered.**

**REVIEW_PASS**
