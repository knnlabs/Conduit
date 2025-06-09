# Blazor Components Library

This directory contains reusable Blazor components designed to reduce code duplication and improve maintainability across the ConduitLLM WebUI.

## Core Components

### Modal.razor
A flexible modal dialog component with customizable size, positioning, and content areas.
- **Features**: Customizable size, centered/scrollable options, backdrop click handling
- **Usage**: Replace inline modal HTML with this component for consistent behavior

### ActionButtonGroup.razor
Standardized action button groups for common operations like edit/delete.
- **Features**: Pre-defined action types, consistent styling, size options
- **Usage**: Replace repeated button group patterns in tables and lists

### Card.razor
A versatile card component following the application's design patterns.
- **Features**: Optional header/footer, shadow options, rounded corners, fill height
- **Usage**: Replace Bootstrap card HTML with this component

### FilterPanel.razor
Consistent filter form layout with apply/clear buttons.
- **Features**: Responsive grid layout, action buttons, customizable padding
- **Usage**: Standardize filter forms across different pages

### StatCard.razor
Display statistics with icons, values, and optional trends.
- **Features**: Icon support, currency formatting, trend indicators, color themes
- **Usage**: Replace custom stat card implementations

## Form Components

### FormInput.razor
Basic form input with label, validation, and help text.
- **Features**: Required field indicator, validation messages, various input types
- **Usage**: Standardize form inputs across the application

### FormInputGroup.razor
Input group with prefix/suffix support for currency, units, etc.
- **Features**: Input group addons, number type conversion, button support
- **Usage**: Replace Bootstrap input group patterns

### LoadingButton.razor
Button component with loading state and spinner.
- **Features**: Loading spinner, customizable text, icon support
- **Usage**: Replace inline loading button implementations

## Utility Components

### CostDisplay.razor
Formatted cost display with color coding.
- **Features**: Currency formatting, decimal places, null handling
- **Usage**: Consistent cost display across tables

### AudioCostDropdown.razor
Specialized dropdown for displaying audio pricing tiers.
- **Features**: Multiple audio cost types, formatted display
- **Usage**: Replace inline audio cost dropdowns

## Usage Example

```razor
<!-- Before: Inline modal HTML -->
<div class="modal show" style="display: block">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Edit Item</h5>
                <button type="button" class="btn-close" @onclick="CloseModal"></button>
            </div>
            <div class="modal-body">
                <!-- Content -->
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" @onclick="CloseModal">Cancel</button>
                <button class="btn btn-primary" @onclick="Save">Save</button>
            </div>
        </div>
    </div>
</div>

<!-- After: Using Modal component -->
<Modal @bind-IsVisible="showModal" Title="Edit Item" Size="Modal.ModalSize.Large">
    <BodyContent>
        <!-- Content -->
    </BodyContent>
    <FooterContent>
        <button class="btn btn-secondary" @onclick="CloseModal">Cancel</button>
        <LoadingButton Text="Save" LoadingText="Saving..." IsLoading="@isSaving" OnClick="Save" />
    </FooterContent>
</Modal>
```

## Benefits

1. **Consistency**: Uniform UI/UX across all pages
2. **Maintainability**: Single source of truth for common patterns
3. **Development Speed**: Faster page development with pre-built components
4. **Testing**: Easier to unit test individual components
5. **Accessibility**: Centralized ARIA attributes and keyboard handling

## Next Steps

To use these components in existing pages:
1. Add `@using ConduitLLM.WebUI.Components` to your page or `_Imports.razor`
2. Replace inline HTML patterns with appropriate components
3. Remove duplicate CSS and JavaScript code
4. Test functionality to ensure compatibility

For new development:
- Always check for existing components before creating custom UI
- Consider creating new components for patterns used 3+ times
- Follow the established component structure and documentation patterns