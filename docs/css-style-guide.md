# Conduit CSS Style Guide

## Table of Contents

1. [Overview](#overview)
2. [Design Token System](#design-token-system)
3. [Typography](#typography)
4. [Color Palette](#color-palette)
5. [Spacing System](#spacing-system)
6. [Component Library](#component-library)
7. [Layout Patterns](#layout-patterns)
8. [BEM Methodology](#bem-methodology)
9. [Responsive Design](#responsive-design)
10. [Accessibility Guidelines](#accessibility-guidelines)
11. [Performance Best Practices](#performance-best-practices)
12. [Development Guidelines](#development-guidelines)
13. [File Organization](#file-organization)
14. [Maintenance](#maintenance)

## Overview

Conduit uses a comprehensive design system built on **275+ design tokens**, **BEM methodology**, and **modern CSS features** to ensure consistency, maintainability, and scalability across the application.

### Key Principles

- **Token-driven design** - All values reference CSS custom properties
- **BEM naming convention** - Clear, semantic class names
- **Mobile-first approach** - Progressive enhancement for larger screens
- **Accessibility-first** - WCAG 2.1 AA compliance
- **Performance-optimized** - Minimal CSS footprint with maximum impact

### Architecture Overview

```
css/
├── design-system.css     # 275+ design tokens (colors, spacing, typography)
├── base/                 # Global styles and resets
├── components/           # Reusable UI components
│   ├── buttons.css       # All button variants and states
│   ├── cards.css         # Card components and layouts
│   ├── forms.css         # Form controls and validation
│   └── navigation.css    # Navigation components
├── layout/              # Layout systems and containers
│   ├── grid.css         # Grid systems (Bootstrap + CSS Grid)
│   ├── header.css       # Header and top navigation
│   ├── sidebar.css      # Sidebar navigation
│   └── main.css         # Main content layouts
└── utilities/           # Utility classes and helpers
```

## Design Token System

### Token Categories

Our design system includes **275+ tokens** organized into semantic categories:

#### Color Tokens (48 tokens)
- **Primary palette**: `--color-primary-50` through `--color-primary-950`
- **Semantic colors**: `--color-success`, `--color-warning`, `--color-danger`
- **Text colors**: `--color-text-primary`, `--color-text-secondary`, `--color-text-muted`
- **Background colors**: `--color-bg-primary`, `--color-bg-secondary`, `--color-bg-tertiary`

#### Spacing Tokens (20 tokens)
- **Scale**: `--space-0` (0) through `--space-80` (20rem)
- **Semantic spacing**: `--space-component`, `--space-section`, `--space-page`

#### Typography Tokens (35 tokens)
- **Font sizes**: `--text-xs` through `--text-6xl`
- **Font weights**: `--font-thin` through `--font-black`
- **Line heights**: `--leading-none` through `--leading-loose`
- **Font families**: `--font-sans`, `--font-serif`, `--font-mono`

#### Border Radius Tokens (8 tokens)
- **Scale**: `--radius-none` through `--radius-full`
- **Semantic radius**: `--radius-sm`, `--radius-md`, `--radius-lg`

#### Shadow Tokens (8 tokens)
- **Elevation system**: `--shadow-sm` through `--shadow-2xl`
- **Inset shadows**: `--shadow-inner`

#### Transition Tokens (6 tokens)
- **Common transitions**: `--transition-all`, `--transition-colors`, `--transition-shadow`

### Usage Examples

```css
/* Good: Using design tokens */
.card {
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  padding: var(--space-6);
  box-shadow: var(--shadow-sm);
}

/* Bad: Hard-coded values */
.card {
  background-color: #ffffff;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}
```

## Typography

### Font Stack

```css
:root {
  --font-sans: 'Inter', 'Roboto', 'Helvetica Neue', Arial, sans-serif;
  --font-serif: 'Georgia', 'Times New Roman', serif;
  --font-mono: 'SF Mono', Monaco, 'Cascadia Code', 'Roboto Mono', monospace;
}
```

### Type Scale

| Token | Size | Usage |
|-------|------|-------|
| `--text-xs` | 0.75rem | Captions, small labels |
| `--text-sm` | 0.875rem | Body text (small) |
| `--text-base` | 1rem | Body text (default) |
| `--text-lg` | 1.125rem | Emphasized body text |
| `--text-xl` | 1.25rem | Small headings |
| `--text-2xl` | 1.5rem | Section headings |
| `--text-3xl` | 1.875rem | Page headings |
| `--text-4xl` | 2.25rem | Hero headings |

### Typography Classes

```css
/* Heading styles */
.heading-1 { font-size: var(--text-4xl); font-weight: var(--font-bold); }
.heading-2 { font-size: var(--text-3xl); font-weight: var(--font-bold); }
.heading-3 { font-size: var(--text-2xl); font-weight: var(--font-semibold); }

/* Body text */
.body-large { font-size: var(--text-lg); line-height: var(--leading-relaxed); }
.body-base { font-size: var(--text-base); line-height: var(--leading-normal); }
.body-small { font-size: var(--text-sm); line-height: var(--leading-tight); }

/* Utility text */
.text-caption { font-size: var(--text-xs); color: var(--color-text-muted); }
.text-mono { font-family: var(--font-mono); }
```

## Color Palette

### Primary Color Scale

```css
:root {
  --color-primary-50: #eff6ff;
  --color-primary-100: #dbeafe;
  --color-primary-200: #bfdbfe;
  --color-primary-300: #93c5fd;
  --color-primary-400: #60a5fa;
  --color-primary-500: #3b82f6;  /* Primary */
  --color-primary-600: #2563eb;
  --color-primary-700: #1d4ed8;
  --color-primary-800: #1e40af;
  --color-primary-900: #1e3a8a;
  --color-primary-950: #172554;
}
```

### Semantic Colors

```css
:root {
  /* Status colors */
  --color-success: #10b981;
  --color-warning: #f59e0b;
  --color-danger: #ef4444;
  --color-info: #06b6d4;
  
  /* Text colors */
  --color-text-primary: #111827;
  --color-text-secondary: #374151;
  --color-text-muted: #6b7280;
  --color-text-inverse: #ffffff;
  
  /* Background colors */
  --color-bg-primary: #ffffff;
  --color-bg-secondary: #f9fafb;
  --color-bg-tertiary: #f3f4f6;
}
```

### Dark Mode Support

```css
@media (prefers-color-scheme: dark) {
  :root {
    --color-text-primary: #f9fafb;
    --color-text-secondary: #d1d5db;
    --color-text-muted: #9ca3af;
    --color-bg-primary: #111827;
    --color-bg-secondary: #1f2937;
    --color-bg-tertiary: #374151;
  }
}
```

## Spacing System

### Spacing Scale

Our spacing system follows a **consistent 4px base unit**:

| Token | Value | Pixels | Usage |
|-------|-------|--------|-------|
| `--space-0` | 0 | 0px | No spacing |
| `--space-1` | 0.25rem | 4px | Fine details |
| `--space-2` | 0.5rem | 8px | Small gaps |
| `--space-3` | 0.75rem | 12px | Default element spacing |
| `--space-4` | 1rem | 16px | Component spacing |
| `--space-5` | 1.25rem | 20px | Medium spacing |
| `--space-6` | 1.5rem | 24px | Large spacing |
| `--space-8` | 2rem | 32px | Section spacing |
| `--space-10` | 2.5rem | 40px | Page spacing |
| `--space-12` | 3rem | 48px | Large sections |

### Layout Tokens

```css
:root {
  /* Layout spacing */
  --nav-height: 4rem;
  --sidebar-width: 16rem;
  --container-max-width: 1200px;
  
  /* Breakpoints */
  --breakpoint-sm: 576px;
  --breakpoint-md: 768px;
  --breakpoint-lg: 992px;
  --breakpoint-xl: 1200px;
  --breakpoint-xxl: 1400px;
}
```

## Component Library

### Button Components

#### Primary Buttons
```css
.btn--primary {
  background-color: var(--color-primary);
  color: var(--color-text-inverse);
  border: 1px solid var(--color-primary);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  font-weight: var(--font-medium);
  transition: var(--transition-all);
}
```

#### Button Sizes
- `.btn--sm` - Small buttons (padding: `--space-2` `--space-3`)
- `.btn--md` - Medium buttons (default)
- `.btn--lg` - Large buttons (padding: `--space-4` `--space-6`)

#### Button States
- `.btn--loading` - Loading state with spinner
- `.btn--disabled` - Disabled state
- `.btn:hover` - Hover effects
- `.btn:focus` - Focus states for accessibility

### Card Components

#### Basic Card
```css
.card {
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  padding: var(--space-6);
  transition: var(--transition-shadow);
}

.card:hover {
  box-shadow: var(--shadow-md);
}
```

#### Card Variants
- `.card--elevated` - Higher elevation
- `.card--flat` - No shadow, bordered
- `.card--interactive` - Hover effects for clickable cards

### Form Components

#### Input Fields
```css
.form-control {
  width: 100%;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--color-border-light);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  line-height: var(--leading-normal);
  transition: var(--transition-all);
}

.form-control:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-color: var(--color-primary);
}
```

#### Validation States
- `.form-control--valid` - Valid input styling
- `.form-control--invalid` - Error state styling
- `.form-control--warning` - Warning state styling

### Navigation Components

#### Top Navigation
```css
.header {
  height: var(--nav-height);
  background: var(--gradient-primary);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 var(--space-4);
  box-shadow: var(--shadow-sm);
}
```

#### Sidebar Navigation
```css
.sidebar {
  width: var(--sidebar-width);
  height: 100vh;
  background: var(--gradient-sidebar);
  position: fixed;
  top: 0;
  left: 0;
  overflow-y: auto;
}
```

## Layout Patterns

### Grid System

#### Bootstrap-compatible Grid
```css
.container {
  max-width: var(--container-max-width);
  margin: 0 auto;
  padding: 0 var(--space-4);
}

.row {
  display: flex;
  flex-wrap: wrap;
  margin: 0 calc(var(--space-2) * -1);
}

.col {
  flex: 1 0 0%;
  padding: 0 var(--space-2);
}
```

#### Modern CSS Grid
```css
.grid {
  display: grid;
  gap: var(--space-4);
}

.grid--auto-fit {
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
}

.grid--2 { grid-template-columns: repeat(2, 1fr); }
.grid--3 { grid-template-columns: repeat(3, 1fr); }
.grid--4 { grid-template-columns: repeat(4, 1fr); }
```

### Layout Containers

#### Main Content Layout
```css
.main {
  flex: 1;
  min-width: 0;
  padding: var(--space-6) var(--space-8);
  background-color: var(--color-bg-secondary);
}
```

#### Dashboard Layout
```css
.dashboard {
  display: grid;
  gap: var(--space-6);
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
}
```

## BEM Methodology

### Naming Convention

```css
/* Block */
.card { }

/* Element */
.card__header { }
.card__body { }
.card__footer { }

/* Modifier */
.card--large { }
.card--highlighted { }

/* Block Element Modifier */
.card__header--sticky { }
```

### BEM Examples

#### Navigation Component
```css
/* Block */
.nav { }

/* Elements */
.nav__list { }
.nav__item { }
.nav__link { }

/* Modifiers */
.nav--horizontal { }
.nav--vertical { }
.nav__link--active { }
.nav__link--disabled { }
```

#### Form Component
```css
/* Block */
.form { }

/* Elements */
.form__group { }
.form__label { }
.form__input { }
.form__error { }

/* Modifiers */
.form--inline { }
.form__input--large { }
.form__input--invalid { }
```

### BEM Guidelines

1. **Use semantic names** - Names should describe purpose, not appearance
2. **Avoid deep nesting** - Maximum 2 levels of elements
3. **Use modifiers sparingly** - Only for variations of the base component
4. **Be consistent** - Follow established patterns within the codebase

## Responsive Design

### Breakpoint Strategy

We use a **mobile-first approach** with these breakpoints:

```css
/* Mobile first (default) */
@media (min-width: 576px) { /* Small tablets */ }
@media (min-width: 768px) { /* Tablets */ }
@media (min-width: 992px) { /* Small laptops */ }
@media (min-width: 1200px) { /* Laptops */ }
@media (min-width: 1400px) { /* Large screens */ }
```

### Responsive Patterns

#### Responsive Grid
```css
.grid--responsive {
  grid-template-columns: 1fr;
}

@media (min-width: 768px) {
  .grid--responsive {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .grid--responsive {
    grid-template-columns: repeat(3, 1fr);
  }
}
```

#### Responsive Typography
```css
.heading-responsive {
  font-size: var(--text-2xl);
}

@media (min-width: 768px) {
  .heading-responsive {
    font-size: var(--text-3xl);
  }
}

@media (min-width: 1024px) {
  .heading-responsive {
    font-size: var(--text-4xl);
  }
}
```

### Container Queries

For modern browsers supporting container queries:

```css
.card-container {
  container-type: inline-size;
}

@container (min-width: 400px) {
  .card {
    padding: var(--space-8);
  }
}
```

## Accessibility Guidelines

### Focus Management

```css
/* Focus styles */
.btn:focus,
.form-control:focus,
.nav__link:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}

/* Focus-visible for mouse users */
.btn:focus:not(:focus-visible) {
  outline: none;
}
```

### High Contrast Support

```css
@media (prefers-contrast: high) {
  .card,
  .btn,
  .form-control {
    border-width: 2px;
  }
}
```

### Reduced Motion Support

```css
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

### Screen Reader Support

```css
/* Screen reader only text */
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

## Performance Best Practices

### CSS Organization

1. **Critical CSS first** - Load essential styles inline
2. **Component-based loading** - Load only needed components
3. **Media query optimization** - Group by breakpoint
4. **Selector efficiency** - Avoid deep nesting

### CSS Custom Properties

```css
/* Efficient custom property usage */
:root {
  --primary-hue: 214;
  --primary-saturation: 100%;
}

.btn--primary {
  background-color: hsl(var(--primary-hue), var(--primary-saturation), 50%);
}

.btn--primary:hover {
  background-color: hsl(var(--primary-hue), var(--primary-saturation), 45%);
}
```

### Avoiding Common Performance Issues

```css
/* Good: Efficient selectors */
.card { }
.card__header { }

/* Bad: Over-specific selectors */
div.container > div.card > div.header { }

/* Good: Hardware acceleration */
.modal {
  transform: translateZ(0);
  will-change: transform;
}

/* Bad: Expensive properties */
.expensive {
  box-shadow: 0 0 0 1px inset, 0 0 0 2px inset, 0 0 0 3px inset;
}
```

## Development Guidelines

### CSS Writing Standards

1. **Use design tokens** - Always reference CSS custom properties
2. **Follow BEM naming** - Consistent class naming convention
3. **Mobile-first approach** - Start with mobile styles
4. **Semantic HTML** - Use appropriate HTML elements
5. **Progressive enhancement** - Layer on advanced features

### Code Style

```css
/* Property order */
.component {
  /* Display & Layout */
  display: flex;
  position: relative;
  top: 0;
  
  /* Box model */
  width: 100%;
  height: auto;
  margin: var(--space-4);
  padding: var(--space-6);
  
  /* Typography */
  font-size: var(--text-base);
  font-weight: var(--font-medium);
  line-height: var(--leading-normal);
  
  /* Visual */
  background-color: var(--color-bg-primary);
  border: 1px solid var(--color-border-light);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-sm);
  
  /* Animation */
  transition: var(--transition-all);
}
```

### Documentation Requirements

1. **Component documentation** - Usage examples and variants
2. **Token documentation** - Purpose and usage guidelines
3. **Accessibility notes** - ARIA requirements and focus management
4. **Browser support** - Feature support and fallbacks

## File Organization

### Directory Structure

```
css/
├── design-system.css     # Design tokens and CSS custom properties
├── base/                 # Base styles and resets
│   ├── reset.css        # CSS reset/normalize
│   ├── typography.css   # Base typography styles
│   └── utilities.css    # Utility classes
├── components/          # Component styles
│   ├── buttons.css      # Button component variants
│   ├── cards.css        # Card component styles
│   ├── forms.css        # Form control styles
│   └── navigation.css   # Navigation components
├── layout/             # Layout and grid systems
│   ├── grid.css        # Grid systems (Bootstrap + CSS Grid)
│   ├── header.css      # Header layout and positioning
│   ├── sidebar.css     # Sidebar layout and navigation
│   └── main.css        # Main content area layouts
└── pages/              # Page-specific styles (if needed)
    ├── dashboard.css   # Dashboard-specific styles
    └── auth.css        # Authentication page styles
```

### Import Order

```css
/* 1. Design tokens */
@import 'design-system.css';

/* 2. Base styles */
@import 'base/reset.css';
@import 'base/typography.css';
@import 'base/utilities.css';

/* 3. Layout */
@import 'layout/grid.css';
@import 'layout/header.css';
@import 'layout/sidebar.css';
@import 'layout/main.css';

/* 4. Components */
@import 'components/buttons.css';
@import 'components/cards.css';
@import 'components/forms.css';
@import 'components/navigation.css';

/* 5. Page-specific (if needed) */
@import 'pages/dashboard.css';
```

## Maintenance

### Regular Maintenance Tasks

1. **Design token audit** - Review unused tokens quarterly
2. **Component audit** - Identify unused components and styles
3. **Performance review** - Monitor CSS bundle size and loading times
4. **Accessibility audit** - Test with screen readers and keyboard navigation
5. **Browser compatibility** - Test across supported browsers

### Updating the Design System

1. **Propose changes** - Document rationale and impact
2. **Update tokens** - Modify design-system.css
3. **Update components** - Propagate changes to affected components
4. **Test thoroughly** - Verify visual and functional integrity
5. **Document changes** - Update style guide and component docs

### Deprecation Process

1. **Mark as deprecated** - Add comments and console warnings
2. **Provide migration path** - Document replacement patterns
3. **Grace period** - Allow time for migration (minimum 2 releases)
4. **Remove deprecated code** - Clean up after grace period

### Version Control

- **Semantic versioning** for design system releases
- **Change logs** documenting all modifications
- **Migration guides** for breaking changes
- **Backward compatibility** maintained when possible

---

This style guide is a living document. Please update it as the design system evolves and new patterns emerge.