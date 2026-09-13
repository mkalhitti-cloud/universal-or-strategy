# PTT / V12 Rules Catalog
# Single authoritative reference for all Jane Street rules, NT8 API constraints, and compiler rules.
# All ptt-orchestrator prompts and ptt-* agent sessions MUST reference this file.
# Last updated: 2026-09-11 (assembled from dna.md, codex_rules.md, CYC_METHODOLOGY.md,
#   JANE_STREET_BASELINE_AUDIT.md, and empirically confirmed PTT pipeline artifacts
#   Lanes A/B/C — PTT-BUGS-LIVE-0911).

---

## Section 1 — Jane Street Rules (JS-XXX)

Rules are classified by severity:
- **P0 (CRITICAL)**: Hard block. Any violation fails REVIEW_PASS, VERIFY_PASS, and FINAL_PASS gates.
- **P1 (HIGH)**: Warning. Must be resolved before FINAL_PASS unless explicitly waived with documented rationale.
- **P2 (MEDIUM)**: Info. Technical debt; tracked in 06-deferred-backlog.md.

---

### JS-001 — No throw in hot paths / dispatch / gate / event-handler methods (P0)

Never use `throw` or `throw new` inside:
- Order dispatch methods (`DispatchCopy`, `SendCopy`, `SendCopyWithAtm`, `FlattenOneAccount`, etc.)
- Gate/guard methods (`FindFollowerBracketOrder`, `TryMarkFollowerSlateNotReady`, etc.)
- NT8 event handlers (`OnOrderUpdate`, `OnFollowerPositionUpdate`, `PositionUpdate`, etc.)

**Correct pattern**: early `return` (void methods) or return a nullable/default value.

**Wrong**:
```csharp
if (order == null) throw new ArgumentNullException(nameof(order));
```
**Correct**:
```csharp
if (order == null) return;
```

---

### JS-002 — No unexpected null return where a value is contractually expected (P0)

If a method signature promises a non-null return, it must not silently return null.
Prefer nullable return types (`Order?`, `string?`) so the contract is explicit.
All void helper methods must return void — not secretly rely on null propagation.

---

### JS-003 — No magic string for discriminated state (P0)

Do not use string literals as discriminators for state machines or type tags.
Use `enum`, `bool`, or `readonly struct` discriminated unions instead.

---

### JS-008 — No mutable fields on structs; no unfrozen SolidColorBrush (P1)

Structs must be immutable (all fields `readonly`).
`SolidColorBrush` instances used as WPF resources must be `.Freeze()`d before assignment.

---

### JS-009 — No plain `Dictionary<K,V>` for any shared or thread-touched state (P1)

Any collection read or written from more than one thread must use `ConcurrentDictionary<K,V>`.
Plain `Dictionary` is permitted only for collections that are exclusively written once
(e.g. construction time) and never mutated again.

---

### JS-010 — No public constructor on singleton or signal struct (P1)

`CopyEngine` is a singleton. Its constructor must not be public.
Signal/value structs used across dispatch boundaries must not expose public constructors
that allow construction outside of the designated factory path.

---

### JS-013 — Cyclomatic Complexity (CYC) limit: 8 per method (P1)

**Limit**: CYC ≤ 8 per method. Exceeding this is a hard P1 failure.

**Counting standard (Manual McCabe — authoritative)**:
- Start at 1 (base).
- Add +1 for each: `if`, `else if`, `case`, `while`, `for`, `foreach`, `catch`, `?:`, `??`, `&&`, `||`.
- Expression-body (`=>`) methods: count boolean operators (`||`, `&&`) as decision points.

**Lizard tool** output is for trend tracking only. Compliance comments (`// CYC=N`) in source
use manual McCabe and are the authoritative gate values.

**If a method exceeds CYC=8**: split it. Extract sub-methods with descriptive names.
New sub-methods must themselves satisfy CYC ≤ 8.

**Special constraint — `DispatchCopy` (CopyEngine.cs)**:
`DispatchCopy` is currently at CYC=8 (the limit). Any new conditional logic for this method
MUST be placed inside `TryLaneDHook` or another extracted helper — NOT added inline in
`DispatchCopy`. An `if` at the call site would raise CYC to 9 (hard fail).

See also: `docs/standards/CYC_METHODOLOGY.md` for Lizard vs. McCabe detail.

---

### JS-021 — No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` for shared state (P0)

`lock(stateLock)` blocks are **STRICTLY BANNED** everywhere in `src/PropTraderTools/`.

**Banned**:
```csharp
lock (_stateLock) { ... }
Monitor.Enter(...);
Mutex m = new Mutex();
SemaphoreSlim s = new SemaphoreSlim(1, 1);
```

**Correct patterns**:
- `ConcurrentDictionary<K,V>` for shared key-value state (see JS-025).
- `Interlocked.CompareExchange` / `Interlocked.Exchange` for atomic scalar updates.
- `ConcurrentQueue<T>` for producer-consumer queues.
- FSM/Actor `Enqueue` model for ordered state mutations.

**Verification scan** (must return 0 executable `lock()` statements):
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('
```
Note: grep hits inside `// comments` are acceptable and not violations.

---

### JS-023 — No UI update from off-thread without `Dispatcher.InvokeAsync` (P0)

NT8 dispatches `OrderUpdate`, `PositionUpdate`, and other account events on background threads.
Any WPF element update (TextBlock, Button, Panel, etc.) from inside an NT8 event handler
MUST be wrapped in `Dispatcher.InvokeAsync(...)`.

**Wrong**:
```csharp
private void OnFollowerPositionUpdate(object s, PositionEventArgs e)
{
    _myLabel.Content = "flat"; // crash: off-thread WPF update
}
```
**Correct**:
```csharp
private void OnFollowerPositionUpdate(object s, PositionEventArgs e)
{
    Dispatcher.InvokeAsync(() => _myLabel.Content = "flat");
}
```

State writes (to `ConcurrentDictionary`, `Interlocked`, etc.) do NOT need `Dispatcher` —
only WPF element mutations do.

---

### JS-025 — Use `ConcurrentDictionary` as the lock-free shared-state pattern (P1)

Any `Dictionary<K,V>` or `HashSet<T>` read or written from NT8 background threads
must be replaced with `ConcurrentDictionary<K,V>`.

Preferred initializer for string keys:
```csharp
private readonly ConcurrentDictionary<string, bool> _myState =
    new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
```

`StringComparer.Ordinal` is required for account-name keyed dictionaries (case-sensitive,
no culture overhead).

---

### JS-100 — No magic numbers (P1)

Numeric literals with domain meaning must be extracted to named constants.
```csharp
// Wrong
if (micros > 1000) ...

// Correct
private const int MicrosecondsPerMillisecond = 1000;
if (micros > MicrosecondsPerMillisecond) ...
```

---

## Section 2 — NT8 API Rules

### NT8-001 — No `async`/`await` in NT8 lifecycle or event-handler methods (P0)

NT8 lifecycle methods (`OnStateChange`, `OnInitialize`, `OnDestroyed`, `OnWindowCreated`,
`OnWindowDestroyed`) and all NT8 event handlers (`OnOrderUpdate`, `PositionUpdate`, etc.)
MUST be synchronous `void`. The NT8 dispatcher does not support `async void` safely.

**Verification scan**:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b|\bawait\b'
```
All hits must be inside `// comments` only. Zero hits in executable code.

---

### NT8-002 — No `Account.All` access outside `Loaded`/`Subscribe` context (P0)

`Account.All` may only be enumerated inside `Subscribe()`, `Unsubscribe()`, or methods
called directly from the `OnStateChange(State.Loaded)` path. Accessing `Account.All`
from a constructor, static initializer, or background thread is undefined behavior in NT8.

---

### NT8-003 — `CreateOrder` order name must carry the `"PTT-"` prefix (P0)

All `account.CreateOrder(...)` calls in the PTT dispatch path must use order names
prefixed with `"PTT-"` (e.g. `"PTT-Copy"`, `"PTT-Stop"`, `"PTT-Target"`).
**Exception**: `StartAtmStrategy` entry orders must use the name `"Entry"` (see NT8-007).

---

### NT8-004 — No `FontFamily` override in WPF controls (P1)

Do not set `FontFamily` on any WPF element in `TradeCopierPanel.cs` or
`TradeCopierWindow.cs`. NT8 has known rendering issues with custom font families.

---

### NT8-005 — No `#RRGGBB` hex color strings (P1)

Do not use hex color literals (e.g. `#FF0000`) in WPF markup or code-behind.
Use NT8 `Brushes.*` constants or pre-defined `SolidColorBrush` fields instead.

---

### NT8-006 — No `DateTime.Now`; use `DateTime.UtcNow` (P1)

`DateTime.Now` is timezone-sensitive. All timestamps in PTT must use `DateTime.UtcNow`.

---

### NT8-007 — `StartAtmStrategy` — AddOnBase available, STATIC form only (P0)

**Available in AddOnBase**: YES. Confirmed NT8_FULL_REFERENCE.md lines 2245–2248.

**Call form** (STATIC — do not use instance form):
```csharp
// Preferred: object overload (when AtmObject is not null)
NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(AtmStrategy atmObj, Order entryOrder);

// Fallback: string overload
NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(string templateName, Order entryOrder);
```

**Order name requirement**: The `Order` passed to `StartAtmStrategy` MUST have the name
`"Entry"`. Creating it with any other name will silently fail to arm ATM brackets.
```csharp
// Correct: entry order for ATM arming
account.CreateOrder(instrument, OrderAction.Buy, OrderType.Market, qty, 0, 0, "", "Entry");
```

**Null guard** (mandatory before every call):
```csharp
if (namedMode.AtmObject != null)
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, entryOrder);
else
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmTemplateName, entryOrder);
```

See `SendCopyWithAtm` (CopyEngine.cs lines ~5142–5151) for the established project pattern.

---

### NT8-008 — `AtmStrategyCreate()` — BANNED in AddOnBase (P0)

`AtmStrategyCreate()` is a StrategyBase-only API. It does not exist in the AddOnBase
context. **Do not use under any circumstances in PTT code.**

---

### NT8-009 — `AtmStrategyChangeStopTarget()` — BANNED in AddOnBase (P0)

`AtmStrategyChangeStopTarget()` is a StrategyBase-only API. **BANNED in AddOnBase.**
The correct pattern for changing ATM bracket prices in AddOnBase is:
`account.Cancel()` + `account.CreateOrder()` + `Submit()`.

---

### NT8-010 — `Account.Change()` is a silent no-op on ATM-owned brackets (P1)

`Account.Change()` is available in AddOnBase but silently does nothing when called on
orders that belong to an NT8 ATM strategy. Do not rely on it for bracket price changes.
Use the Cancel + CreateOrder + Submit pattern instead.

---

### NT8-011 — `account.PositionUpdate` — AddOnBase available (informational)

`account.PositionUpdate` event subscription is available in AddOnBase.
Subscribe in `Subscribe()`, unsubscribe in `Unsubscribe()` — always paired.
Handler must be synchronous `void` (NT8-001).

---

### NT8-043 — Null-conditional event unsubscription crash (WATCH — P2)

`?.Event -=` syntax on NT8 event delegates may cause a silent runtime crash under
NT8's Roslyn compiler. Status: WATCH (empirically suspected, not confirmed).
All current PTT code uses unconditional `-=` (SCAN-07 = 0 null-conditional unsubscriptions).
Promote to P0 CONFIRMED only after explicit runtime crash confirmation.

---

## Section 3 — ASCII / Encoding Rules

### ASCII-001 — ASCII-only in all C# string literals and comments (P0)

Zero non-ASCII bytes are permitted anywhere in `src/PropTraderTools/*.cs`.

**Banned characters** (non-exhaustive): Unicode letters, emoji, curly quotes (`""`),
em-dashes (`—`), en-dashes (`–`), ellipsis (`…`), non-breaking spaces.

**Allowed**: straight ASCII printable characters (0x20–0x7E), newlines (0x0A), carriage
returns (0x0D), tabs (0x09).

**Verification scan**:
```powershell
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') |
    Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
# Must return 0
```

---

## Section 4 — Mandatory 7-Scan Protocol (Ph4b Verifier)

Every VERIFY_PASS artifact must include independent results for all 7 scans.
Zero deviations are required unless explicitly documented with acceptance rationale.

| Scan | Command / Method | Required Result |
|------|-----------------|-----------------|
| SCAN-01 | `Select-String '^\s+lock\s*\(' CopyEngine.cs` | 0 executable `lock()` statements |
| SCAN-02 | Manual McCabe count of all new/modified methods | All ≤ CYC 8 |
| SCAN-03 | `Select-String '\basync\b\|\bawait\b' CopyEngine.cs` | 0 in new/modified executable code |
| SCAN-04 | PowerShell byte scan for non-ASCII | 0 bytes > 127 |
| SCAN-05 | `Select-String 'AtmStrategyCreate\|AtmStrategyChangeStopTarget'` | 0 actual calls (comments only) |
| SCAN-06 | `dotnet build` + `dotnet test` | 0 errors, 0 test failures, 0 regressions |
| SCAN-07 | Structural: verify every new call site, field, guard at reported line numbers | All items confirmed |

**L2 / L3 independence requirement**: Engineer (L2) runs scans and self-reports.
Verifier (L3) re-runs every scan independently. Any discrepancy between L2 and L3
results is a VERIFY_FAIL until reconciled.

---

## Section 5 — Pipeline Gates

All PTT pipeline phases are mandatory. No phase may be skipped or combined.

```
Ph1  ptt-architect       → 02-architecture-plan-lane-X.md
Ph2  ptt-plan-reviewer   → 02-plan-review-lane-X.md        [REVIEW_PASS gate]
Ph3  ptt-architect       → 04-tickets-lane-X.md
Ph3.5 ptt-ticket-reviewer → 04-ticket-review-lane-X.md     [TICKET_REVIEW_PASS gate]
Ph4a ptt-engineer        → src edits + ticket-N-completion-lane-X.md
Ph4b ptt-verifier        → ticket-N-verification-lane-X.md [VERIFY_PASS gate]
Ph5  ptt-plan-reviewer   → 05-final-review-lane-X.md
                           + append to 06-deferred-backlog.md
```

**SRC CODE BAN**: `ptt-engineer` is the ONLY role permitted to edit `.cs` files,
and only during Ph4a. No other role may touch source files under any circumstances.
"Add it" always means "add it to the spec HTML" — never "edit src code".

---

## Section 6 — Post-Edit Deploy Protocol

After every `src/` edit in Ph4a, before notifying the orchestrator:

```powershell
powershell -File .\deploy-sync.ps1
```

Required output (all 3 gates must pass):
```
ASCII GATE PASS - all source files are clean
DIFF GUARD PASS: Diff size (N chars) is within limits.
SOVEREIGN AUDIT PASS: Architectural integrity verified.
SYNC COMPLETE: One Source of Truth Established
```

Exit code 1 from Droid auth stderr is a known non-failure and does not affect gate status.
Only the three gate lines above determine pass/fail.

---

## Section 7 — Deferred Work Protocol

Every Ph5 final review MUST append a new block to `docs/brain/PTT-BUGS-LIVE-0911/06-deferred-backlog.md`.

Required section format:
```markdown
## Block [BLOCK-ID] — [Lane Name] ([short description])

**Block ID:** ...
**Status:** COMPLETE
**Phase 5 Reviewed:** YYYY-MM-DD
**Final verdict:** FINAL_PASS

### Block Summary
[what was built]

### [Prior block] Items Carried Forward
[table of all prior OPEN items, status unchanged]

### [This block] Items (New This Block)
| ID | Item | Priority | Target Block | Status |
```

All prior OPEN items must be carried forward in every block. Items are never silently dropped.
Items are closed only when the implementation explicitly addresses them.

---

*Assembled 2026-09-11 from: .bob/rules-v12-engineer/dna.md, docs/brain/codex_rules.md,
docs/standards/CYC_METHODOLOGY.md, docs/brain/JANE_STREET_BASELINE_AUDIT.md,
PTT-BUGS-LIVE-0911 Lanes A/B/C verified artifacts.*
