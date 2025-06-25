# BEM Naming Conventions for ConduitLLM

**Block Element Modifier (BEM)** methodology for CSS class naming in the ConduitLLM project.

## Overview

BEM is a naming convention that helps create reusable components and code sharing in front-end development. It aims to make CSS more maintainable and predictable.

## BEM Structure

```
.block__element--modifier
```

- **Block**: Standalone entity that is meaningful on its own
- **Element**: A part of a block that has no standalone meaning
- **Modifier**: A flag on a block or element used to change appearance or behavior

## Naming Rules

### 1. Block Names
- Use lowercase letters
- Use hyphens to separate words
- Should describe the purpose, not the appearance

```css
/* ✅ Good */
.card
.navigation
.user-profile
.search-form

/* ❌ Bad */
.redBox
.left-sidebar
.bigText
```

### 2. Element Names
- Use double underscores (`__`) to separate from block
- Use lowercase letters
- Use hyphens to separate words within element name

```css
/* ✅ Good */
.card__header
.card__body
.card__footer
.navigation__item
.user-profile__avatar
.search-form__input

/* ❌ Bad */
.card-header
.cardHeader
.card_header
```

### 3. Modifier Names
- Use double hyphens (`--`) to separate from block or element
- Use lowercase letters
- Use hyphens to separate words within modifier name

```css
/* ✅ Good */
.card--featured
.card--large
.button--primary
.button--disabled
.navigation__item--active
.user-profile--compact

/* ❌ Bad */
.card_featured
.card.featured
.cardFeatured
```

## ConduitLLM-Specific Conventions

### 1. Component Prefix
For custom components, use the `conduit-` prefix:

```css
.conduit-card
.conduit-button
.conduit-modal
.conduit-toast
```

### 2. Modern Components
For enhanced design system components, use the `modern-` prefix:

```css
.modern-card
.modern-button
.modern-table
.modern-form
```

### 3. Layout Components
For layout-specific components:

```css
.layout-sidebar
.layout-header
.layout-main
.layout-footer
```

### 4. State Classes
For state-based styling, use `is-` or `has-` prefixes:

```css
.is-active
.is-disabled
.is-loading
.is-visible
.has-error
.has-content
```

## Component Examples

### 1. Card Component

```css
/* Block */
.card {
  background: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-md);
}

/* Elements */
.card__header {
  padding: var(--space-4);
  border-bottom: 1px solid var(--color-border-light);
}

.card__body {
  padding: var(--space-4);
}

.card__footer {
  padding: var(--space-4);
  border-top: 1px solid var(--color-border-light);
}

.card__title {
  font-size: var(--text-lg);
  font-weight: var(--font-semibold);
  margin: 0;
}

.card__actions {
  display: flex;
  gap: var(--space-2);
}

/* Modifiers */
.card--featured {
  border: 2px solid var(--color-primary);
}

.card--compact {
  padding: var(--space-2);
}

.card--large {
  padding: var(--space-8);
}

/* Element modifiers */
.card__header--borderless {
  border-bottom: none;
}
```

### 2. Button Component

```css
/* Block */
.button {
  display: inline-flex;
  align-items: center;
  padding: var(--space-2) var(--space-4);
  border: none;
  border-radius: var(--radius-md);
  font-weight: var(--font-medium);
  transition: var(--transition-all);
  cursor: pointer;
}

/* Elements */
.button__icon {
  margin-right: var(--space-2);
}

.button__text {
  /* Text-specific styling */
}

/* Modifiers */
.button--primary {
  background: var(--color-primary);
  color: white;
}

.button--secondary {
  background: var(--color-bg-secondary);
  color: var(--color-text-primary);
}

.button--large {
  padding: var(--space-3) var(--space-6);
  font-size: var(--text-lg);
}

.button--small {
  padding: var(--space-1) var(--space-3);
  font-size: var(--text-sm);
}

.button--disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Element modifiers */
.button__icon--left {
  margin-right: var(--space-2);
  margin-left: 0;
}

.button__icon--right {
  margin-left: var(--space-2);
  margin-right: 0;
}
```

### 3. Navigation Component

```css
/* Block */
.navigation {
  background: var(--gradient-sidebar);
  width: var(--sidebar-width);
}

/* Elements */
.navigation__list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.navigation__item {
  /* Individual nav item */
}

.navigation__link {
  display: flex;
  align-items: center;
  padding: var(--space-3) var(--space-4);
  color: white;
  text-decoration: none;
  transition: var(--transition-all);
}

.navigation__icon {
  margin-right: var(--space-3);
}

.navigation__text {
  /* Link text styling */
}

.navigation__category {
  font-weight: var(--font-bold);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: var(--space-4);
  color: rgba(255, 255, 255, 0.8);
}

/* Modifiers */
.navigation--collapsed {
  width: 60px;
}

/* Element modifiers */
.navigation__item--active {
  background: rgba(255, 255, 255, 0.1);
}

.navigation__link--active {
  background: rgba(255, 255, 255, 0.15);
  font-weight: var(--font-semibold);
}
```

## Utility Classes

Utility classes should be single-purpose and not follow BEM:

```css
/* ✅ Good - Utility classes */
.text-center
.mt-4
.d-flex
.bg-primary
.shadow-lg

/* ❌ Bad - Don't use BEM for utilities */
.text__center
.margin--top-4
```

## Integration with Existing Code

### 1. Legacy Class Names
Keep existing class names for backward compatibility:

```css
/* Legacy */
.card { /* existing styles */ }

/* New BEM approach */
.conduit-card { /* new BEM-based component */ }
```

### 2. Bootstrap Integration
BEM works alongside Bootstrap classes:

```html
<!-- ✅ Good - Combining Bootstrap and BEM -->
<div class="row">
  <div class="col-md-6">
    <div class="card conduit-card conduit-card--featured">
      <div class="card-body conduit-card__body">
        <h5 class="card-title conduit-card__title">Title</h5>
      </div>
    </div>
  </div>
</div>
```

### 3. Blazor Component Integration
For Blazor components with CSS isolation:

```css
/* MyComponent.razor.css */
.component {
  /* Block styles */
}

.component__element {
  /* Element styles */
}

.component--modifier {
  /* Modifier styles */
}
```

## Guidelines for Implementation

### 1. Start Small
- Begin with new components
- Gradually refactor existing components
- Maintain backward compatibility

### 2. Documentation
- Document each component's BEM structure
- Provide usage examples
- Include modifier combinations

### 3. Team Consistency
- Use consistent naming across the team
- Review BEM structure in code reviews
- Create component style guides

### 4. Avoid Deep Nesting
```css
/* ✅ Good - Flat structure */
.card__header
.card__title
.card__body
.card__footer

/* ❌ Bad - Deep nesting */
.card__header__title__text
```

### 5. When NOT to Use BEM
- Utility classes (use functional naming)
- One-off styles (use descriptive names)
- Third-party framework overrides

## Migration Strategy

### Phase 1: New Components (Current)
- All new components use BEM methodology
- Document BEM patterns in style guide

### Phase 2: Gradual Migration
- Refactor high-traffic components
- Maintain dual class names during transition
- Update component documentation

### Phase 3: Complete Adoption
- Remove legacy class names
- Standardize all components on BEM
- Complete style guide documentation

## Tools and Validation

### 1. CSS Linting
Configure Stylelint with BEM-specific rules:

```json
{
  "rules": {
    "selector-class-pattern": "^[a-z]([a-z0-9-]+)?(__([a-z0-9]+-?)+)?(--([a-z0-9]+-?)+)?$",
    "selector-id-pattern": "^[a-z]([a-z0-9-]+)?$"
  }
}
```

### 2. Naming Validation
Use regex pattern for validation:
```regex
^[a-z][a-z0-9-]*(__[a-z0-9-]+)?(--[a-z0-9-]+)?$
```

## Resources

- [BEM Methodology Official Site](http://getbem.com/)
- [CSS Guidelines by Harry Roberts](https://cssguidelin.es/)
- [SUIT CSS Naming Conventions](https://suitcss.github.io/)

---

This document should be updated as the BEM implementation evolves and new patterns emerge in the ConduitLLM project.