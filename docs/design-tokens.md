# Design Token Documentation

## Overview

Conduit's design system is built on **275+ design tokens** implemented as CSS custom properties. These tokens provide a single source of truth for all design decisions, ensuring consistency across the application and enabling easy theming and maintenance.

## Token Categories

### Color Tokens (48 total)

#### Primary Color Scale (11 tokens)
Complete color scale from light to dark for the primary brand color.

```css
:root {
  --color-primary-50: #eff6ff;   /* Lightest tint */
  --color-primary-100: #dbeafe;  /* Very light */
  --color-primary-200: #bfdbfe;  /* Light */
  --color-primary-300: #93c5fd;  /* Light medium */
  --color-primary-400: #60a5fa;  /* Medium light */
  --color-primary-500: #3b82f6;  /* Base primary color */
  --color-primary-600: #2563eb;  /* Medium dark */
  --color-primary-700: #1d4ed8;  /* Dark */
  --color-primary-800: #1e40af;  /* Very dark */
  --color-primary-900: #1e3a8a;  /* Darkest */
  --color-primary-950: #172554;  /* Deepest dark */
}
```

**Usage Guidelines:**
- Use `--color-primary-500` for primary actions and brand elements
- Use lighter shades (50-400) for backgrounds and subtle accents
- Use darker shades (600-950) for text on light backgrounds and hover states

#### Semantic Color Tokens (12 tokens)
Colors with semantic meaning for consistent UI communication.

```css
:root {
  /* Status Colors */
  --color-success: #10b981;      /* Success states, confirmations */
  --color-success-light: #d1fae5; /* Success backgrounds */
  --color-success-dark: #047857;  /* Success text */
  
  --color-warning: #f59e0b;       /* Warning states, cautions */
  --color-warning-light: #fef3c7; /* Warning backgrounds */
  --color-warning-dark: #d97706;  /* Warning text */
  
  --color-danger: #ef4444;        /* Error states, destructive actions */
  --color-danger-light: #fee2e2;  /* Error backgrounds */
  --color-danger-dark: #dc2626;   /* Error text */
  
  --color-info: #06b6d4;          /* Informational content */
  --color-info-light: #cffafe;    /* Info backgrounds */
  --color-info-dark: #0891b2;     /* Info text */
}
```

#### Text Color Tokens (8 tokens)
Hierarchical text colors for content organization.

```css
:root {
  --color-text-primary: #111827;    /* Main headings, important text */
  --color-text-secondary: #374151;  /* Body text, descriptions */
  --color-text-muted: #6b7280;      /* Captions, less important text */
  --color-text-disabled: #9ca3af;   /* Disabled text */
  --color-text-inverse: #ffffff;    /* Text on dark backgrounds */
  --color-text-link: #2563eb;       /* Links and interactive text */
  --color-text-link-hover: #1d4ed8; /* Hover state for links */
  --color-text-on-primary: #ffffff; /* Text on primary color backgrounds */
}
```

#### Background Color Tokens (6 tokens)
Layered background system for depth and organization.

```css
:root {
  --color-bg-primary: #ffffff;      /* Main content backgrounds */
  --color-bg-secondary: #f9fafb;    /* Page backgrounds, subtle containers */
  --color-bg-tertiary: #f3f4f6;     /* Subtle highlights, disabled states */
  --color-bg-overlay: rgba(0, 0, 0, 0.5); /* Modal overlays */
  --color-bg-elevated: #ffffff;     /* Cards, dropdowns, elevated surfaces */
  --color-bg-inset: #f3f4f6;       /* Inset areas, input backgrounds */
}
```

#### Border Color Tokens (5 tokens)
Border colors for various interface elements.

```css
:root {
  --color-border-light: #e5e7eb;    /* Subtle borders, dividers */
  --color-border-medium: #d1d5db;   /* Default borders */
  --color-border-dark: #9ca3af;     /* Emphasized borders */
  --color-border-focus: #3b82f6;    /* Focus state borders */
  --color-border-error: #ef4444;    /* Error state borders */
}
```

#### Gradient Tokens (6 tokens)
Pre-defined gradients for consistent visual effects.

```css
:root {
  --gradient-primary: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  --gradient-secondary: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
  --gradient-success: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
  --gradient-warning: linear-gradient(135deg, #fdbb2d 0%, #22c1c3 100%);
  --gradient-danger: linear-gradient(135deg, #ff6b6b 0%, #feca57 100%);
  --gradient-light: linear-gradient(135deg, #ffecd2 0%, #fcb69f 100%);
}
```

### Spacing Tokens (20 total)

#### Base Spacing Scale (16 tokens)
Consistent spacing system based on 4px increments.

```css
:root {
  --space-0: 0;           /* 0px - No spacing */
  --space-1: 0.25rem;     /* 4px - Fine details, borders */
  --space-2: 0.5rem;      /* 8px - Small gaps, tight spacing */
  --space-3: 0.75rem;     /* 12px - Default element spacing */
  --space-4: 1rem;        /* 16px - Component internal spacing */
  --space-5: 1.25rem;     /* 20px - Medium component spacing */
  --space-6: 1.5rem;      /* 24px - Large component spacing */
  --space-8: 2rem;        /* 32px - Section spacing */
  --space-10: 2.5rem;     /* 40px - Large section spacing */
  --space-12: 3rem;       /* 48px - Page-level spacing */
  --space-16: 4rem;       /* 64px - Large page sections */
  --space-20: 5rem;       /* 80px - Extra large spacing */
  --space-24: 6rem;       /* 96px - Hero sections */
  --space-32: 8rem;       /* 128px - Major page divisions */
  --space-40: 10rem;      /* 160px - Large page elements */
  --space-80: 20rem;      /* 320px - Maximum spacing */
}
```

**Usage Guidelines:**
- Use `--space-1` to `--space-3` for fine-tuning and small adjustments
- Use `--space-4` to `--space-6` for component internal spacing
- Use `--space-8` to `--space-12` for spacing between components
- Use `--space-16` and above for page-level layout spacing

#### Layout Spacing Tokens (4 tokens)
Semantic spacing for common layout patterns.

```css
:root {
  --space-component: var(--space-4);  /* Default component spacing */
  --space-section: var(--space-8);    /* Section spacing */
  --space-page: var(--space-12);      /* Page-level spacing */
  --space-gutter: var(--space-4);     /* Grid gutter spacing */
}
```

### Typography Tokens (35 total)

#### Font Size Scale (13 tokens)
Harmonious type scale for all text elements.

```css
:root {
  --text-xs: 0.75rem;     /* 12px - Captions, labels */
  --text-sm: 0.875rem;    /* 14px - Small body text */
  --text-base: 1rem;      /* 16px - Default body text */
  --text-lg: 1.125rem;    /* 18px - Large body text */
  --text-xl: 1.25rem;     /* 20px - Small headings */
  --text-2xl: 1.5rem;     /* 24px - Medium headings */
  --text-3xl: 1.875rem;   /* 30px - Large headings */
  --text-4xl: 2.25rem;    /* 36px - Extra large headings */
  --text-5xl: 3rem;       /* 48px - Display headings */
  --text-6xl: 3.75rem;    /* 60px - Hero headings */
  --text-7xl: 4.5rem;     /* 72px - Large display */
  --text-8xl: 6rem;       /* 96px - Extra large display */
  --text-9xl: 8rem;       /* 128px - Maximum display size */
}
```

#### Font Weight Scale (9 tokens)
Complete range of font weights for typographic hierarchy.

```css
:root {
  --font-thin: 100;       /* Thin weight */
  --font-extralight: 200; /* Extra light weight */
  --font-light: 300;      /* Light weight */
  --font-normal: 400;     /* Normal weight (default) */
  --font-medium: 500;     /* Medium weight */
  --font-semibold: 600;   /* Semi-bold weight */
  --font-bold: 700;       /* Bold weight */
  --font-extrabold: 800;  /* Extra bold weight */
  --font-black: 900;      /* Black weight */
}
```

#### Line Height Scale (6 tokens)
Line height variations for different content types.

```css
:root {
  --leading-none: 1;      /* Tight line height for headings */
  --leading-tight: 1.25;  /* Tight line height for large text */
  --leading-snug: 1.375;  /* Snug line height */
  --leading-normal: 1.5;  /* Normal line height (default) */
  --leading-relaxed: 1.625; /* Relaxed line height for reading */
  --leading-loose: 2;     /* Loose line height for special cases */
}
```

#### Font Family Tokens (4 tokens)
Consistent font stack definitions.

```css
:root {
  --font-sans: 'Inter', 'Roboto', 'Helvetica Neue', Arial, sans-serif;
  --font-serif: 'Georgia', 'Times New Roman', serif;
  --font-mono: 'SF Mono', Monaco, 'Cascadia Code', 'Roboto Mono', monospace;
  --font-display: 'Inter', 'Roboto', 'Helvetica Neue', Arial, sans-serif;
}
```

#### Letter Spacing Tokens (3 tokens)
Subtle adjustments for specific use cases.

```css
:root {
  --tracking-tight: -0.025em;  /* Tight letter spacing */
  --tracking-normal: 0;        /* Normal letter spacing */
  --tracking-wide: 0.025em;    /* Wide letter spacing */
}
```

### Border Radius Tokens (8 total)
Consistent corner radius system.

```css
:root {
  --radius-none: 0;          /* No radius - sharp corners */
  --radius-sm: 0.125rem;     /* 2px - Subtle rounding */
  --radius-base: 0.25rem;    /* 4px - Default radius */
  --radius-md: 0.375rem;     /* 6px - Medium radius */
  --radius-lg: 0.5rem;       /* 8px - Large radius */
  --radius-xl: 0.75rem;      /* 12px - Extra large radius */
  --radius-2xl: 1rem;        /* 16px - Very large radius */
  --radius-full: 9999px;     /* Full radius - circular */
}
```

**Usage Guidelines:**
- Use `--radius-sm` for small elements like tags and badges
- Use `--radius-base` to `--radius-md` for buttons and form controls
- Use `--radius-lg` to `--radius-xl` for cards and containers
- Use `--radius-full` for circular elements like avatars and icons

### Shadow Tokens (8 total)
Elevation system using consistent shadow patterns.

```css
:root {
  --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
  --shadow-base: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
  --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
  --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
  --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
  --shadow-2xl: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
  --shadow-inner: inset 0 2px 4px 0 rgba(0, 0, 0, 0.06);
  --shadow-none: none;
}
```

**Elevation Guidelines:**
- `--shadow-sm`: Subtle elevation for buttons and form controls
- `--shadow-base`: Default card elevation
- `--shadow-md`: Hover states and active elements
- `--shadow-lg`: Dropdowns and overlays
- `--shadow-xl`: Modals and important overlays
- `--shadow-2xl`: Hero elements and dramatic elevation

### Transition Tokens (6 total)
Consistent animation timing and easing.

```css
:root {
  --transition-all: all 0.15s ease-in-out;
  --transition-colors: color 0.15s ease-in-out, background-color 0.15s ease-in-out, border-color 0.15s ease-in-out;
  --transition-shadow: box-shadow 0.15s ease-in-out;
  --transition-transform: transform 0.15s ease-in-out;
  --transition-opacity: opacity 0.15s ease-in-out;
  --transition-fast: all 0.1s ease-in-out;
}
```

### Z-Index Tokens (8 total)
Stacking context management.

```css
:root {
  --z-index-dropdown: 1000;     /* Dropdowns, tooltips */
  --z-index-sticky: 1020;       /* Sticky headers, navigation */
  --z-index-banner: 1030;       /* Site-wide banners */
  --z-index-docked: 1040;       /* Docked elements */
  --z-index-modal: 1050;        /* Modal dialogs */
  --z-index-popover: 1060;      /* Popovers */
  --z-index-overlay: 1070;      /* Page overlays */
  --z-index-skiplink: 1080;     /* Skip navigation links */
}
```

### Layout Tokens (12 total)
Common layout dimensions and breakpoints.

```css
:root {
  /* Layout Dimensions */
  --nav-height: 4rem;           /* Top navigation height */
  --sidebar-width: 16rem;       /* Sidebar width */
  --footer-height: 4rem;        /* Footer height */
  --container-max-width: 1200px; /* Maximum container width */
  
  /* Breakpoints */
  --breakpoint-sm: 576px;       /* Small devices */
  --breakpoint-md: 768px;       /* Medium devices */
  --breakpoint-lg: 992px;       /* Large devices */
  --breakpoint-xl: 1200px;      /* Extra large devices */
  --breakpoint-xxl: 1400px;     /* Extra extra large devices */
  
  /* Grid System */
  --grid-columns: 12;           /* Number of grid columns */
  --grid-gutter-width: 1.5rem;  /* Grid gutter width */
  --grid-container-padding: 0.75rem; /* Container padding */
}
```

## Token Usage Examples

### Creating Consistent Components

```css
/* Good: Using design tokens */
.card {
  background-color: var(--color-bg-primary);
  border: 1px solid var(--color-border-light);
  border-radius: var(--radius-lg);
  padding: var(--space-6);
  box-shadow: var(--shadow-sm);
  transition: var(--transition-shadow);
}

.card:hover {
  box-shadow: var(--shadow-md);
}

/* Bad: Hard-coded values */
.card {
  background-color: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 24px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
  transition: box-shadow 0.15s ease-in-out;
}
```

### Building Color Variations

```css
/* Using the primary color scale */
.btn--primary {
  background-color: var(--color-primary-500);
  color: var(--color-text-on-primary);
  border: 1px solid var(--color-primary-500);
}

.btn--primary:hover {
  background-color: var(--color-primary-600);
  border-color: var(--color-primary-600);
}

.btn--primary:active {
  background-color: var(--color-primary-700);
  border-color: var(--color-primary-700);
}

.btn--primary:disabled {
  background-color: var(--color-primary-200);
  border-color: var(--color-primary-200);
  color: var(--color-text-disabled);
}
```

### Responsive Typography

```css
/* Using typography tokens responsively */
.heading {
  font-size: var(--text-2xl);
  font-weight: var(--font-bold);
  line-height: var(--leading-tight);
  margin-bottom: var(--space-4);
}

@media (min-width: 768px) {
  .heading {
    font-size: var(--text-3xl);
    margin-bottom: var(--space-6);
  }
}

@media (min-width: 1024px) {
  .heading {
    font-size: var(--text-4xl);
    margin-bottom: var(--space-8);
  }
}
```

## Dark Mode Support

### Color Token Overrides

```css
@media (prefers-color-scheme: dark) {
  :root {
    /* Text colors */
    --color-text-primary: #f9fafb;
    --color-text-secondary: #d1d5db;
    --color-text-muted: #9ca3af;
    --color-text-disabled: #6b7280;
    
    /* Background colors */
    --color-bg-primary: #111827;
    --color-bg-secondary: #1f2937;
    --color-bg-tertiary: #374151;
    --color-bg-elevated: #1f2937;
    --color-bg-inset: #374151;
    
    /* Border colors */
    --color-border-light: #374151;
    --color-border-medium: #4b5563;
    --color-border-dark: #6b7280;
    
    /* Adjust shadows for dark backgrounds */
    --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
    --shadow-base: 0 1px 3px 0 rgba(0, 0, 0, 0.4), 0 1px 2px 0 rgba(0, 0, 0, 0.2);
    --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.4), 0 2px 4px -1px rgba(0, 0, 0, 0.2);
  }
}
```

## Custom Token Creation

### Guidelines for New Tokens

1. **Follow naming convention**: `--category-variant-modifier`
2. **Use semantic names**: Describe purpose, not appearance
3. **Reference existing tokens**: Build upon the existing system
4. **Document usage**: Provide clear usage guidelines
5. **Consider dark mode**: Ensure tokens work in both light and dark themes

### Example: Adding a New Color

```css
:root {
  /* Add to the existing color system */
  --color-accent: #8b5cf6;           /* New accent color */
  --color-accent-light: #ede9fe;     /* Light variant */
  --color-accent-dark: #7c3aed;      /* Dark variant */
  
  /* Create semantic usage */
  --color-highlight: var(--color-accent);
  --color-highlight-bg: var(--color-accent-light);
}
```

## Token Migration Guide

### Updating Existing Tokens

1. **Identify usage**: Find all references to the token
2. **Plan migration**: Determine if change is breaking
3. **Create fallback**: Provide backward compatibility if needed
4. **Update documentation**: Document the change
5. **Test thoroughly**: Verify visual consistency

### Deprecating Tokens

```css
/* Mark token as deprecated */
:root {
  --old-token: var(--new-token); /* @deprecated Use --new-token instead */
}
```

## Token Validation

### CSS Custom Property Testing

```css
/* Test token existence */
.test-element {
  /* This will fall back to red if token doesn't exist */
  background-color: var(--color-primary, red);
}
```

### Design Token Linting

Use tools like Stylelint to enforce token usage:

```json
{
  "rules": {
    "color-no-hex": true,
    "length-zero-no-unit": true,
    "declaration-property-value-blacklist": {
      "color": ["/^#/", "/^rgb/", "/^hsl/"],
      "background-color": ["/^#/", "/^rgb/", "/^hsl/"]
    }
  }
}
```

## Performance Considerations

### Token Loading

```css
/* Critical tokens loaded immediately */
:root {
  --color-primary: #3b82f6;
  --color-text-primary: #111827;
  --color-bg-primary: #ffffff;
  --space-4: 1rem;
  --radius-md: 0.375rem;
}

/* Non-critical tokens can be loaded later */
.extended-tokens {
  --color-primary-50: #eff6ff;
  --color-primary-100: #dbeafe;
  /* ... additional tokens ... */
}
```

### Memory Usage

- **Group related tokens**: Organize by category for easier maintenance
- **Use logical defaults**: Provide sensible fallback values
- **Avoid redundancy**: Don't duplicate values across multiple tokens
- **Optimize for reuse**: Create tokens that work in multiple contexts

---

This documentation should be updated whenever new tokens are added or existing tokens are modified. Always maintain backward compatibility when possible and document any breaking changes clearly.