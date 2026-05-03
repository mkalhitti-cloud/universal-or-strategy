---
name: frontend-design
description: >
  Create distinctive, production-grade frontend interfaces with high design quality for the Universal OR Strategy project. 
  Generates creative, polished code that avoids generic AI aesthetics. 
  Use this whenever creating or refactoring dashboard UI, NinjaTrader overlays, or any visual components.
---

# Frontend Design Skill (V12.15 Platinum Standard)

This skill guides creation of distinctive, production-grade frontend interfaces that avoid generic "AI slop" aesthetics. Implement real working code with exceptional attention to aesthetic detail and creative choices.

## Design Thinking

Before coding, understand the context and commit to a BOLD aesthetic direction:

- **Purpose**: What problem does this interface solve? Who uses it?
- **Tone**: Pick an extreme (e.g., Tactical Brutalist, Refined Luxury, Retro-Futuristic, Industrial/Utilitarian, Art Deco/Geometric, Editorial/Magazine, Organic/Natural, Playful, Soft Pastel, etc.)
- **Constraints**: Technical requirements (framework, performance, accessibility).
- **Differentiation**: What makes this UNFORGETTABLE? What's the one thing someone will remember?

**CRITICAL**: Choose a clear conceptual direction and execute it with precision. Bold maximalism and refined minimalism both work — the key is **intentionality**, not intensity.

Then implement working code (HTML/CSS/JS, React, Vue, etc.) that is:

- Production-grade and functional
- Visually striking and memorable
- Cohesive with a clear aesthetic point-of-view
- Meticulously refined in every detail

## Frontend Aesthetics Guidelines

### 1. Typography (BANNED: Inter, Roboto, Arial, system-ui)

- Choose fonts that are beautiful, unique, and technically expressive.
- Opt for characterful choices: `Syncopate`, `IBM Plex Mono`, `Work Sans`, `Outfit`, `Public Sans`, `Poppins`, `DM Mono`.
- Pair a distinctive display font with a refined body font.
- Unexpected, characterful font choices elevate aesthetics — avoid convergence on common picks (e.g., Space Grotesk).

### 2. Color & Theme

- Commit to a cohesive, high-contrast palette using CSS variables.
- Use dominant colors with sharp accents (e.g., Signal Green `#00ff88`, Hazard Orange `#ff6b00`).
- Avoid timid, evenly-balanced palettes.
- Vary between light and dark themes across generations — never default to the same scheme.

### 3. Motion & Interaction

- Use staggered reveals (`animation-delay`) for page loads — one well-orchestrated load creates more delight than scattered micro-interactions.
- Implement high-impact micro-interactions on hover/focus states that surprise.
- **The Pulse Engine**: Use role-based node glows (`pulse-cyan`, `pulse-indigo`, etc.) and edge flow animations (racing protons on SVG paths) to visualize data velocity.
- Use scroll-triggering and CSS-only solutions for reliability and zero-JS-overhead.

### 4. Spatial Composition & Texture

- Use grid-breaking elements, asymmetry, overlap, and diagonal flow.
- Generous negative space OR controlled density — both work with precision.
- Add atmospheric depth: grain/noise textures, scanlines, gradient meshes, layered transparencies, dramatic shadows, decorative borders, and geometric patterns.
- Avoid generic solid backgrounds — always create atmosphere.

## Implementation Standard

- **Production Grade**: Functional, responsive, and performance-optimized.
- **Zero AI Slop**: No overused font families; no clichéd "purple on white" gradients; no predictable layouts.
- **Meticulous Detail**: Every border, shadow, and transition must be intentional.
- **No two designs the same**: Each output should have a unique aesthetic fingerprint.

## V12 Sovereign Arena Dashboard — Specific Standards

For the Sovereign Arena Dashboard and NinjaTrader overlays, additionally enforce:

- **Typography stack**: `Syncopate` (headings) + `IBM Plex Mono` (data/mono) — no exceptions.
- **Color palette**: High-contrast dark-mode with Signal Green, Hazard Orange, and Electric Cyan accents.
- **Pulse Engine**: All live data nodes must have role-appropriate glow animations.
- **Texture**: Noise grain overlay + subtle scanline effect on main background.
- **Industrial corners**: `clip-path` or sharp `border-radius: 0` preferred over round corners.

> For Anthropic-branded outputs (reports, presentations, client docs), use the `brand-guidelines` skill instead.

## Post-Use Audit (MANDATORY)

After EVERY use, perform the V12 audit:

1. Did I use `Inter`, `Roboto`, or `Arial`? (If yes → **FAILURE**).
2. Is the background just a solid hex color? (If yes → add texture/depth).
3. Do cards have generic border-radius? (If yes → try tactical/industrial corners).
4. Is the color palette unique to this output, or is it a convergence on common AI defaults? (If yes → redesign).

State: `skill(frontend-design): no gaps identified.` or log what was adapted.
