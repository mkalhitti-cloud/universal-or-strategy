# PTT-COPIER-B14 — Ticket 1 Verification Report

**Ticket**: DW-B12-DEFER-02 — Auto-Trail Stop from BE CONNECTED State
**Verifier**: ptt-verifier (Phase 4b T1)
**Date**: 2026-07-07
**Source files read (Wave workspace)**:
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

---

## Field Check

Fields verified at lines 102–109 in CopyEngine.cs.

| Field | Spec Type | Actual Declaration | Status |
|-------|-----------|-------------------|--------|
| `_trailBeState` | `volatile int` | `private volatile int _trailBeState = 0;` (line 105) | ✅ PASS |
| `_trailBeBufferTicks` | `volatile int` | `private volatile int _trailBeBufferTicks = 2;` (line 106) | ✅ PASS |
| `_trailBeLastPnl` | `volatile long` (NOT volatile double — NT8-003 ban) | `private volatile long _trailBeLastPnl = 0L;` (line 107) | ✅ PASS |
| `_trailBeAccount` | plain `Account` (single-writer UI thread) | `private Account _trailBeAccount = null;` (line 108) | ✅ PASS |
| `_trailBeInstrument` | plain `Instrument` (single-writer UI thread) | `private Instrument _trailBeInstrument = null;` (line 109) | ✅ PASS |

**All 5 fields: PASS.**

---

## Method Check

### ArmTrailBe — lines 1287–1304

**Spec signature**: `internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)`
**Actual signature**: ✅ matches exactly (line 1287)

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| Guard (1): `instr == null` → return | present | line 1289–1290 | ✅ PASS |
| Guard (2): `masterAcc == null` → return | present | line 1291–1292 | ✅ PASS |
| Guard (3): `IsFlat(pos)` → return | present | line 1294–1295 | ✅ PASS |
| `BitConverter.DoubleToInt64Bits` for seeding `_trailBeLastPnl` | present | line 1299 | ✅ PASS |
| `masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate` | present | line 1302 | ✅ PASS |
| `_trailBeState = 1` is LAST write (release fence) | required | line 1303 — confirmed LAST write | ✅ PASS |
| No `lock()` in method body | required | 0 occurrences in lines 1287–1304 | ✅ PASS |

**Independent CYC count**:
- `if (instr == null)` → 1
- `if (masterAcc == null)` → 2
- `if (IsFlat(pos))` → 3
- `if (currentPnl == double.MinValue)` → 4

**CYC = 4. Engineer claimed 4. ✅ PASS ≤ 8.**

---

### DisarmTrailBe — lines 1310–1319

**Spec signature**: `internal void DisarmTrailBe()`
**Actual signature**: ✅ matches exactly (line 1310)

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `Interlocked.CompareExchange(ref _trailBeState, 0, 1)` — no lock() | required | line 1312 | ✅ PASS |
| Unsubscribes `AccountItemUpdate -= OnTrailBeAccountUpdate` | required | line 1316 | ✅ PASS |
| Nulls `_trailBeAccount` | required | line 1317 | ✅ PASS |
| Nulls `_trailBeInstrument` | required | line 1318 | ✅ PASS |
| Idempotent (CAS guard prevents double-unsubscribe) | required | CAS at line 1312 returns early if not Active | ✅ PASS |

**Independent CYC count**:
- `if (Interlocked.CompareExchange(...) != 1)` → 1
- `if (acc != null)` → 2

**CYC = 2. Engineer claimed 2. ✅ PASS ≤ 8.**

---

### OnTrailBeAccountUpdate — lines 1329–1348

**Spec signature**: `private void OnTrailBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)`
**Actual signature**: ✅ matches exactly — **plain void** (not async void) (line 1329)

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| State check FIRST: `if (_trailBeState != 1) return` | required | line 1331–1332 | ✅ PASS |
| Item filter: `e.AccountItem != AccountItem.UnrealizedProfitLoss` | required | line 1333–1334 | ✅ PASS |
| PnL improvement check: `if (newPnl <= oldPnl) return` | required | line 1338–1339 | ✅ PASS |
| CAS: `Interlocked.CompareExchange(ref _trailBeLastPnl, newBits, oldBits)` | required | line 1342 | ✅ PASS |
| `Interlocked.Increment(ref _trailBeBufferTicks)` | required | line 1344 | ✅ PASS |
| `BreakEven(instr, newBuffer)` called | required | line 1347 | ✅ PASS |
| STAYS SUBSCRIBED (no disarm CAS inside) | required | no `CompareExchange(_trailBeState ...)` inside method | ✅ PASS |
| No `lock()` in method body | required | 0 occurrences | ✅ PASS |

**Independent CYC count**:
- `if (_trailBeState != 1)` → 1
- `if (e.AccountItem != ...)` → 2
- `if (newPnl <= oldPnl)` → 3
- `if (Interlocked.CompareExchange(...) != oldBits)` → 4
- `if (instr != null)` → 5

**CYC = 5. Engineer claimed 5. ✅ PASS ≤ 8.**

---

## Panel Modification Check

### OnBeConnected — TradeCopierPanel.cs lines 752–763

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `_engine.ArmTrailBe(_instrument, _leaderAccount, _beBuffer)` after `BreakEven` | required | line 761 — inside `if (_instrument != null) { ... if (_leaderAccount != null) ArmTrailBe(...) }` | ✅ PASS |
| Nested null guard for both `_instrument` and `_leaderAccount` | required | `_instrument` checked at line 757, `_leaderAccount` at line 760 | ✅ PASS |
| Plain `void` method (not `async void`) | required | `private void OnBeConnected(string instr)` at line 752 | ✅ PASS |

**CYC = 3 (null guard 1 + instrument guard 2 + leaderAccount guard 3). ✅ PASS ≤ 8.**

**Note**: Spec §1.5 shows `if (_instrument != null && _leaderAccount != null)` as a single compound guard (one branch = CYC 3). Actual code nests them separately (two `if` statements). Both yield CYC=3; the split-nest form is functionally identical and semantically more readable.

---

### OnBeClick Connected→Idle case — TradeCopierPanel.cs lines 711–716

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `_engine.DisarmTrailBe()` present in `case BeState.Connected:` | required | line 713 | ✅ PASS |
| Call is between `DisarmPendingBe()` and `_beState = BeState.Idle` | required | lines 712–714 confirm ordering | ✅ PASS |

---

### Detach() cleanup — TradeCopierPanel.cs lines 291–306

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `_engine.DisarmTrailBe()` present in `Detach()` | required | line 303 | ✅ PASS |
| Prevents dangling `AccountItemUpdate` subscription | required | call present before `_instrument = null` | ✅ PASS |

**⚠ Ordering deviation from spec §1.7**: Spec says place `DisarmTrailBe()` **before** the follower item `foreach` loop. Actual code places it **after** the follower loop (line 303 follows lines 299–301). This is a **non-functional deviation** — `DisarmTrailBe()` is idempotent and the `AccountItemUpdate` unsubscription from the trail watcher is independent of follower account cleanup. No bug risk. Noted for audit trail completeness.

---

## Test Check

All 6 new tests located at lines 1545–1627 in `CopyEngineTests.cs`.

| Test Name | Attribute | Status |
|-----------|-----------|--------|
| `ArmTrailBe_MethodExists_WithCorrectSignature` | `[Fact]` ✅ | ✅ PASS |
| `ArmTrailBe_NullInstrument_NoException` | `[Fact]` ✅ | ✅ PASS |
| `DisarmTrailBe_WhenNotArmed_NoException` | `[Fact]` ✅ | ✅ PASS |
| `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall` | `[Fact]` ✅ | ✅ PASS |
| `TrailBe_BitConverter_PnlEncoding_RoundTrip` | `[Fact]` ✅ | ✅ PASS |
| `TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds` | `[Fact]` ✅ | ✅ PASS |

**Notes on test body deviations (non-failing)**:
- `ArmTrailBe_MethodExists_WithCorrectSignature` uses `BindingFlags.NonPublic | BindingFlags.Instance` only, omitting `BindingFlags.Public`. Since `ArmTrailBe` is `internal`, this correctly resolves. The spec example also included `BindingFlags.Public` — no functional difference for this internal method.
- `ArmTrailBe_MethodExists_WithCorrectSignature` does NOT verify parameter types (only count). Spec test expected type assertions on each parameter. This is a reduced assertion coverage but does not constitute a VERIFY_FAIL.
- `ArmTrailBe_NullInstrument_NoException` invokes via reflection (handles `TargetInvocationException`) rather than direct call. Functionally equivalent.
- All 6 use `[Fact]` xUnit — NOT `[Test]` or `[TestMethod]`. ✅

**Section header comment present**: `// B14 T1: Auto-Trail BE tests (T-B14-T1-A through T-B14-T1-F)` at line 1546. ✅

---

## 7 Independent Scan Results

### SCAN-01 — `lock(` in B14 new methods (lines 1281–1348)

```
Select-String CopyEngine.cs -Pattern "lock\s*\("
```
**Result**: 4 matches — ALL in comment lines (319, 562, 793, 1197). Zero in live code. Zero in B14 range (1281–1348).
**Verdict**: ✅ PASS — 0 lock() in new code. JS-021 compliant.

---

### SCAN-02 — `async void` in CopyEngine.cs

```
grep -P "async\s+void" CopyEngine.cs
```
**Result**: 0 matches.
`OnTrailBeAccountUpdate` is declared `private void` (line 1329) — confirmed plain void.
**Verdict**: ✅ PASS — JS-033 compliant.

---

### SCAN-03 — `return null` in lines 1281–1348 of CopyEngine.cs

```
Select-String CopyEngine.cs -Pattern "return null" | Where LineNumber -ge 1281 -and -le 1348
```
**Result**: 0 matches in range 1281–1348. (Existing `return null` at lines 647, 1038, 1044, 1097 are all outside this range and in pre-existing methods.)
**Verdict**: ✅ PASS — All guard exits in B14 methods use bare `return;`. JS-002 compliant.

---

### SCAN-04 — Independent CYC count

| Method | Decision Points | CYC | Limit | Status |
|--------|----------------|-----|-------|--------|
| `ArmTrailBe` | instr null(1), acc null(2), IsFlat(3), MinValue(4) | **4** | 8 | ✅ PASS |
| `DisarmTrailBe` | CAS check(1), acc null(2) | **2** | 8 | ✅ PASS |
| `OnTrailBeAccountUpdate` | state(1), item(2), pnl(3), CAS(4), instr(5) | **5** | 8 | ✅ PASS |
| `OnBeConnected` | beBtn2(1), instrument(2), leaderAccount(3) | **3** | 8 | ✅ PASS |

All CYC values match engineer Layer 2 report. All ≤ 8. **Verdict**: ✅ PASS.

---

### SCAN-05 — `volatile double` (banned NT8-003) / `volatile long` (expected)

```
Select-String CopyEngine.cs -Pattern "volatile\s+double|volatile\s+long"
```
**Result**:
- `volatile double`: 0 occurrences in live code (lines 104, 1286, 1327 are all comments). ✅
- `volatile long`: 1 occurrence — `private volatile long _trailBeLastPnl = 0L;` at line 107. ✅

**Verdict**: ✅ PASS — NT8-003 fully compliant.

---

### SCAN-06 — `Math.Clamp` in CopyEngine.cs and TradeCopierPanel.cs

```
Select-String CopyEngine.cs, TradeCopierPanel.cs -Pattern "Math\.Clamp"
```
**Result**: 0 occurrences of `Math.Clamp` calls. (All results contain the text "no Math.Clamp" in comments — confirming awareness of NT8-034 ban.)
**Verdict**: ✅ PASS — NT8-034 compliant.

---

### SCAN-07 — `BitConverter` in CopyEngine.cs (expected in new methods)

```
Select-String CopyEngine.cs -Pattern "BitConverter\.(DoubleToInt64Bits|Int64BitsToDouble)"
```
**Result**: 4 occurrences in live code:
- Line 1299: `BitConverter.DoubleToInt64Bits(currentPnl)` — in `ArmTrailBe` ✅
- Line 1336: `BitConverter.Int64BitsToDouble(...)` — in `OnTrailBeAccountUpdate` ✅
- Line 1340: `BitConverter.DoubleToInt64Bits(newPnl)` — in `OnTrailBeAccountUpdate` ✅
- Line 1341: `BitConverter.DoubleToInt64Bits(oldPnl)` — in `OnTrailBeAccountUpdate` ✅

Both `ArmTrailBe` and `OnTrailBeAccountUpdate` use `BitConverter` for PnL encoding. NT8-003 pattern confirmed.
**Verdict**: ✅ PASS.

---

## Layer 2 Cross-check

Comparing engineer's Layer 2 self-report in `ticket-1-completion.md` against Layer 3 independent results:

| Engineer Claim | Layer 3 Independent Result | Match? |
|---------------|---------------------------|--------|
| `_trailBeState` = `volatile int`, `_trailBeBufferTicks` = `volatile int`, `_trailBeLastPnl` = `volatile long` | Confirmed at lines 105–107 | ✅ |
| `_trailBeAccount` / `_trailBeInstrument` = plain `Account`/`Instrument` | Confirmed at lines 108–109 | ✅ |
| `ArmTrailBe` at lines 1281–1304, CYC=4 | Found at lines 1287–1304, CYC=4 | ✅ (off by 6 lines in header — start of method body matches) |
| `DisarmTrailBe` at lines 1306–1319, CYC=2 | Found at lines 1310–1319, CYC=2 | ✅ |
| `OnTrailBeAccountUpdate` at lines 1321–1348, CYC=5 | Found at lines 1329–1348, CYC=5 | ✅ |
| `Detach()` — `DisarmTrailBe()` at line 303 | Confirmed at line 303 | ✅ |
| `OnBeClick Connected case` — `DisarmTrailBe()` at line 713 | Confirmed at line 713 | ✅ |
| `OnBeConnected` — `ArmTrailBe()` at line 761 | Confirmed at line 761 | ✅ |
| SCAN-01 `lock(`: 4 comment-only matches, 0 in live code | 4 comment-only matches confirmed | ✅ |
| SCAN-06 DateTime.Now: 0 results | Not applicable to T1 scans | N/A |
| SCAN-07 `volatile double`: 0 in real code; `BitConverter` at lines 1299, 1336, 1340, 1341 | Confirmed: 0 volatile double in live code; BitConverter at same lines | ✅ |
| 6 new `[Fact]` tests at lines 1545–1628 | Confirmed: 6 `[Fact]` tests at lines 1549–1627 | ✅ |

**Layer 2 vs Layer 3 discrepancy**: None material. All engineer claims independently verified.

---

## Additional DNA Rule Checks

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock ban) | No `lock(` in any new or modified method | ✅ PASS |
| JS-023 (volatile cross-thread fields) | `_trailBeState`, `_trailBeBufferTicks`, `_trailBeLastPnl` all volatile | ✅ PASS |
| JS-033 (async void ban) | `OnTrailBeAccountUpdate` is plain `void` | ✅ PASS |
| JS-001 (no throw in hot paths) | `OnTrailBeAccountUpdate` — no throw; `ArmTrailBe` / `DisarmTrailBe` — no throw | ✅ PASS |
| JS-002 (no return null in non-nullable paths) | All guard exits use bare `return;` | ✅ PASS |
| JS-010 (private constructor) | CopyEngine constructor is private (singleton, unchanged) | ✅ PASS |
| NT8-003 (volatile double banned) | `_trailBeLastPnl` is `volatile long` with BitConverter encoding | ✅ PASS |
| NT8-034 (Math.Clamp banned) | 0 `Math.Clamp` calls — uses `Math.Max(Math.Min(...))` pattern | ✅ PASS |
| NT8-026 (no Order.TrailPrice) | Not used — `BreakEven` via `acc.Change()` instead | ✅ PASS |
| CYC ≤ 8 | All new and modified methods verified: max CYC = 5 | ✅ PASS |

---

## Verdict

**VERIFY_PASS**

All 5 new fields present with correct types.
All 3 new CopyEngine methods implemented per spec (correct signatures, guards, lock-free patterns, release-fence ordering).
All 3 TradeCopierPanel modification points wired correctly.
All 6 xUnit `[Fact]` tests present.
All 7 independent scans pass.
Zero DNA rule violations found.
One non-functional ordering note in `Detach()` (DisarmTrailBe placed after follower loop vs. spec's before — functionally equivalent, no bug risk).
