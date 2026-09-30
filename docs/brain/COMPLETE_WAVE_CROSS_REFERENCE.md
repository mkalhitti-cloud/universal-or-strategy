# Complete Wave 6/7/8 Cross-Reference

**Generated**: 2026-06-18

---

## Executive Summary

### Baseline (CYC > 8)
- **Total Methods**: 180

### Wave 6 (EPIC-CCN-001 through 080)
- **Total Epics**: 80
- **Phase 0 Complete**: 80/80
- **Phase 1 Complete**: 1/80
- **Both Complete (READY)**: 1/80
- **Mapped Methods**: 0

### Wave 7 (Remaining Methods)
- **Total Methods**: 180
- **Status**: Ready for Phase 0 generation

### Wave 8 (Wave 6 + Wave 7)
- **Total Methods**: 180
- **Validation**: ✅ PASS

### Jane Street Violations
- **Total P0 Violations**: 299
- **In Wave 8 Files**: 174
- **NOT in Wave 8 Files**: 125

---

## Wave 6 Ready Epics (Phase 0 AND Phase 1 Complete)

| Epic ID | Method | File | CYC | Status |
|---------|--------|------|-----|--------|
| EPIC-CCN-003 | N/A | N/A | 0 | ✅ READY |

---

## Wave 6 Pending Epics (Phase 0 Complete, Phase 1 Pending)

**Count**: 79 epics

| Epic ID | Method | File | CYC |
|---------|--------|------|-----|
| EPIC-CCN-001 | N/A | N/A | 0 |
| EPIC-CCN-002 | N/A | N/A | 0 |
| EPIC-CCN-004 | N/A | N/A | 0 |
| EPIC-CCN-005 | N/A | N/A | 0 |
| EPIC-CCN-006 | N/A | N/A | 0 |
| EPIC-CCN-007 | N/A | N/A | 0 |
| EPIC-CCN-008 | N/A | N/A | 0 |
| EPIC-CCN-009 | N/A | N/A | 0 |
| EPIC-CCN-010 | N/A | N/A | 0 |
| EPIC-CCN-011 | N/A | N/A | 0 |

*... and 69 more*

---

## Wave 7 Methods (Not in Wave 6)

**Count**: 180 methods

| Method | File | CYC |
|--------|------|-----|
| GetSubscriberCounts | SignalBroadcaster.cs | 9 |
| ProcessSessionReset | V12_002.BarUpdate.cs | 11 |
| OnBarUpdate | V12_002.BarUpdate.cs | 10 |
| DrawORBox | V12_002.DrawingHelpers.cs | 12 |
| CheckFFMAConditions | V12_002.Entries.FFMA.cs | 16 |
| ExecuteFFMAManualMarketEntry | V12_002.Entries.FFMA.cs | 12 |
| ExecuteFFMALimitEntry | V12_002.Entries.FFMA.cs | 9 |
| ExecuteMOMOEntry | V12_002.Entries.MOMO.cs | 10 |
| EnterORPosition | V12_002.Entries.OR.cs | 11 |
| ExecuteRetestEntry | V12_002.Entries.Retest.cs | 12 |

*... and 170 more*

---

## Jane Street Integration Plan

### Violations in Wave 8 Files (174)

These violations are in files being refactored by Wave 8 and should be addressed during refactoring.

### Violations NOT in Wave 8 Files (125)

These violations are in files NOT being refactored by Wave 8 and require separate epics.

---

## Next Steps

1. Complete Wave 6 Phase 1 for remaining epics
2. Generate Wave 7 epic structure
3. Execute Wave 7 Phase 0
4. Integrate Jane Street violations into Wave 8 execution
5. Create separate epics for Jane Street violations NOT in Wave 8
