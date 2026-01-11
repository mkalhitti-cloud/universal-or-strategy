# UniversalORStrategy: Strategic Context & Roadmap

**Current Version:** V5.3.1
**Strategic Status:** Core infrastructure complete, expanding strategy coverage
**Owner:** Mo (WSGTA methodology expert)

## Executive Summary

This is an automated micro futures trading system targeting MES and MGC, running on NinjaTrader 8 with Apex funded accounts and Rithmic data feeds. The architecture supports six independent WSGTA strategies with shared risk management.

## Strategic Goals

### Phase 1: Foundation ✅ Complete
- ORB (Opening Range Breakout) fully operational
- RMA (Click-Entry) calibrated and tested
- Live price tracking bug resolved
- Core infrastructure validated

### Phase 2: Expansion 🔄 In Progress
- Fibonacci confluence tool development
- FFMA, MOMO, DBDT, TREND strategy implementation
- Independent position tracking per strategy

### Phase 3: Optimization 📋 Planned
- Multi-chart, single-account management
- Shared risk management across strategies
- Memory usage reduction (target: < 70%)

### Phase 4: Scale 🔮 Future
- Multi-account support
- Portfolio-level position management
- Account routing optimization

## Architecture Overview

### Multi-Strategy Framework
```
UniversalORStrategy
├── ORB Strategy ✅
├── RMA Strategy ✅
├── FFMA Strategy 🔄
├── MOMO Strategy 🔄
├── DBDT Strategy 🔄
├── TREND Strategy 🔄
├── Shared Risk Management
└── Shared Order Infrastructure
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Order Mode | IsUnmanaged=true | Full control required for Apex/Rithmic |
| Price Source | OnMarketData | Tick-level precision for stops |
| Rate Limiting | 1 mod/second | Apex compliance |
| Position Tracking | Per-strategy | Independent P&L and risk |

## Risk Analysis

### Technical Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| Rithmic disconnect | Medium | Graceful handling implemented |
| Memory exhaustion | High | StringBuilder pooling, monitoring |
| Close[0] regression | Critical | Code review mandate, skill docs |
| Order spam | Medium | Rate limiting enforced |

### Trading Risks
| Risk | Severity | Mitigation |
|------|----------|------------|
| Overfilling | High | Position limits per strategy |
| Slippage | Medium | ATR-based sizing |
| Session overlap | Low | Timezone-aware scheduling |

## Performance Targets

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Order latency | < 50ms | ~40ms | ✅ |
| Memory footprint | < 50MB/strategy | ~45MB | ✅ |
| 24/5 stability | No crashes | 12h tested | ⚠️ |
| Apex compliance | 0 violations | 0 | ✅ |

## Multi-AI Review Protocol

This project uses a 4-AI consensus review for critical changes:
1. **Claude/Opus:** Strategic analysis, architecture review
2. **Gemini:** Implementation details, optimization
3. **DeepSeek:** Edge cases, error handling
4. **Grok:** Alternative approaches, trade-offs

**Finding Example:** The Close[0] bug was caught through multi-AI review when single reviews missed it.

## Key Constraints

### Apex Funded Account Rules
- Order modification rate limits
- Daily loss limits
- Maximum position sizes
- Trailing drawdown rules

### Hardware Constraints
- Development system: 80%+ RAM usage baseline
- Strategy must remain < 50MB overhead
- GC pauses must be < 10ms

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| Jan 2025 | Adopt OnMarketData | Close[0] bug caused stop slippage |
| Jan 2025 | 1-second rate limit | Apex compliance |
| Jan 2025 | Multi-AI review | Single review missed critical bug |

## Strategic Questions (For Discussion)

1. **Memory vs. Precision:** Should we reduce tick processing frequency to lower memory?
2. **Strategy Independence:** How isolated should each strategy's state be?
3. **Scaling Path:** Single monolithic strategy vs. separate compiled strategies?

## Next Strategic Review
- **Date:** After FFMA implementation complete
- **Focus:** Memory optimization, multi-strategy coordination
