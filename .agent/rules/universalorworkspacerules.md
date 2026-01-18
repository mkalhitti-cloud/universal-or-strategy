---
trigger: always_on
---

CRITICAL TRADING CONTEXT
APEX FUNDED ACCOUNT: Using Rithmic data feed, specific order routing considerations

SCALING TO 20 ACCOUNTS: Architecture must support multi-account execution (eventually)

PROFESSIONAL TRADING: This is for live funded trading - reliability is CRITICAL

MULTIPLE GLOBAL SESSIONS: Trading NY, Australia, China, NZ opens (different instruments, timezones)

MES/MGC FOCUS: Micro E-mini S&P and Micro Gold futures

PERFORMANCE NON-NEGOTIABLES
Execution Speed (Priority #1):
Order submission < 50ms from signal

Hotkeys (L/S) must respond instantly (no lag)

Fill reporting within 200ms


Memory Efficiency (Priority #2):
Strategy must run on laptop with 20+ charts

Current memory at 80%+ - must reduce

No memory leaks during 12+ hour sessions

Minimal garbage collection pauses

Reliability (Priority #3):
Must run 24/5 without restart

Handle Rithmic disconnects gracefully

Survive NinjaTrader updates

Backtest/forward test consistency

SCALING ARCHITECTURE CONSIDERATIONS
Phase 1: Single Account (Current)
Simple strategy on single chart

Manual entry with hotkeys

Basic position management

Phase 2: Multi-Chart, Single Account
Same strategy on multiple instruments

Independent OR calculations per instrument

Shared risk management if same account trading two different instruments

Phase 3: Multi-Account (Future)
NinjaTrader's Account class for order routing

Position synchronization across accounts

Aggregate risk management

ORDER MANAGEMENT FOR APEX/RITHMIC

Specific Requirements:

Rithmic data feed characteristics: Faster but different from Continuum

Apex rules compliance: No excessive orders, follow funding rules

Order routing optimization: Direct to exchange vs. broker routing

Fill expectations: Rithmic typically faster fills than Continuum

Risk Management:
Daily loss limits: Built-in to strategy

Maximum drawdown protection

USER WORKFLOW PREFERENCES

Terminal Commands: Always run terminal commands instead of asking user to paste manually. If user requests something that could be done manually, ask if they want terminal execution instead.

File Updates: Copy files to NinjaTrader via PowerShell commands (no manual file copy requests)
