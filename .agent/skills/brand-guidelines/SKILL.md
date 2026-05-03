---
name: brand-guidelines
description: >
  Apply Anthropic's official brand colors and typography to visual artifacts and designs.
  Use this whenever creating visual outputs (HTML dashboards, presentations, reports, PDFs)
  that should adhere to Anthropic's brand identity: color palette, typography, and visual hierarchy.
  Keywords: branding, visual identity, brand colors, typography, Anthropic brand, visual formatting.
---

# Anthropic Brand Guidelines Skill

Apply Anthropic's official brand identity to all visual artifacts and design outputs.

## Brand Colors

### Main Colors

| Role       | Hex       | Usage                           |
| ---------- | --------- | ------------------------------- |
| Dark       | `#141413` | Primary text, dark backgrounds  |
| Light      | `#faf9f5` | Light backgrounds, text on dark |
| Mid Gray   | `#b0aea5` | Secondary elements, subtitles   |
| Light Gray | `#e8e6dc` | Subtle backgrounds, dividers    |

### Accent Colors

| Role   | Hex       | Cycle Order            |
| ------ | --------- | ---------------------- |
| Orange | `#d97757` | Primary accent (1st)   |
| Blue   | `#6a9bcc` | Secondary accent (2nd) |
| Green  | `#788c5d` | Tertiary accent (3rd)  |

**Rule**: Non-text shapes and decorative elements cycle through orange → blue → green accents to maintain visual interest while staying on-brand.

## Typography

| Context          | Font    | Fallback |
| ---------------- | ------- | -------- |
| Headings (24pt+) | Poppins | Arial    |
| Body text        | Lora    | Georgia  |

**Font Notes:**

- Pre-install Poppins and Lora for best results; fallbacks are automatic.
- No custom font installation required — works with existing system fonts.
- Preserve text hierarchy: display text always uses Poppins, body always uses Lora.

## Color Application Rules

1. **Smart contrast**: Always select text color based on background context (dark bg → Light `#faf9f5`; light bg → Dark `#141413`).
2. **Accent cycling**: Cycle orange → blue → green for shapes, badges, highlights. Never repeat the same accent consecutively.
3. **Gray usage**: Use Mid Gray for secondary/supporting text; Light Gray for borders and subtle containers.
4. **RGB precision**: Use exact RGB values for programmatic use (e.g., `python-pptx` `RGBColor`):
   - Dark: `RGBColor(0x14, 0x14, 0x13)`
   - Light: `RGBColor(0xfa, 0xf9, 0xf5)`
   - Orange: `RGBColor(0xd9, 0x77, 0x57)`
   - Blue: `RGBColor(0x6a, 0x9b, 0xcc)`
   - Green: `RGBColor(0x78, 0x8c, 0x5d)`

## CSS Variables (for HTML/Web outputs)

```css
:root {
  /* Brand Neutrals */
  --brand-dark: #141413;
  --brand-light: #faf9f5;
  --brand-mid-gray: #b0aea5;
  --brand-light-gray: #e8e6dc;

  /* Brand Accents */
  --brand-orange: #d97757;
  --brand-blue: #6a9bcc;
  --brand-green: #788c5d;

  /* Typography */
  --font-heading: "Poppins", Arial, sans-serif;
  --font-body: "Lora", Georgia, serif;
}
```

## V12 Integration Notes

- This skill complements `frontend-design`: use brand-guidelines for Anthropic-branded outputs; use frontend-design for custom tactical UI (e.g., the Sovereign Arena Dashboard which uses its own V12.15 Platinum palette).
- When producing client-facing documents, presentations, or marketing artifacts, always apply brand-guidelines.
- For the Sovereign Arena Dashboard and NinjaTrader overlays, defer to `frontend-design` skill instead.

## Post-Use Audit (MANDATORY)

After every use:

1. Are headings using Poppins (or Arial fallback)? If no → FIX.
2. Are accent colors cycling orange → blue → green? If not → FIX.
3. Is text contrast correct for background context? If not → FIX.

State: `skill(brand-guidelines): no gaps identified.` or log any gap found.
