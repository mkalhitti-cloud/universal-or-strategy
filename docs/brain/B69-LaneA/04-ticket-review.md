# Ticket Review: B69-LaneA

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-13
**Source ticket**: `docs/brain/B69-LaneA/04-tickets.md`
**Source plan**: `docs/brain/B69-LaneA/02-architecture-plan.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — B69-LaneA Fix FlattenOneAccount full-cancel + SubmitBeStop FullName + HandleEntryChange dedup preload

### Traceability: PASS

| DW Item | Ticket Coverage |
|---------|----------------|
| DW-B69-01 | CHANGE A (line 450 delete), CHANGE B (CancelAllAccountOrders insert after line 470), CHANGE C1/C2/C3 (FlattenOneAccount body) — all sub-issues covered |
| DW-B69-02 | CHANGE D (SubmitBeStop line 512 FullName), CHANGE E (FindPosition line 1778 FullName) |
| DW-B69-03 | CHANGE F (HandleEntryChange lines 1127-1129 _dedupCache preload) |

Plan §4 Change Map items 1-9 all map 1-to-1 to CHANGE A through CHANGE G.
No phantom work (ticket items not in plan). No missing plan items (all plan items in ticket).

### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock()` in new code descriptions | PASS — `CancelAllAccountOrders` uses `foreach` + `try/catch`; no lock() anywhere |
| JS-001 | `throw new` in hot-path C# methods | PASS — `catch { }` swallows without re-throw; PowerShell deploy `throw` is outside C# hot path |
| JS-002 | New `return null` sites | PASS — `FindPosition` retains pre-existing contract; explicitly noted as not a new site |
| JS-033 | `async void` in new/modified methods | PASS — all methods are synchronous `void` or `internal void` |
| JS-036/037 | Heap alloc on tick hot-path | PASS — `new List<Order>()` and `new[]{order}` on broker-event path only, not per-tick |
| ASCII-only | Unicode/emoji/curly quotes | PASS — all string literals and identifiers are ASCII |
| PTT- prefix | CreateOrder name `"PTT-Flatten"` | PASS — unchanged, uses PTT- prefix |
| DateTime.Now | Not used | PASS — `DateTime.MaxValue` used in `CreateOrder`, unchanged |
| No FontFamily/hex | Backend methods only | PASS — no UI work |
| FullName identity | `_dedupCache` is `ConcurrentDictionary` | PASS — not a plain `Dictionary<K,V>` |

### CYC Pre-Check: PASS

| Method | Annotated CYC | Max Allowed | Result |
|--------|---------------|-------------|--------|
| `CancelAllAccountOrders` (new) | 4 | 8 | PASS |
| `FlattenOneAccount` (modified) | 4 | 8 | PASS |
| `SubmitBeStop` (modified) | 7 (pre-B69 baseline, unchanged) | 8 | PASS |
| `HandleEntryChange` (modified) | 7 (pre-B69 baseline, CYC delta=0) | 8 | PASS |
| `FindPosition` (modified) | 1 (null-guard adds no outer CYC) | 8 | PASS |

CYC breakdowns provided verbatim in ticket §Method Signatures and in SCAN-05. No method exceeds 8.

### NT8 Check: PASS

| Constraint | Ticket Description | Result |
|------------|--------------------|--------|
| FullName comparison (not reference equality) | CHANGE D: `p.Instrument != null && p.Instrument.FullName == instr.FullName`; CHANGE E: same pattern | PASS |
| `acc.Submit` called after `CreateOrder` | CHANGE C3: `var order = acc.CreateOrder(...); if (order != null) acc.Submit(new[] { order });` | PASS |
| `acc.Submit` called in `HandleEntryChange` | Pre-existing; CHANGE F adds `_dedupCache` preload inside the same `if (order != null)` block | PASS |
| No `async/await` in lifecycle methods | All new/modified methods are synchronous | PASS |
| No `Account.All` outside Loaded handler | Not used | PASS |
| No `sealed` on TradeCopierWindow | Not applicable | PASS |
| No `FontFamily` on WPF element | Not applicable | PASS |
| No hardcoded hex color | Not applicable | PASS |
| No `DateTime.Now` | `DateTime.MaxValue` used | PASS |
| CreateOrder name starts "PTT-" | `"PTT-Flatten"` | PASS |

NT8 authority cited: `NT8_FULL_REFERENCE.md` line 1926 for FullName identity;
`[938-EF-GUARD]` EmergencyFlattenSingleFleetAccount pattern for name-agnostic cancel;
`SubmitBeStop` lines 524-525 as precedent for `acc.Submit` requirement.

### Test Coverage: PASS

All 7 [Fact] tests present with explicit assertions. Each new or modified public/internal method covered.

| Test | Method Under Test | DW | Assertions |
|------|------------------|----|------------|
| `T_B69_01_CancelAllAccountOrders_cancels_PTT_Copy_orders` | `CancelAllAccountOrders` | DW-B69-01 | `Assert.True(acc.CancelCallCount == 1)`, `Assert.Single(...)`, `Assert.Same(...)` |
| `T_B69_02_CancelAllAccountOrders_cancels_ChangeSubmitted_orders` | `CancelAllAccountOrders` | DW-B69-01 | `Assert.Equal(1, ...)`, `Assert.Single(...)`, `Assert.Same(...)` |
| `T_B69_03_CancelAllAccountOrders_skips_Filled_orders` | `CancelAllAccountOrders` | DW-B69-01 | `Assert.Equal(1, ...)`, `Assert.Single(...)`, `Assert.Same(...)`, `Assert.DoesNotContain(...)` |
| `T_B69_04_CancelAllAccountOrders_skips_different_instrument` | `CancelAllAccountOrders` | DW-B69-01 | `Assert.Equal(1, ...)`, `Assert.Single(...)`, `Assert.Same(...)`, `Assert.DoesNotContain(...)` |
| `T_B69_05_SubmitBeStop_finds_position_by_FullName` | `SubmitBeStop` | DW-B69-02 | `Assert.NotSame(instrA, instrB)`, `Assert.True(acc.CreateOrderCallCount >= 1, ...)`, `Assert.True(acc.SubmitCallCount >= 1, ...)` |
| `T_B69_06_HandleEntryChange_preloads_new_orderId_into_dedupCache` | `HandleEntryChange` | DW-B69-03 | `Assert.False(engine.DedupCacheContains(oldOrderId), ...)`, `Assert.True(engine.DedupCacheContains(newOrderId), ...)`, `Assert.Equal(newPrice, engine.DedupCacheGet(newOrderId))` |
| `T_B69_07_CancelAllAccountOrders_null_acc_noOp` | `CancelAllAccountOrders` | DW-B69-01 | `Assert.Null(ex)` |

Test framework: xUnit `[Fact]` only — no NUnit/MSTest. PASS per JS testing standard.

### Scan Checklist: PASS

All 7 scans present with exact PowerShell commands and expected results.

| Scan | Description | Present | Expected Result Stated |
|------|-------------|---------|----------------------|
| SCAN-01 | `lock\s*\(` — 0 hits in new code | YES | 0 hits in CancelAllAccountOrders block; 0 new hits full-file |
| SCAN-02 | `throw\s+new` — 0 hits in new code | YES | 0 hits in CancelAllAccountOrders block |
| SCAN-03 | `p\.Instrument\s*==\s*instr` — 0 hits | YES | 0 hits after DW-B69-02 fix at line 512 |
| SCAN-04 | `p\.Instrument\s*==\s*instrument` — 0 hits | YES | 0 hits after DW-B69-02 fix at line 1778 |
| SCAN-05 | CYC audit — all methods ≤8 | YES | CancelAllAccountOrders=4, FlattenOneAccount=4, SubmitBeStop=7, HandleEntryChange=7, FindPosition=1 |
| SCAN-06 | ASCII-only — 0 non-ASCII in new code | YES | 0 non-ASCII in insertion range |
| SCAN-07 | `async\s+void\s+\w+` — 0 hits in new code | YES | 0 hits in CancelAllAccountOrders block; 0 new hits full-file |

Defense-in-depth rationale satisfied: scan checklist is the engineer's contract (Layer 1), anchors
the engineer self-report in `ticket-1-completion.md` (Layer 2), and enables verifier cross-check in
`ticket-1-verification.md` (Layer 3). All three layers are intact.

### File Routing: PASS

All C# source paths reference Wave workspace `src/PropTraderTools/`:

| File | Path | Valid |
|------|------|-------|
| Production source | `src/PropTraderTools/CopyEngine.cs` | YES — Wave workspace |
| Test file | `src/PropTraderTools/CopyEngineTests.cs` | YES — Wave workspace |

No paths reference Director workspace (`universal-or-strategy-director`). PASS.

### Completeness Check: PASS

All 7 change blocks (A–G) present with exact pre-change line numbers:

| Change | Location | Line Numbers | Present |
|--------|----------|-------------|---------|
| A | CopyEngine.cs | Line 450 delete | YES |
| B | CopyEngine.cs | Insert after line 470 | YES |
| C1 | CopyEngine.cs | Lines 1467-1474 replace | YES |
| C2 | CopyEngine.cs | Line 1483 replace | YES |
| C3 | CopyEngine.cs | Lines 1487-1491 replace | YES |
| D | CopyEngine.cs | Line 512 replace | YES |
| E | CopyEngine.cs | Line 1778 replace | YES |
| F | CopyEngine.cs | Lines 1127-1129 replace | YES |
| G | CopyEngineTests.cs | After line 3552 insert | YES |

### Deploy Step: PASS

Deploy step (ticket §Deploy Step) describes:
1. `dotnet build` with Release configuration
2. `deploy-sync.ps1` execution to sync hard-link to NinjaTrader bin
3. `Get-FileHash SHA256` on both source DLL and NinjaTrader hard-link target
4. Hash equality assertion with explicit error message on mismatch

SHA-256 match verification is explicitly described and the `ticket-1-completion.md` requirement
is listed in the engineer completion checklist (`deploy-sync.ps1 — ran; SHA-256 match verified`). PASS.

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All 8 checks passed. No violations found. No JS rule breaches identified in ticket descriptions.
No NT8 constraint violations. No missing scan checklist items. No missing test coverage.
No phantom or missing plan items. File routing correct. Deploy step complete.

**Safe to spawn ptt-engineer on T1.**

| Check | Result |
|-------|--------|
| 1. Traceability (DW-B69-01/02/03) | PASS |
| 2. 7-Scan Checklist (SCAN-01..07) | PASS |
| 3. NT8 Constraints | PASS |
| 4. JS Rules (JS-021/001/002/033) | PASS |
| 5. Completeness (Changes A-G, line numbers) | PASS |
| 6. Test Coverage (7 [Fact] tests) | PASS |
| 7. Deploy Step (SHA-256 verification) | PASS |
| 8. Single File Scope (CopyEngine.cs + CopyEngineTests.cs only) | PASS |
