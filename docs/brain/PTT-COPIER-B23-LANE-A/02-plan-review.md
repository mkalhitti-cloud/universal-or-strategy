# PTT-COPIER-B23-LANE-A — Plan Review
# Block: PTT-COPIER-B23 | Lane: A | Phase: Plan Review
# Reviewer: ptt-plan-reviewer
# Date: 2026-07-16

---

## Review Checklist

### C1 — Fix direction: Dispatcher.InvokeAsync in BOTH prose and code block?
**PASS**
- §2 prose (line 44): `"NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync()"` — exact match.
- §2 "After" code block (line 84): `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() => {` — exact match.
- Prose and code block are now fully consistent. The re-run prose fix is confirmed.

### C2 — No async void (JS-033): No `async void` in plan design?
**PASS**
- The lambda passed to `InvokeAsync` is a synchronous `Action`, not an `async void` delegate.
- §2 JS Compliance (line 119): "InvokeAsync returns Task, not void; we do not await".
- No `async void` keyword appears anywhere in the plan design.
- Rule JS-033 satisfied.

### C3 — No await on InvokeAsync: Plan explicitly forbids await on InvokeAsync?
**PASS**
- §2 JS Compliance (line 119): "we do not await (fire-and-forget is correct for order submission)".
- The "After" code block contains no `await` keyword before `InvokeAsync`.
- Fire-and-forget dispatch is the correct NT8 AddOn pattern for UI-thread marshaling.

### C4 — CYC remains 5: SendCopy CYC = 5 after change?
**PASS**
- §2 CYC Impact (line 115): "SendCopy CYC: 5 → 5 (no new branches; InvokeAsync lambda is not a branch)".
- The wrapping lambda adds no conditional branches to the method's control flow.
- CYC <= 8 constraint satisfied with margin.

### C5 — Write-set minimal: Only CopyEngine.cs + CopyEngineTests.cs?
**PASS**
- §3 write-set table lists exactly two files: `CopyEngine.cs` and `CopyEngineTests.cs`.
- Line 136 explicitly forbids touching `TradeCopierPanel.cs`, `TradeCopierWindow.cs`,
  `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`, and any `.md` files.
- Write-set is minimal and correct.

### C6 — JS-021: No new lock()?
**PASS**
- §2 JS Compliance (line 118): "JS-021: no lock() added — Dispatcher.InvokeAsync is fire-and-forget async marshal".
- No `lock()` keyword appears in the plan design or code blocks.
- Rule JS-021 satisfied.

### C7 — JS-002: No new return null?
**PASS**
- The "After" code block returns `true` (line 101) or `false` (line 106) — boolean values, not null references.
- No `return null;` appears anywhere in the plan design.
- Rule JS-002 satisfied.

### C8 — NT8 compiler: No NT8-P0 violations (NT8-001/002/003)?
**PASS**
- NT8-001 (`{ get; init; }`): not present in plan design.
- NT8-002 (`abstract record` / `sealed record`): not present.
- NT8-003 (`volatile double`): not present.
- `Dispatcher.InvokeAsync(() => { ... })` is a standard NT8 AddOn API call; no restricted
  language features are introduced.
- No NT8-P0 compiler violations detected.

### C9 — Traceability: Plan references DW-B22-NULLREF-01?
**PASS**
- File header (line 4): `Defect: DW-B22-NULLREF-01 (P0)`.
- §1 "Defect ID" section (line 13): `` `DW-B22-NULLREF-01` (P0) ``.
- Full symptom, root cause, and evidence documented in §1.
- Traceability to the originating defect is complete.

---

## Verdict

REVIEW_PASS
