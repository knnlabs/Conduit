# CSS Development Guidelines

## Overview

These guidelines establish best practices for CSS development within the Conduit project, ensuring consistency, maintainability, and performance across the codebase.

## Table of Contents

1. [General Principles](#general-principles)
2. [Code Organization](#code-organization)
3. [Naming Conventions](#naming-conventions)
4. [Design Token Usage](#design-token-usage)
5. [CSS Writing Standards](#css-writing-standards)
6. [BEM Methodology](#bem-methodology)
7. [Responsive Design](#responsive-design)
8. [Performance Guidelines](#performance-guidelines)
9. [Accessibility Requirements](#accessibility-requirements)
10. [Quality Assurance](#quality-assurance)
11. [Documentation Standards](#documentation-standards)
12. [Code Review Process](#code-review-process)

## General Principles

### 1. Consistency First
- **Use design tokens exclusively** - Never hard-code values
- **Follow established patterns** - Refer to existing components before creating new ones
- **Maintain naming conventions** - Stick to BEM methodology throughout
- **Document everything** - Every component should have usage examples

### 2. Progressive Enhancement
- **Mobile-first approach** - Start with mobile styles, enhance for larger screens
- **Graceful degradation** - Ensure basic functionality works everywhere
- **Feature detection** - Use `@supports` for advanced CSS features
- **Accessibility baseline** - Meet WCAG 2.1 AA standards minimum

### 3. Performance Awareness
- **Minimize specificity** - Keep selectors simple and efficient
- **Reduce redundancy** - Use design tokens to avoid duplicate values
- **Optimize loading** - Critical CSS first, progressive enhancement
- **Monitor bundle size** - Track CSS file size growth

### 4. Maintainability
- **Modular architecture** - Component-based organization
- **Clear dependencies** - Explicit relationships between components
- **Version documentation** - Track changes and breaking updates
- **Deprecation process** - Graceful removal of outdated styles

## Code Organization

### File Structure

```
css/
├── design-system.css         # Design tokens (275+ CSS custom properties)
├── base/                     # Global styles
│   ├── reset.css            # CSS reset/normalize
│   ├── typography.css       # Base typography
│   └── utilities.css        # Utility classes
├── components/              # UI components
│   ├── buttons.css          # Button variants and states
│   ├── cards.css            # Card components
│   ├── forms.css            # Form controls and validation
│   └── navigation.css       # Navigation components
├── layout/                  # Layout systems
│   ├── grid.css            # Grid systems and utilities
│   ├── header.css          # Header layout
│   ├── sidebar.css         # Sidebar navigation
│   └── main.css            # Main content layouts
└── pages/                  # Page-specific styles (when needed)
    ├── dashboard.css       # Dashboard-specific styles
    └── auth.css            # Authentication pages
```

### Import Order

```css
/* 1. Design system foundation */
@import 'design-system.css';

/* 2. Base styles */
@import 'base/reset.css';
@import 'base/typography.css';
@import 'base/utilities.css';

/* 3. Layout systems */
@import 'layout/grid.css';
@import 'layout/header.css';
@import 'layout/sidebar.css';
@import 'layout/main.css';

/* 4. Components (alphabetical order) */
@import 'components/buttons.css';
@import 'components/cards.css';
@import 'components/forms.css';
@import 'components/navigation.css';

/* 5. Page-specific styles (only when necessary) */
@import 'pages/dashboard.css';
```

### File Size Guidelines

- **Individual component files**: Maximum 1000 lines
- **Design system file**: No specific limit (currently 400+ lines)
- **Layout files**: Maximum 800 lines each
- **Page-specific files**: Maximum 500 lines
- **Total CSS bundle**: Monitor and optimize regularly

## Naming Conventions

### BEM (Block Element Modifier) Methodology

```css
/* Block */
.card { }

/* Elements */
.card__header { }
.card__body { }
.card__footer { }
.card__title { }
.card__actions { }

/* Modifiers */
.card--large { }
.card--highlighted { }
.card--interactive { }

/* Element Modifiers */
.card__header--sticky { }
.card__title--truncated { }
```

### Naming Rules

1. **Use semantic names** - Describe purpose, not appearance
   ```css
   /* Good */
   .btn--primary { }
   .text--muted { }
   .status--success { }
   
   /* Bad */
   .btn--blue { }
   .text--gray { }
   .box--green { }
   ```

2. **Maximum 3 BEM levels** - Avoid deep nesting
   ```css
   /* Good */
   .navigation { }
   .navigation__list { }
   .navigation__item { }
   .navigation__link { }
   
   /* Bad */
   .navigation__list__item__link__text { }
   ```

3. **Use consistent terminology**
   - **Actions**: `btn`, `link`, `action`
   - **Content**: `title`, `text`, `description`, `content`
   - **Layout**: `header`, `body`, `footer`, `sidebar`, `main`
   - **State**: `active`, `disabled`, `loading`, `error`, `success`

### CSS Custom Property Naming

```css
/* Category-based naming */
--color-primary-500          /* Color tokens */
--space-4                    /* Spacing tokens */
--text-lg                    /* Typography tokens */
--radius-md                  /* Border radius tokens */
--shadow-sm                  /* Shadow tokens */
--transition-all             /* Animation tokens */

/* Semantic naming */
--color-text-primary         /* Text colors */
--color-bg-secondary         /* Background colors */
--color-border-light         /* Border colors */
```

## Design Token Usage

### Mandatory Token Usage

**Always use design tokens for:**
- Colors (text, background, border)
- Spacing (margin, padding, gap)
- Typography (font-size, line-height, font-weight)
- Border radius
- Box shadows
- Transitions and animations

```css
/* Required: Use design tokens */
.component {
  background-color: var(--color-bg-primary);
  color: var(--color-text-secondary);
  padding: var(--space-4) var(--space-6);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  transition: var(--transition-all);
}

/* Forbidden: Hard-coded values */
.component {
  background-color: #ffffff;
  color: #374151;
  padding: 16px 24px;
  border-radius: 8px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  transition: all 0.15s ease-in-out;
}
```

### Token Categories

1. **Color Tokens** (48 total)
   - Primary scale: `--color-primary-50` to `--color-primary-950`
   - Semantic colors: `--color-success`, `--color-warning`, `--color-danger`
   - Text colors: `--color-text-primary`, `--color-text-secondary`
   - Background colors: `--color-bg-primary`, `--color-bg-secondary`

2. **Spacing Tokens** (20 total)
   - Scale: `--space-0` (0) to `--space-80` (20rem)
   - Layout: `--nav-height`, `--sidebar-width`

3. **Typography Tokens** (35 total)
   - Font sizes: `--text-xs` to `--text-9xl`
   - Font weights: `--font-thin` to `--font-black`
   - Line heights: `--leading-none` to `--leading-loose`

### Creating New Tokens

```css
/* Follow existing patterns */
:root {
  /* Add to existing category */
  --color-accent: #8b5cf6;
  --color-accent-light: #ede9fe;
  --color-accent-dark: #7c3aed;
  
  /* Create semantic reference */
  --color-highlight: var(--color-accent);
  --color-highlight-bg: var(--color-accent-light);
}
```

## CSS Writing Standards

### Property Order

Use the following property order (enforced by Stylelint):

```css
.component {
  /* Positioning */
  position: relative;
  top: 0;
  z-index: var(--z-index-dropdown);
  
  /* Display & Layout */
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  
  /* Box Model */
  width: 100%;
  height: auto;
  margin: var(--space-4);
  padding: var(--space-6);
  border: 1px solid var(--color-border-light);
  border-radius: var(--radius-md);
  
  /* Typography */
  font-size: var(--text-base);
  font-weight: var(--font-medium);
  line-height: var(--leading-normal);
  color: var(--color-text-primary);
  
  /* Visual */
  background-color: var(--color-bg-primary);
  box-shadow: var(--shadow-sm);
  opacity: 1;
  
  /* Animation */
  transition: var(--transition-all);
  transform: translateY(0);
}
```

### Code Formatting

```css
/* Use consistent formatting */
.selector {
  property: value;
}

/* Multiple selectors on separate lines */
.selector-one,
.selector-two,
.selector-three {
  property: value;
}

/* Group related properties */
.component {
  /* Layout */
  display: flex;
  justify-content: space-between;
  
  /* Appearance */
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-md);
  
  /* Animation */
  transition: var(--transition-all);
}

/* Comment complex calculations */
.layout {
  /* Calculate main content height: 100vh - header - footer */
  height: calc(100vh - var(--nav-height) - var(--footer-height));
}
```

### Selector Guidelines

```css
/* Good: Low specificity, semantic */
.card { }
.card__header { }
.card--highlighted { }

/* Bad: High specificity, brittle */
div.container > div.card > div.header.large { }

/* Good: Reusable, predictable */
.btn--primary:hover { }
.form__input:focus { }

/* Bad: Overly specific */
.page .form .fieldset .input[type="text"]:not(.disabled):focus { }
```

### Responsive Design Patterns

```css
/* Mobile-first approach */
.component {
  /* Mobile styles (default) */
  font-size: var(--text-base);
  padding: var(--space-4);
}

@media (min-width: 768px) {
  .component {
    /* Tablet styles */
    font-size: var(--text-lg);
    padding: var(--space-6);
  }
}

@media (min-width: 1024px) {
  .component {
    /* Desktop styles */
    font-size: var(--text-xl);
    padding: var(--space-8);
  }
}

/* Container queries (when supported) */
@supports (container-type: inline-size) {
  .card-container {
    container-type: inline-size;
  }
  
  @container (min-width: 400px) {
    .card {
      padding: var(--space-8);
    }
  }
}
```

## BEM Methodology

### Component Structure

```html
<!-- Block -->
<div class="modal">
  <!-- Elements -->
  <div class="modal__header">
    <h2 class="modal__title">Title</h2>
    <button class="modal__close">×</button>
  </div>
  
  <div class="modal__body">
    <p class="modal__text">Content</p>
  </div>
  
  <div class="modal__footer">
    <button class="modal__action modal__action--primary">Save</button>
    <button class="modal__action modal__action--secondary">Cancel</button>
  </div>
</div>

<!-- Modifiers -->
<div class="modal modal--large modal--centered">
  <!-- Modified block content -->
</div>
```

### CSS Implementation

```css
/* Block */
.modal {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  max-width: 32rem;
  width: 90%;
}

/* Elements */
.modal__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--space-6);
  border-bottom: 1px solid var(--color-border-light);
}

.modal__title {
  margin: 0;
  font-size: var(--text-xl);
  font-weight: var(--font-semibold);
  color: var(--color-text-primary);
}

.modal__close {
  background: none;
  border: none;
  font-size: var(--text-2xl);
  color: var(--color-text-muted);
  cursor: pointer;
  transition: var(--transition-colors);
}

.modal__close:hover {
  color: var(--color-text-primary);
}

.modal__body {
  padding: var(--space-6);
}

.modal__text {
  margin: 0;
  color: var(--color-text-secondary);
  line-height: var(--leading-relaxed);
}

.modal__footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  padding: var(--space-6);
  border-top: 1px solid var(--color-border-light);
}

.modal__action {
  padding: var(--space-3) var(--space-4);
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  font-size: var(--text-sm);
  font-weight: var(--font-medium);
  cursor: pointer;
  transition: var(--transition-all);
}

/* Element Modifiers */
.modal__action--primary {
  background-color: var(--color-primary);
  color: var(--color-text-on-primary);
  border-color: var(--color-primary);
}

.modal__action--primary:hover {
  background-color: var(--color-primary-600);
  border-color: var(--color-primary-600);
}

.modal__action--secondary {
  background-color: transparent;
  color: var(--color-text-secondary);
  border-color: var(--color-border-medium);
}

.modal__action--secondary:hover {
  background-color: var(--color-bg-tertiary);
  color: var(--color-text-primary);
}

/* Block Modifiers */
.modal--large {
  max-width: 48rem;
}

.modal--centered {
  text-align: center;
}

.modal--centered .modal__footer {
  justify-content: center;
}
```

### BEM Best Practices

1. **Single responsibility** - Each block serves one purpose
2. **No cascade dependencies** - Blocks work independently
3. **Predictable naming** - Clear hierarchy and relationships
4. **Modifier restraint** - Only for genuine variations
5. **Element independence** - Elements can be moved within their block

## Responsive Design

### Breakpoint Strategy

```css
/* Mobile First Breakpoints */
/* Default: 0px and up (mobile) */

@media (min-width: 576px) {
  /* Small tablets and large phones */
}

@media (min-width: 768px) {
  /* Tablets */
}

@media (min-width: 992px) {
  /* Small laptops */
}

@media (min-width: 1200px) {
  /* Laptops and desktops */
}

@media (min-width: 1400px) {
  /* Large screens */
}
```

### Responsive Component Patterns

```css
/* Responsive Grid */
.grid {
  display: grid;
  gap: var(--space-4);
  grid-template-columns: 1fr;
}

@media (min-width: 768px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

/* Responsive Typography */
.heading {
  font-size: var(--text-2xl);
  line-height: var(--leading-tight);
}

@media (min-width: 768px) {
  .heading {
    font-size: var(--text-3xl);
  }
}

@media (min-width: 1024px) {
  .heading {
    font-size: var(--text-4xl);
  }
}

/* Responsive Spacing */
.section {
  padding: var(--space-6) var(--space-4);
}

@media (min-width: 768px) {
  .section {
    padding: var(--space-8) var(--space-6);
  }
}

@media (min-width: 1024px) {
  .section {
    padding: var(--space-12) var(--space-8);
  }
}
```

## Performance Guidelines

### CSS Optimization

1. **Minimize HTTP requests** - Combine related CSS files
2. **Use efficient selectors** - Avoid complex descendant selectors
3. **Eliminate unused CSS** - Regular audits and cleanup
4. **Optimize for critical path** - Inline critical CSS
5. **Leverage browser caching** - Proper cache headers

### Selector Performance

```css
/* Fast: Class selectors */
.component { }
.component__element { }

/* Slower: Descendant selectors */
.component .element .nested { }

/* Slowest: Universal and complex selectors */
* { }
.component > div + div:nth-child(odd) { }

/* Efficient: Direct child combinators */
.component > .element { }

/* Inefficient: Deep nesting */
.page .sidebar .nav .list .item .link { }
```

### Critical CSS Strategy

```css
/* Critical CSS (inline in <head>) */
:root {
  /* Essential design tokens */
  --color-primary: #3b82f6;
  --color-text-primary: #111827;
  --color-bg-primary: #ffffff;
  --space-4: 1rem;
}

body {
  font-family: var(--font-sans);
  color: var(--color-text-primary);
  background-color: var(--color-bg-primary);
}

.header {
  /* Critical header styles */
}

/* Non-critical CSS (external file) */
.complex-component {
  /* Detailed component styles */
}
```

## Accessibility Requirements

### Minimum Standards

All CSS must meet **WCAG 2.1 AA** requirements:

- **Color contrast**: Minimum 4.5:1 for normal text, 3:1 for large text
- **Focus indicators**: Visible focus states for all interactive elements
- **Text scaling**: Support up to 200% zoom without horizontal scrolling
- **Motion preferences**: Respect `prefers-reduced-motion`

### Implementation Examples

```css
/* Focus Management */
.btn:focus,
.form__input:focus,
.nav__link:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}

/* High Contrast Support */
@media (prefers-contrast: high) {
  .card,
  .btn,
  .form__input {
    border-width: 2px;
  }
}

/* Reduced Motion Support */
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}

/* Color Contrast Compliance */
.text--muted {
  color: var(--color-text-muted); /* Must meet 4.5:1 contrast ratio */
}

.btn--secondary {
  border: 2px solid var(--color-primary); /* Ensure 3:1 contrast for non-text */
}

/* Screen Reader Support */
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
```

## Quality Assurance

### CSS Linting

We use **Stylelint** with custom rules to enforce standards:

```json
{
  "rules": {
    "color-no-hex": true,
    "declaration-property-value-disallowed-list": {
      "color": ["/^#/", "/^rgb/", "/^hsl/"],
      "background-color": ["/^#/", "/^rgb/", "/^hsl/"]
    },
    "selector-class-pattern": "^([a-z][a-z0-9]*)(-[a-z0-9]+)*(__[a-z0-9]+(-[a-z0-9]+)*)?(--[a-z0-9]+(-[a-z0-9]+)*)?$",
    "max-nesting-depth": 3,
    "selector-max-specificity": "0,3,2"
  }
}
```

### Pre-commit Hooks

```bash
# Run Stylelint before commits
npx stylelint "**/*.css" --fix

# Check for design token usage
grep -r "#[0-9a-fA-F]" css/ && echo "Hard-coded colors found!"

# Validate CSS syntax
npx postcss css/**/*.css --use autoprefixer --no-map --dir build/
```

### Testing Checklist

- [ ] **Stylelint passes** - No linting errors
- [ ] **Design tokens used** - No hard-coded values
- [ ] **BEM naming** - Follows methodology correctly
- [ ] **Responsive design** - Works on all breakpoints
- [ ] **Accessibility** - WCAG 2.1 AA compliance
- [ ] **Browser testing** - Cross-browser compatibility
- [ ] **Performance** - No significant bundle size increase

## Documentation Standards

### Component Documentation

Every component must include:

```css
/**
 * Card Component
 * 
 * A flexible content container with optional header, body, and footer.
 * 
 * @example
 * <div class="card">
 *   <div class="card__header">
 *     <h3 class="card__title">Title</h3>
 *   </div>
 *   <div class="card__body">
 *     <p>Content goes here.</p>
 *   </div>
 * </div>
 * 
 * @modifier --elevated - Adds higher box shadow
 * @modifier --interactive - Adds hover effects
 * 
 * @accessibility
 * - Use semantic HTML within card structure
 * - Ensure proper heading hierarchy
 * - Provide focus management for interactive cards
 */
.card {
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  transition: var(--transition-shadow);
}
```

### Change Documentation

```css
/**
 * CHANGELOG
 * 
 * v2.1.0 - 2024-01-15
 * - Added --interactive modifier
 * - Improved accessibility with focus states
 * - Updated to use new design tokens
 * 
 * v2.0.0 - 2023-12-01
 * - BREAKING: Renamed .card-header to .card__header
 * - BREAKING: Removed .card-large (use .card--large)
 * - Added BEM naming convention
 * 
 * v1.2.0 - 2023-11-15
 * - Added elevation variants
 * - Improved responsive design
 */
```

## Code Review Process

### Review Checklist

#### Design Token Compliance
- [ ] No hard-coded colors, spacing, or typography values
- [ ] All values reference CSS custom properties
- [ ] New tokens follow established naming conventions
- [ ] Semantic tokens used appropriately

#### BEM Methodology
- [ ] Class names follow BEM structure
- [ ] Maximum 3 levels of nesting
- [ ] Semantic, not presentational naming
- [ ] Consistent terminology usage

#### Responsive Design
- [ ] Mobile-first approach implemented
- [ ] All breakpoints tested
- [ ] Progressive enhancement applied
- [ ] Container queries used where appropriate

#### Accessibility
- [ ] Focus states defined for interactive elements
- [ ] Color contrast meets WCAG 2.1 AA
- [ ] Reduced motion preferences respected
- [ ] Screen reader considerations addressed

#### Performance
- [ ] Selector specificity kept low
- [ ] No redundant or duplicate styles
- [ ] Efficient property order followed
- [ ] Bundle size impact assessed

#### Documentation
- [ ] Component usage examples provided
- [ ] Modifiers and variants documented
- [ ] Accessibility requirements noted
- [ ] Breaking changes highlighted

### Review Process

1. **Automated Checks**
   - Stylelint validation
   - Design token usage verification
   - Accessibility scanning
   - Performance impact analysis

2. **Manual Review**
   - Code quality assessment
   - Design consistency check
   - Documentation completeness
   - Browser compatibility verification

3. **Testing Requirements**
   - Cross-browser testing
   - Responsive design validation
   - Accessibility testing with screen readers
   - Performance benchmarking

### Common Issues and Solutions

#### Hard-coded Values
```css
/* Problem */
.component {
  color: #374151;
  padding: 16px;
}

/* Solution */
.component {
  color: var(--color-text-secondary);
  padding: var(--space-4);
}
```

#### High Specificity
```css
/* Problem */
.page .sidebar .nav ul li a.active {
  color: var(--color-primary);
}

/* Solution */
.nav__link--active {
  color: var(--color-primary);
}
```

#### Missing Responsive Design
```css
/* Problem */
.component {
  font-size: var(--text-2xl);
}

/* Solution */
.component {
  font-size: var(--text-lg);
}

@media (min-width: 768px) {
  .component {
    font-size: var(--text-2xl);
  }
}
```

#### Poor Accessibility
```css
/* Problem */
.btn:focus {
  outline: none;
}

/* Solution */
.btn:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}
```

---

These guidelines ensure consistent, maintainable, and high-quality CSS across the Conduit project. All team members should familiarize themselves with these standards and refer to them during development and code review processes.