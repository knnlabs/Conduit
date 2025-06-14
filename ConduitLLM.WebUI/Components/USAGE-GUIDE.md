# Component Usage Guide

This guide provides quick examples for using the reusable Blazor components in your pages.

## Import Components

Add this to your page or `_Imports.razor`:
```razor
@using ConduitLLM.WebUI.Components
```

## Modal Component

### Basic Modal
```razor
<Modal @bind-IsVisible="showModal" Title="My Modal">
    <BodyContent>
        <p>Modal content goes here</p>
    </BodyContent>
    <FooterContent>
        <button class="btn btn-secondary" @onclick="() => showModal = false">Close</button>
        <button class="btn btn-primary" @onclick="SaveData">Save</button>
    </FooterContent>
</Modal>

@code {
    private bool showModal = false;
}
```

### Large Modal with Options
```razor
<Modal @bind-IsVisible="showEditModal" 
       Title="Edit Item"
       Size="Modal.ModalSize.Large"
       IsCentered="true"
       IsScrollable="true"
       CloseOnBackdropClick="false">
    <BodyContent>
        <!-- Your form content -->
    </BodyContent>
</Modal>
```

## Card Component

### Simple Card
```razor
<Card Title="Statistics" ShowShadow="true">
    <BodyContent>
        <p>Card content here</p>
    </BodyContent>
</Card>
```

### Card with Custom Header
```razor
<Card>
    <HeaderContent>
        <div class="d-flex justify-content-between align-items-center">
            <h5 class="mb-0">Custom Header</h5>
            <button class="btn btn-sm btn-primary">Action</button>
        </div>
    </HeaderContent>
    <BodyContent>
        <!-- Content -->
    </BodyContent>
</Card>
```

## ActionButtonGroup Component

### Standard Edit/Delete Actions
```razor
<ActionButtonGroup 
    Size="ActionButtonGroup.ButtonSize.Small"
    Actions="@(new List<ActionButtonGroup.ActionButton>
    {
        ActionButtonGroup.ActionButton.Edit(
            EventCallback.Factory.Create(this, () => EditItem(item))
        ),
        ActionButtonGroup.ActionButton.Delete(
            EventCallback.Factory.Create(this, () => DeleteItem(item))
        )
    })" />
```

### Custom Actions
```razor
<ActionButtonGroup 
    Actions="@(new List<ActionButtonGroup.ActionButton>
    {
        new ActionButtonGroup.ActionButton 
        { 
            Title = "Download", 
            IconClass = "fa fa-download", 
            Color = "info",
            OnClick = EventCallback.Factory.Create(this, Download)
        },
        new ActionButtonGroup.ActionButton 
        { 
            Title = "Archive", 
            IconClass = "fa fa-archive", 
            Color = "secondary",
            OnClick = EventCallback.Factory.Create(this, Archive)
        }
    })" />
```

## FilterPanel Component

```razor
<FilterPanel Title="Search Filters" 
             OnApply="ApplyFilters" 
             OnClear="ClearFilters"
             IsApplyDisabled="@(!HasFilters)">
    <FilterContent>
        <div class="col-12 col-md-4">
            <label class="form-label">Date Range</label>
            <select class="form-select" @bind="dateRange">
                <option value="">All time</option>
                <option value="7">Last 7 days</option>
                <option value="30">Last 30 days</option>
            </select>
        </div>
        <div class="col-12 col-md-4">
            <label class="form-label">Status</label>
            <select class="form-select" @bind="status">
                <option value="">All</option>
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
            </select>
        </div>
    </FilterContent>
</FilterPanel>
```

## StatCard Component

```razor
<div class="row">
    <div class="col-md-3">
        <StatCard Value="@totalRevenue.ToString("F2")" 
                  Label="Total Revenue" 
                  IconClass="fa fa-dollar-sign" 
                  Color="StatCard.StatCardColor.Success"
                  IsCurrency="true" />
    </div>
    <div class="col-md-3">
        <StatCard Value="@activeUsers.ToString()" 
                  Label="Active Users" 
                  IconClass="fa fa-users" 
                  Color="StatCard.StatCardColor.Primary"
                  ShowTrend="true"
                  TrendValue="@userGrowth" />
    </div>
</div>
```

## Form Components

### FormInput
```razor
<FormInput Label="Email Address" 
           @bind-Value="email" 
           InputType="email"
           Placeholder="user@example.com"
           IsRequired="true"
           ValidationMessage="@emailError" />
```

### FormInputGroup with Type Safety
```razor
<FormInputGroup TValue="decimal" 
                Label="Price" 
                @bind-Value="price" 
                Prefix="$" 
                InputType="number"
                Min="0"
                Step="0.01"
                HelpText="Enter the product price"
                IsRequired="true" />

<FormInputGroup TValue="int" 
                Label="Quantity" 
                @bind-Value="quantity" 
                InputType="number"
                Min="1"
                Max="100"
                Suffix="units" />
```

## LoadingButton Component

```razor
<LoadingButton Text="Save Changes" 
               LoadingText="Saving..."
               IsLoading="@isSaving"
               CssClass="btn-primary"
               IconClass="fa fa-save"
               OnClick="SaveData" />

@code {
    private bool isSaving = false;
    
    private async Task SaveData()
    {
        isSaving = true;
        // Your save logic here
        await Task.Delay(2000);
        isSaving = false;
    }
}
```

## CostDisplay Component

```razor
<table class="table">
    <tr>
        <td>Input Cost</td>
        <td><CostDisplay Value="@model.InputCost" Color="success" /></td>
    </tr>
    <tr>
        <td>Output Cost</td>
        <td><CostDisplay Value="@model.OutputCost" Color="warning" /></td>
    </tr>
</table>
```

## Best Practices

1. **Type Safety**: Always specify `TValue` for generic components like `FormInputGroup`
2. **Event Callbacks**: Use `EventCallback.Factory.Create()` for better performance
3. **Null Safety**: Components handle null values gracefully, displaying appropriate defaults
4. **Accessibility**: Components include proper ARIA attributes automatically
5. **Consistent Styling**: Use the provided color and size enums for consistency

## Migration Example

### Before (Inline HTML):
```razor
<div class="modal show" style="display: block">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Edit User</h5>
                <button type="button" class="btn-close" @onclick="CloseModal"></button>
            </div>
            <div class="modal-body">
                <!-- Form content -->
            </div>
            <div class="modal-footer">
                <button class="btn btn-secondary" @onclick="CloseModal">Cancel</button>
                <button class="btn btn-primary" @onclick="SaveUser">Save</button>
            </div>
        </div>
    </div>
</div>
```

### After (Using Modal Component):
```razor
<Modal @bind-IsVisible="showEditModal" Title="Edit User" Size="Modal.ModalSize.Large">
    <BodyContent>
        <!-- Form content -->
    </BodyContent>
    <FooterContent>
        <button class="btn btn-secondary" @onclick="() => showEditModal = false">Cancel</button>
        <LoadingButton Text="Save" IsLoading="@isSaving" OnClick="SaveUser" />
    </FooterContent>
</Modal>
```

This reduces code by ~70% and provides consistent behavior across the application.