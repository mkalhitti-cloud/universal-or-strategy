# Ticket 3 Completion: B193 Spec HTML Update

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Ticket**: T3 -- Spec HTML Update
**Engineer**: ptt-engineer
**Date**: 2026-10-06
**File modified**: specs/002-trade-copier-spec.html

---

## Scope Confirmation

TICKET 3 ONLY. No other tickets or files touched in this session.

---

## Updates Applied (1-6)

### Update 1: Button Label Table
**Section found**: B41 scope table (line 18862-18868) plus new B193-DIRECT-3 rename reference table (new section starting ~line 42068).

**Changes made**:
- B41 scope table cell: `[Quick ▲▼]` updated to `[3x ▲▼]` (line 18862)
- B41 scope table cell: `[Quick ALL]` updated to `[3x All]` (line 18868)
- New button label rename reference table added in B193 block with all 4 rows:
  - Quick -> 3x (_quickBtn / FormatBuffer)
  - Quick2t -> 2x (_instr2tBtn / FormatBuffer)
  - Quick ALL -> 3x All (_quickAllBtn / FormatQuickAllBuffer)
  - QAll2t -> 2x All (_instrQAll2tBtn / FormatBuffer)
- Historical references in B122/B132/B190 blocks preserved unchanged (not touched)

### Update 2: B193 Block History Row
**Section found**: New Block History table created (no pre-existing block history table found in spec).
**Anchor**: `id="section-block-history"` at end of file.

**Row added**:
- Block: B193
- Date: 2026-10-06
- Summary: Formalize B193-DIRECT-1/2/3: per-panel _quickAllT1 field, singleton removed from Quick ALL display path, button labels renamed (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All)
- Status: COMPLETE

### Update 3: InstrumentDefaults Table Verification
**Section found**: New InstrumentDefaults verification table added in B193 block (~line 42107).

**All 5 rows verified present** (via InstrumentDefaults.GetQuickTicks() implementation in PttQuickExit.cs):
- ES: t1=4, t2=8 -- verified present (StartsWith "ES" falls into MES prefix default)
- NQ: t1=4, t2=8 -- verified present (StartsWith "NQ" falls into default (4,8))
- MNQ: t1=4, t2=8 -- verified present (StartsWith "MNQ" falls into default (4,8))
- GC: t1=10, t2=20 -- verified present (StartsWith "GC" handled by GC check)
- MGC: t1=10, t2=20 -- verified present (StartsWith "MGC" returns (10,20))

All rows documented in table as "verified present".

### Update 4: Per-Panel _quickAllT1 Field in SetInstrument() AC Table
**Section found**: New SetInstrument() AC table added in B193 block (~line 42157).
**Anchor**: `card-title "SetInstrument() Acceptance Criteria -- per-panel _quickAllT1 (B193-DIRECT-2)"`

**AC rows added**:
- AC-B193-1: _quickAllT1 is per-panel instance field (not singleton)
- AC-B193-2: SetInstrument() sets _quickAllT1 from InstrumentDefaults.GetQuickTicks().t1; 3x All button uses FormatQuickAllBuffer; 2x All button uses FormatBuffer; independent of GlobalQuickAllT1
- AC-B193-3: No cross-panel stomp on _quickAllT1 field

### Update 5: DW-B192-SINST-01 Open Card
**Section found**: New DW card added in B193 block (~line 42200).
**Content**:
```
DW-B192-SINST-01 (P2, OPEN): SetInstrument() in TradeCopierPanel.cs is at CYC=8
(JS-066 ceiling). Any branch addition to SetInstrument() without prior helper extraction
is an automatic P1 JS-066 violation. Engineers modifying SetInstrument() MUST extract
a helper method first (same pattern as DW-B183-03 for IsLaneEEligible).
Not blocking; CYC is at limit, not over it.
```

### Update 6: DW-B192-QALL-RESET-01 Conditional Close
**Search result**: DW-B192-QALL-RESET-01 NOT FOUND in existing spec (pre-B193 the card was absent).

**Action taken** (per ticket: "if not found, add a note in the DW section"):
- Added new closed DW card in B193 block documenting:
  `DW-B192-QALL-RESET-01: CLOSED -- per-panel _quickAllT1 eliminates singleton state reset concern`
- Resolution note: Closed by B193-DIRECT-2; card was not previously present in spec; added and closed per B193-T3 Section 6.

---

## 7-Scan Checklist

| Scan | Applicability | Result |
|------|--------------|--------|
| SCAN-01 CYC | N/A -- HTML file has no cyclomatic complexity | PASS (N/A) |
| SCAN-02 lock() | N/A -- HTML file | PASS (N/A) |
| SCAN-03 async void | N/A -- HTML file | PASS (N/A) |
| SCAN-04 return null | N/A -- HTML file | PASS (N/A) |
| SCAN-05 throw new | N/A -- HTML file | PASS (N/A) |
| SCAN-06 ASCII | MANDATORY -- all newly added HTML text content | **PASS** |
| SCAN-07 JS-033 | N/A -- HTML file | PASS (N/A) |

**SCAN-06 ASCII detail**: Scanned all new content added after line 42060 (the B193 block and Block History section). Result: 0 non-ASCII characters in any newly added text node or visible content. Pre-existing U+25B2/U+25BC (triangle arrows) in B41 cell were not added by this ticket -- they were already present. The two HTML comment decorators initially used box-drawing chars (U+2550) were corrected to ASCII `==` pattern.

**New text verified ASCII-only**:
- "B193", "2026-10-06", "3x", "2x", "3x All", "2x All"
- "Formalize B193-DIRECT-1/2/3", "_quickAllT1", "DW-B192-SINST-01"
- "SetInstrument()", "CYC=8", "InstrumentDefaults.GetQuickTicks()"
- "ES", "NQ", "MNQ", "GC", "MGC", "verified present"
- "DW-B192-QALL-RESET-01", "CLOSED", "OPEN", "COMPLETE"
- "AC-B193-1", "AC-B193-2", "AC-B193-3"

All 7-bit ASCII. PASS.

---

## Manual 6-Section Review

| Section | Verified | Result |
|---------|----------|--------|
| Section 1: Button label rename table | Old Label / New Label columns present with all 4 renames. B41 table cells updated. Historical refs preserved. | PASS |
| Section 2: B193 block history row | Row present in Block History table with B193, 2026-10-06, full summary, COMPLETE status | PASS |
| Section 3: InstrumentDefaults table | All 5 rows (ES, NQ, MNQ, GC, MGC) with correct t1/t2 values and "verified present" status | PASS |
| Section 4: SetInstrument() AC | 3 AC rows covering per-panel field, SetInstrument reset behavior, independence from singleton | PASS |
| Section 5: DW-B192-SINST-01 open card | Card present with P2 priority, CYC=8 ceiling warning, engineer guidance for helper extraction | PASS |
| Section 6: DW-B192-QALL-RESET-01 | Card not found in pre-B193 spec; note added and marked CLOSED with resolution note per ticket instructions | PASS |

All 6 sections: **PASS**

---

## HTML Validity Check

- No unclosed tags introduced
- All new `<div>`, `<table>`, `<tr>`, `<td>`, `<th>` elements properly opened and closed
- New B193 block structure matches existing B190 block pattern
- `</body></html>` preserved at end of file

---

## BUILD_PASS

All 6 spec updates applied. All 7 scans pass (SCAN-06 ASCII verified clean; others N/A for HTML). Manual 6-section review: all PASS. HTML remains well-formed.
