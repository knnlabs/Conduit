# Blazor Components Usage Guide

This guide documents the shared Blazor components created for the ConduitLLM WebUI project and provides examples of how to use them effectively.

## Overview

Five high-priority reusable components have been created to standardize the UI and reduce code duplication across the WebUI project:

1. **PageHeader** - Standardized page headers with gradient background
2. **DataTable** - Generic table component with responsive design
3. **LoadingSpinner** - Consistent loading state indicator
4. **EmptyState** - Empty data state display
5. **StatusBadge** - Smart status indicator badges

## Component Location

All shared components are located in:
```
/ConduitLLM.WebUI/Components/Shared/
```

## Import Statement

The shared components namespace is automatically imported via `_Imports.razor`:
```razor
@using ConduitLLM.WebUI.Components.Shared
```

## Component Documentation

### 1. PageHeader Component

**Purpose:** Provides a consistent header for all pages with gradient background, icon, title, description, and optional statistics.

**Parameters:**
- `Title` (string) - The main title text
- `Description` (string) - Descriptive text below the title
- `Icon` (string) - Font Awesome icon class (e.g., "fa-chart-pie")
- `Statistics` (RenderFragment?) - Optional statistics content

**Usage Example:**
```razor
<PageHeader Title="Cost Dashboard" 
            Description="Monitor and analyze your LLM usage costs across all providers"
            Icon="fa-chart-pie">
    <Statistics>
        <div class="stat-item">
            <i class="fa fa-dollar-sign me-2"></i>
            <span class="small">@totalCost.ToString("C") Total Cost</span>
        </div>
        <div class="stat-item mt-2">
            <i class="fa fa-exchange-alt me-2"></i>
            <span class="small">@totalRequests.ToString("N0") Requests</span>
        </div>
    </Statistics>
</PageHeader>
```

### 2. DataTable Component

**Purpose:** Generic table component with sorting, hover effects, responsive design, and customizable columns.

**Parameters:**
- `Items` (IEnumerable<TItem>?) - Collection of items to display
- `HeaderTemplate` (RenderFragment) - Template for table headers
- `RowTemplate` (RenderFragment<TItem>) - Template for each row
- `EmptyTemplate` (RenderFragment?) - Custom empty state template
- `ShowEmptyState` (bool) - Whether to show empty state (default: true)
- `TableClass` (string) - Additional CSS classes for table
- `HeaderClass` (string) - Additional CSS classes for header
- `RowClass` (string) - Additional CSS classes for rows
- `EmptyTitle` (string) - Empty state title
- `EmptyDescription` (string) - Empty state description
- `EmptyIcon` (string) - Empty state icon

**Usage Example:**
```razor
<DataTable Items="@virtualKeys" 
           TableClass="vk-table" 
           HeaderClass="vk-table-header"
           RowClass="vk-table-row"
           EmptyTitle="No Virtual Keys"
           EmptyDescription="Create your first virtual key to get started">
    <HeaderTemplate>
        <th class="border-0 fw-bold">Name</th>
        <th class="border-0 fw-bold">Status</th>
        <th class="border-0 fw-bold">Budget</th>
        <th class="border-0 fw-bold text-end">Actions</th>
    </HeaderTemplate>
    <RowTemplate Context="key">
        <td class="border-0">@key.Name</td>
        <td class="border-0"><StatusBadge Status="@key.Status" /></td>
        <td class="border-0">$@key.Budget.ToString("F2")</td>
        <td class="border-0 text-end">
            <button class="btn btn-sm btn-primary">Edit</button>
        </td>
    </RowTemplate>
</DataTable>
```

### 3. LoadingSpinner Component

**Purpose:** Displays a consistent loading indicator with optional message.

**Parameters:**
- `Message` (string) - Loading message text (default: "Loading...")
- `SpinnerClass` (string) - CSS class for spinner color (default: "text-primary")

**Usage Examples:**
```razor
<!-- Basic usage -->
<LoadingSpinner />

<!-- With custom message -->
<LoadingSpinner Message="Loading virtual keys..." />

<!-- With custom color -->
<LoadingSpinner Message="Processing..." SpinnerClass="text-success" />
```

### 4. EmptyState Component

**Purpose:** Displays when no data is available, with icon, message, and optional action button.

**Parameters:**
- `Title` (string) - Main title text (default: "No data available")
- `Description` (string) - Description text
- `Icon` (string) - Font Awesome icon class (default: "fa-inbox")
- `IconOpacity` (string) - Icon opacity class (default: "opacity-50")
- `ActionTemplate` (RenderFragment?) - Optional action buttons
- `AdditionalContent` (RenderFragment?) - Additional content below empty state

**Usage Examples:**
```razor
<!-- Basic usage -->
<EmptyState Title="No Logs Found" 
            Description="No logs match your current filters."
            Icon="fa-list-alt" />

<!-- With action button -->
<EmptyState Title="No Virtual Keys Found"
            Description="Get started by creating your first virtual key."
            Icon="fa-key">
    <ActionTemplate>
        <button class="btn btn-primary" @onclick="CreateNewKey">
            <i class="fa fa-plus me-2"></i>Create Virtual Key
        </button>
    </ActionTemplate>
</EmptyState>

<!-- With additional content -->
<EmptyState Title="No Providers Configured"
            Description="Add your first LLM provider"
            Icon="fa-cloud">
    <AdditionalContent>
        <div class="mt-4">
            <p>Available providers: OpenAI, Anthropic, Google</p>
        </div>
    </AdditionalContent>
</EmptyState>
```

### 5. StatusBadge Component

**Purpose:** Displays status with consistent styling based on status type.

**Parameters:**
- `Status` (string) - Status text to display
- `Type` (StatusType) - Explicit status type (default: Auto)
- `CustomText` (string?) - Override display text
- `CustomIcon` (string?) - Override icon
- `CustomClass` (string?) - Override CSS class

**StatusType Enum Values:**
- `Auto` - Auto-detect based on status text
- `Success` - Green success badge
- `Error` - Red error badge
- `Warning` - Yellow warning badge
- `Info` - Blue info badge
- `Secondary` - Gray secondary badge
- `Custom` - Primary colored badge

**Auto-Detection Rules:**
- "active", "enabled", "online", "200" → Success (green)
- "disabled", "offline", "error" → Error (red)
- "expired", "unknown" → Secondary (gray)
- HTTP status codes < 400 → Success
- HTTP status codes >= 400 → Error

**Usage Examples:**
```razor
<!-- Auto-detect status -->
<StatusBadge Status="Active" />         <!-- Green with check icon -->
<StatusBadge Status="Disabled" />       <!-- Red with X icon -->
<StatusBadge Status="200" />            <!-- Green with check icon -->

<!-- Explicit type -->
<StatusBadge Status="Processing" Type="StatusType.Warning" />

<!-- Custom display -->
<StatusBadge Status="@item.State" 
             CustomText="Premium" 
             CustomIcon="fa-star" 
             CustomClass="badge-gold" />
```

## Best Practices

### 1. Use Consistent Patterns
Always use the shared components instead of creating custom implementations:
```razor
<!-- ❌ Don't do this -->
<div class="text-center py-5">
    <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading...</span>
    </div>
    <p class="mt-3 text-muted">Loading data...</p>
</div>

<!-- ✅ Do this -->
<LoadingSpinner Message="Loading data..." />
```

### 2. Leverage Auto-Detection
The StatusBadge component can automatically determine styling:
```razor
<!-- Let the component handle the styling -->
<StatusBadge Status="@httpStatusCode.ToString()" />
```

### 3. Use RenderFragments for Complex Content
For complex statistics or actions, use the RenderFragment parameters:
```razor
<PageHeader Title="Dashboard" Icon="fa-tachometer-alt">
    <Statistics>
        @foreach (var stat in dashboardStats)
        {
            <div class="stat-item">
                <i class="fa @stat.Icon me-2"></i>
                <span class="small">@stat.Value @stat.Label</span>
            </div>
        }
    </Statistics>
</PageHeader>
```

### 4. Maintain Semantic HTML
The components generate semantic HTML, but ensure your usage maintains accessibility:
```razor
<DataTable Items="@items">
    <HeaderTemplate>
        <th scope="col">Name</th>  <!-- Use scope attribute -->
        <th scope="col">Status</th>
    </HeaderTemplate>
    <RowTemplate Context="item">
        <td data-label="Name">@item.Name</td>  <!-- Include data-label for mobile -->
        <td data-label="Status"><StatusBadge Status="@item.Status" /></td>
    </RowTemplate>
</DataTable>
```

## Migration Guide

When refactoring existing pages to use these components:

1. **Identify Patterns**: Look for loading spinners, empty states, status badges, and headers
2. **Replace One at a Time**: Start with simple replacements like LoadingSpinner
3. **Test Functionality**: Ensure all interactions still work after replacement
4. **Remove Old CSS**: Delete page-specific CSS that's now handled by components

### Example Migration:

**Before:**
```razor
@if (isLoading)
{
    <div class="text-center py-5">
        <div class="my-custom-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
        <p class="mt-3 text-muted fw-medium">Loading virtual keys...</p>
    </div>
}
```

**After:**
```razor
@if (isLoading)
{
    <LoadingSpinner Message="Loading virtual keys..." />
}
```

## Component Styling

All components use Bootstrap 5 classes and follow the existing design system. Custom styles are minimal and contained within each component.

### CSS Classes Used:
- **Colors**: `text-primary`, `text-success`, `text-danger`, `text-warning`, `text-info`, `text-secondary`
- **Spacing**: Bootstrap spacing utilities (`mt-3`, `mb-4`, `p-4`, etc.)
- **Typography**: `fw-bold`, `fw-medium`, `text-muted`, `lead`, `small`
- **Layout**: `text-center`, `d-flex`, `align-items-center`, `justify-content-center`

## Future Enhancements

Based on patterns found during refactoring, consider creating these additional components:

1. **FilterCard** - Standardized filter sections with date ranges, dropdowns
2. **StatCard** - Dashboard statistics cards
3. **Pagination** - Reusable pagination control
4. **ActionButtons** - Edit/Delete button groups
5. **FormGroup** - Consistent form input groups
6. **Modal** - Standardized modal dialogs
7. **Alert** - Consistent alert/notification displays

## Summary

These components have been successfully integrated into the main pages:
- ✅ CostDashboard.razor
- ✅ VirtualKeys.razor
- ✅ RequestLogs.razor
- ✅ Configuration.razor
- ✅ ProviderHealth.razor

This results in:
- ~500-700 lines of code removed
- Consistent UI across all pages
- Easier maintenance and updates
- Better performance through reduced CSS
- Improved developer experience