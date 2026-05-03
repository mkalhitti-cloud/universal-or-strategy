---
name: web-artifacts-builder
description: >
  Build self-contained, portable web artifacts (HTML files) that work with zero dependencies.
  Use when creating dashboard demos, tool previews, standalone reports, shareable visualizations,
  or any single-file web output. Keywords: web artifact, single-file HTML, portable, self-contained, demo.
---

# Web Artifacts Builder Skill

Build stunning, self-contained HTML artifacts that work offline, share easily, and need zero build tooling.

## Core Constraint: One File, Zero Dependencies

Every artifact MUST work by double-clicking the `.html` file:

- All CSS inline in `<style>`
- All JS inline in `<script>`
- Fonts via Google Fonts CDN (graceful fallback if offline)
- Images: inline SVG or base64 data URIs
- External libraries: CDN only (add offline fallbacks)

## File Structure Template

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>[Artifact Title]</title>
    <!-- CDN deps (optional) -->
    <style>
      /* Design system tokens first */
      :root {
        /* CSS vars */
      }
      /* Component styles */
    </style>
  </head>
  <body>
    <!-- Content -->
    <script>
      // All JS here
    </script>
  </body>
</html>
```

## Design Tokens Required

Always define these before any component styles:

```css
:root {
  /* Color */
  --bg: #050a14;
  --surface: #0a111e;
  --border: #1e2b45;
  --text: #ffffff;
  --text-dim: #6a7381;
  --accent: #0052ff;

  /* Type */
  --font-mono: "IBM Plex Mono", monospace;
  --font-display: "Syncopate", sans-serif;

  /* Space */
  --gap: 20px;
  --radius: 0px; /* tactical: 0, soft: 8px */
}
```

## SVG Inline Patterns

### Animated Flow Diagram

```html
<svg viewBox="0 0 300 80" style="width:100%" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <style>
      @keyframes flow {
        from {
          stroke-dashoffset: 120;
        }
        to {
          stroke-dashoffset: -120;
        }
      }
      @keyframes glow {
        0%,
        100% {
          stroke-width: 2px;
        }
        50% {
          stroke-width: 4px;
        }
      }
      .node {
        animation: glow 1.8s ease-in-out infinite;
      }
      .edge {
        stroke-dasharray: 10 110;
        animation: flow 1.1s linear infinite;
      }
    </style>
    <filter id="blur-glow">
      <feGaussianBlur in="SourceAlpha" stdDeviation="4" result="b" />
      <feFlood flood-color="#0052FF" flood-opacity="0.9" result="f" />
      <feComposite in="f" in2="b" operator="in" result="g" />
      <feMerge>
        <feMergeNode in="g" />
        <feMergeNode in="SourceGraphic" />
      </feMerge>
    </filter>
  </defs>
  <rect
    x="10"
    y="25"
    width="70"
    height="28"
    fill="#050a14"
    stroke="#0052FF"
    class="node"
    filter="url(#blur-glow)"
  />
  <text
    x="45"
    y="42"
    text-anchor="middle"
    fill="#4da6ff"
    font-family="IBM Plex Mono"
    font-size="9"
    font-weight="700"
  >
    NODE A
  </text>
  <path
    d="M80 39 L120 39"
    fill="none"
    stroke="#0052FF"
    stroke-width="2"
    class="edge"
  />
</svg>
```

### Color Swatch Grid

```html
<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:6px">
  <div
    style="height:40px;background:#d97757;border-radius:2px"
    title="#d97757 Orange"
  ></div>
  <div
    style="height:40px;background:#6a9bcc;border-radius:2px"
    title="#6a9bcc Blue"
  ></div>
</div>
```

## Tool Belt Artifact Pattern (V12-specific)

For the Sovereign Arena Dashboard Tool Belt, each artifact card should:

1. Have an **inline SVG preview** (no mermaid dependency) — 70-120px tall
2. Use a **branded top-border** cycling: orange → blue → green (brand-guidelines)
3. Include `#ARTIFACT-NNN` id, a category tag, title, and 1-sentence description
4. Entry animation via `cardEntry` keyframe with staggered delay

## Quality Gates

Before finalizing any artifact:

- [ ] Opens correctly offline (no external dependencies for core functionality)
- [ ] Renders in Chrome, Firefox, Safari
- [ ] No console errors
- [ ] `<title>` is descriptive
- [ ] Mobile viewport meta tag present

## Post-Use Audit

1. Does the artifact work offline?
2. Are all fonts/libs on CDN with fallbacks?
3. Is there a `<title>` and viewport meta?

State: `skill(web-artifacts-builder): no gaps identified.` or document fix.
