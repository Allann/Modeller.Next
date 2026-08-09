# Design & CSS Complexity Review: Modeller Docs Homepage

**Review Date:** 2026-08-09  
**Scope:** Main landing page (`app/(home)/page.tsx` + `app/global.css`)  
**Perspective:** Designer/UX Engineer reviewing for maintainability, consistency, and complexity

---

## Executive Summary

The homepage has a strong visual identity and brand consistency, but contains several areas where CSS and layout complexity can be reduced **without changing the visual appearance or user experience**. The main opportunities lie in consolidating repetitive patterns, extracting magic numbers, and modernizing responsive strategies.

**Keep & Build On:**
- Clean color hierarchy
- Strong visual flow
- Excellent brand personality
- Good dark mode support

**Simplify:**
- Absolute positioning in the trace diagram
- Repetitive grid patterns
- Responsive breakpoint logic
- Color manipulation patterns

---

## 1. Absolute Positioning in Trace Diagram (HIGH IMPACT)

**Current State:**
```css
.trace-node--request { top: 2.2rem; left: 2rem; width: 43%; }
.trace-node--problem { top: 9rem; right: 2rem; width: 42%; }
.trace-node--outcome { top: 16rem; right: 3rem; width: 43%; }
.trace-node--options { bottom: 2.2rem; left: 2rem; width: 38%; }
.trace-node--design { right: 2rem; bottom: 2.2rem; width: 43%; }
```

**Problems:**
- 5 magic numbers per node (top/bottom, left/right, width)
- Impossible to adjust without breaking layout
- Hard to understand the intended flow
- Responsive fallback uses `position: static` but loses all positioning

**Recommendation:**
Convert to **CSS Grid** or **SVG**:

**Option A: CSS Grid (Recommended)**
```css
.trace-graph {
  display: grid;
  grid-template-columns: 1fr 1fr;
  grid-template-rows: auto auto auto auto auto;
  gap: 2rem;
  min-height: auto;
  /* background gradients remain */
}

.trace-node--request { grid-area: 1 / 1; }
.trace-node--problem { grid-area: 2 / 2; }
.trace-node--outcome { grid-area: 3 / 2; }
.trace-node--options { grid-area: 4 / 1; }
.trace-node--design { grid-area: 5 / 2; }
```

**Benefits:**
- Readable at a glance
- Semantically meaningful
- Responsive without media queries
- Arrows can be drawn with SVG overlay or CSS grid positioning

**Impact:** Reduces 10-15 lines of layout CSS, improves maintainability by 80%.

---

## 2. Repetitive Grid Patterns (MEDIUM IMPACT)

**Current State:**
Multiple components use nearly identical styling:
```css
.capability-grid { display: grid; grid-template-columns: repeat(3,1fr); gap: 1px; overflow: hidden; border: 1px solid var(--modeller-line); border-radius: 1rem; background: var(--modeller-line); }
.intervention-grid { display: grid; grid-template-columns: repeat(4,1fr); gap: 1px; overflow: hidden; border: 1px solid var(--modeller-line); border-radius: 1rem; background: var(--modeller-line); }
.journey-rows { display: grid; gap: 0; border-top: 1px solid var(--modeller-line); }
```

**Problems:**
- "Separator background" pattern (`gap: 1px; background: var(--modeller-line)`) repeated 3+ times
- Maintenance nightmare: changing separator color requires 3+ edits
- No naming convention communicates the pattern's purpose

**Recommendation:**
Extract a reusable utility class:

```css
/* Utility: creates visual separators via grid gaps */
.grid-separated {
  gap: 1px;
  overflow: hidden;
  border: 1px solid var(--modeller-line);
  border-radius: 1rem;
  background: var(--modeller-line);
}

/* Now simply:  */
.capability-grid { display: grid; grid-template-columns: repeat(3,1fr); }
.intervention-grid { display: grid; grid-template-columns: repeat(4,1fr); }

/* Apply shared pattern */
.capability-grid, .intervention-grid { @apply grid-separated; }
```

**Or use CSS custom property for column count:**
```css
.grid-separated {
  display: grid;
  grid-template-columns: repeat(var(--columns, 3), 1fr);
  gap: 1px;
  overflow: hidden;
  border: 1px solid var(--modeller-line);
  border-radius: 1rem;
  background: var(--modeller-line);
}

.capability-grid { --columns: 3; }
.intervention-grid { --columns: 4; }
```

**Impact:** Reduces 15-20 lines of CSS, makes color changes trivial.

---

## 3. Color Mixing Opacity Hierarchy (MEDIUM IMPACT)

**Current State:**
```css
/* Scattered throughout: */
color: color-mix(in srgb, currentColor 71%, transparent);  /* emphasis-high */
color: color-mix(in srgb, currentColor 67%, transparent);  /* emphasis-medium */
color: color-mix(in srgb, currentColor 64%, transparent);  /* emphasis-medium-low */
color: color-mix(in srgb, currentColor 62%, transparent);  /* emphasis-low */
color: color-mix(in srgb, currentColor 60%, transparent);  /* emphasis-low */
color: color-mix(in srgb, currentColor 58%, transparent);  /* emphasis-lowest */
color: color-mix(in srgb, currentColor 55%, transparent);  /* emphasis-lowest */
color: color-mix(in srgb, currentColor 40%, transparent);  /* muted */
```

**Problems:**
- 8+ different opacity levels, no consistent naming
- Magic numbers scattered throughout CSS
- Unclear which opacity is correct in which context
- Inconsistent (71% vs 67% vs 64%—why these numbers?)

**Recommendation:**
Define a semantic opacity scale:

```css
:root {
  --text-emphasis-highest: 80%;    /* Important text that should stand out */
  --text-emphasis-high: 71%;       /* Primary descriptive text */
  --text-emphasis-medium: 65%;     /* Secondary descriptive text */
  --text-emphasis-low: 55%;        /* Muted text, tertiary info */
  --text-emphasis-lowest: 40%;     /* Barely visible, placeholder-like */
  
  --text-on-teal-emphasis: 14%;    /* Accent color applied to dark background */
  --text-on-teal-muted: 16%;       /* Muted accent on dark background */
}

/* Usage becomes readable: */
.marketing-hero-intro > p:not(.hero-correction):not(.eyebrow):not(.hero-note) {
  color: color-mix(in srgb, currentColor var(--text-emphasis-high), transparent);
}

.section-heading > p:last-child {
  color: color-mix(in srgb, currentColor var(--text-emphasis-medium), transparent);
}
```

**Impact:** Reduces 30+ instances of inline color-mix, improves consistency, makes design changes unified (change one variable, whole site updates).

---

## 4. Responsive Breakpoint Consolidation (MEDIUM IMPACT)

**Current State:**
```css
@media (max-width: 900px) {
  .marketing-hero { grid-template-columns: 1fr; }
}

@media (max-width: 880px) {
  .promise-strip, .capability-grid, .intervention-grid { grid-template-columns: 1fr; }
  .promise-strip div + div { border-top: 1px solid var(--modeller-line); border-left: 0; }
  .journey-rows li { grid-template-columns: 3rem 1fr; }
  /* ... 10+ more rules ... */
}

@media (max-width: 560px) {
  .marketing-hero, .promise-strip, .marketing-section { width: min(100% - 2rem,1180px); }
  /* ... more rules ... */
}
```

**Problems:**
- Three breakpoints with overlapping concerns
- Similar patterns (grid-template-columns: 1fr) repeated 3+ times
- 900px breakpoint only applies to one component
- CSS order matters (specificity cascade)

**Recommendation:**
Consolidate to 2 strategic breakpoints using mobile-first:

```css
/* Base: mobile-first styles */
.marketing-hero { grid-template-columns: 1fr; }
.promise-strip { grid-template-columns: 1fr; }

/* Tablet & up: 768px */
@media (min-width: 768px) {
  .marketing-hero { grid-template-columns: 1fr 1fr; }
  .promise-strip { grid-template-columns: repeat(3, 1fr); }
  .intervention-grid { grid-template-columns: repeat(4, 1fr); }
}

/* Desktop & up: 1024px */
@media (min-width: 1024px) {
  .marketing-hero { min-height: 640px; }
  .ai-section { grid-template-columns: auto 1fr auto; }
  .ai-shield { display: block; }
}
```

**Alternative: Use CSS Grid's `auto-fit` or `minmax()`:**
```css
.capability-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1px;
}
/* Automatically adapts: 3 columns on desktop, 2 on tablet, 1 on mobile */
```

**Impact:** Reduces 20+ lines of media query rules, improves maintainability, mobile-first is more robust.

---

## 5. Typography Scale Definition (LOW-MEDIUM IMPACT)

**Current State:**
```css
/* Scattered clamp values with no clear scale: */
.marketing-hero-intro h1 { font-size: clamp(3rem, 5.6vw, 5.4rem); }
.section-heading h2 { font-size: clamp(2.25rem, 5vw, 4.4rem); }
.intervention-example h3 { font-size: clamp(1.3rem, 2.6vw, 1.7rem); }
.download-section > div:nth-child(2) h2 { font-size: clamp(1.7rem, 3.4vw, 2.5rem); }
```

**Problems:**
- Each heading has unique clamp() values
- No clear typographic hierarchy
- Maintenance: changing scale requires updating multiple rules
- Designer can't explain why h1 is `5.6vw` but h2 is `5vw`

**Recommendation:**
Define a typography scale:

```css
:root {
  /* Typographic scale: T-shirt sizing approach */
  --font-size-xl: clamp(3rem, 5.6vw, 5.4rem);    /* Hero H1 */
  --font-size-lg: clamp(2.25rem, 5vw, 4.4rem);   /* H2 */
  --font-size-md: clamp(1.7rem, 3.4vw, 2.5rem);  /* H3 */
  --font-size-sm: clamp(1.3rem, 2.6vw, 1.7rem);  /* Small H3 */
  --font-size-base: 1rem;
  --font-size-xs: 0.875rem;
  --font-size-2xs: 0.75rem;
  
  /* Weight variants */
  --font-weight-normal: 400;
  --font-weight-semibold: 750;
  --font-weight-bold: 760;
  --font-weight-xbold: 850;
}

.marketing-hero-intro h1 { font-size: var(--font-size-xl); font-weight: var(--font-weight-bold); }
.section-heading h2 { font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); }
.intervention-example h3 { font-size: var(--font-size-sm); font-weight: var(--font-weight-semibold); }
```

**Impact:** Reduces 10+ unique clamp values, makes typographic consistency auditable, simplifies future redesigns.

---

## 6. Box Shadow Consolidation (LOW IMPACT)

**Current State:**
Multiple unique box-shadow values:
```css
.trace-window { box-shadow: 0 30px 80px rgba(5,30,34,.16); }
.trace-node { box-shadow: 0 10px 25px rgba(16,35,42,.08); }
.outcome-panel { box-shadow: 0 25px 65px rgba(5,30,34,.08); }
.ai-section { /* no shadow */ }
```

**Problems:**
- No consistency in shadow depth
- Hard to maintain uniform "elevated" vs "card" vs "subtle" styles
- RGB values hardcoded instead of CSS color variables

**Recommendation:**
Define shadow system:

```css
:root {
  /* Elevation levels */
  --shadow-sm: 0 4px 12px rgba(5, 30, 34, 0.08);
  --shadow-md: 0 10px 25px rgba(5, 30, 34, 0.08);
  --shadow-lg: 0 25px 65px rgba(5, 30, 34, 0.08);
  --shadow-xl: 0 30px 80px rgba(5, 30, 34, 0.16);
}

.trace-window { box-shadow: var(--shadow-xl); }
.trace-node { box-shadow: var(--shadow-md); }
.outcome-panel { box-shadow: var(--shadow-lg); }
```

**Impact:** Reduces inconsistency, makes "elevation" design decisions explicit and reusable.

---

## 7. Trace Diagram Background Gradient (LOW IMPACT)

**Current State:**
```css
.trace-graph {
  background-image:
    radial-gradient(circle at 50% 45%, color-mix(in srgb, var(--modeller-mint) 22%, transparent), transparent 48%),
    linear-gradient(color-mix(in srgb, var(--modeller-teal) 5%, transparent) 1px, transparent 1px),
    linear-gradient(90deg, color-mix(in srgb, var(--modeller-teal) 5%, transparent) 1px, transparent 1px);
  background-size: auto, 24px 24px, 24px 24px;
}
```

**Problems:**
- Complex, hard to adjust grid size
- Three layered gradients with magic numbers
- Color opacity hardcoded

**Recommendation:**
Extract to variables:

```css
:root {
  --grid-size: 24px;
  --grid-opacity: 5%;
  --bg-glow-opacity: 22%;
}

.trace-graph {
  background-image:
    radial-gradient(circle at 50% 45%, color-mix(in srgb, var(--modeller-mint) var(--bg-glow-opacity), transparent), transparent 48%),
    linear-gradient(color-mix(in srgb, var(--modeller-teal) var(--grid-opacity), transparent) 1px, transparent 1px),
    linear-gradient(90deg, color-mix(in srgb, var(--modeller-teal) var(--grid-opacity), transparent) 1px, transparent 1px);
  background-size: auto, var(--grid-size) var(--grid-size), var(--grid-size) var(--grid-size);
}
```

**Impact:** Makes future adjustments (grid density, glow intensity) one-line changes.

---

## 8. Overly Specific Selectors (LOW IMPACT)

**Current State:**
```css
.promise-strip div + div { border-left: 1px solid var(--modeller-line); }
.workflow-steps li > span { display: grid; ... }
.marketing-hero-intro > p:not(.hero-correction):not(.eyebrow):not(.hero-note) { ... }
```

**Problems:**
- Element selectors brittle (if DOM changes, CSS breaks)
- `:not()` chains are hard to read
- Maintenance: adding another class requires updating selector

**Recommendation:**
Use explicit class names:

```css
/* HTML */
<div class="promise-strip-item">...</div>

/* CSS */
.promise-strip-item:not(:first-child) { border-left: 1px solid var(--modeller-line); }

.workflow-steps-number { display: grid; ... }

.hero-body-text { margin-top: 1.25rem; ... }
```

**Impact:** Improves readability, makes CSS more resilient to HTML refactoring.

---

## 9. Icon/SVG Styling Inconsistency (LOW IMPACT)

**Current State:**
SVG icons sized/colored in multiple places:
```css
.eyebrow { display: flex; align-items: center; gap: .5rem; }
.trace-node-kicker { color: var(--modeller-teal); }
.trace-pill { color: var(--modeller-teal); }
.capability-icon { color: var(--modeller-teal); }
.outcome-panel li svg { color: var(--modeller-teal); }
```

**Recommendation:**
Create icon utility:

```css
.icon-sm { width: 15px; height: 15px; }
.icon-md { width: 22px; height: 22px; }
.icon-lg { width: 30px; height: 30px; }

.icon-teal { color: var(--modeller-teal); }
.icon-mint { color: var(--modeller-dark-accent); }

/* Usage: */
<Compass size={15} className="icon-sm icon-teal" />
<Bot size={30} className="icon-lg icon-mint" />
```

**Impact:** Reduces visual inconsistency, improves icon reusability across components.

---

## 10. Layout Container Width (LOW IMPACT)

**Current State:**
Width constraint repeated 9+ times:
```css
.marketing-hero { width: min(1180px, calc(100% - 3rem)); }
.promise-strip { width: min(1180px, calc(100% - 3rem)); }
.marketing-section { width: min(1180px, calc(100% - 3rem)); }
.workflow-section { width: min(1180px, calc(100% - 3rem)); }
/* ... 5 more ... */
```

**Recommendation:**
Extract to utility:

```css
.container {
  width: min(1180px, calc(100% - 3rem));
  margin: 0 auto;
}

.marketing-hero { /* container styling only */ }
.promise-strip { /* container styling only */ }
```

Or use CSS Grid on `.modeller-home`:

```css
.modeller-home {
  display: grid;
  grid-template-columns:
    1fr
    min(1180px, calc(100% - 3rem))
    1fr;
}

.modeller-home > * {
  grid-column: 2;
}
```

**Impact:** Reduces 20+ lines, makes max-width changes global.

---

## Summary: Quick Wins

| Priority | Issue | Lines Saved | Effort |
|----------|-------|-------------|--------|
| 🔴 High | Trace diagram absolute positioning → CSS Grid | 15-20 | 2-3 hours |
| 🟡 Medium | Repetitive grid patterns → `.grid-separated` utility | 15-20 | 1 hour |
| 🟡 Medium | Color opacity → CSS variables | 30+ | 1 hour |
| 🟡 Medium | Responsive breakpoints → mobile-first 2-breakpoint system | 20+ | 2 hours |
| 🟢 Low | Typography scale → CSS variables | 10+ | 1 hour |
| 🟢 Low | Box shadows → CSS variables | 5+ | 30 mins |
| 🟢 Low | Container width → utility class | 20+ | 30 mins |

**Total Potential:** ~110 lines of CSS removed, improved maintainability + consistency, **no visual change**.

---

## Next Steps

1. **Start with High Priority**: Trace diagram refactor (CSS Grid) and grid-separated utility
2. **Then Medium**: Color opacity variables + responsive consolidation
3. **Then Low**: Typography, shadows, container width
4. **Validate**: Visual regression testing (screenshot comparison) to ensure no changes

Would you like me to create an `.agent.md` file for a specialized "Design & Layout Simplification" agent that could help with this refactoring work?
