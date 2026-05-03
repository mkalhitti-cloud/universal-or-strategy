---
name: antigravity-manager
description: AI orchestrator for managing Google/Anthropic sessions and model routing.
---

# Antigravity Manager

Local AI relay station for managing Google/Anthropic sessions and orchestrating multi-agent workflows.

## Core Features

- **Model Routing**: OpenAI-compatible, Anthropic-native, and Gemini-native endpoints.
- **Account Management**: Batch import (JSON) and quota monitoring.
- **CLI Connection**: Bridges Claude Code, Cursor, and other IDE-based agents.

## Connection Reference

- **Port**: 8045 (default)
- **Protocol**: HTTP/WebSocket
- **Endpoints**: `/v1/chat/completions` (OpenAI), `/v1/messages` (Anthropic).

## Orchestration Patterns

- **P1 Orchestrator**: Main entry point for user requests.
- **P3 Architect**: Strategic planning and structural repair.
- **P4 Engineer**: Surgical implementation and execution.
- **P5 Forensics**: Logic audits and verification.
