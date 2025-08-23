# CSS Development Guidelines

Comprehensive guidelines for writing maintainable, scalable, and performant CSS in the Conduit ecosystem using modern CSS features, design tokens, and best practices.

## Overview

These guidelines establish consistent CSS development practices across all Conduit applications, ensuring maintainable codebases, optimal performance, and excellent user experiences.

## Documentation Structure

The CSS development guidelines have been organized by topics and implementation patterns:

### 🎨 Core Standards
- **[CSS Architecture](./css/architecture.md)** - File organization, naming conventions, and structure
- **[Design Tokens](./css/design-tokens.md)** - CSS custom properties and token management
- **[Component Patterns](./css/component-patterns.md)** - BEM methodology and component styling

### 📱 Responsive Design
- **[Mobile-First CSS](./css/mobile-first.md)** - Mobile-first responsive design implementation
- **[Grid Systems](./css/grid-systems.md)** - CSS Grid and Flexbox layouts
- **[Breakpoint Management](./css/breakpoints.md)** - Media query organization and responsive utilities

### ⚡ Performance & Optimization
- **[CSS Performance](./css/performance.md)** - Optimization techniques and best practices
- **[Critical CSS](./css/critical-css.md)** - Above-the-fold optimization strategies
- **[Modern CSS Features](./css/modern-features.md)** - Container queries, cascade layers, and new CSS

## Core Principles

### 1. Design Token-First Approach
```css
/* ✅ Good: Use design tokens */
.button {
  background-color: var(--color-primary-500);
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-6);
  font-size: var(--text-sm);
  font-weight: var(--font-medium);
}

/* ❌ Bad: Hard-coded values */
.button {
  background-color: #3b82f6;
  border-radius: 6px;
  padding: 12px 24px;
  font-size: 14px;
  font-weight: 500;
}
```

### 2. BEM Methodology
```css
/* Block */
.card {
  background: var(--color-white);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-md);
}

/* Element */
.card__header {
  padding: var(--space-6);
  border-bottom: 1px solid var(--color-gray-200);
}

.card__title {
  font-size: var(--text-xl);
  font-weight: var(--font-semibold);
  margin: 0;
}

.card__content {
  padding: var(--space-6);
}

/* Modifier */
.card--highlighted {
  border: 2px solid var(--color-primary-500);
}

.card--large {
  max-width: var(--max-width-4xl);
}
```

### 3. Mobile-First Responsive Design
```css
/* Default: Mobile styles */
.navigation {
  display: flex;
  flex-direction: column;
  padding: var(--space-4);
}

.navigation__menu {
  display: none; /* Hidden on mobile */
}

.navigation__toggle {
  display: block;
  background: none;
  border: none;
  padding: var(--space-2);
}

/* Progressive enhancement for larger screens */
@media (min-width: 768px) {
  .navigation {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
  
  .navigation__menu {
    display: flex;
    gap: var(--space-6);
  }
  
  .navigation__toggle {
    display: none;
  }
}
```

## Design Token System

### Color Tokens
```css
:root {
  /* Primary colors */
  --color-primary-50: #eff6ff;
  --color-primary-100: #dbeafe;
  --color-primary-200: #bfdbfe;
  --color-primary-300: #93c5fd;
  --color-primary-400: #60a5fa;
  --color-primary-500: #3b82f6;
  --color-primary-600: #2563eb;
  --color-primary-700: #1d4ed8;
  --color-primary-800: #1e40af;
  --color-primary-900: #1e3a8a;
  
  /* Semantic colors */
  --color-success: var(--color-green-500);
  --color-warning: var(--color-yellow-500);
  --color-error: var(--color-red-500);
  --color-info: var(--color-blue-500);
  
  /* Gray scale */
  --color-gray-50: #f9fafb;
  --color-gray-100: #f3f4f6;
  --color-gray-500: #6b7280;
  --color-gray-900: #111827;
  
  /* Surface colors */
  --color-background: var(--color-white);
  --color-surface: var(--color-gray-50);
  --color-border: var(--color-gray-200);
}

/* Dark theme */
@media (prefers-color-scheme: dark) {
  :root {
    --color-background: var(--color-gray-900);
    --color-surface: var(--color-gray-800);
    --color-border: var(--color-gray-700);
  }
}
```

### Typography Tokens
```css
:root {
  /* Font families */
  --font-sans: 'Inter', system-ui, sans-serif;
  --font-mono: 'JetBrains Mono', Consolas, monospace;
  
  /* Font sizes */
  --text-xs: 0.75rem;      /* 12px */
  --text-sm: 0.875rem;     /* 14px */
  --text-base: 1rem;       /* 16px */
  --text-lg: 1.125rem;     /* 18px */
  --text-xl: 1.25rem;      /* 20px */
  --text-2xl: 1.5rem;      /* 24px */
  --text-3xl: 1.875rem;    /* 30px */
  --text-4xl: 2.25rem;     /* 36px */
  
  /* Line heights */
  --leading-tight: 1.25;
  --leading-normal: 1.5;
  --leading-relaxed: 1.75;
  
  /* Font weights */
  --font-light: 300;
  --font-normal: 400;
  --font-medium: 500;
  --font-semibold: 600;
  --font-bold: 700;
}
```

### Spacing Tokens
```css
:root {
  /* Spacing scale */
  --space-0: 0;
  --space-px: 1px;
  --space-0-5: 0.125rem;   /* 2px */
  --space-1: 0.25rem;      /* 4px */
  --space-2: 0.5rem;       /* 8px */
  --space-3: 0.75rem;      /* 12px */
  --space-4: 1rem;         /* 16px */
  --space-5: 1.25rem;      /* 20px */
  --space-6: 1.5rem;       /* 24px */
  --space-8: 2rem;         /* 32px */
  --space-10: 2.5rem;      /* 40px */
  --space-12: 3rem;        /* 48px */
  --space-16: 4rem;        /* 64px */
  --space-20: 5rem;        /* 80px */
  --space-24: 6rem;        /* 96px */
  
  /* Container sizes */
  --max-width-xs: 20rem;
  --max-width-sm: 24rem;
  --max-width-md: 28rem;
  --max-width-lg: 32rem;
  --max-width-xl: 36rem;
  --max-width-2xl: 42rem;
  --max-width-4xl: 56rem;
  --max-width-6xl: 72rem;
  --max-width-full: 100%;
}
```

## Component Architecture

### Base Component Pattern
```css
/* Component base styles */
.button {
  /* Reset and base styles */
  appearance: none;
  border: none;
  background: none;
  margin: 0;
  padding: 0;
  
  /* Component styles */
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  
  /* Typography */
  font-family: inherit;
  font-size: var(--text-sm);
  font-weight: var(--font-medium);
  line-height: var(--leading-normal);
  text-decoration: none;
  
  /* Layout */
  padding: var(--space-3) var(--space-6);
  border-radius: var(--radius-md);
  
  /* Interaction */
  cursor: pointer;
  transition: all 150ms ease;
  
  /* Accessibility */
  &:focus-visible {
    outline: 2px solid var(--color-primary-500);
    outline-offset: 2px;
  }
  
  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }
}

/* Size variants */
.button--sm {
  padding: var(--space-2) var(--space-4);
  font-size: var(--text-xs);
}

.button--lg {
  padding: var(--space-4) var(--space-8);
  font-size: var(--text-base);
}

/* Visual variants */
.button--primary {
  background-color: var(--color-primary-500);
  color: var(--color-white);
  
  &:hover:not(:disabled) {
    background-color: var(--color-primary-600);
  }
  
  &:active {
    background-color: var(--color-primary-700);
  }
}

.button--secondary {
  background-color: var(--color-surface);
  color: var(--color-gray-900);
  border: 1px solid var(--color-border);
  
  &:hover:not(:disabled) {
    background-color: var(--color-gray-100);
  }
}

.button--outline {
  background-color: transparent;
  color: var(--color-primary-500);
  border: 1px solid var(--color-primary-500);
  
  &:hover:not(:disabled) {
    background-color: var(--color-primary-50);
  }
}
```

### Layout Components
```css
/* Container */
.container {
  width: 100%;
  max-width: var(--max-width-6xl);
  margin-inline: auto;
  padding-inline: var(--space-4);
}

@media (min-width: 768px) {
  .container {
    padding-inline: var(--space-6);
  }
}

@media (min-width: 1024px) {
  .container {
    padding-inline: var(--space-8);
  }
}

/* Grid system */
.grid {
  display: grid;
  gap: var(--space-4);
}

.grid--cols-1 { grid-template-columns: 1fr; }
.grid--cols-2 { grid-template-columns: repeat(2, 1fr); }
.grid--cols-3 { grid-template-columns: repeat(3, 1fr); }
.grid--cols-4 { grid-template-columns: repeat(4, 1fr); }

/* Responsive grid utilities */
@media (min-width: 768px) {
  .grid--md-cols-2 { grid-template-columns: repeat(2, 1fr); }
  .grid--md-cols-3 { grid-template-columns: repeat(3, 1fr); }
}

@media (min-width: 1024px) {
  .grid--lg-cols-3 { grid-template-columns: repeat(3, 1fr); }
  .grid--lg-cols-4 { grid-template-columns: repeat(4, 1fr); }
}

/* Flexbox utilities */
.flex {
  display: flex;
}

.flex--column {
  flex-direction: column;
}

.flex--wrap {
  flex-wrap: wrap;
}

.items-center {
  align-items: center;
}

.justify-between {
  justify-content: space-between;
}

.gap-4 {
  gap: var(--space-4);
}
```

## Modern CSS Features

### Container Queries
```css
/* Container query support */
@supports (container-type: inline-size) {
  .card-container {
    container-type: inline-size;
    container-name: card;
  }
  
  .card {
    /* Default mobile layout */
    display: block;
  }
  
  /* Container-based responsive design */
  @container card (min-width: 400px) {
    .card {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: var(--space-4);
    }
    
    .card__image {
      grid-row: 1 / -1;
    }
  }
}
```

### CSS Cascade Layers
```css
/* Define layer order */
@layer reset, tokens, base, components, utilities;

/* Reset layer */
@layer reset {
  *,
  *::before,
  *::after {
    box-sizing: border-box;
  }
  
  body {
    margin: 0;
    font-family: var(--font-sans);
  }
}

/* Base layer */
@layer base {
  h1, h2, h3, h4, h5, h6 {
    margin: 0;
    font-weight: var(--font-semibold);
  }
  
  p {
    margin: 0 0 var(--space-4) 0;
  }
}

/* Component layer */
@layer components {
  .button {
    /* Button styles */
  }
  
  .card {
    /* Card styles */
  }
}

/* Utility layer */
@layer utilities {
  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }
}
```

### CSS Logical Properties
```css
/* Use logical properties for internationalization */
.card {
  padding-block: var(--space-6);
  padding-inline: var(--space-6);
  margin-block-end: var(--space-4);
  border-inline-start: 4px solid var(--color-primary-500);
}

.navigation {
  padding-inline-start: var(--space-4);
  padding-inline-end: var(--space-4);
}

/* Text alignment */
.text-start { text-align: start; }
.text-end { text-align: end; }
```

## Performance Best Practices

### CSS Optimization
```css
/* Minimize repaints and reflows */
.smooth-animation {
  /* Use transform and opacity for animations */
  transform: translateX(0);
  opacity: 1;
  transition: transform 200ms ease, opacity 200ms ease;
}

.smooth-animation--hidden {
  transform: translateX(-100%);
  opacity: 0;
}

/* Avoid expensive properties in animations */
.expensive-animation {
  /* ❌ Avoid animating these properties */
  /* width, height, top, left, border-width */
  
  /* ✅ Prefer these for animations */
  transform: scale(1);
  opacity: 1;
}

/* Use will-change sparingly */
.about-to-animate {
  will-change: transform;
}

.finished-animating {
  will-change: auto; /* Reset after animation */
}
```

### Critical CSS Strategy
```css
/* Above-the-fold critical styles */
.critical {
  /* Header */
  .header {
    background: var(--color-white);
    border-bottom: 1px solid var(--color-border);
  }
  
  /* Hero section */
  .hero {
    padding: var(--space-16) var(--space-4);
    text-align: center;
  }
  
  /* Essential typography */
  h1 {
    font-size: var(--text-4xl);
    font-weight: var(--font-bold);
    margin-bottom: var(--space-6);
  }
}
```

### CSS Loading Strategy
```html
<!-- Critical CSS inline -->
<style>
  /* Critical above-the-fold styles */
</style>

<!-- Non-critical CSS with media attribute -->
<link rel="preload" href="/css/main.css" as="style" onload="this.onload=null;this.rel='stylesheet'">
<noscript><link rel="stylesheet" href="/css/main.css"></noscript>

<!-- Component-specific CSS -->
<link rel="stylesheet" href="/css/components/modal.css" media="print" onload="this.media='all'">
```

## Accessibility Guidelines

### Focus Management
```css
/* Consistent focus styles */
.focusable {
  &:focus-visible {
    outline: 2px solid var(--color-primary-500);
    outline-offset: 2px;
    border-radius: var(--radius-sm);
  }
}

/* Skip links */
.skip-link {
  position: absolute;
  top: -40px;
  left: 6px;
  background: var(--color-primary-500);
  color: var(--color-white);
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-md);
  text-decoration: none;
  z-index: 1000;
  
  &:focus {
    top: 6px;
  }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
  .button {
    border: 2px solid;
  }
  
  .card {
    border: 1px solid;
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

### Color Accessibility
```css
/* Ensure sufficient color contrast */
.text-on-primary {
  background-color: var(--color-primary-500);
  color: var(--color-white); /* 4.5:1 contrast ratio */
}

.text-muted {
  color: var(--color-gray-600); /* Ensure 4.5:1 contrast on white */
}

/* Don't rely solely on color for information */
.status--success {
  color: var(--color-success);
}

.status--success::before {
  content: "✓ "; /* Visual indicator in addition to color */
}

.status--error {
  color: var(--color-error);
}

.status--error::before {
  content: "⚠ ";
}
```

## File Organization

### Directory Structure
```
styles/
├── tokens/              # Design tokens
│   ├── colors.css
│   ├── typography.css
│   ├── spacing.css
│   └── shadows.css
├── base/                # Base styles and resets
│   ├── reset.css
│   ├── typography.css
│   └── utilities.css
├── components/          # Component styles
│   ├── button.css
│   ├── card.css
│   ├── modal.css
│   └── navigation.css
├── layouts/            # Layout-specific styles
│   ├── grid.css
│   ├── container.css
│   └── sidebar.css
└── pages/              # Page-specific styles
    ├── home.css
    ├── dashboard.css
    └── profile.css
```

### CSS Import Strategy
```css
/* main.css - Import order matters */

/* 1. Cascade layers definition */
@layer reset, tokens, base, components, utilities;

/* 2. Design tokens */
@import './tokens/colors.css' layer(tokens);
@import './tokens/typography.css' layer(tokens);
@import './tokens/spacing.css' layer(tokens);

/* 3. Reset and base styles */
@import './base/reset.css' layer(reset);
@import './base/typography.css' layer(base);

/* 4. Layout styles */
@import './layouts/grid.css' layer(base);
@import './layouts/container.css' layer(base);

/* 5. Component styles */
@import './components/button.css' layer(components);
@import './components/card.css' layer(components);
@import './components/modal.css' layer(components);

/* 6. Utility classes */
@import './base/utilities.css' layer(utilities);
```

## Testing CSS

### Visual Regression Testing
```javascript
// Using Playwright for visual testing
test('button appears correctly', async ({ page }) => {
  await page.goto('/components/button');
  await expect(page.locator('.button--primary')).toHaveScreenshot('button-primary.png');
});

// Test different states
test('button states', async ({ page }) => {
  await page.goto('/components/button');
  
  // Default state
  await expect(page.locator('.button')).toHaveScreenshot('button-default.png');
  
  // Hover state
  await page.locator('.button').hover();
  await expect(page.locator('.button')).toHaveScreenshot('button-hover.png');
  
  // Focus state
  await page.locator('.button').focus();
  await expect(page.locator('.button')).toHaveScreenshot('button-focus.png');
});
```

### CSS Linting Configuration
```json
// .stylelintrc.json
{
  "extends": [
    "stylelint-config-standard",
    "stylelint-config-recess-order"
  ],
  "rules": {
    "custom-property-pattern": "^[a-z]([a-z0-9-]+)?$",
    "selector-class-pattern": "^[a-z]([a-z0-9-]+)?(__[a-z0-9-]+)?(--[a-z0-9-]+)?$",
    "property-no-vendor-prefix": true,
    "value-no-vendor-prefix": true,
    "declaration-no-important": true,
    "selector-max-id": 0,
    "selector-max-compound-selectors": 3,
    "color-function-notation": "modern",
    "alpha-value-notation": "percentage"
  }
}
```

## Migration Guidelines

### Legacy CSS Modernization
```css
/* Before: Legacy approach */
.old-component {
  display: -webkit-box;
  display: -ms-flexbox;
  display: flex;
  
  -webkit-box-pack: justify;
  -ms-flex-pack: justify;
  justify-content: space-between;
  
  background: #3b82f6;
  border-radius: 6px;
  padding: 12px 24px;
}

/* After: Modern approach */
.new-component {
  display: flex;
  justify-content: space-between;
  
  background: var(--color-primary-500);
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-6);
}
```

### Design Token Migration
```css
/* Migration helper: Create mapping variables */
:root {
  /* Legacy mappings */
  --legacy-blue: var(--color-primary-500);
  --legacy-padding: var(--space-4);
  --legacy-border-radius: var(--radius-md);
}

/* Gradually replace legacy variables */
.migrating-component {
  /* Step 1: Use mapping variables */
  background: var(--legacy-blue);
  
  /* Step 2: Replace with proper tokens */
  background: var(--color-primary-500);
}
```

## Related Documentation

- [Responsive Design Patterns](./responsive-design-patterns.md) - Mobile-first responsive design implementation
- [Component Library](./component-library.md) - UI component design system and patterns
- [Integration Examples](./examples/INTEGRATION-EXAMPLES.md) - CSS integration in real applications