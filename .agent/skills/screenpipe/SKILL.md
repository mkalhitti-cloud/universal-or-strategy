---
name: screenpipe
description: AI screen and audio memory. Allows agents to query the user's screen history, see the current screen (including Remote Desktop windows), and search transcribed audio. Useful for seeing NinjaTrader charts on the GCP VM without API hooks.
allowed-tools: Bash(npx screenpipe-mcp), Bash(curl localhost:3030/*)
---

# Screenpipe Agent Integration

Screenpipe continuously captures the user's screen and audio into a local database.

## How Agents Use It

Agents can access the user's screen or history in two ways:

1. **REST API**: Screenpipe runs a local server on port `3030`.
   - `curl "http://localhost:3030/search?content_type=ocr&limit=5"` to get recent text seen on screen.
   - `curl "http://localhost:3030/search?content_type=audio&limit=5"` to get recent transcribed speech.

2. **MCP Server**: 
   - Start the MCP server using: `npx -y @screenpipe/mcp`
   - This provides tools to search screen OCR data natively.

## How to Start / Stop

- **Start**: The user must run `npx screenpipe@latest record` in a background terminal.
- **Stop/Disable**: Simply close the terminal running the `screenpipe record` process.
