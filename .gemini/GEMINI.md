# Gemini Preferences & Context

## Interaction Style
- Be concise, technical, and direct.
- Focus on NinjaTrader 8 (NT8) and C# optimization.
- Always check `skills/` before answering complex queries.

## Coding Standards
- **Price Updates:** Always use `OnMarketData` for live price tracking. Never use `Close[0]` for real-time logic.
- **Apex Compliance:** Strictly enforce rate limiting (max 1 order modification per second) and risk management rules.
- **Memory Efficiency:** Use `StringBuilder` for logging, avoid excessive object creation, and explicitly null out objects when done.
- **Error Handling:** robust handling for Rithmic data feed disconnects.
