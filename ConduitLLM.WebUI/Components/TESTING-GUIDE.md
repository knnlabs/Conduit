# Component Testing Guide

This guide explains how to test the new reusable Blazor components.

## 1. Visual Testing with Component Showcase

The easiest way to test all components is using the Component Showcase page:

1. **Start the WebUI application**:
   ```bash
   dotnet run --project ConduitLLM.WebUI
   ```

2. **Navigate to the showcase**:
   ```
   http://localhost:5001/component-showcase
   ```

3. **Test each component interactively**:
   - Click buttons to show/hide modals
   - Test form inputs with different values
   - Verify loading states and animations
   - Check responsive behavior at different screen sizes

## 2. Integration Testing in Existing Pages

### Step 1: Choose a Test Page
Start with a simple page that uses patterns we've componentized. Good candidates:
- `ModelCosts.razor` (uses modals, action buttons, cards)
- `VirtualKeys.razor` (uses action buttons, stat displays)
- `IpAccessFiltering.razor` (uses modals, forms)

### Step 2: Create a Test Branch
```bash
git checkout -b test-blazor-components
```

### Step 3: Refactor One Section at a Time

#### Example: Refactoring a Modal in ModelCosts.razor

**Original code:**
```razor
<div class="modal @(showModal ? "show" : "")" style="display: @(showModal ? "block" : "none")">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Edit Model Cost</h5>
                <button type="button" class="btn-close" @onclick="CloseModal"></button>
            </div>
            <div class="modal-body">
                <!-- Form content -->
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" @onclick="CloseModal">Cancel</button>
                <button class="btn btn-primary" @onclick="Save">Save</button>
            </div>
        </div>
    </div>
</div>
```

**Refactored with Modal component:**
```razor
@using ConduitLLM.WebUI.Components

<Modal @bind-IsVisible="showModal" Title="Edit Model Cost">
    <BodyContent>
        <!-- Form content -->
    </BodyContent>
    <FooterContent>
        <button class="btn btn-secondary" @onclick="() => showModal = false">Cancel</button>
        <LoadingButton Text="Save" 
                       LoadingText="Saving..." 
                       IsLoading="@isSaving" 
                       OnClick="Save" />
    </FooterContent>
</Modal>
```

### Step 4: Test Functionality
1. **Modal behavior**:
   - Opens when triggered
   - Closes on backdrop click (if enabled)
   - Close button works
   - ESC key closes modal (if implemented)

2. **Form components**:
   - Values bind correctly
   - Validation messages appear
   - Type conversion works (for FormInputGroup)
   - Min/max constraints are enforced

3. **Action buttons**:
   - Click events fire correctly
   - Tooltips appear on hover
   - Disabled state works

## 3. Unit Testing Components

Create unit tests for the components:

```csharp
// Example: ConduitLLM.WebUI.Tests/Components/ModalTests.cs
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ModalTests : TestContext
{
    [Fact]
    public void Modal_ShowsWhenIsVisibleTrue()
    {
        // Arrange
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.BodyContent, "<p>Test content</p>"));

        // Assert
        Assert.Contains("show", cut.Find(".modal").GetClasses());
        Assert.Contains("Test Modal", cut.Find(".modal-title").TextContent);
    }

    [Fact]
    public void Modal_ClosesOnBackdropClick()
    {
        // Arrange
        var isVisible = true;
        var cut = RenderComponent<Modal>(parameters => parameters
            .Add(p => p.IsVisible, isVisible)
            .Add(p => p.IsVisibleChanged, EventCallback.Factory.Create<bool>(this, (value) => isVisible = value))
            .Add(p => p.CloseOnBackdropClick, true));

        // Act
        cut.Find(".modal").Click();

        // Assert
        Assert.False(isVisible);
    }
}
```

## 4. Testing Checklist

### Modal Component
- [ ] Shows/hides based on IsVisible parameter
- [ ] Title displays correctly
- [ ] Close button works
- [ ] Backdrop click behavior (configurable)
- [ ] Different sizes render correctly
- [ ] Centered and scrollable options work

### Card Component
- [ ] Title displays when provided
- [ ] Shadow options apply correctly
- [ ] Border and rounded corner options work
- [ ] Custom header/footer content renders
- [ ] FillHeight class applies when set

### ActionButtonGroup
- [ ] All action buttons render
- [ ] Click events fire correctly
- [ ] Icons display properly
- [ ] Disabled state works
- [ ] Button size options apply

### FilterPanel
- [ ] Filter content renders in grid layout
- [ ] Apply button triggers callback
- [ ] Clear button triggers callback
- [ ] Button alignment options work
- [ ] Disabled states work correctly

### StatCard
- [ ] Values display with correct formatting
- [ ] Currency symbol shows when enabled
- [ ] Icons render with correct colors
- [ ] Trend indicators work
- [ ] Hover effects apply

### Form Components
- [ ] Two-way binding works
- [ ] Type conversion in FormInputGroup
- [ ] Validation messages display
- [ ] Required field indicators show
- [ ] Help text displays
- [ ] Input group prefixes/suffixes render

### LoadingButton
- [ ] Shows spinner when loading
- [ ] Text changes during loading
- [ ] Disabled during loading
- [ ] Click events don't fire when loading

## 5. Performance Testing

1. **Check render count**: Use Blazor's built-in diagnostics to ensure components don't re-render unnecessarily
2. **Memory usage**: Monitor for memory leaks, especially with event handlers
3. **Large lists**: Test ActionButtonGroup in tables with many rows

## 6. Accessibility Testing

1. **Keyboard navigation**: Ensure all interactive elements are keyboard accessible
2. **Screen readers**: Test with NVDA or JAWS
3. **ARIA attributes**: Verify proper ARIA labels and roles
4. **Focus management**: Check focus moves correctly when modals open/close

## 7. Browser Compatibility

Test in:
- [ ] Chrome (latest)
- [ ] Firefox (latest)
- [ ] Safari (latest)
- [ ] Edge (latest)
- [ ] Mobile browsers

## 8. Common Issues to Watch For

1. **Event handler memory leaks**: Ensure components properly dispose of event handlers
2. **CSS conflicts**: Check that component styles don't break existing page layouts
3. **JavaScript interop**: Modal backdrop clicks might need JS interop
4. **Validation timing**: Form components should validate at appropriate times

## 9. Gradual Migration Strategy

1. Start with one component type (e.g., all modals)
2. Test thoroughly in development
3. Deploy to staging environment
4. Monitor for issues
5. Move to next component type

## 10. Rollback Plan

If issues arise:
1. Components are isolated - can revert individual pages
2. Keep original HTML as comments during migration
3. Use feature flags to toggle between old/new implementations

## Example Test Script

```bash
# 1. Build and run
dotnet build
dotnet run --project ConduitLLM.WebUI

# 2. Open browser to http://localhost:5001/component-showcase

# 3. Test each component systematically:
#    - Visual appearance
#    - Interactive behavior  
#    - Edge cases (null values, long text, etc.)
#    - Responsive design

# 4. Check browser console for errors

# 5. Test in a real page by refactoring one component at a time
```

Remember: The goal is to ensure the new components work identically to the original HTML while providing better maintainability and consistency.