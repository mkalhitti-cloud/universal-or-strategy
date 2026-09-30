# Final Review — DW-B161B166-01 (VIOLATION-01-FIX)

**Reviewer:** ptt-plan-reviewer (Phase 5)
**Date:** 2026-09-15
**Epic:** DW-B161B166-01
**File reviewed:** `src/PropTraderTools/CopyEngine.cs`

**Inputs read:**
- `docs/brain/DW-B161B166-01/02-architecture-plan.md`
- `docs/brain/DW-B161B166-01/04-ticket-review.md`
- `docs/brain/DW-B161B166-01/ticket-1-completion.md`
- `docs/brain/DW-B161B166-01/ticket-1-verification.md`
- `src/PropTraderTools/CopyEngine.cs` (L3155–3260, plus targeted grep scans)
- `docs/brain/B161-B166/06-deferred-backlog.md`

---

## (a) VIOLATION-01 Resolved — `IsFollowerPositionBlocked` CYC = 8

**Required:** CYC was 9 (JS-CYC violation). Fix must restore to 8.

Independent branch count from live source (L3213–L3256):

| # | Branch | Line |
|---|--------|------|
| 1 | base | — |
| 2 | `if (acc == null)` | L3215 |
| 3 | `if (instr == null)` | L3216 |
| 4 | `if (HasOpenPosition(…))` | L3217 |
| 5 | `if (HasActivePttBeExits(…))` | L3226 |
| 6 | `if (HasSettlingBeTargets(…))` | L3235 |
| 7 | `if (HasActivePttBeTargets(…))` | L3244 |
| 8 | `if (IsBeSettleOrRetrySignalActive(…))` | L3253 |

No `&&`, `||`, `for`, `while`, or `case` in parent body. **CYC = 8.**

Verifier (Phase 4b) independently confirmed CYC = 8 via live source read (check c, L3213–3256).
Engineer (Phase 4a) confirmed CYC = 8 via code inspection (completion §c).

**CHECK (a): PASS — JS-CYC violation resolved. CYC 9 → 8. ✓**

---

## (b) No New Violations Introduced

### `IsBeSettleOrRetrySignalActive` (L3165–L3187)

| Rule | Check | Evidence | Result |
|------|-------|----------|--------|
| JS-001 | No `throw` in new helper | Not present in L3165–3187; returns `false`/`true` only | PASS |
| JS-002 | No `null` return from non-void | Returns `bool`; base case is `return false;` at L3186 | PASS |
| JS-021 | No `lock()` | SCAN-01: all 14 `lock(` hits file-wide are in comments only | PASS |
| JS-042 | ASCII-only string literals | SCAN-02: 0 non-ASCII chars file-wide | PASS |
| JS-CYC | CYC ≤ 8 | CYC = 5 (base+if+\|\|+IsBeSlotNonTerminal+IsBeRetryArmedFlat) | PASS |
| JS-008 | No mutable struct fields | No structs introduced | PASS |
| JS-009 | No `Dictionary<K,V>` for shared state | No dictionary introduced | PASS |
| NT8: async/await | No async in lifecycle method | Not present | PASS |
| NT8: DateTime.Now | SCAN-06 | All hits in comments only | PASS |
| NT8: FontFamily | SCAN-03 | All 3 hits in comments only | PASS |
| NT8: hex color | SCAN-04 | 0 hits file-wide | PASS |
| NT8: CreateOrder prefix | SCAN-05 | Not present in new helper | PASS |
| NT8: new NT8 API surface | All API calls carried verbatim from extracted blocks | Zero new NT8 surface | PASS |

No new fields, properties, or class members beyond the helper method. ✓
No `Dictionary<K,V>`. Only `ConcurrentDictionary` reads (pre-existing, via `IsBeSlotNonTerminal` and `IsBeRetryArmedFlat`). ✓

**CHECK (b): PASS — No new violations. ✓**

---

## (c) Coherent System — Call Sites Verified

All grep results from live source (`src/PropTraderTools/CopyEngine.cs`):

### `IsBeSettleOrRetrySignalActive` — 3 hits total

| Line | Context |
|------|---------|
| L3165 | Definition (new helper) |
| L3208 | CYC comment inside `IsFollowerPositionBlocked` doc block |
| L3253 | Only executable call — inside `IsFollowerPositionBlocked` body |

**Called only from `IsFollowerPositionBlocked`. No other callers. ✓**

### `IsBeSlotNonTerminal` — 3 hits total

| Line | Context |
|------|---------|
| L3162 | Comment inside new helper doc block |
| L3168 | Executable call — inside `IsBeSettleOrRetrySignalActive` (L3165–3187) |
| L9397 | Definition |

No executable call inside `IsFollowerPositionBlocked` body (L3213–3256). ✓
**Called only from `IsBeSettleOrRetrySignalActive` within the context of this change. ✓**

### `IsBeRetryArmedFlat` — 8 hits total

| Line | Context |
|------|---------|
| L3162 | Comment (helper doc block) |
| L3164 | Comment (helper doc block) |
| L3177 | Executable call — inside `IsBeSettleOrRetrySignalActive` (L3165–3187) |
| L3198 | Comment (parent `IsFollowerPositionBlocked` doc block) |
| L3205 | Comment (parent `IsFollowerPositionBlocked` doc block) |
| L3268 | Definition |
| L7226 | Comment (unrelated — `TryArmBePriceRetry` area) |
| L7258 | Comment (unrelated — `RegisterBeRetrySlotIfNeeded` area) |

No executable call inside `IsFollowerPositionBlocked` body (L3213–3256). ✓
**Called only from `IsBeSettleOrRetrySignalActive` (one executable call site). ✓**

### `IsFollowerPositionBlocked` call sites (unchanged)

| Site | File | Line | Status |
|------|------|------|--------|
| `TryReplaceOnAtmCancel` | CopyEngine.cs | L957 | Unchanged — signature unmodified ✓ |
| `ShouldSkipFollower` | CopyEngine.cs | L3285 | Unchanged — signature unmodified ✓ |

**CHECK (c): PASS — System coherent. Call sites correct. No inadvertent callers. ✓**

---

## (d) All 7 Scans Zero

Verifier (Phase 4b) ran all 7 scans independently against live source:

| Scan | Rule | Command | Result |
|------|------|---------|--------|
| SCAN-01 | JS-021 `lock(` | `Select-String -Pattern "lock\("` | 14 hits — ALL in comments. Zero executable. **PASS** |
| SCAN-02 | JS-042 non-ASCII | `Get-Content \| Where-Object { $_ -match '[^\x00-\x7F]' }` | 0 hits file-wide. **PASS** |
| SCAN-03 | NT8-001 FontFamily | `Select-String -Pattern "FontFamily"` | 3 hits — ALL in comments. **PASS** |
| SCAN-04 | NT8-001 hex color | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | 0 hits. **PASS** |
| SCAN-05 | NT8-001 CreateOrder | Not applicable — no CreateOrder in new helper. | N/A / **PASS** |
| SCAN-06 | NT8-001 DateTime.Now | `Select-String -Pattern "DateTime\.Now[^U]"` | All hits in comments. **PASS** |
| SCAN-07 | GREP-LOCK | `Select-String -Pattern "\block\s*\("` | All hits in comments. Zero executable. **PASS** |

Engineer's Layer 2 scan results (completion §f) agree with verifier's independent Layer 3 results on all 7 scans.

**CHECK (d): PASS — All 7 scans zero (no executable violations). ✓**

---

## (e) All Spec Requirements Satisfied

| Requirement | Expected | Evidence | Status |
|-------------|---------|----------|--------|
| CYC budget parent = 8 | ≤ 8 | Live source count = 8; verifier check (c) = 8 | ✓ PASS |
| CYC budget helper = 5 | ≤ 8 | Verifier check (d) = 5; engineer check (d) = 5 | ✓ PASS |
| Log string Signal 5 byte-for-byte preserved | Exact match | Verifier check (e): L3171–3172 identical to original | ✓ PASS |
| Log string Signal 6 byte-for-byte preserved | Exact match | Verifier check (e): L3180–3181 identical to original | ✓ PASS |
| Signal evaluation order: 5 before 6 | L(Signal5) < L(Signal6) | L3168 (Signal 5) < L3177 (Signal 6) | ✓ PASS |
| Pure extraction — no logic change | Semantics unchanged | Same two if-blocks moved verbatim; verifier check (g) confirms diff minimal | ✓ PASS |
| Stale CYC comment corrected | Old text absent; new text present | Verifier check (f): old text absent, L3207–3209 contains new 3-line comment with `IsBeSettleOrRetrySignalActive(8)`, `DW-B161B166-01`, "Signals 5+6 delegated." | ✓ PASS |
| Null guard before `acc.Name` access | `if (acc == null \|\| instr == null) return false;` first | Verifier check (a): L3167 is first statement | ✓ PASS |
| ptt-sync 0 errors | 0 errors | Completion §e: 18 linked, 0 errors | ✓ PASS |
| Single file, single class | SINGLE-PIPELINE | Only `CopyEngine.cs` modified; ticket-reviewer §f confirmed | ✓ PASS |

**CHECK (e): PASS — All spec requirements satisfied. ✓**

---

## (f) Deferred Backlog Update

`docs/brain/B161-B166/06-deferred-backlog.md` — DW-B161B166-01 entry:

- **Status updated:** OPEN → CLOSED ✓
- **Closed date added:** 2026-09-15 ✓
- **Closed by added:** "Resolved by DW-B161B166-01 pipeline (ptt-plan-reviewer Phase 5 Final Review)" ✓
- **Summary table updated:** OPEN → CLOSED (2026-09-15) ✓
- **No other entries modified:** DW-B161B166-02 (OPEN), DW-B161B166-03 (OPEN), DW-B161B166-04 (CLOSED) — untouched ✓

**CHECK (f): PASS — Backlog entry correctly closed. ✓**

---

## Section K — Deferred Work

Section K: No new deferred items. DW-B161B166-01 is fully resolved.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B161B166-01 | VIOLATION-01-FIX: Extract Signals 5+6 into `IsBeSettleOrRetrySignalActive` to restore `IsFollowerPositionBlocked` CYC from 9 to 8 | P1 | DW-B161B166-01 | CLOSED (2026-09-15) |
| DW-B161B166-02 | CONCERN-02: Rename `IsPttBeStopActiveState` to `IsPttBeOrderActiveState` | P3 | future | OPEN |
| DW-B161B166-03 | B162-B doc note: Add asymmetry comment to `TryTriggerBeRecovery` at L5193 | P3 | future | OPEN |
| DW-B161B166-04 | B161 original SIM gate superseded by combined B166 SIM gate | P3 | — | CLOSED |

This pipeline introduced no new deferred items. DW-B161B166-02 and DW-B161B166-03 are pre-existing P3 cosmetic items carried from the B161-B166 block; they are out of scope for this epic and remain OPEN.

---

## Final Summary

| Check | Result |
|-------|--------|
| (a) VIOLATION-01 resolved — `IsFollowerPositionBlocked` CYC = 8 | ✓ PASS |
| (b) No new violations introduced | ✓ PASS |
| (c) Coherent system — call sites correct, no inadvertent callers | ✓ PASS |
| (d) All 7 scans zero (executable violations) | ✓ PASS |
| (e) All spec requirements satisfied | ✓ PASS |
| (f) Deferred backlog DW-B161B166-01 OPEN → CLOSED | ✓ PASS |
| Section K present | ✓ PRESENT |

---

PIPELINE_COMPLETE
