---
name: animate
description: >
  Create CSS and JavaScript animations for web interfaces. Use when adding motion, transitions,
  micro-interactions, page entry effects, scroll-triggered reveals, or any animated UI elements.
  Keywords: animation, CSS keyframes, transition, micro-interaction, motion, scroll reveal, GSAP.
---

# Animate Skill

Apply purposeful, performant animations to web interfaces. Motion communicates state, guides attention, and creates delight.

## Core Principles

1. **Purpose-driven**: Every animation must communicate something (state change, hierarchy, feedback)
2. **Performance-first**: Animate only `transform` and `opacity` for 60fps. Avoid animating `width`, `height`, `margin`, `top/left`.
3. **Subtlety wins**: Animations should feel inevitable, not showy. Duration 150-600ms for UI transitions.
4. **Respect user preferences**: Always check `prefers-reduced-motion`.

## CSS Keyframe Patterns

### Page Entry (Staggered Reveal)

```css
@keyframes fadeUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
.card {
  animation: fadeUp 0.5s cubic-bezier(0.23, 1, 0.32, 1) both;
}
.card:nth-child(2) {
  animation-delay: 0.1s;
}
.card:nth-child(3) {
  animation-delay: 0.2s;
}
```

### Pulse / Heartbeat (Live data indicators)

```css
@keyframes pulse {
  0%,
  100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.6;
    transform: scale(1.05);
  }
}
.live-dot {
  animation: pulse 1.5s ease-in-out infinite;
}
```

### Flow / Data Travel (SVG edges)

```css
@keyframes flowEdge {
  from {
    stroke-dashoffset: 200;
  }
  to {
    stroke-dashoffset: -200;
  }
}
path[data-flow] {
  stroke-dasharray: 12 188;
  animation: flowEdge var(--dur, 1s) linear var(--delay, 0s) infinite;
}
```

### Glow Pulse (Node borders)

```css
@keyframes glowPulse {
  0%,
  100% {
    filter: drop-shadow(0 0 4px currentColor);
  }
  50% {
    filter: drop-shadow(0 0 12px currentColor)
      drop-shadow(0 0 24px currentColor);
  }
}
```

### Scanline Overlay (Texture / atmosphere)

```css
@keyframes scanline {
  0% {
    transform: translateY(-100%);
  }
  100% {
    transform: translateY(100vh);
  }
}
.scanline::after {
  content: "";
  position: fixed;
  width: 100%;
  height: 2px;
  background: rgba(255, 255, 255, 0.03);
  animation: scanline 4s linear infinite;
  pointer-events: none;
}
```

## JavaScript Animation Patterns

### Intersection Observer (Scroll Reveal)

```javascript
const observer = new IntersectionObserver(
  (entries) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add("visible");
        observer.unobserve(entry.target); // once only
      }
    });
  },
  { threshold: 0.1 },
);
document.querySelectorAll(".reveal").forEach((el) => observer.observe(el));
```

### Counter Animation (Metric roll-up)

```javascript
function animateCounter(el, target, duration = 1200) {
  const start = performance.now();
  const update = (time) => {
    const t = Math.min((time - start) / duration, 1);
    const ease = t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t; // easeInOut
    el.textContent = Math.floor(ease * target).toLocaleString();
    if (t < 1) requestAnimationFrame(update);
  };
  requestAnimationFrame(update);
}
```

### RAF Loop (Continuous data animation)

```javascript
function startPulseLoop(svgEl) {
  let frame;
  const tick = () => {
    // update data-driven visuals
    frame = requestAnimationFrame(tick);
  };
  frame = requestAnimationFrame(tick);
  return () => cancelAnimationFrame(frame); // cleanup
}
```

## V12 Dashboard — Approved Patterns

For the Sovereign Arena Dashboard, these animations are pre-approved:

- **Node pulse**: `pulseInput` / `pulseOutput` / `pulseHot` keyframes (defined in arena_dashboard.html)
- **Edge flow**: `flowEdge` keyframe with `data-flow` attribute + CSS custom property `--fdur`
- **Tab reveal**: `tacticalReveal` — scale + translateY + blur
- **Card entry**: `cardEntry` — translateX from left

## Performance Rules

| Check        | ✅ OK                                         | ❌ Avoid                   |
| ------------ | --------------------------------------------- | -------------------------- |
| Property     | `transform`, `opacity`                        | `width`, `margin`, `top`   |
| Duration     | 150-600ms transitions                         | >1s for UI responses       |
| GPU layers   | `will-change: transform` for heavy animations | Blanket `will-change: all` |
| `!important` | Only for overriding Mermaid SVG styles        | General CSS                |

## Post-Use Audit

1. Are all animations using `transform`/`opacity`? (performance)
2. Is `prefers-reduced-motion` respected?
3. Do animations communicate state, not just decorate?

State: `skill(animate): no gaps identified.` or document fix.
