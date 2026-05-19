# V12 Sovereign Stack Registry

## Purpose

This document tracks the verified high-performance open-source technologies integrated into the Morpheus OS and Universal OR Strategy ecosystem.

## 👁️ Sovereign Vision & OCR

### [Tencent-Hunyuan OCR](https://github.com/Tencent-Hunyuan/HunyuanOCR)

- **Role:** High-precision, local-only document and screen parsing.
- **Spec:** 1B Parameter VLM.
- **Mission:** Enable agents to read NinjaTrader charts and brokerage statements with zero-API dependency.

## 🌐 Spatial Observability

### [HY-World-2.0](https://github.com/Tencent-Hunyuan/HY-World-2.0)

- **Role:** 3D World Reconstruction and Simulation.
- **Spec:** Generates 3DGS, meshes, and point clouds from single images/videos.
- **Mission:** Transform the Arena Dashboard into a navigable 3D Digital Twin of the trading infrastructure.

## ⚡ High-Performance Networking

### [AF_XDP (Zero-Copy)](https://github.com/xdp-project/xdp-tools)

- **Role:** OS-bypass for ultra-low latency data transfer.
- **Mission:** Reach the 92ns NanoFusion latency floor by eliminating kernel syscalls.

## 🏗️ Knowledge & Agent Logic

### [Graphify](https://github.com/mkalhitti-cloud/graphify)

- **Role:** 3D Knowledge Layer.
- **Mission:** Provide 71x token efficiency for agent repository navigation.

### [Agent Client Protocol (ACP)](https://github.com/mkalhitti-cloud/agent-client-protocol)

- **Role:** Standardized transport layer for agent-to-tool communication.
- **Mission:** Standardize the "Rail Network" for high-performance agent interaction, replacing ad-hoc tool calls with traceable RPC channels.

### [ACP Agent Registry](https://github.com/agentclientprotocol/registry)

- **Role:** Public CDN-served discovery index of ACP-compatible agents.
- **Spec:** `GET https://cdn.agentclientprotocol.com/registry/v1/latest/registry.json` — returns verified agents with npm/PyPI endpoints and auth methods.
- **Mission:** Resolve physical agent endpoints (Claude CLI, Codex, Gemini) from the public registry, feeding our Director's Gate harness selector with the correct binary target per session.
- **Key distinction:** Complements our harness selector — the registry resolves the _transport endpoint_, our harness enforces the _role and gate authority_ on top.

## ⚖️ Economic & Prediction Markets

### [Polymarket](https://polymarket.com/)

- **Role:** Direct API integration for hedging and event-driven arbitrage.
- **Mission:** Correlate NinjaTrader signals with global event probability to de-risk high-volatility trades.

### [Aave / DeFi Liquidity Pools](https://aave.com/)

- **Role:** Autonomous Investment Pool Management.
- **Mission:** Auto-deploy excess trading profits from RMA accounts into interest-bearing DeFi pools to build a "Sovereign Reserve."

### Auction & Kelashi Engine (Native)

- **Role:** Dynamic resource allocation and internal bid-ask matching.
- **Mission:** Use internal auctions (Kelashi style) to allocate high-performance compute time (NUMA nodes) among competing sub-strategies.

---

_Last Updated: 2026-04-18 17:08 UTC_
