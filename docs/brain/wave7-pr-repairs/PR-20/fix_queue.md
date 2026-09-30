# PR #20 Fix Queue — wave7/pr1-s2-execution
# S2 Execution Engine — 12 files
# Reviewers: Gemini, Sourcery, CodeAnt, Cubic

---

## [MECHANICAL] P0 — Orders.Management.StopSync.cs:763 DateTime.Now → UtcNow

**File**: `src/V12_002.Orders.Management.StopSync.cs`
**Line**: ~763
**Issue**: Latency calculation mixes timezones.
`pendingForLatency.CreatedTime` is set with `DateTime.UtcNow` (line ~414).
Latency calc uses `DateTime.Now` — on non-UTC systems this produces a
negative or inflated latency reading.
**Fix**: `DateTime.Now` → `DateTime.UtcNow`
**OKF**: how-to-build-an-exchange.md → `determinism`

---

## [MECHANICAL] P0 — Trailing.StopUpdate.cs:342 DateTime.Now → UtcNow

**File**: `src/V12_002.Trailing.StopUpdate.cs`
**Line**: ~342
**Issue**: `circuitBreakerActivatedTime = DateTime.Now` — circuit breaker
timeout comparisons elsewhere use UtcNow, producing timezone mismatch.
**Fix**: `DateTime.Now` → `DateTime.UtcNow`
**OKF**: how-to-build-an-exchange.md → `determinism`

---

## [MECHANICAL] P1 — Orders.Callbacks.cs:672 O(N) .Values.Contains() redundant

**File**: `src/V12_002.Orders.Callbacks.cs`
**Lines**: ~668-672
**Issue**: `stopOrders.Values.Contains(order)` is O(N) scan on a
ConcurrentDictionary. The helper `TryHandleRejectedStop` already does
`TryGetValue` internally — the outer Contains is redundant work.
**Fix**: Remove the `.Values.Contains()` guard. Let `TryHandleRejectedStop`
do its own TryGetValue check (it already does).

---

## [DNA] P1 — Orders.Callbacks.AccountOrders.cs: Unicode em dash in comments

**File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs`
**Lines**: 1069, 1085, 1097
**Issue**: 3× U+2014 em dash `—` in `// Extracted: Check N —` comments.
V12 DNA: ASCII-only. No Unicode in C# source.
**Fix**: Replace `—` with ` --` (double hyphen, ASCII).

---

## [DNA] P1 — Orders.Callbacks.AccountOrders.cs: underscore-prefix local var

**File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs`
**Line**: ~223
**Issue**: Local variable `_oqDepth` uses underscore prefix.
V12 DNA: camelCase locals only.
**Fix**: Rename `_oqDepth` → `oqDepth`.

---

## STATUS
- [ ] MECHANICAL: StopSync DateTime.Now → UtcNow
- [ ] MECHANICAL: Trailing.StopUpdate DateTime.Now → UtcNow
- [ ] MECHANICAL: Orders.Callbacks O(N) Contains removal
- [ ] DNA: em dash × 3
- [ ] DNA: _oqDepth rename
