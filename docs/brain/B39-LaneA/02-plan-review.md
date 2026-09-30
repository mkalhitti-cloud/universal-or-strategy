# B39-LaneA Plan Review
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan reviewed**: `docs/brain/B39-LaneA/02-architecture-plan.md`
**Spec**: `specs/002-trade-copier-spec.html` id="section-b39" lines 17297-17869
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`
**Result**: **REVIEW_FAIL**

---

## Violations Summary

| # | Rule ID | Severity | Description | Plan Location |
|---|---------|----------|-------------|--------------|
| V1 | JS-008 | P1 AUTO-FAIL | SolidColorBrush created without `.Freeze()` — 3 instances | §5.2, §6.2, §6.5 |
| V2 | SPEC-CYC-01 | Advisory | `Execute()` CYC=5 exceeds spec target of 3-4 | §3.4 (line ~92-107) |
| V3 | SPEC-CYC-02 | Advisory | `ExecuteOne()` CYC=4 exceeds spec target of 2 | §3.5 (line ~115-130) |

---

## V1 — JS-008: SolidColorBrush Not Freeze()d (P1 AUTO-FAIL)

**Rule**: JS-008 — Immutability. SolidColorBrush not `.Freeze()`d = FAIL.
**Severity**: P1 — auto-FAIL per DNA block.

**Locations in plan**:

**§5.2 (TradeCopierPanel.cs — BE ALL cluster, lines ~246-249)**:
```csharp
_globalBeBtn2 = new Button
{
    BorderBrush = new SolidColorBrush(Color.FromRgb(0xa8, 0x55, 0xf7)),  // NOT Frozen
    Foreground  = new SolidColorBrush(Color.FromRgb(0xa8, 0x55, 0xf7)),  // NOT Frozen
```

**§6.2 (TradeCopierWindow.cs — window BE ALL button, lines ~392-394)**:
```csharp
_windowGlobalBeBtn = new Button
{
    BorderBrush = new SolidColorBrush(Color.FromRgb(0xa8, 0x55, 0xf7)),  // NOT Frozen
    Foreground  = new SolidColorBrush(Color.FromRgb(0xa8, 0x55, 0xf7)),  // NOT Frozen
```

**§6.5 (TradeCopierWindow.cs — WBrushFlash static field)**:
```csharp
private static readonly SolidColorBrush WBrushFlash =
    new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));  // NOT Frozen
```

**Required fix**: Every `new SolidColorBrush(...)` that is assigned to a property or stored in a field must have `.Freeze()` called immediately after construction. Pattern:
```csharp
var brush = new SolidColorBrush(Color.FromRgb(0xa8, 0x55, 0xf7));
brush.Freeze();
```
Or inline for field initializers:
```csharp
private static readonly SolidColorBrush WBrushFlash =
    (SolidColorBrush)new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)).Also(b => b.Freeze());
```
The simplest NT8-compatible pattern is to freeze after creation in the UI method before assigning.

---

## V2 — SPEC-CYC-01: Execute() CYC=5 Exceeds Spec Target (Advisory)

**Not an auto-FAIL** (CYC=5 is within the absolute ≤8 budget), but the spec states:
- Paste prompt (line 17804): `CYC: Execute=3`
- HTML comment (line 17509): `CYC target: Execute()=4`
- Review checklist: `Execute() ≤ 4 (spec says 3-4)`

**Plan §3.4** documents CYC=5:
```
// CYC = 1 + foreach(1) + foreach(1) + if(1) + null-check-or(1) = 5.
```

The plan's CYC count is correct. The `||` in `pos == null || pos.Quantity == 0` is a decision point that adds 1. This is not a rules violation (CYC=5 ≤ 8 passes) but deviates from the spec's stated target. The plan declares this "Well within <= 8 budget. ✅" without addressing the spec target mismatch.

**To resolve**: Either (a) restructure Execute() to bring CYC to ≤4 (e.g. extract null-check into a helper `IsFlat(pos)` that removes the `||` from Execute's cyclomatic path), or (b) update §3.4 to explicitly acknowledge the spec target mismatch and document the trade-off.

---

## V3 — SPEC-CYC-02: ExecuteOne() CYC=4 Exceeds Spec Target (Advisory)

**Not an auto-FAIL** (CYC=4 ≤ 8 passes), but the spec states:
- Paste prompt (line 17804): `CYC: ExecuteOne=2`
- Review checklist: `ExecuteOne() ≤ 3 (spec says 2)`

**Plan §3.5** first states CYC=3 in the comment (line ~115), then corrects itself at line ~129:
```
// CYC = 3 (1 base + 1 if + 1 `||`; ternary = +1 decision point => 4 total)
```

CYC=4 is the correct count and it exceeds the spec's stated target of 2. The spec's own code snippet at line 17528 shows `// CYC=2: direction branch only` — meaning the spec envisioned no re-check null guard inside ExecuteOne. The plan adds a defensive re-check guard (`if (pos == null || pos.Quantity == 0) return;`) which adds 2 CYC points.

**To resolve**: Either (a) remove the defensive re-check (trust Execute() caller, reduce to CYC=2 per spec), or (b) acknowledge the spec target mismatch and document the trade-off.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| New file `PttGlobalBreakEven.cs` | YES | §3 |
| `Execute(bufferTicks)` method | YES | §3.4 |
| `ExecuteOne()` private | YES | §3.5 |
| `IncrementBuffer()` / `DecrementBuffer()` | YES | §3.6 |
| `GlobalBeBuffer` property | YES | §3.6 |
| `volatile int _globalBeBuffer = 0` | YES | §3.2 |
| `CopyEngine.GlobalBe` property (Option A) | YES | §4.3 |
| `SubmitBeStop` private→internal | YES (correctly inferred) | §4.2 |
| Build tag update | YES | §4.1, §15 |
| Panel Row 2: Cancel→BE ALL + ▲▼ | YES | §5.2 |
| Panel Row 3: UniformGrid Cancel + COPY | YES | §5.3 |
| Green flash 500ms (no async void) | YES | §5.4 |
| `FormatGlobalBeBuffer` 0/+/- formatting | YES | §5.5 |
| Window global toolbar row | YES | §6.2 |
| Window handlers + format helper | YES | §6.3, §6.4 |
| 6 xUnit [Fact] tests T_B39_01..06 | YES | §10 |
| 7-scan checklist | YES | §11 |
| Deferred items documented | YES | §13 |
| `GlobalBeBufferChanged` event (spec recommendation) | DEFERRED | §13, §7 |
| Buffer clamps ±10 on both IncrementBuffer/DecrementBuffer | YES (plan adds upper clamp missing from spec code) | §3.6 |

**Note — `GlobalBeBufferChanged` event**: The spec (line 17774) says "Fire a `GlobalBeBufferChanged` event on CopyEngine whenever buffer changes — both surfaces subscribe and update their label." The plan explicitly defers this to §13 with rationale: "best-effort; acceptable per §7." Since the spec says "Recommended" (not "Required"), and the plan provides a clear rationale, this deferral is acceptable. The plan does NOT lose the buffer sync — each surface updates its own label in its own handler. The deferred item is visual sync only (Window label doesn't auto-update when Panel spinner is used). This is documented.

---

## JS Rules Compliance Findings

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock() | No lock() in any new code — `_globalBeBuffer` is volatile int, UI-thread only | PASS |
| JS-023 volatile int OK | `private volatile int _globalBeBuffer` — compliant | PASS |
| JS-023 volatile double BANNED | No volatile double in new code | PASS |
| JS-002 no return null | Uses `return` (void), `continue`; no `return null` | PASS |
| JS-033 no async void | All handlers are `private void` synchronous — DispatcherTimer used for flash | PASS |
| NT8-003 no volatile double | No volatile double | PASS |
| NT8-001 no `{ get; init; }` | `GlobalBe { get; }` getter-only — compliant | PASS |
| **JS-008 SolidColorBrush Freeze()** | **3 instances created without `.Freeze()` — §5.2, §6.2, §6.5** | **FAIL** |
| SCAN-04 no FontFamily strings | No FontFamily usage | PASS |
| DateTime.Now banned | Not used in new code | PASS |
| Hardcoded #RRGGBB hex | Colors use `Color.FromRgb()` API, not string literals | PASS |
| CreateOrder PTT- prefix | `SubmitBeStop` in CopyEngine already uses PTT- prefix | PASS |

---

## CYC Budget

| Method | Plan CYC | Spec Target | Budget (≤8) | Status |
|--------|----------|-------------|-------------|--------|
| `Execute()` | 5 | 3-4 | ≤8 | Within budget; exceeds spec target |
| `ExecuteOne()` | 4 | 2-3 | ≤8 | Within budget; exceeds spec target |
| `IncrementBuffer()` | 2 | 2 | ≤8 | PASS |
| `DecrementBuffer()` | 2 | 2 | ≤8 | PASS |
| `GlobalBeBuffer` (property) | 1 | 1 | ≤8 | PASS |
| `OnGlobalBeClick` | 3 | ≤8 | ≤8 | PASS |
| `OnGlobalBeUp` | 2 | ≤8 | ≤8 | PASS |
| `OnGlobalBeDown` | 2 | ≤8 | ≤8 | PASS |
| `FormatGlobalBeBuffer` | 3 | ≤8 | ≤8 | PASS |
| `OnWindowGlobalBeClick` | 3 | ≤8 | ≤8 | PASS |
| `FormatWindowGlobalBe` | 3 | ≤8 | ≤8 | PASS |

No method exceeds the absolute CYC ≤8 budget. V2 and V3 are advisory deviations from spec targets only.

---

## Test Coverage Review

| ID | Test Name | Assertions | Status |
|----|-----------|-----------|--------|
| T_B39_01 | `GlobalBe_FiresOnAllAccountsAllInstruments` | 3 accs × 2 instrs → 6 calls | PASS |
| T_B39_02 | `GlobalBe_SkipsFlatAccounts` | flat account → 0 calls | PASS |
| T_B39_03 | `GlobalBe_WorksWithNoCopyRule` | zero rules → still fires | PASS |
| T_B39_04 | `GlobalBe_B35GuardInherited_UnderwaterSkipped` | underwater → warning, no exception | PASS |
| T_B39_05 | `GlobalBe_BufferAppliedPerDirectionCorrectly` | +2 long→7500.50, +2 short→7499.50 | PASS |
| T_B39_06 | `GlobalBe_AllAccountsFlat_NoCalls` | all flat → 0 calls, no exception | PASS |

**Note — test seam ambiguity**: §9 acknowledges that `Account.All` is an NT8 static collection that may not be injectable. The plan defers the seam decision to the engineer ("Engineer must verify which seam is compatible"). This is acceptable for a plan document (the seam choice is an implementation concern), but the plan's T_B39_01 through T_B39_06 descriptions assume the seam works. If the engineer cannot inject `Account.All`, T_B39_01/02/06 would need an `IEnumerable<Account>` overload per §9's recommended alternative. The plan should make this decision in the plan, not defer it.

---

## Architecture Coherence

| Check | Result |
|-------|--------|
| Panel uses `CopyEngine.Instance.GlobalBe` (not own field) | PASS — §5.2/5.4 all calls use `CopyEngine.Instance.GlobalBe` |
| Window uses `CopyEngine.Instance.GlobalBe` (not own field) | PASS — §6.2/6.3 all calls use `CopyEngine.Instance.GlobalBe` |
| No circular dependency | PASS — dependency is `PttGlobalBreakEven` → `CopyEngine.Instance.SubmitBeStop` (call-time only, not constructor-time); lambda is captured, not executed during CopyEngine initialization |
| Testability seam specified | PARTIAL — injection constructor specified (§3.3) but `Account.All` seam left unresolved (§9) |
| `InternalsVisibleTo` noted | PASS — §9 notes requirement and defers to engineer |

---

## 7-Scan Checklist

Plan §11 lists all 7 scans with commands and zero-hit requirements. PASS.

| Scan | Command | Required |
|------|---------|---------|
| SCAN-01 | `grep -r "lock("` | 0 matches |
| SCAN-02 | `grep -r "async void"` | 0 matches |
| SCAN-03 | `grep -r "return null"` | 0 matches |
| SCAN-04 | `grep -r "throw new"` | 0 matches |
| SCAN-05 | `complexity_audit.py` | CYC ≤ 8 |
| SCAN-06 | `dotnet build` | 0 errors |
| SCAN-07 | `dotnet test` | all pass, ≥186 |

---

## Section §13 Deferred Items

Plan §13 documents 6 deferred items with reasons. All are explicitly out-of-scope with rationale. PASS.

| Item | Disposition |
|------|------------|
| Keyboard shortcut (Shift+G) | Spec explicitly defers — OK |
| `PttBus.GlobalBeFired` pub-sub event | Not needed in B39 — OK |
| Armed state | Spec says "fires immediately, no armed state" — correct |
| Visual buffer sync between Panel and Window | Best-effort, spec recommendation deferred — documented |
| Independent BE buffer per rule-row | Out of scope — OK |
| `[assembly: InternalsVisibleTo(...)]` | Engineer must verify — OK |

---

## Required Fixes Before REVIEW_PASS

The plan must be updated to address the following before REVIEW_PASS can be granted:

### Fix 1 (BLOCKING — JS-008)
Add `.Freeze()` calls to all `SolidColorBrush` instances in the plan code:
- §5.2: `BorderBrush` and `Foreground` brush construction
- §6.2: `BorderBrush` and `Foreground` brush construction
- §6.5: `WBrushFlash` static field

### Fix 2 (Recommended — clarify spec CYC targets)
§3.4 and §3.5: Either restructure to meet spec CYC targets or explicitly document the deviation with rationale. The current plan inconsistently claims both "CYC=3" and "CYC=4" for ExecuteOne in the same section.

### Fix 3 (Recommended — resolve Account.All test seam)
§9: Commit to the `IEnumerable<Account>` overload or confirm existing test infrastructure handles it. Do not leave the seam choice open for the engineer — the plan must specify the test interface.

---

## Verdict

**REVIEW_FAIL**

**Blocking violation**: JS-008 (P1 auto-FAIL) — SolidColorBrush instances in §5.2, §6.2, and §6.5 are not Freeze()d. All three are assigned to WPF dependency properties or stored in static readonly fields. Per the DNA block: "SolidColorBrush not Freeze()d = FAIL (JS-008)."

Non-blocking advisory items (V2, V3) do not independently trigger REVIEW_FAIL but should be resolved to align with spec CYC targets before ticket generation.

Return plan to ptt-architect for fix. Re-review required after corrections.

---

## Re-Review — Phase 2 Rev 2 (2026-07-30)

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan revision reviewed**: Rev 2 — `<!-- Rev 2 — JS-008 fix applied per plan review (2026-07-30) -->`
**Trigger**: REVIEW_FAIL from Rev 1 (V1 JS-008 — 3 unfrozen SolidColorBrush instances)

### JS-008 Fix Verification

All three locations cited in the Rev 1 REVIEW_FAIL have been corrected:

| Location (Rev 1) | Rev 1 Code | Rev 2 Code | Fixed? |
|---|---|---|---|
| §5.2 `_globalBeBtn2` BorderBrush/Foreground | `new SolidColorBrush(Color.FromRgb(0xa8,0x55,0xf7))` (inline, not frozen) | `BrushPurple` — `static readonly` field via `MakeBrush(168,85,247)` (Freeze()d internally) | ✅ YES |
| §6.2 `_windowGlobalBeBtn` BorderBrush/Foreground | `new SolidColorBrush(Color.FromRgb(0xa8,0x55,0xf7))` (inline, not frozen) | `WBrushPurple` — `static readonly` field via `MakeWinBrush(168,85,247)` (Freeze()d internally) | ✅ YES |
| §6.5 `WBrushFlash` static field | `new SolidColorBrush(Color.FromRgb(0x22,0xc5,0x5e))` (not frozen) | `WBrushFlash` — `static readonly` field via `MakeWinBrush(34,197,94)` (Freeze()d internally) | ✅ YES |

No `new SolidColorBrush(...)` appears anywhere in button-creation code in Rev 2. The only `new SolidColorBrush` reference is inside a comment block in §6.5 explicitly labelled `// BANNED`.

### Advisory Items V2 / V3 — Resolution Confirmed

- **V2 (Execute CYC=5)**: Rev 2 §3.4 now explicitly documents the deviation from spec target (3-4) with rationale ("defensive `pos == null` guard required for NT8 sim compatibility; CYC=5 ≤ 8 absolute budget, accepted"). Advisory resolved — no auto-FAIL.
- **V3 (ExecuteOne CYC=4)**: Rev 2 §3.5 now explicitly acknowledges spec target of 2, labels it "Advisory V3 from plan review", and documents the trade-off. Advisory resolved — no auto-FAIL.

### Full Re-Check — All Other Rules

| Rule | Status |
|------|--------|
| JS-021 no lock() | PASS |
| JS-023 volatile int OK | PASS |
| JS-023/NT8-003 no volatile double | PASS |
| JS-002 no return null | PASS |
| JS-033 no async void | PASS |
| NT8-001 no `{ get; init; }` | PASS |
| NT8-007 CreateOrder | PASS (not called) |
| CYC ≤ 8 all methods (max Execute=5) | PASS |
| ASCII-only identifiers | PASS |
| No FontFamily strings | PASS |
| No DateTime.Now | PASS |
| PTT- order prefix | PASS |
| No hardcoded #RRGGBB hex | PASS |
| 7-scan checklist present | PASS |
| Spec coverage complete | PASS |
| **JS-008 SolidColorBrush Freeze()** | **PASS** (all 3 violations cleared) |

No new violations introduced in Rev 2.

### Re-Review Verdict

**REVIEW_PASS**

All blocking violations from Rev 1 have been cleared. No new violations detected in Rev 2. Plan is approved for Phase 3 (ticket generation).
