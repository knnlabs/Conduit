# Searchable Dropdown Components

This document describes the searchable dropdown components added to the ConduitLLM WebUI.

## Components

### SearchableDropdown<TItem>

A generic, reusable searchable dropdown component that provides:
- Type-safe item selection
- Real-time search filtering
- Customizable display and value selectors
- Optional descriptions for items
- Click-outside-to-close functionality
- Smooth animations and modern styling

#### Basic Usage

```razor
<SearchableDropdown TItem="string"
                    Items="@myStringList"
                    @bind-SelectedValue="@selectedString"
                    Placeholder="Select an option..."
                    SearchPlaceholder="Search..." />
```

#### Advanced Usage with Custom Objects

```razor
<SearchableDropdown TItem="MyCustomClass"
                    Items="@myObjectList"
                    @bind-SelectedValue="@selectedId"
                    ValueSelector="@(item => item.Id.ToString())"
                    DisplaySelector="@(item => item.Name)"
                    DescriptionSelector="@(item => $"{item.Category} - ${item.Price:F2}")"
                    CustomSearchFilter="@MyCustomSearchMethod" />
```

### ModelSearchableDropdown

A specialized dropdown for AI model selection that:
- Displays model costs alongside model names
- Supports searching by model name, provider, or cost
- Highlights free models
- Shows cost per million tokens for input/output

#### Usage

```razor
<ModelSearchableDropdown 
    Models="@providerModels"
    @bind-SelectedModelId="@selectedModelId"
    ProviderName="@currentProvider"
    ModelCosts="@modelCostsList"
    Disabled="@isLoading" />
```

## Features

### Search Functionality
- Real-time filtering as you type
- Search across multiple fields (name, description, custom fields)
- Custom search filters for complex scenarios
- Clear button to reset search

### User Experience
- Click outside to close dropdown
- Keyboard navigation support (planned)
- Smooth fade-in animation
- Responsive design
- Scrollable list with custom scrollbar styling
- Active item highlighting

### Customization
- Custom placeholders and messages
- Optional empty/null option
- Disable state support
- Custom value and display selectors
- Description support for additional context

## Integration in MappingEdit.razor

The ModelSearchableDropdown has been integrated into the MappingEdit page to replace the standard select dropdown for Provider Model ID selection. Users can now:

1. **Search models** by typing keywords
2. **Filter by cost** (e.g., search for "free" to find free models)
3. **See costs** directly in the dropdown
4. **Toggle** between searchable dropdown and manual text input

## JavaScript Dependencies

The component requires the `searchable-dropdown.js` file which provides:
- Click-outside detection
- Proper cleanup on component disposal

## Styling

The component uses:
- Bootstrap 5 classes for consistent theming
- Custom CSS for enhanced visual appeal
- Smooth animations and transitions
- Custom scrollbar styling for better aesthetics

## Demo Page

A demo page is available at `/searchable-dropdown-demo` showcasing:
- Basic string selection
- Model selection with costs
- Complex object selection with custom search

## Future Enhancements

Potential improvements include:
- Keyboard navigation (arrow keys, enter, escape)
- Multi-select support
- Async data loading
- Virtual scrolling for large datasets
- Grouping/categorization support