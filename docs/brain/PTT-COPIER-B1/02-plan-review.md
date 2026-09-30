# PTT-COPIER-B1 Plan Review

**Reviewer:** PTT Plan Reviewer (Phase 2)  
**Date:** 2026-07-06  
**Plan:** `docs/brain/PTT-COPIER-B1/02-architecture-plan.md`  
**Spec:** `specs/002-trade-copier-spec.html`  
**Rules:** `docs/standards/jane-street/RULES_CATALOG.md`  
**Protocol:** `docs/protocol/PTT_WORKSPACE_PROTOCOL.md`  

---

## Audit Results

### SECTION A — Completeness

| ID | Check | Status | Note |
|----|-------|--------|------|
| A1 | Plan has all 11 required sections | **PASS** | Sections 1–11 all present: Overview, File Map, Data Structures, CopyEngine API, Gate Chain, IsBracketLeg, TradeCopierPanel API, TradeCopierWindow API, Concurrency Model, 7-Scan Compliance, Ticket Decomposition. |
| A2 | File paths are in Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) | **PASS** | All three file paths in §2 File Map and §11 Ticket Decomposition reference the correct Wave workspace path. |
| A3 | Three tickets defined (T1=CopyEngine, T2=Panel, T3=Window) | **PASS** | §11 defines T1 (`CopyEngine.cs`), T2 (`TradeCopierPanel.cs`), T3 (`TradeCopierWindow.cs`). |
| A4 | Line count estimates present (~170/~100/~80) | **PASS** | §2 File Map shows: CopyEngine ~170, TradeCopierPanel ~100, TradeCopierWindow ~80. |

---

### SECTION B — Data Structures

| ID | Check | Status | Note |
|----|-------|--------|------|
| B1 | `TrimSignal` has NO `qty` field (JS-003 correctness by construction) | **PASS** | §3.3 explicitly declares `// NO qty field — by design` with rationale. No quantity field present in the struct definition. |
| B2 | `CopySignal` has: `OrderAction`, `OrderType`, `int Quantity`, `double LimitPrice`, `string OrderId` | **PASS** | §3.2 declares all five fields with matching types (`OrderAction Action`, `OrderType Type`, `int Quantity`, `double LimitPrice`, `string OrderId`). |
| B3 | `CopyRule` has: `string Instrument`, `Account MasterAccount`, `Account[] FollowerAccounts` | **PASS** | §3.1 declares all three fields with exact types. |
| B4 | All three structs are `private readonly struct` (JS-008) | **PASS** | §3.1–3.3 each declare `private readonly struct`; §3 preamble confirms "All three structs are `private readonly struct` in CopyEngine.cs." |
| B5 | All three structs use `private` constructor + `static Create()` factory (JS-010) | **PASS** | §3.1–3.3 each show a `private` constructor and a `public static Create()` factory method. |

---

### SECTION C — Gate Chain

| ID | Check | Status | Note |
|----|-------|--------|------|
| C1 | Gate 1 is `_isCopyEnabled` check | **PASS** | §5 pseudocode line: `if (!_isCopyEnabled) return;` is the first gate. |
| C2 | Gate 2 is master account + instrument check | **PASS** | §5 pseudocode: `if (order.Account != _rule.MasterAccount) return;` and `if (order.Instrument.FullName != _rule.Instrument) return;`. |
| C3 | Gate 3 is `OrderState.Submitted` + `IsMarket/IsLimit` check | **PASS** | §5 pseudocode: `if (order.OrderState != OrderState.Submitted) return;` then `if (!isMarket && !isLimit) return;`. |
| C4 | Gate 4 is `IsDedup` check | **PASS** | §5 pseudocode: `if (IsDedup(order.Id.ToString())) return;`. |
| C5 | Stops/targets/bracket orders are NOT copied (`IsMarket/IsLimit` gate filters them) | **PASS** | Gate 3 rejects any order type other than Market or Limit; stop-market and stop-limit orders are structurally excluded. |
| C6 | Never copies a copy (source == master account check at Gate 2) | **PASS** | Gate 2 checks `order.Account != _rule.MasterAccount`; follower-originated orders are always rejected here. |

---

### SECTION D — IsBracketLeg

| ID | Check | Status | Note |
|----|-------|--------|------|
| D1 | Layer 1 is `order.FromEntrySignal != null` | **PASS** | §6 code: `if (order.FromEntrySignal != null) return true;` is Layer 1. |
| D2 | Layer 2 is `order.Name.StartsWith("PTT-")` | **PASS** | §6 code: `if (order.Name.StartsWith("PTT-")) return true;` is Layer 2. |
| D3 | Layer 3 is `order.Name.StartsWith("Stop") \|\| order.Name.StartsWith("Target")` | **PASS** | §6 code: `if (order.Name.StartsWith("Stop") \|\| order.Name.StartsWith("Target")) return true;` is Layer 3. |

---

### SECTION E — Concurrency (JS rules) ⚠️ ZERO-TOLERANCE SECTION

| ID | Check | Status | Note |
|----|-------|--------|------|
| E1 | `_isCopyEnabled` is `volatile bool` (JS-023) | **PASS** | §9.1 declares `private volatile bool _isCopyEnabled;` with explicit `volatile` rationale. |
| E2 | `_dedupCache` is `ConcurrentDictionary<string, long>` (JS-025) | **PASS** | §9.2 declares `private readonly ConcurrentDictionary<string, long> _dedupCache = new();`. |
| E3 | No `lock()` anywhere in the plan (JS-021) — ZERO-TOLERANCE | **PASS** | §9.4 "No lock() Contract" explicitly states: "There is no `lock()` statement anywhere in any of the three files." No `lock(` token appears anywhere in the plan document. |
| E4 | Dedup expiry is 10 seconds | **PASS** | §9.2 states: "entries older than 10 seconds are pruned. Threshold: `DateTime.UtcNow.Ticks - 10_000_000L * 10L`". §4.6 also confirms "10-second TTL". |

---

### SECTION F — NT-Native UI

| ID | Check | Status | Note |
|----|-------|--------|------|
| F1 | All buttons use `NTButtonStyle` | **PASS** | §7.6 states "All buttons: `Style="{DynamicResource NTButtonStyle}"`"; §8 BuildUI states "All controls use `NTButtonStyle`". |
| F2 | Account selectors use `AccountComboBoxStyle` | **PASS** | §8 BuildUI specifies `AccountComboBoxStyle` for the leader ComboBox in per-rule rows; §8 T3 ticket confirms `AccountComboBoxStyle`. |
| F3 | Colors use `NTBrushes.*` only, no hardcoded hex | **PASS** | §7.6 states "All color references: `NTBrushes.*` dynamic resource keys (SCAN-04)"; §8 BuildUI states "All colors via `NTBrushes.*`". |
| F4 | No `FontFamily` override anywhere | **PASS** | §7.6 states "No `FontFamily` property set anywhere (SCAN-03)"; §8 BuildUI states "No FontFamily property anywhere". |
| F5 | Keyboard shortcuts: Shift+T=Trim, Shift+F=Flatten, Shift+C=Cancel | **PASS** | §7.5 table and `InputBindings.Add` code block confirms all three: `Key.T/Shift`, `Key.F/Shift`, `Key.C/Shift`. |

---

### SECTION G — Scan Compliance

| ID | Check | Status | Note |
|----|-------|--------|------|
| G1 | SCAN-05 — all `CreateOrder` name params start with `"PTT-"` (`PTT-Copy`, `PTT-Trim`, `PTT-Flatten`) | **PASS** | §4.2 names `"PTT-Copy"`, §4.3 names `"PTT-Trim"`, §4.4 names `"PTT-Flatten"`; §10 SCAN-05 row confirms all three. |
| G2 | SCAN-06 — plan specifies `DateTime.UtcNow`, never `DateTime.Now` | **PASS** | §3.3 TrimSignal ctor uses `DateTime.UtcNow`; §4.6/§9.2 use `DateTime.UtcNow.Ticks`; §10 SCAN-06 row guarantees 0 results. |
| G3 | SCAN-02 — plan contains no non-ASCII characters | **PASS** | Full text of `02-architecture-plan.md` reviewed; all identifiers, string literals, and comments are ASCII-only. No Unicode, emoji, or curly quotes present. |

---

### SECTION H — Spec Fidelity

| ID | Check | Status | Note |
|----|-------|--------|------|
| H1 | Copy triggers on `OrderState.Submitted` (NOT on Fill — that is the SimpleTradeCopier mistake) | **PASS** | Gate 3 in §5 explicitly checks `order.OrderState != OrderState.Submitted`; §4.1 JS-enforced note says "SCAN-06: DateTime.UtcNow.Ticks used in IsDedup, never DateTime.Now"; matches spec design decision "COPY WHEN: OrderState.Submitted... Not on fill." |
| H2 | Limit orders copied at same limit price (not converted to market) | **PASS** | `CopySignal` carries `double LimitPrice`; §4.2 `SendCopy` passes `signal.LimitPrice` to `CreateOrder`; spec spec-events tab: "Limit order — same limit price, same qty." |
| H3 | ATM brackets delegated entirely to NT — plan never writes stop/target orders | **PASS** | Design Pillar 1 ("Parasitic infrastructure"); Gate 3 rejects non-Market/non-Limit orders; no `CreateOrder` for stops or targets exists anywhere in the plan. |
| H4 | Singleton pattern — both surfaces share same `CopyEngine` instance | **PASS** | §4 declares `public static CopyEngine Instance { get; } = new CopyEngine(); private CopyEngine() { }`. T2 and T3 both reference `CopyEngine.Instance`. |
| H5 | `AllAccounts()` helper used by Trim, Flatten, and `CancelPendingEntries` | **PASS** | §4.3 "For every account in `AllAccounts(instrument)`", §4.4 same, §4.5 same. §4.8 defines `AllAccounts`. |
| H6 | Flatten sends full qty (not ceil), Trim sends `ceil(qty/2)` | **PASS** | §4.3 Trim: `(int)Math.Ceiling(Math.Abs(qty) / 2.0)`; §4.4 Flatten: "submits a market order for the entire quantity." |
| H7 | Zero-Launch UX — Add-On registered in Control Center, panel appears automatically | **PASS** | Design Pillar 3 in §1: "Zero-launch: the Add-On is registered at install. No startup moment visible to the user." `TradeCopierWindow` subclasses `NTWindow` (§8). |

---

## VIOLATIONS

**None.**

All 38 checklist items PASS. No violations were found in any section.

---

## Final Verdict

**REVIEW_PASS**

The architecture plan is complete, internally consistent, and fully compliant with the spec, Jane Street rules catalog, and PTT workspace protocol. All concurrency requirements (Section E) are met with zero-tolerance compliance. All NT-native UI, scan compliance, and spec fidelity requirements pass without exception. The plan is cleared for engineer execution (Phase 5: Ticket Execution).
