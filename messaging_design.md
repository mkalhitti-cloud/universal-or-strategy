# Sovereign Messaging Bridge (iMessage Integration)

## Technical Specification V1.0

### 1. The "Sovereign Link" Pattern

The integration utilizes the native iOS `sms:` URI scheme to create a seamless, zero-app handshake between the web dashboard and the user's mobile device.

### 2. Physical Workflow

1. **Trigger**: User clicks "Connect Messaging" on the Antigravity Dashboard.
2. **Challenge**: The Dashboard generates a unique `SESSION_ID` and displays a QR Code.
3. **Action**: The QR Code encodes: `sms:<NEXUS_BRIDGE_NUMBER>?&body=NEXUS_AUTH_<SESSION_ID>`.
4. **Handshake**: User sends the pre-filled text. The backend receives the SMS via webhook, extracts the `SESSION_ID`, and instantly promotes the web session to "Linked."

### 3. Frontend Architecture (Aesthetic: Ara/Glassmorphism)

- **Overlay**: `backdrop-filter: blur(24px)` with a deep obsidian tint.
- **Component**: Centralized QR container with "Waiting for signal..." pulse animation.
- **Status Indigo**: Pulsing bridge connection icon (Cyan & Magenta).

### 4. Backend Requirements

- **Webhook Endpoint**: `/api/messaging/handshake`
- **SMS Gateway**: Twilio or BlueBubbles (Private Mac Bridge).
- **Session Store**: Redis or internal State Dictionary to map `AUTH_CODE` -> `SocketID`.

### 5. Future Capability

- **Trade Alerts**: Push execution Fills directly to iMessage.
- **RMA Killswitch**: Ability to send "FLATTEN" via iMessage to emergency-close all positions.

---

_Status: Architecture Saved. Implementation Deferred per Director's Mandate (2026-04-11)._
