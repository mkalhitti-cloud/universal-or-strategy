# B182 Plan Review
## Ph2 Output -- ptt-plan-reviewer
### Date: 2026-09-28
### Gate: REVIEW_PASS

---

## Review Checklist

### R1 -- Baseline State Verification
- [x] Direct repair source confirmed: `PttBreakEvenSwap.cs:193` reads `newStop` (DW-B182-01).
- [x] Direct repair source confirmed: `PttBeSwapOne.cs:41` calls `SnapshotStopPrice` before `PttBreakEvenSwap.Execute` (DW-B182-02).
- [x] Direct repair source confirmed: `PttBeSwapAll.cs:56` calls `SnapshotStopPrice` before `PttBreakEvenSwap.Execute` (DW-B182-02).
- [x] Build baseline confirmed: 0 errors, 1 pre-existing warning (B131Tests.cs xUnit2004).

### R2 -- DW-B179 Design Review
- [x] Method name `TryEmitBeFlattenWarn` follows `Try*` naming convention matching existing helpers.
- [x] CYC=4: (1) state != Cancelled, (2) name prefix, (3) position qty, (4) emit. Within <= 8 limit.
- [x] Detection logic is correct: PTT-BE-* or PTT-QX-* cancel + position == 0 = flatten race.
- [x] `FindPositionPublic` reuse is correct -- same pattern used in `PttBreakEvenSwap.Execute`.
- [x] `pos != null && pos.Quantity > 0` returns early = position still open, not a flatten race. Correct.
- [x] `pos == null || pos.Quantity == 0` falls through to emit. Correct: position gone = flatten race.
- [x] Wiring: pre-Gate-1 placement (unconditional) matches `TryFireFollowerBeDisarm` pattern.
- [x] No armed-state machine required -- stateless detection. Compliant with B180 stateless mandate.
- [x] ASCII-only: `[BE-WARN]`, `flatten overtook BE All -- orders cancelled`. Verified.
- [x] JS-021: no lock. No new shared state introduced.
- [x] JS-033: synchronous void. Not async void.
- [x] OcoId-free: detection is by order Name prefix, not OCO ID. Resilient to format changes.

**CONCERN:** `PTT-QX-*` orders are also included in the warn path. Rationale: a QX fire followed
by immediate flatten also races. Including PTT-QX-* is correct for completeness and matches the
DW-B179 description of "any mid-flight PTT operation". ACCEPTED.

**CONCERN:** Position may be `null` (instrument removed from account before cancel arrives).
Current logic: `pos == null` falls through to emit. This is acceptable -- if position is null,
either the account is flat (correct to warn) or the instrument has been removed (benign extra warn).
The warn is informational and not a gate. ACCEPTED.

### R3 -- DW-B181-G5 Design Review
- [x] Method name `CheckAtmTemplatesForReverse` is descriptive and follows existing `Check*` pattern.
- [x] Overload design: public/internal overload `(string dirPath)` for test seam. Correct.
- [x] CYC=6: try(1), dir-not-found(2), foreach(3), already-warned(4), xml-check(5), emit(6). Within <= 8.
- [x] Fail-open: `Directory.Exists` guard + `catch(Exception)` wrap. No crash on missing dir.
- [x] `[PTT-STARTUP-INFO]` (not WARN) for missing dir -- correct: this is an expected state on first install.
- [x] `[PTT-STARTUP-WARN]` for Reverse=Yes -- correct severity.
- [x] `_warnedAtmTemplates HashSet<string>`: written only at `State.Configure` (NT8 single-threaded startup). No lock needed. Correct.
- [x] Wire-up at `State.Configure` after `SetFlags` call -- correct placement. Flags are set before the check.
- [x] File.ReadAllText + IndexOf: simple and sufficient. No XML deserialization complexity needed.
- [x] Case-insensitive: `StringComparison.OrdinalIgnoreCase`. Correct for XML attribute values.
- [x] `NinjaTrader.Core.Globals.UserDataDir` confirmed available from `TradeCopierAddOn.OnStateChange` at `State.Configure` (seen at line 675 in TradeCopierAddOn.cs).
- [x] ASCII-only strings confirmed in both log messages.
- [x] `using System.IO` already in CopyEngine.cs (confirmed `System.IO` namespace in using block).

**CONCERN:** `File.ReadAllText` loads entire XML into memory. ATM templates are small (< 10KB each).
This is not a hot path (once per startup). Performance is not a concern. ACCEPTED.

**CONCERN:** Inner iteration could throw (e.g., file permissions). The outer `catch(Exception)` wraps
the entire `foreach` block. This means a single permission error aborts all remaining files.
**RECOMMENDATION:** Move `File.ReadAllText` into its own try/catch so one unreadable file does not
block scanning others. This is a quality improvement, not a blocking issue. Escalate to Ticket 3
implementation note.

### R4 -- Test Strategy Review
- [x] Ticket 1 tests (4 facts): pure-math / structural. No NT8 runtime. All runnable in xUnit harness.
- [x] `BE_SubmitSwapPair_TargetPriceIsEntryNotOriginalAtm`: validates the arithmetic (newStop used for target). Correct.
- [x] `BE_SubmitSwapPair_StopPriceIsSnapshotNotEntry`: validates snapshotStop != newStop when they differ. Correct.
- [x] `BE_SubmitBareStopSwap_UsesSnapshotStop`: validates 0-targets path uses snapshotStop. Correct.
- [x] `BE_Execute_NoIsStopPriceSubmittableGuard`: structural check -- verifies the method signature has `snapshotStop` param and the old `IsStopPriceSubmittable` guard does not appear. Can be a code-path logic test. Correct.
- [x] Ticket 2 test `BE_Flatten_AfterBeAll_EmitsWarnLog`: Test seam via pure logic -- call `TryEmitBeFlattenWarn` directly using a fake `OrderEventArgs`... BUT `OrderEventArgs` is an NT8 type. Mark with `[Fact(Skip="NT8-runtime:...")]` if seam unavailable. This is consistent with B180Tests.cs pattern. ACCEPTED.
- [x] Ticket 3 test `StartupWarn_EmitsForReverseYesTemplate`: In-memory XML string passed to `CheckAtmTemplatesForReverse(string dirPath)` overload. This IS testable without NT8 runtime. Test creates a temp dir with a crafted XML file. ACCEPTED.

**NOTE:** Ticket 3 test requires `Directory.CreateTemp` + `File.WriteAllText` in test body. This
is acceptable for integration-style xUnit tests. Test cleanup needed (temp dir delete in finally block).

### R5 -- Jane Street Compliance
- [x] JS-001: no throw anywhere in new methods.
- [x] JS-002: no return null (all void).
- [x] JS-021: no lock (startup-only HashSet, Interlocked not needed).
- [x] JS-033: no async void.
- [x] CYC <= 8 for all new methods: CONFIRMED.
- [x] NT8-014: PTT-prefix convention followed.
- [x] ASCII-only: CONFIRMED.

### R6 -- Scope Guard
- [x] DW-B181-G3 (concurrent flatten test) is OUT OF SCOPE for B182. Deferred to B183.
- [x] DW-B181-G1/G2 (event burst / price divergence tests) are OUT OF SCOPE. Deferred to B183.
- [x] Ph4a engineer may NOT re-apply direct repair. Three files must be read-verified only.
- [x] ptt-sync.ps1 must run after every .cs edit.
- [x] dotnet build 0 errors required after each ticket.

---

## AMENDMENTS REQUIRED BEFORE Ph3

1. **Per-file try/catch in Ticket 3 (DW-B181-G5):** `CheckAtmTemplatesForReverse(string)` must wrap
   `File.ReadAllText` in its own try/catch so a single unreadable file does not abort the scan.
   Updated design in Ph3 04-tickets.md must reflect this.

2. **Ticket 3 test temp dir cleanup:** `StartupWarn_EmitsForReverseYesTemplate` must delete the
   temp directory in a `finally` block to avoid test pollution.

---

## GATE RESULT

```
REVIEW_PASS
Amendments: 2 minor (per-file catch in Ticket 3; temp dir cleanup in test).
Ph3 may proceed. Amendments must be reflected in 04-tickets.md.
```
