# Responsive Design Patterns

Conduit implements a comprehensive responsive design system using **mobile-first methodology**, **design tokens**, and **modern CSS features** to ensure optimal user experience across all devices and screen sizes.

## Documentation Structure

The responsive design documentation has been organized into focused guides:

### 📱 Core Responsive Design
- **[Mobile-First Strategy](./responsive/mobile-first.md)** - Mobile-first principles and implementation
- **[Breakpoint System](./responsive/breakpoints.md)** - Breakpoint definitions and media queries
- **[Grid Systems](./responsive/grids.md)** - Flexible grid layouts and utilities

### 🎨 Design Patterns  
- **[Typography Scaling](./responsive/typography.md)** - Responsive typography and text scaling
- **[Layout Patterns](./responsive/layouts.md)** - Common responsive layout patterns
- **[Component Adaptations](./responsive/components.md)** - Component responsive behavior

### 🧭 Navigation & Advanced
- **[Navigation Patterns](./responsive/navigation.md)** - Responsive navigation and menus
- **[Container Queries](./responsive/container-queries.md)** - Modern container query patterns
- **[Testing Guidelines](./responsive/testing.md)** - Testing responsive designs

## Quick Reference

### Breakpoints
```css
:root {
  --breakpoint-sm: 576px;    /* Small devices (landscape phones) */
  --breakpoint-md: 768px;    /* Medium devices (tablets) */
  --breakpoint-lg: 992px;    /* Large devices (small laptops) */
  --breakpoint-xl: 1200px;   /* Extra large devices (laptops) */
  --breakpoint-xxl: 1400px;  /* Extra extra large devices (large screens) */
}
```

### Mobile-First Media Queries
```css
/* Mobile First Approach */

/* Default: 320px+ (Mobile phones) */
.component {
  font-size: var(--text-base);
  padding: var(--space-4);
}

/* Small: 576px+ (Large phones, small tablets) */
@media (min-width: 576px) {
  .component {
    padding: var(--space-5);
  }
}

/* Medium: 768px+ (Tablets) */
@media (min-width: 768px) {
  .component {
    font-size: var(--text-lg);
    padding: var(--space-6);
  }
}

/* Large: 992px+ (Small laptops) */
@media (min-width: 992px) {
  .component {
    padding: var(--space-8);
  }
}

/* Extra Large: 1200px+ (Laptops and desktops) */
@media (min-width: 1200px) {
  .component {
    font-size: var(--text-xl);
    padding: var(--space-10);
  }
}
```

### Responsive Grid
```css
.grid {
  display: grid;
  gap: var(--space-4);
  grid-template-columns: 1fr; /* Mobile: single column */
}

@media (min-width: 768px) {
  .grid {
    grid-template-columns: repeat(2, 1fr); /* Tablet: 2 columns */
  }
}

@media (min-width: 1024px) {
  .grid {
    grid-template-columns: repeat(3, 1fr); /* Desktop: 3 columns */
  }
}
```

### Responsive Typography
```css
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
```

## Core Principles

### 1. Mobile-First Strategy
- **Start with mobile** - Design and develop for the smallest screen first
- **Progressive enhancement** - Add features and complexity for larger screens
- **Content priority** - Most important content accessible on all devices
- **Touch-friendly design** - Minimum 44px touch targets
- **Performance focus** - Optimize for slower mobile connections

### 2. Design Token Integration
- Use CSS custom properties for all responsive values
- Maintain consistency across breakpoints
- Enable easy theme switching and customization
- Provide semantic naming for maintainability

### 3. Container-First Thinking
- Design components to be container-aware
- Use container queries where supported
- Implement flexible grid systems
- Create adaptable component patterns

### 4. Performance Optimization
- Minimize layout shifts
- Optimize images for different screen densities
- Use efficient CSS selectors
- Implement progressive loading strategies

## Layout Patterns

### Sidebar Layout
```css
.layout {
  display: grid;
  gap: var(--space-6);
  grid-template-columns: 1fr; /* Mobile: stacked */
}

@media (min-width: 992px) {
  .layout {
    grid-template-columns: 250px 1fr; /* Desktop: sidebar + main */
  }
}
```

### Card Grid
```css
.card-grid {
  display: grid;
  gap: var(--space-4);
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
}

/* Cards automatically wrap and resize */
```

### Hero Section
```css
.hero {
  padding: var(--space-8) var(--space-4);
  text-align: center;
}

@media (min-width: 768px) {
  .hero {
    padding: var(--space-16) var(--space-8);
  }
}

@media (min-width: 1200px) {
  .hero {
    padding: var(--space-24) var(--space-12);
  }
}
```

## Component Adaptations

### Navigation
- **Mobile**: Hamburger menu with overlay
- **Tablet**: Collapsed menu with dropdowns
- **Desktop**: Full horizontal navigation

### Forms
- **Mobile**: Single column, large touch targets
- **Tablet**: Optimized spacing and grouping
- **Desktop**: Multi-column layouts where appropriate

### Data Tables
- **Mobile**: Card-based layout or horizontal scroll
- **Tablet**: Reduced columns with tooltips
- **Desktop**: Full table with all columns

## Testing Strategy

### Device Testing
- **Physical devices** - Test on actual mobile devices
- **Browser DevTools** - Use device simulation mode
- **Responsive testing tools** - Automated responsive testing
- **Screen readers** - Test accessibility across breakpoints

### Performance Testing
- **Mobile performance** - Test on slower connections
- **Image optimization** - Verify appropriate image sizes
- **Layout stability** - Check for cumulative layout shift
- **Touch interaction** - Verify touch target sizes

## Modern CSS Features

### Container Queries
```css
@supports (container-type: inline-size) {
  .card-container {
    container-type: inline-size;
  }
  
  @container (min-width: 400px) {
    .card {
      display: grid;
      grid-template-columns: auto 1fr;
    }
  }
}
```

### CSS Grid with Subgrid
```css
@supports (grid-template-rows: subgrid) {
  .card-grid {
    display: grid;
    grid-template-rows: subgrid;
  }
}
```

### Aspect Ratio
```css
.media {
  aspect-ratio: 16 / 9;
  object-fit: cover;
}
```

## Best Practices

### Design Tokens
- Always use design tokens for responsive values
- Maintain consistent spacing scales across breakpoints
- Use semantic color names that work in different contexts
- Test token values across all screen sizes

### Performance
- Minimize layout recalculations
- Use `will-change` sparingly and remove when done
- Optimize critical rendering path
- Implement efficient responsive images

### Accessibility
- Ensure touch targets are at least 44px
- Test with screen readers at different screen sizes
- Provide appropriate focus management
- Maintain readable text at all zoom levels

### Maintenance
- Document responsive behavior in component libraries
- Use consistent naming conventions
- Implement automated responsive testing
- Regularly audit and update responsive patterns

## Related Documentation

- [CSS Development Guidelines](./css-development-guidelines.md) - Complete CSS standards and best practices
- [Component Library](./component-library.md) - UI component documentation with responsive examples
- [Design Tokens](./design-tokens.md) - Design token system and usage