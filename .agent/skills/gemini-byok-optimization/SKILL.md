---
name: gemini-byok-optimization
description: >
  Manages and optimizes Droid/Goose BYOK (Bring Your Own Key) configurations for Gemini 3.x models. 
  Use this whenever the user reports model connection errors (429, 404), asks to add "3.0 Flash" or 
  "3.1 Pro" to Droid, or needs to clean up the model selection menu.
---

# Gemini BYOK Optimization

This skill ensures that the Universal OR Strategy agents (Droid, Goose) are configured with the most efficient and functional Gemini model IDs.

## Core Configuration Pattern (Droid)

Droid uses `~/.factory/settings.json` for custom model definitions.

### 1. Verified Model IDs (Gemini 3.x)

| Feature | API ID | Display Name | Status |
|---------|--------|--------------|--------|
| **Primary Pro** | `gemini-3.1-pro-preview` | Gemini 3.1 Pro (Preview) | 🟢 Verified |
| **Primary Flash** | `gemini-3-flash-preview` | Gemini 3 Flash (Preview) | 🟢 Verified |
| **Secondary Pro** | `gemini-3-pro-preview` | Gemini 3 Pro (Preview) | 🟢 Verified |

### 2. Provider Settings

- **Provider**: `generic-chat-completion-api`
- **Base URL**: `https://generativelanguage.googleapis.com/v1beta/openai`
- **Max Output Tokens**: `16384` (Standard for Gemini 3.x)

## Troubleshooting & Verification

### 1. 429 Errors (Rate Limits)
If the user receives a 429 error without a body, verify:
- The API key is valid and has not reached its quota.
- The model ID matches the list returned by the `v1beta/models` endpoint.

### 2. API Model Audit
Always run this command to find the most current IDs available for the user's API key:
```powershell
powershell -Command "Invoke-RestMethod -Uri 'https://generativelanguage.googleapis.com/v1beta/models?key=YOUR_API_KEY' | Select-Object -ExpandProperty models | Select-Object name, displayName"
```

## Maintenance

- **Menu Hygiene**: Remove legacy `gemini-1.5` or `gemini-2.x` models if they are no longer used to reduce cognitive load in the selection menu.
- **Goose Sync**: Ensure `Goose`'s `config.yaml` uses the same `gemini-3-flash-preview` or `gemini-3.1-pro-preview` IDs for cross-agent parity.
