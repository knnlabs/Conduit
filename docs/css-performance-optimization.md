# CSS Performance Optimization Guidelines

## Overview

This guide provides comprehensive strategies for optimizing CSS performance in the Conduit application, focusing on **load times**, **rendering efficiency**, **bundle size optimization**, and **runtime performance**.

## Table of Contents

1. [Critical CSS Strategy](#critical-css-strategy)
2. [Bundle Size Optimization](#bundle-size-optimization)
3. [Selector Performance](#selector-performance)
4. [Rendering Optimization](#rendering-optimization)
5. [Loading Strategies](#loading-strategies)
6. [Design Token Efficiency](#design-token-efficiency)
7. [Animation Performance](#animation-performance)
8. [Memory Management](#memory-management)
9. [Monitoring and Metrics](#monitoring-and-metrics)
10. [Performance Testing](#performance-testing)

## Critical CSS Strategy

### Identifying Critical CSS

Critical CSS includes styles necessary for rendering above-the-fold content during initial page load.

```css
/* Critical CSS - Inline in <head> */
:root {
  /* Essential design tokens only */
  --color-primary: #3b82f6;
  --color-text-primary: #111827;
  --color-bg-primary: #ffffff;
  --color-bg-secondary: #f9fafb;
  --space-4: 1rem;
  --space-6: 1.5rem;
  --nav-height: 4rem;
  --font-sans: 'Inter', 'Roboto', sans-serif;
  --text-base: 1rem;
  --leading-normal: 1.5;
  --transition-all: all 0.15s ease-in-out;
  --radius-md: 0.375rem;
  --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
}

/* Base styles */
html {
  font-family: var(--font-sans);
  line-height: var(--leading-normal);
}

body {
  margin: 0;
  font-size: var(--text-base);
  color: var(--color-text-primary);
  background-color: var(--color-bg-primary);
}

/* Critical layout */
.header {
  position: sticky;
  top: 0;
  height: var(--nav-height);
  background: var(--color-primary);
  z-index: 1000;
}

.main {
  min-height: calc(100vh - var(--nav-height));
}

/* Critical components */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-3) var(--space-4);
  background: var(--color-primary);
  color: white;
  border: none;
  border-radius: var(--radius-md);
  cursor: pointer;
}

.card {
  background: var(--color-bg-primary);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-sm);
  padding: var(--space-6);
}
```

### Critical CSS Extraction

```javascript
// Build process - Extract critical CSS
const critical = require('critical');

critical.generate({
  base: 'dist/',
  src: 'index.html',
  dest: 'critical.css',
  width: 375,  // Mobile viewport
  height: 667,
  minify: true,
  ignore: {
    atrule: ['@font-face', '@media'],
    rule: [/\.non-critical/],
    decl: (node, value) => {
      // Skip non-critical properties
      return /transition|animation/.test(node.prop);
    }
  }
});
```

### Implementation Strategy

```html
<!DOCTYPE html>
<html>
<head>
  <!-- Critical CSS inlined -->
  <style>
    /* Critical CSS content here */
  </style>
  
  <!-- Non-critical CSS loaded asynchronously -->
  <link rel="preload" href="styles.css" as="style" onload="this.onload=null;this.rel='stylesheet'">
  <noscript><link rel="stylesheet" href="styles.css"></noscript>
</head>
</html>
```

## Bundle Size Optimization

### CSS File Organization

```
css/
├── critical.css           # ~8KB - Inlined
├── design-system.css      # ~12KB - Above fold
├── components/            # ~25KB total
│   ├── buttons.css        # ~3KB
│   ├── forms.css          # ~8KB
│   ├── cards.css          # ~4KB
│   └── navigation.css     # ~10KB
├── layout/               # ~15KB total
│   ├── grid.css          # ~6KB
│   ├── header.css        # ~4KB
│   └── sidebar.css       # ~5KB
└── utilities.css         # ~2KB
```

### Bundle Splitting Strategy

```css
/* Core bundle - Always loaded */
@import 'critical.css';
@import 'design-system.css';

/* Component bundles - Lazy loaded */
@import 'components/buttons.css' layer(components);
@import 'components/forms.css' layer(components);

/* Page-specific bundles */
@import 'pages/dashboard.css' layer(pages) (prefers-color-scheme: light);
```

### CSS Size Optimization Techniques

```css
/* Use shorthand properties */
/* Before: 4 lines */
margin-top: var(--space-4);
margin-right: var(--space-6);
margin-bottom: var(--space-4);
margin-left: var(--space-6);

/* After: 1 line */
margin: var(--space-4) var(--space-6);

/* Combine similar selectors */
/* Before: Separate rules */
.btn-primary { background: var(--color-primary); }
.btn-secondary { background: var(--color-secondary); }
.btn-success { background: var(--color-success); }

/* After: Combined */
.btn-primary,
.btn-secondary,
.btn-success {
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-4);
  transition: var(--transition-all);
}

.btn-primary { background: var(--color-primary); }
.btn-secondary { background: var(--color-secondary); }
.btn-success { background: var(--color-success); }

/* Use efficient custom properties */
/* Before: Repeated values */
.component-1 { box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1); }
.component-2 { box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1); }
.component-3 { box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1); }

/* After: Design token */
:root { --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.1); }
.component-1,
.component-2,
.component-3 { box-shadow: var(--shadow-sm); }
```

### Tree Shaking CSS

```javascript
// PurgeCSS configuration
module.exports = {
  content: [
    './src/**/*.html',
    './src/**/*.razor',
    './src/**/*.js'
  ],
  css: ['./wwwroot/css/**/*.css'],
  safelist: [
    // Preserve dynamic classes
    /^btn-/,
    /^card-/,
    /^nav-/,
    // Preserve state classes
    'active',
    'disabled',
    'loading',
    // Preserve utility classes
    /^m[xy]?-/,
    /^p[xy]?-/
  ],
  defaultExtractor: content => content.match(/[\w-/:]+(?<!:)/g) || []
};
```

## Selector Performance

### Efficient Selector Patterns

```css
/* Fast: Class selectors */
.component { }              /* Fastest */
.component__element { }     /* Fast */

/* Moderate: Attribute selectors */
[data-state="active"] { }   /* Moderate */
input[type="text"] { }      /* Moderate */

/* Slow: Complex selectors */
.component > div + div { }  /* Slow */
.component div:nth-child(odd) { } /* Slower */

/* Slowest: Universal and complex combinators */
* { }                       /* Slowest */
.component * div { }        /* Very slow */
```

### Optimized Selector Structure

```css
/* Good: Direct, semantic selectors */
.card { }
.card__header { }
.card__title { }
.card--highlighted { }

/* Bad: Over-specific selectors */
div.container > div.card > div.header > h3.title { }

/* Good: Efficient pseudo-selectors */
.btn:hover { }
.form__input:focus { }

/* Bad: Complex pseudo-selectors */
.form input:not([type="hidden"]):not([disabled]):focus { }

/* Good: Scoped specificity */
.modal .btn { }

/* Bad: High specificity chains */
.page .content .section .modal .actions .btn { }
```

### BEM for Performance

```css
/* BEM methodology naturally creates efficient selectors */
.modal { }                    /* Block */
.modal__header { }            /* Element */
.modal__title { }             /* Element */
.modal__close { }             /* Element */
.modal--large { }             /* Modifier */
.modal__header--sticky { }    /* Element modifier */

/* Avoid nested selectors */
/* Bad */
.modal .header .title { }

/* Good */
.modal__title { }
```

## Rendering Optimization

### Layout Performance

```css
/* Prefer transform over changing layout properties */
/* Bad: Triggers layout */
.element:hover {
  width: 200px;
  height: 200px;
  left: 100px;
  top: 100px;
}

/* Good: Uses composite layer */
.element:hover {
  transform: scale(1.1) translate(10px, 10px);
}

/* Avoid layout-triggering properties in animations */
/* Bad: Causes reflow */
@keyframes slideIn {
  from { margin-left: -100px; }
  to { margin-left: 0; }
}

/* Good: Uses transform */
@keyframes slideIn {
  from { transform: translateX(-100px); }
  to { transform: translateX(0); }
}

/* Use will-change for animation optimization */
.animated-element {
  will-change: transform, opacity;
  transition: transform 0.3s ease, opacity 0.3s ease;
}

/* Remove will-change after animation */
.animated-element.animation-complete {
  will-change: auto;
}
```

### Paint Optimization

```css
/* Use transform3d to create composite layers */
.card {
  transform: translateZ(0); /* Creates new layer */
  /* or */
  will-change: transform;
}

/* Optimize background gradients */
/* Bad: Complex gradient */
background: linear-gradient(45deg, #ff0000, #00ff00, #0000ff, #ffff00);

/* Good: Simple gradient with design tokens */
background: var(--gradient-primary);

/* Use CSS containment */
.independent-component {
  contain: layout style paint;
}

.list-item {
  contain: layout;
}
```

### Composite Layer Management

```css
/* Strategic layer creation */
.modal {
  /* Create new layer for modal */
  transform: translateZ(0);
  position: fixed;
  z-index: var(--z-index-modal);
}

.sidebar {
  /* Isolate sidebar animations */
  will-change: transform;
  transform: translateX(var(--sidebar-offset));
}

/* Avoid excessive layer creation */
.list-item {
  /* Don't create layers for every list item */
  /* transform: translateZ(0); - Avoid this */
}

.list-item:hover {
  /* Create layer only when needed */
  transform: translateZ(0) scale(1.02);
}
```

## Loading Strategies

### Progressive CSS Loading

```html
<!-- Critical CSS inlined -->
<style>/* Critical styles */</style>

<!-- High priority - Design system -->
<link rel="preload" href="design-system.css" as="style" onload="this.rel='stylesheet'">

<!-- Medium priority - Components -->
<link rel="prefetch" href="components.css" as="style">

<!-- Low priority - Page specific -->
<script>
  if (document.querySelector('.dashboard')) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'dashboard.css';
    document.head.appendChild(link);
  }
</script>
```

### CSS Modules and Code Splitting

```javascript
// Dynamic CSS imports
async function loadDashboardStyles() {
  if (!document.querySelector('#dashboard-styles')) {
    const { default: styles } = await import('./dashboard.css');
    const link = document.createElement('link');
    link.id = 'dashboard-styles';
    link.rel = 'stylesheet';
    link.href = styles;
    document.head.appendChild(link);
  }
}

// Component-based CSS loading
class Dashboard {
  async connectedCallback() {
    await loadDashboardStyles();
    this.render();
  }
}
```

### Resource Hints

```html
<!-- Preload critical CSS -->
<link rel="preload" href="critical.css" as="style">

<!-- Prefetch likely needed CSS -->
<link rel="prefetch" href="components.css" as="style">

<!-- Preconnect to font providers -->
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>

<!-- DNS prefetch for CDN assets -->
<link rel="dns-prefetch" href="//cdn.example.com">
```

## Design Token Efficiency

### Token Organization for Performance

```css
/* Efficient token structure */
:root {
  /* Base tokens - Used frequently */
  --color-primary: #3b82f6;
  --color-text-primary: #111827;
  --space-4: 1rem;
  --text-base: 1rem;
  
  /* Computed tokens - Derived from base */
  --color-primary-hover: color-mix(in srgb, var(--color-primary) 90%, black);
  --space-component: var(--space-4);
  --font-size-body: var(--text-base);
}

/* Conditional tokens for reduced bundle size */
@media (prefers-color-scheme: dark) {
  :root {
    --color-text-primary: #f9fafb;
    --color-bg-primary: #111827;
  }
}

@media (min-width: 768px) {
  :root {
    --space-component: var(--space-6);
    --font-size-body: var(--text-lg);
  }
}
```

### Token Usage Optimization

```css
/* Efficient token usage */
.component {
  /* Use composite tokens */
  margin: var(--space-component);
  font-size: var(--font-size-body);
  color: var(--color-text-primary);
}

/* Avoid token overuse */
/* Bad: Too granular */
.element {
  margin-top: var(--space-component-top);
  margin-right: var(--space-component-right);
  margin-bottom: var(--space-component-bottom);
  margin-left: var(--space-component-left);
}

/* Good: Semantic grouping */
.element {
  margin: var(--space-component-vertical) var(--space-component-horizontal);
}
```

### Dynamic Token Updates

```css
/* Efficient theme switching */
[data-theme="dark"] {
  color-scheme: dark;
  --color-text-primary: #f9fafb;
  --color-bg-primary: #111827;
}

/* Batch token updates */
.theme-transition {
  transition: 
    background-color 0.2s ease,
    color 0.2s ease,
    border-color 0.2s ease;
}
```

## Animation Performance

### Hardware-Accelerated Animations

```css
/* Use transform and opacity for smooth animations */
.modal {
  transform: scale(0.8) translateY(-20px);
  opacity: 0;
  transition: transform 0.3s ease, opacity 0.3s ease;
}

.modal--open {
  transform: scale(1) translateY(0);
  opacity: 1;
}

/* Avoid animating layout properties */
/* Bad */
.expanding-box {
  width: 100px;
  transition: width 0.3s ease;
}

.expanding-box:hover {
  width: 200px;
}

/* Good */
.expanding-box {
  transform: scaleX(1);
  transition: transform 0.3s ease;
}

.expanding-box:hover {
  transform: scaleX(2);
}
```

### Efficient Keyframe Animations

```css
/* Optimized keyframes */
@keyframes slideIn {
  0% {
    transform: translateX(-100%);
    opacity: 0;
  }
  100% {
    transform: translateX(0);
    opacity: 1;
  }
}

/* Use animation-fill-mode to reduce layout recalculations */
.slide-in {
  animation: slideIn 0.3s ease forwards;
}

/* Batch similar animations */
.fade-in-up {
  animation: fadeInUp 0.6s ease-out;
}

@keyframes fadeInUp {
  0% {
    opacity: 0;
    transform: translateY(30px);
  }
  100% {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### Animation Performance Patterns

```css
/* Stagger animations for better perceived performance */
.stagger-children > * {
  animation: fadeInUp 0.6s ease-out;
  animation-fill-mode: both;
}

.stagger-children > *:nth-child(1) { animation-delay: 0.1s; }
.stagger-children > *:nth-child(2) { animation-delay: 0.2s; }
.stagger-children > *:nth-child(3) { animation-delay: 0.3s; }

/* Reduce motion for accessibility */
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

## Memory Management

### CSS Memory Optimization

```css
/* Avoid memory-intensive selectors */
/* Bad: Creates large selector cache */
div div div div div { }

/* Good: Specific, efficient selectors */
.nested-content { }

/* Limit pseudo-element usage */
/* Bad: Excessive pseudo-elements */
.element::before,
.element::after,
.element::first-line,
.element::first-letter {
  content: '';
}

/* Good: Targeted pseudo-element usage */
.element::before {
  content: '';
  /* Specific styling */
}

/* Clean up unused styles */
/* Remove commented code and unused rules */
```

### Resource Cleanup

```css
/* Remove will-change after animations */
.element {
  will-change: transform;
  transition: transform 0.3s ease;
}

.element.animation-complete {
  will-change: auto;
}

/* Limit composite layers */
.card {
  /* Don't create unnecessary layers */
  /* transform: translateZ(0); - Remove if not needed */
}

.card:hover {
  /* Create layer only when needed */
  transform: translateZ(0) scale(1.02);
}
```

## Monitoring and Metrics

### Performance Metrics to Track

```javascript
// CSS performance monitoring
const observer = new PerformanceObserver((list) => {
  for (const entry of list.getEntries()) {
    if (entry.name.includes('.css')) {
      console.log('CSS Load Time:', entry.name, entry.duration);
    }
  }
});

observer.observe({ entryTypes: ['resource'] });

// Critical CSS coverage
function measureCriticalCSS() {
  const criticalCSS = document.querySelector('style');
  const allCSS = document.querySelectorAll('link[rel="stylesheet"]');
  
  return {
    criticalSize: criticalCSS?.textContent.length || 0,
    totalStylesheets: allCSS.length,
    timestamp: Date.now()
  };
}

// Layout thrashing detection
let lastTime = 0;
function detectLayoutThrashing() {
  const now = performance.now();
  if (now - lastTime < 16) { // Less than 60fps
    console.warn('Possible layout thrashing detected');
  }
  lastTime = now;
  requestAnimationFrame(detectLayoutThrashing);
}
```

### Bundle Analysis

```bash
# Analyze CSS bundle size
npm install -g bundle-analyzer

# Generate bundle report
bundle-analyzer analyze --css ./wwwroot/css/**/*.css

# Critical CSS size check
du -h ./wwwroot/css/critical.css

# Unused CSS detection
npm install -g purifycss
purifycss ./wwwroot/css/*.css ./Pages/**/*.razor --info
```

### Performance Budgets

```json
{
  "budgets": {
    "critical-css": "8KB",
    "total-css": "50KB",
    "first-contentful-paint": "1.5s",
    "largest-contentful-paint": "2.5s"
  },
  "thresholds": {
    "css-load-time": "200ms",
    "style-recalculation": "50ms",
    "layout-duration": "16ms"
  }
}
```

## Performance Testing

### Automated Performance Testing

```javascript
// Lighthouse CSS audits
const lighthouse = require('lighthouse');
const chromeLauncher = require('chrome-launcher');

async function auditCSS() {
  const chrome = await chromeLauncher.launch({chromeFlags: ['--headless']});
  const options = {
    logLevel: 'info',
    output: 'json',
    onlyCategories: ['performance'],
    port: chrome.port,
  };
  
  const runnerResult = await lighthouse('http://localhost:3000', options);
  
  const cssAudits = runnerResult.lhr.audits;
  console.log('Unused CSS:', cssAudits['unused-css-rules']);
  console.log('Critical Request Chains:', cssAudits['critical-request-chains']);
  
  await chrome.kill();
}
```

### Manual Testing Checklist

#### Load Performance
- [ ] **Critical CSS** under 8KB
- [ ] **Total CSS bundle** under 50KB
- [ ] **First Contentful Paint** under 1.5s
- [ ] **Largest Contentful Paint** under 2.5s

#### Runtime Performance
- [ ] **Smooth animations** at 60fps
- [ ] **No layout thrashing** during interactions
- [ ] **Efficient selector matching** (under 50ms style recalculation)
- [ ] **Proper layer usage** (no excessive composite layers)

#### Memory Usage
- [ ] **No memory leaks** from CSS animations
- [ ] **Efficient will-change** usage
- [ ] **Clean composite layer** management

### Performance Optimization Workflow

```bash
# 1. Audit current performance
npm run lighthouse:css

# 2. Analyze bundle size
npm run analyze:css

# 3. Extract critical CSS
npm run extract:critical

# 4. Optimize assets
npm run optimize:css

# 5. Test performance
npm run test:performance

# 6. Monitor in production
npm run monitor:css
```

### Common Performance Anti-Patterns

```css
/* Anti-pattern: Expensive selectors */
/* Bad */
* { box-sizing: border-box; }
.component * div:nth-child(3n+1) { }

/* Good */
.component { box-sizing: border-box; }
.component__item--third { }

/* Anti-pattern: Layout-triggering animations */
/* Bad */
@keyframes expand {
  from { width: 100px; height: 100px; }
  to { width: 200px; height: 200px; }
}

/* Good */
@keyframes expand {
  from { transform: scale(1); }
  to { transform: scale(2); }
}

/* Anti-pattern: Excessive specificity */
/* Bad */
body div.container > div.content > div.card > div.header > h3 { }

/* Good */
.card__title { }
```

---

Following these performance optimization guidelines ensures Conduit's CSS delivers fast load times, smooth interactions, and efficient resource usage across all devices and network conditions.