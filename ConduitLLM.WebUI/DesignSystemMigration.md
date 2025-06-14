# ConduitLLM Design System Migration Guide

## Overview
This guide helps migrate all ConduitLLM WebUI pages to use the modern design system defined in `/wwwroot/css/design-system.css`.

## Migration Checklist

### ✅ Completed Pages
- [x] CostDashboard.razor
- [x] VirtualKeys.razor  
- [x] ModelCosts.razor
- [x] RequestLogs.razor
- [x] LLMProviders.razor
- [x] ModelMappings.razor
- [x] Home.razor
- [x] Configuration.razor
- [x] StyleGuide.razor (new)
- [x] SystemInfo.razor
- [x] ProviderHealth.razor
- [x] About.razor

### 📋 Pages to Migrate
- [ ] VirtualKeyEdit.razor
- [ ] ProviderEdit.razor
- [ ] MappingEdit.razor
- [ ] AudioProviders.razor
- [ ] AudioProviderEdit.razor
- [ ] ProviderHealth.razor
- [ ] ProviderHealthConfig.razor
- [ ] RoutingSettings.razor
- [ ] CachingSettings.razor
- [ ] IpAccessFiltering.razor
- [ ] VirtualKeysDashboard.razor
- [ ] AudioUsage.razor
- [ ] Chat.razor
- [ ] SystemInfo.razor
- [ ] About.razor
- [ ] Login.razor
- [ ] Error pages

## Migration Steps

### 1. Cards
Replace:
```razor
<div class="card shadow-sm border-0">
    <div class="card-header bg-light border-0">
```

With:
```razor
<div class="card modern-card">
    <div class="card-header modern-card-header">
```

Also update card body:
```razor
<div class="card-body modern-card-body">
```

### 2. Form Controls
Replace:
```razor
<div class="form-group">
    <label for="inputId">Label</label>
    <input type="text" class="form-control" id="inputId">
```

With:
```razor
<div class="modern-form-group">
    <label for="inputId" class="modern-form-label">Label</label>
    <input type="text" class="form-control modern-form-control" id="inputId">
```

For selects:
```razor
<select class="form-select modern-form-select">
```

### 3. Buttons
Replace:
```razor
<button class="btn btn-primary shadow-sm">
```

With:
```razor
<button class="btn btn-primary modern-btn modern-btn-primary">
```

For secondary buttons:
```razor
<button class="btn btn-secondary modern-btn modern-btn-secondary">
```

### 4. Tables
Replace:
```razor
<div class="table-responsive">
    <table class="table table-hover">
        <thead class="table-light">
```

With:
```razor
<div class="table-responsive modern-table-container">
    <table class="table modern-table table-hover">
        <thead class="modern-table-header">
```

And table rows:
```razor
<tr class="modern-table-row">
```

### 5. Badges
Replace:
```razor
<span class="badge bg-success">Success</span>
<span class="badge bg-warning">Warning</span>
```

With:
```razor
<span class="badge modern-badge modern-badge-success">Success</span>
<span class="badge modern-badge modern-badge-warning">Warning</span>
```

### 6. Alerts
Replace:
```razor
<div class="alert alert-info">
```

With:
```razor
<div class="alert modern-alert modern-alert-info">
```

### 7. Info/Feature Cards
Replace:
```razor
<div class="bg-light p-4 border rounded">
```

With:
```razor
<div class="modern-info-card p-4 rounded">
```

### 8. Remove Page-Specific Styles
Remove or minimize `<style>` sections in pages. Common styles that can be removed:
- `.card { box-shadow: ... }`
- `.card:hover { transform: ... }`
- `.table th { background-color: ... }`
- `.stat-item { ... }` (already in design system)
- Border radius styles
- Shadow styles
- Hover effects

Keep only truly page-specific styles that don't conflict with the design system.

## Quick Replace Patterns

### Visual Studio Code Find & Replace (Regex)

1. **Cards:**
   - Find: `class="card\s+shadow-sm\s+border-0`
   - Replace: `class="card modern-card`

2. **Card Headers:**
   - Find: `class="card-header\s+bg-light\s+border-0"`
   - Replace: `class="card-header modern-card-header"`

3. **Buttons:**
   - Find: `class="btn\s+btn-primary\s+shadow-sm"`
   - Replace: `class="btn btn-primary modern-btn modern-btn-primary"`

4. **Form Groups:**
   - Find: `<div class="form-group`
   - Replace: `<div class="modern-form-group`

5. **Form Labels:**
   - Find: `<label for="([^"]+)">([^<]+)</label>`
   - Replace: `<label for="$1" class="modern-form-label">$2</label>`

## Testing After Migration

1. **Visual Inspection:**
   - Cards have rounded corners and shadows
   - Hover effects work on cards and buttons
   - Form inputs have rounded borders
   - Tables have gradient headers

2. **Responsive Testing:**
   - Test on mobile viewport
   - Ensure forms remain usable
   - Check table scrolling

3. **Dark Mode Compatibility:**
   - If dark mode is implemented, ensure colors work

## Benefits of Migration

1. **Consistency:** All pages look and feel the same
2. **Maintainability:** Changes to design tokens update everywhere
3. **Performance:** Less duplicate CSS
4. **Modern Look:** Professional, polished appearance
5. **Better UX:** Consistent interactions and visual feedback

## Notes

- The design system is in `/wwwroot/css/design-system.css`
- View the Style Guide at `/style-guide` for component examples
- Keep page-specific styles minimal
- Use CSS variables for consistency
- Test thoroughly after migration