# Component Library Documentation

## Overview

Conduit's component library provides a comprehensive set of reusable UI components built with **BEM methodology**, **design tokens**, and **accessibility best practices**. All components are documented with usage examples, variants, and implementation guidelines.

## Component Categories

- [Buttons](#buttons)
- [Forms](#forms)
- [Cards](#cards)
- [Navigation](#navigation)
- [Layout](#layout)
- [Typography](#typography)
- [Utilities](#utilities)

---

## Buttons

### Base Button

```css
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-3) var(--space-4);
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  font-weight: var(--font-medium);
  line-height: var(--leading-tight);
  text-decoration: none;
  cursor: pointer;
  transition: var(--transition-all);
  user-select: none;
}
```

### Button Variants

#### Primary Button
```html
<button class="btn btn--primary">Primary Action</button>
```

```css
.btn--primary {
  background-color: var(--color-primary);
  color: var(--color-text-on-primary);
  border-color: var(--color-primary);
}

.btn--primary:hover {
  background-color: var(--color-primary-600);
  border-color: var(--color-primary-600);
}

.btn--primary:active {
  background-color: var(--color-primary-700);
  border-color: var(--color-primary-700);
}
```

#### Secondary Button
```html
<button class="btn btn--secondary">Secondary Action</button>
```

```css
.btn--secondary {
  background-color: transparent;
  color: var(--color-primary);
  border-color: var(--color-primary);
}

.btn--secondary:hover {
  background-color: var(--color-primary-50);
  color: var(--color-primary-700);
}
```

#### Danger Button
```html
<button class="btn btn--danger">Delete</button>
```

```css
.btn--danger {
  background-color: var(--color-danger);
  color: var(--color-text-inverse);
  border-color: var(--color-danger);
}
```

#### Ghost Button
```html
<button class="btn btn--ghost">Ghost Button</button>
```

```css
.btn--ghost {
  background-color: transparent;
  color: var(--color-text-secondary);
  border-color: transparent;
}

.btn--ghost:hover {
  background-color: var(--color-bg-tertiary);
  color: var(--color-text-primary);
}
```

### Button Sizes

#### Small Button
```html
<button class="btn btn--primary btn--sm">Small Button</button>
```

```css
.btn--sm {
  padding: var(--space-2) var(--space-3);
  font-size: var(--text-sm);
}
```

#### Large Button
```html
<button class="btn btn--primary btn--lg">Large Button</button>
```

```css
.btn--lg {
  padding: var(--space-4) var(--space-6);
  font-size: var(--text-lg);
}
```

### Button States

#### Loading State
```html
<button class="btn btn--primary btn--loading">
  <span class="btn__spinner"></span>
  Loading...
</button>
```

```css
.btn--loading {
  pointer-events: none;
  opacity: 0.7;
}

.btn__spinner {
  width: 1rem;
  height: 1rem;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: btn-spin 0.8s linear infinite;
  margin-right: var(--space-2);
}
```

#### Disabled State
```html
<button class="btn btn--primary" disabled>Disabled Button</button>
```

```css
.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}
```

### Button Groups

```html
<div class="btn-group">
  <button class="btn btn--secondary">Left</button>
  <button class="btn btn--secondary">Center</button>
  <button class="btn btn--secondary">Right</button>
</div>
```

```css
.btn-group {
  display: inline-flex;
  border-radius: var(--radius-md);
  overflow: hidden;
}

.btn-group .btn {
  border-radius: 0;
  border-right-width: 0;
}

.btn-group .btn:first-child {
  border-radius: var(--radius-md) 0 0 var(--radius-md);
}

.btn-group .btn:last-child {
  border-radius: 0 var(--radius-md) var(--radius-md) 0;
  border-right-width: 1px;
}
```

---

## Forms

### Form Structure

```html
<form class="form">
  <div class="form__group">
    <label class="form__label" for="email">Email Address</label>
    <input class="form__input" type="email" id="email" required>
    <div class="form__help">Enter your email address</div>
  </div>
</form>
```

### Form Controls

#### Text Input
```css
.form__input {
  width: 100%;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--color-border-medium);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  line-height: var(--leading-normal);
  transition: var(--transition-colors);
  background-color: var(--color-bg-primary);
}

.form__input:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
  border-color: var(--color-primary);
}
```

#### Select Dropdown
```html
<select class="form__select">
  <option>Choose an option</option>
  <option value="1">Option 1</option>
  <option value="2">Option 2</option>
</select>
```

```css
.form__select {
  appearance: none;
  background-image: url("data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
  background-position: right var(--space-3) center;
  background-repeat: no-repeat;
  background-size: 1.5rem;
  padding-right: var(--space-10);
}
```

#### Checkbox and Radio

```html
<div class="form__check">
  <input class="form__check-input" type="checkbox" id="check1">
  <label class="form__check-label" for="check1">
    Remember me
  </label>
</div>
```

```css
.form__check {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.form__check-input {
  width: 1.25rem;
  height: 1.25rem;
  border: 1px solid var(--color-border-medium);
  border-radius: var(--radius-sm);
  transition: var(--transition-colors);
}

.form__check-input:checked {
  background-color: var(--color-primary);
  border-color: var(--color-primary);
}
```

#### Toggle Switch
```html
<div class="form__switch">
  <input class="form__switch-input" type="checkbox" id="switch1">
  <label class="form__switch-label" for="switch1">
    Enable notifications
  </label>
</div>
```

```css
.form__switch-input {
  width: 2rem !important;
  min-width: 2rem !important;
  max-width: 2rem !important;
  height: 1.25rem;
  border-radius: var(--radius-full);
  background-color: var(--color-border-medium);
  transition: var(--transition-colors);
}

.form__switch-input:checked {
  background-color: var(--color-primary);
}
```

### Form Validation States

#### Valid State
```html
<input class="form__input form__input--valid" type="email" value="user@example.com">
<div class="form__feedback form__feedback--valid">
  Email address is valid
</div>
```

```css
.form__input--valid {
  border-color: var(--color-success);
}

.form__feedback--valid {
  color: var(--color-success-dark);
  font-size: var(--text-sm);
  margin-top: var(--space-1);
}
```

#### Invalid State
```html
<input class="form__input form__input--invalid" type="email" value="invalid-email">
<div class="form__feedback form__feedback--invalid">
  Please enter a valid email address
</div>
```

```css
.form__input--invalid {
  border-color: var(--color-danger);
}

.form__feedback--invalid {
  color: var(--color-danger-dark);
  font-size: var(--text-sm);
  margin-top: var(--space-1);
}
```

### Form Layouts

#### Horizontal Form
```html
<div class="form__row">
  <div class="form__col form__col--label">
    <label class="form__label">Email</label>
  </div>
  <div class="form__col form__col--input">
    <input class="form__input" type="email">
  </div>
</div>
```

```css
.form__row {
  display: flex;
  align-items: flex-start;
  gap: var(--space-4);
  margin-bottom: var(--space-4);
}

.form__col--label {
  flex: 0 0 8rem;
  padding-top: var(--space-3);
}

.form__col--input {
  flex: 1;
}
```

---

## Cards

### Base Card

```html
<div class="card">
  <div class="card__header">
    <h3 class="card__title">Card Title</h3>
  </div>
  <div class="card__body">
    <p>Card content goes here.</p>
  </div>
  <div class="card__footer">
    <button class="btn btn--primary">Action</button>
  </div>
</div>
```

```css
.card {
  background-color: var(--color-bg-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  transition: var(--transition-shadow);
}

.card:hover {
  box-shadow: var(--shadow-md);
}
```

### Card Elements

```css
.card__header {
  padding: var(--space-5) var(--space-6);
  border-bottom: 1px solid var(--color-border-light);
}

.card__title {
  margin: 0;
  font-size: var(--text-lg);
  font-weight: var(--font-semibold);
  color: var(--color-text-primary);
}

.card__body {
  padding: var(--space-6);
}

.card__footer {
  padding: var(--space-4) var(--space-6);
  background-color: var(--color-bg-secondary);
  border-top: 1px solid var(--color-border-light);
}
```

### Card Variants

#### Elevated Card
```html
<div class="card card--elevated">
  <!-- Card content -->
</div>
```

```css
.card--elevated {
  box-shadow: var(--shadow-lg);
}
```

#### Interactive Card
```html
<div class="card card--interactive">
  <!-- Card content -->
</div>
```

```css
.card--interactive {
  cursor: pointer;
  transition: var(--transition-all);
}

.card--interactive:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-xl);
}
```

#### Media Card
```html
<div class="card card--media">
  <div class="card__media">
    <img src="image.jpg" alt="Description">
  </div>
  <div class="card__body">
    <h3 class="card__title">Media Card</h3>
    <p>Card with media content.</p>
  </div>
</div>
```

```css
.card__media {
  width: 100%;
  height: 12rem;
  overflow: hidden;
}

.card__media img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
```

---

## Navigation

### Primary Navigation

```html
<nav class="nav nav--primary">
  <ul class="nav__list">
    <li class="nav__item">
      <a class="nav__link nav__link--active" href="/dashboard">Dashboard</a>
    </li>
    <li class="nav__item">
      <a class="nav__link" href="/settings">Settings</a>
    </li>
  </ul>
</nav>
```

```css
.nav {
  display: flex;
  align-items: center;
}

.nav__list {
  display: flex;
  list-style: none;
  margin: 0;
  padding: 0;
  gap: var(--space-1);
}

.nav__link {
  display: flex;
  align-items: center;
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-secondary);
  text-decoration: none;
  border-radius: var(--radius-md);
  transition: var(--transition-colors);
}

.nav__link:hover {
  background-color: var(--color-bg-tertiary);
  color: var(--color-text-primary);
}

.nav__link--active {
  background-color: var(--color-primary);
  color: var(--color-text-on-primary);
}
```

### Sidebar Navigation

```html
<nav class="sidebar-nav">
  <div class="sidebar-nav__section">
    <h4 class="sidebar-nav__heading">Main</h4>
    <ul class="sidebar-nav__list">
      <li class="sidebar-nav__item">
        <a class="sidebar-nav__link sidebar-nav__link--active" href="/dashboard">
          <span class="sidebar-nav__icon">📊</span>
          Dashboard
        </a>
      </li>
    </ul>
  </div>
</nav>
```

```css
.sidebar-nav {
  padding: var(--space-4) 0;
}

.sidebar-nav__section {
  margin-bottom: var(--space-6);
}

.sidebar-nav__heading {
  font-size: var(--text-xs);
  font-weight: var(--font-semibold);
  text-transform: uppercase;
  letter-spacing: var(--tracking-wide);
  color: var(--color-text-muted);
  margin: 0 0 var(--space-3) 0;
  padding: 0 var(--space-4);
}

.sidebar-nav__link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-inverse);
  text-decoration: none;
  border-radius: var(--radius-md);
  margin: 0 var(--space-2);
  transition: var(--transition-colors);
}

.sidebar-nav__link:hover {
  background-color: rgba(255, 255, 255, 0.1);
}

.sidebar-nav__link--active {
  background-color: rgba(255, 255, 255, 0.2);
  font-weight: var(--font-medium);
}
```

### Breadcrumb Navigation

```html
<nav class="breadcrumb">
  <ol class="breadcrumb__list">
    <li class="breadcrumb__item">
      <a class="breadcrumb__link" href="/">Home</a>
    </li>
    <li class="breadcrumb__item">
      <span class="breadcrumb__separator">/</span>
      <a class="breadcrumb__link" href="/settings">Settings</a>
    </li>
    <li class="breadcrumb__item">
      <span class="breadcrumb__separator">/</span>
      <span class="breadcrumb__current">Profile</span>
    </li>
  </ol>
</nav>
```

```css
.breadcrumb__list {
  display: flex;
  align-items: center;
  list-style: none;
  margin: 0;
  padding: 0;
  font-size: var(--text-sm);
}

.breadcrumb__item {
  display: flex;
  align-items: center;
}

.breadcrumb__link {
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: var(--transition-colors);
}

.breadcrumb__link:hover {
  color: var(--color-primary);
}

.breadcrumb__separator {
  margin: 0 var(--space-2);
  color: var(--color-text-muted);
}

.breadcrumb__current {
  color: var(--color-text-primary);
  font-weight: var(--font-medium);
}
```

---

## Layout

### Container System

```html
<div class="container">
  <div class="row">
    <div class="col col-md-6">
      <p>Column content</p>
    </div>
    <div class="col col-md-6">
      <p>Column content</p>
    </div>
  </div>
</div>
```

### CSS Grid Layout

```html
<div class="grid grid--3">
  <div class="grid__item">Item 1</div>
  <div class="grid__item">Item 2</div>
  <div class="grid__item">Item 3</div>
</div>
```

```css
.grid {
  display: grid;
  gap: var(--space-4);
}

.grid--3 {
  grid-template-columns: repeat(3, 1fr);
}

.grid--auto-fit {
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
}
```

### Flexbox Utilities

```html
<div class="flex flex--center">
  <div>Centered content</div>
</div>
```

```css
.flex {
  display: flex;
}

.flex--center {
  justify-content: center;
  align-items: center;
}

.flex--between {
  justify-content: space-between;
}

.flex--column {
  flex-direction: column;
}
```

---

## Typography

### Heading Styles

```html
<h1 class="heading heading--1">Main Heading</h1>
<h2 class="heading heading--2">Section Heading</h2>
<h3 class="heading heading--3">Subsection Heading</h3>
```

```css
.heading {
  margin: 0 0 var(--space-4) 0;
  font-weight: var(--font-bold);
  line-height: var(--leading-tight);
  color: var(--color-text-primary);
}

.heading--1 {
  font-size: var(--text-4xl);
}

.heading--2 {
  font-size: var(--text-3xl);
}

.heading--3 {
  font-size: var(--text-2xl);
}
```

### Text Utilities

```html
<p class="text text--large">Large body text</p>
<p class="text text--muted">Muted text</p>
<p class="text text--center">Centered text</p>
```

```css
.text--large {
  font-size: var(--text-lg);
}

.text--small {
  font-size: var(--text-sm);
}

.text--muted {
  color: var(--color-text-muted);
}

.text--center {
  text-align: center;
}

.text--bold {
  font-weight: var(--font-bold);
}
```

---

## Utilities

### Spacing Utilities

```html
<div class="m-4">Margin on all sides</div>
<div class="p-6">Padding on all sides</div>
<div class="mt-8">Top margin only</div>
```

```css
/* Margin utilities */
.m-0 { margin: var(--space-0); }
.m-1 { margin: var(--space-1); }
.m-2 { margin: var(--space-2); }
.m-3 { margin: var(--space-3); }
.m-4 { margin: var(--space-4); }
.m-5 { margin: var(--space-5); }
.m-6 { margin: var(--space-6); }
.m-8 { margin: var(--space-8); }

/* Margin directional */
.mt-4 { margin-top: var(--space-4); }
.mr-4 { margin-right: var(--space-4); }
.mb-4 { margin-bottom: var(--space-4); }
.ml-4 { margin-left: var(--space-4); }

/* Padding utilities */
.p-0 { padding: var(--space-0); }
.p-1 { padding: var(--space-1); }
.p-2 { padding: var(--space-2); }
.p-3 { padding: var(--space-3); }
.p-4 { padding: var(--space-4); }
.p-5 { padding: var(--space-5); }
.p-6 { padding: var(--space-6); }
.p-8 { padding: var(--space-8); }
```

### Display Utilities

```html
<div class="d-none">Hidden element</div>
<div class="d-block">Block element</div>
<div class="d-flex">Flex container</div>
```

```css
.d-none { display: none; }
.d-block { display: block; }
.d-inline { display: inline; }
.d-inline-block { display: inline-block; }
.d-flex { display: flex; }
.d-grid { display: grid; }
```

### Color Utilities

```html
<div class="bg-primary">Primary background</div>
<div class="text-success">Success text</div>
<div class="border-danger">Danger border</div>
```

```css
/* Background colors */
.bg-primary { background-color: var(--color-primary); }
.bg-success { background-color: var(--color-success); }
.bg-warning { background-color: var(--color-warning); }
.bg-danger { background-color: var(--color-danger); }

/* Text colors */
.text-primary { color: var(--color-primary); }
.text-success { color: var(--color-success); }
.text-warning { color: var(--color-warning); }
.text-danger { color: var(--color-danger); }
.text-muted { color: var(--color-text-muted); }

/* Border colors */
.border-primary { border-color: var(--color-primary); }
.border-success { border-color: var(--color-success); }
.border-warning { border-color: var(--color-warning); }
.border-danger { border-color: var(--color-danger); }
```

## Component Implementation Guidelines

### 1. Use Design Tokens

Always reference design tokens instead of hard-coded values:

```css
/* Good */
.component {
  padding: var(--space-4);
  color: var(--color-text-primary);
  border-radius: var(--radius-md);
}

/* Bad */
.component {
  padding: 16px;
  color: #111827;
  border-radius: 6px;
}
```

### 2. Follow BEM Naming

Structure component classes using BEM methodology:

```css
/* Block */
.component { }

/* Elements */
.component__header { }
.component__body { }
.component__footer { }

/* Modifiers */
.component--large { }
.component--highlighted { }
```

### 3. Include All States

Define hover, focus, active, and disabled states:

```css
.button {
  /* Base styles */
}

.button:hover {
  /* Hover styles */
}

.button:focus {
  /* Focus styles for accessibility */
}

.button:active {
  /* Active/pressed styles */
}

.button:disabled {
  /* Disabled styles */
}
```

### 4. Provide Responsive Behavior

Use mobile-first responsive design:

```css
.component {
  /* Mobile styles first */
  font-size: var(--text-base);
}

@media (min-width: 768px) {
  .component {
    font-size: var(--text-lg);
  }
}
```

### 5. Support Accessibility

Include ARIA attributes and focus management:

```html
<button class="btn btn--primary" aria-describedby="btn-help">
  Submit Form
</button>
<div id="btn-help" class="sr-only">
  This will submit the form and redirect you
</div>
```

### 6. Document Usage

Provide clear documentation for each component:

- **Purpose**: What the component is for
- **Usage**: How to implement it
- **Variants**: Available modifications
- **Accessibility**: ARIA requirements
- **Examples**: Code examples and demos

### 7. Test Thoroughly

Verify components work correctly:

- **Visual testing**: Ensure consistent appearance
- **Functional testing**: Verify interactions work
- **Accessibility testing**: Screen reader compatibility
- **Browser testing**: Cross-browser support
- **Responsive testing**: Mobile and desktop layouts

---

This component library serves as the foundation for Conduit's user interface. All components should be implemented according to these guidelines to ensure consistency, accessibility, and maintainability across the application.