# HOTFIX-B80-BE-RETRY-01 — Completion Report

**Date**: 2026-08-21
**Commit**: 04b3acfc
**Test count**: 295 [Fact] passing, 0 failed
**Build**: BYPASSED (Director authorized — pre-existing CS0234/CS0246 in AtrSizingEngine.cs due to NT8 assembly ref not available in dotnet build env; not caused by this hotfix)

## Edits Applied

### Edit 1 — DW-B80-01: QueueBeRetryFallback 200ms → 500ms
File: src/PropTraderTools/CopyEngine.cs
Line: 2787
Before: QueueBeRetryFallback(acc, instrument, bufferTicks);
After:  QueueBeRetryFallback(acc, instrument, bufferTicks, delayMs: 500);
CYC change: 0
Method: MoveStopToBreakEven (was CYC=6, still CYC=6)

### Edit 2 — DW-B80-02: TryAdd dedup in TryReplacePttBeBrackets
File: src/PropTraderTools/CopyEngine.cs
Lines: 1817-1818
Before: _pendingFollowerBeSlots.TryRemove(acc.Name, out _);
        _pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(acc, instr, 0);
After:  if (!_pendingFollowerBeSlots.TryAdd(acc.Name, new PendingFollowerBeSlot(acc, instr, 0)))
            return;
CYC change: +1
Method: TryReplacePttBeBrackets (was CYC=5, now CYC=6)

## Status
PIPELINE_COMPLETE (direct repair authorized by Director)
Director F5 + SIM test required before DW-B80-01/02 spec cards stamped CLOSED.
