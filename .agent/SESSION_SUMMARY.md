# Session Summary: Jan 27, 2026 - Debugging Execution Freezes

## Overview
Investigated reports of NinjaTrader 8 "freezing" or locking up during trade execution on versions V8.28 and V8.29. 

## What was Tested/Analyzed
- Analyzed `UniversalORStrategyV8_29.cs` core execution loop and UI update logic.
- Compared version differences between V8.28 and V8.29.
- Reviewed stop management throttling (100ms) and UI refresh throttling (1000ms).

## Results and Observations
- **Execution Bottleneck**: The strategy uses `Calculate.OnPriceChange`, which processes every tick. This is likely the primary source of strain during high volatility.
- **Race Condition Risk**: The `UpdateStopOrder` logic relies on a `pendingStopReplacements` dictionary to manage unmanaged orders. In fast markets, there is a risk of deep recursion or race conditions if cancellations aren't handled perfectly.
- **UI Lag**: Despite throttling, the use of `Dispatcher.InvokeAsync` can still backup the UI thread if the strategy thread sends snapshots too rapidly during "tick storms."

## Next Planned Changes (for Sub-Agent)
- **Log Deep Dive**: Sub-agent to review NinjaTrader `trace` files to correlate freeze timestamps with specific code execution (e.g., stop updates or signal broadcasts).
- **Execution Hardening**: Consider moving non-critical calculations out of `OnBarUpdate` or adding further protection against "Collection modified" errors during UI snapshots.

## Risks or Concerns
- **Critical Level**: Execution freezes are high-risk as they leave trades unprotected.
- **Apex Compliance**: Frequent order cancellations could trigger rate-limiting or compliance flags if not handled cleanly.

**Next Step**: Run the provided Opus CLI prompt to perform the automated log analysis and final bug fix.
