TICKET: 7
FINDINGS: C16
FILE: src/PropTraderTools/AtrSizingEngine.cs
GUARD PLACED AT: OnStateChange -> DataLoaded branch, line 71 (immediately after the NT8 Add() constraint comment)
  `if (Period < 1) Period = 1; // C16: clamp Period to minimum of 1 (ATR requires Period >= 1)`
BUILD: 0 errors (Linting.csproj, 4576 pre-existing SA warnings, 0 new errors)
LIZARD CCN>8: none (OnStateChange CCN=6, all other methods <= 4)
LOCK SCAN: 0
ASCII SCAN: 0
THROW SCAN: 0
STATUS: BUILD_PASS
