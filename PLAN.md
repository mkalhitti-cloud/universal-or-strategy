# Development Plan

## Current Phase: V4 Development

### V4 Goals (In Progress)
- [x] Create GitHub repository structure
- [x] Archive V2 and V3 versions
- [x] Create documentation files
- [ ] Implement box visualization (replacing rays)
- [ ] Session-based time properties
- [ ] Timeframe dropdown (1, 5, 10, 15 min)
- [ ] RAM optimization pass
- [ ] Testing and validation

---

## Phase 1: Single Account (Current - V4)

### Objectives
- Stable, RAM-efficient strategy for single account trading
- Manual entry with hotkeys and UI buttons
- Support for multiple global sessions

### Features
- [x] Opening Range detection
- [x] Multi-target profit system
- [x] Trailing stop management
- [x] Risk-based position sizing
- [ ] Box visualization (V4)
- [ ] Session/timeframe configuration (V4)
- [ ] Memory optimization (V4)

### Testing Checklist
- [ ] NY Open (MES) - 9:30 ET
- [ ] London Open (if applicable)
- [ ] Asia sessions (China, Australia, NZ)
- [ ] 12+ hour session stability
- [ ] Memory usage monitoring
- [ ] Rithmic disconnect recovery

---

## Phase 2: Multi-Chart, Single Account

### Objectives
- Run same strategy on multiple instruments simultaneously
- Independent OR calculations per chart
- Shared risk management across instruments

### Planned Features
- [ ] Instrument-specific parameters
- [ ] Global daily loss limit
- [ ] Aggregate position tracking
- [ ] Cross-instrument correlation awareness

### Technical Requirements
- Static risk manager class
- Event-based position updates
- Instrument identification system

---

## Phase 3: Multi-Account (Future)

### Objectives
- Scale to 20 funded accounts
- Synchronized order execution
- Per-account risk tracking

### Planned Features
- [ ] NinjaTrader Account class integration
- [ ] Account routing selection
- [ ] Per-account position limits
- [ ] Aggregate P&L dashboard

### Technical Requirements
- Account enumeration
- Order routing per account
- Position synchronization
- Latency optimization (<50ms execution)

---

## Performance Targets

### Execution Speed
| Metric | Target | Current |
|--------|--------|---------|
| Order submission | <50ms | TBD |
| Hotkey response | <20ms | TBD |
| Fill reporting | <200ms | TBD |

### Memory Efficiency
| Metric | Target | V3 Baseline |
|--------|--------|-------------|
| Strategy memory | <50MB | ~80MB |
| 12-hour session | Stable | Minor growth |
| 20 charts | <400MB total | Not tested |

### Reliability
| Metric | Target | Current |
|--------|--------|---------|
| Session uptime | 24/5 | Good |
| Disconnect recovery | Automatic | Manual |
| Order rejection rate | <1% | ~2% |

---

## Known Issues & Backlog

### High Priority
1. **RAM usage on long sessions** - V4 addresses with box visualization
2. **Ray memory accumulation** - V4 eliminates rays entirely
3. **Stop validation edge cases** - Monitoring in production

### Medium Priority
1. Global session presets (one-click configs for NY/London/Asia)
2. Sound alerts for entries/exits
3. Telegram/Discord notifications

### Low Priority
1. Automated entry mode (no manual trigger required)
2. Custom indicator integration
3. Backtesting optimization mode

---

## Testing Protocol

### Before Production
1. **Compile test** - No errors or warnings
2. **Historical backtest** - Verify signals match expected
3. **Replay test** - Test on Market Replay data
4. **Sim account test** - 1 hour live simulation
5. **Production deployment** - Single contract first

### After Changes
1. Save backup of working version
2. Test changes in isolation
3. Sim test before production
4. Monitor first session closely

---

## Session Configuration Presets (Planned)

### New York Open
- Session Start: 9:30 AM ET
- OR Timeframe: 5 minutes
- Primary Instrument: MES

### London Open
- Session Start: 3:00 AM ET
- OR Timeframe: 15 minutes
- Primary Instrument: 6E (Euro futures)

### China Open
- Session Start: 9:00 PM ET (day before)
- OR Timeframe: 15 minutes
- Primary Instrument: As applicable

### Australia Open
- Session Start: 6:00 PM ET (day before)
- OR Timeframe: 10 minutes
- Primary Instrument: As applicable

---

## Notes

- All times in Eastern Time (ET) unless specified
- Risk parameters should be reviewed weekly
- Backup strategy files before any changes
- Document all production issues for improvement
