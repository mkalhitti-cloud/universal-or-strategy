# B52-LaneB Plan Review
**Epic**: PTT-COPIER-B52 Lane B (knowledge-doc-weak-refs)
**Reviewer**: ptt-plan-reviewer
**Plan file**: docs/brain/B52-LaneB/02-architecture-plan.md
**Result**: REVIEW_FAIL

---

## Check 1 — DW-B50C-02 Documentation Accuracy
**Result: PASS**

| Sub-item | Expected | Actual in Plan | Pass? |
|---|---|---|---|
| CS0433 error names "Globals" type | Yes | "The type 'Globals' exists in both..." | PASS |
| NinjaTrader.Core.dll named (not Gui.dll) | Core.dll | "NinjaTrader.Core.dll (already referenced) provides Account, Order, Instrument..." | PASS |
| Prohibition against re-adding NinjaTrader.Client.dll | Yes | "Do NOT add NinjaTrader.Client.dll back in future blocks." | PASS |
| B50-LaneC cited as block of removal | B50-LaneC | "done in B50-LaneC to resolve CS0433 Globals ambiguity" | PASS |
| No invented unique types for NinjaTrader.Client.dll | No unique types invented | "every type it exposes is duplicated in the core SDK assemblies" | PASS |

---

## Check 2 — DW-B50-02 WeakReference Pattern Correctness
**Result: FAIL**

| Sub-item | Expected | Actual in Plan | Pass? |
|---|---|---|---|
| WeakReference<T> .NET version stated (.NET 4.5+, safe in NT8 .NET 4.8) | "WeakReference<T> availability: .NET 4.5+ — SAFE in .NET 4.8" stated explicitly | NOT STATED — plan introduces WeakReference<T> without any NT8 runtime availability rationale | **FAIL** |
| TryGetTarget uses `out var` pattern | `out var cb` / `out var existing` | Section 3b: `wr.TryGetTarget(out var existing)`. Section 3c: `_atmComboRefs[i].TryGetTarget(out var cb)` | PASS |
| Prune pattern: backward iteration (i = Count-1 down to 0) | for-loop backward | Section 3c: `for (int i = _atmComboRefs.Count - 1; i >= 0; i--)` with `RemoveAt(i)` on dead ref | PASS |
| Idempotency check replaces `_atmComboRefs.Contains(cb)` with foreach TryGetTarget loop | Replace Contains with foreach TryGetTarget | Section 3b: replaces `if (!_atmComboRefs.Contains(cb))` with `bool alreadyTracked = false; foreach (var wr in _atmComboRefs) if (wr.TryGetTarget(...) && existing == cb) {...}` | PASS |
| `_atmComboRefs.Add(cb)` becomes `_atmComboRefs.Add(new WeakReference<ComboBox>(cb))` | WeakReference wrapper | Section 3b: `_atmComboRefs.Add(new WeakReference<ComboBox>(cb))` | PASS |

**Violation detail**:

> The plan introduces `WeakReference<System.Windows.Controls.ComboBox>` (Section 3a, 3b, 3c) without
> stating the NT8 runtime compatibility rationale. The checklist requires the plan explicitly confirm:
> "WeakReference<T> availability: .NET 4.5+ — SAFE in .NET 4.8 (NT8)."
>
> This omission leaves no documented basis for why WeakReference<T> is legal under NT8's Roslyn
> constraint set. The NT8_COMPILER_RULES.md pattern (every construct receives an explicit NT8 compat
> rationale comment) requires this confirmation be present in the plan so the engineer has the
> reasoning when writing the code comment.
>
> Required addition: Section 3a or Section 3d must include a line such as:
> "WeakReference<T> is available in .NET 4.5+. NT8 runs .NET Framework 4.8 — this construct is safe."

---

## Check 3 — CYC Math
**Result: PASS**

| Measurement | Expected | Actual in Plan | Code-confirmed? | Pass? |
|---|---|---|---|---|
| UpdateAtmComboVisibility CYC before | 2 | "CYC=2: (1) foreach loop body, (2) null guard" | Code at lines 1479-1489 matches: foreach branch + `if (cb != null)` branch = CYC 2 | PASS |
| UpdateAtmComboVisibility CYC after | 4 | "1 (base) + 1 (for-loop) + 1 (TryGetTarget true) + 1 (TryGetTarget false) = 4" | New for-loop + two TryGetTarget branches + base = 4. ≤ 8. | PASS |

---

## Check 4 — NT8 Compliance
**Result: PASS**

| Rule | Expected | Actual in Plan | Pass? |
|---|---|---|---|
| No `lock()` introduced (JS-021 / NT8-018) | Zero lock() | WeakReference<T> is pure data; no synchronization construct used anywhere in plan | PASS |
| No `async void` non-event-handler (JS-033 / NT8-019) | Zero async void | Plan introduces no async methods | PASS |
| WeakReference<T> constructor syntax safe in NT8 | `new WeakReference<ComboBox>(cb)` | Sections 3a/3b/3c all use `new WeakReference<ComboBox>(cb)` / `new WeakReference<System.Windows.Controls.ComboBox>()` — standard .NET syntax | PASS |
| NT8-003: no volatile double introduced | No volatile double | No volatile field introduced; UI-thread-only field unchanged | PASS |

---

## Check 5 — No Scope Creep
**Result: PASS**

| Item | Expected | Actual in Plan | Pass? |
|---|---|---|---|
| Changes limited to DW-B50C-02 and DW-B50-02 only | Two deferred items only | Section 1 states "This lane closes two deferred work items" and no additional scope | PASS |
| CopyEngine.cs untouched | Not mentioned | Not mentioned anywhere in plan | PASS |
| TradeCopierWindow.cs untouched | Not mentioned | Not mentioned anywhere in plan | PASS |
| TradeCopierAddOn.cs untouched | Not mentioned | Not mentioned anywhere in plan | PASS |
| No test files touched | Not mentioned | Section 4 explicitly states "No new xUnit [Fact] tests are required" | PASS |
| Ticket 1 is docs-only | NT8_ADDON_KNOWLEDGE.md only | Section 2 and Ticket 1 scan list confirm docs-only (zero .cs files) | PASS |

---

## Check 6 — Scan Coverage
**Result: PASS**

| Ticket | Scan | Check | Pass Condition in Plan | Pass? |
|---|---|---|---|---|
| Ticket 1 | SCAN-08 | `grep "NinjaTrader.Client" docs/standards/NT8_ADDON_KNOWLEDGE.md` | At least one match | PASS |
| Ticket 2 | SCAN-01 | `grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs` | Zero matches | PASS |
| Ticket 2 | SCAN-02 | `grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs` | Zero matches | PASS |
| Ticket 2 | SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Zero errors | PASS |
| Ticket 2 | SCAN-06 | CYC audit UpdateAtmComboVisibility | CYC = 4 ≤ 8 | PASS |
| Ticket 2 | SCAN-07 | `powershell -File scripts\verify_links.ps1` | Zero broken links | PASS |

---

## Violation Summary

| # | Check | Rule / Requirement | Location in Plan | Severity |
|---|---|---|---|---|
| V-01 | Check 2 | WeakReference<T> .NET version compatibility not stated — plan must confirm ".NET 4.5+, SAFE in NT8 (.NET 4.8)" | Section 3a (field declaration) or Section 3d (CYC table) | P1 — REVIEW_FAIL |

---

## Final Verdict

**REVIEW_FAIL**

One violation found. The plan must be updated by ptt-architect to add a single sentence
confirming WeakReference<T> is available in .NET 4.5+ and therefore safe in NT8 (.NET Framework 4.8).
Suggested placement: Section 3a comment block or a new "NT8 Compatibility" subsection in Section 3.
No structural changes to the plan are required. After this sentence is added, re-submit for review.

---

## Re-review Verdict (Cycle 1 fix)

- **V-01 Fix**: PRESENT — exact sentence found in Section 3d:
  > "WeakReference<T> was introduced in .NET 4.5 and is available in .NET Framework 4.8 — safe for NT8 use without compiler rule violation."
- **Check 2**: PASS — WeakReference<T> .NET availability is now explicitly stated in the plan.
- **All other checks**: unchanged — all PASS from cycle 0 (Checks 1, 3, 4, 5, 6).
- **Final verdict**: REVIEW_PASS
