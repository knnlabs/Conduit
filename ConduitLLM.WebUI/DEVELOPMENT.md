# WebUI Development Guide

This guide provides essential information for developing and maintaining the ConduitLLM WebUI Blazor application.

## Table of Contents

- [Blazor Render Modes](#blazor-render-modes)
- [Common Gotchas](#common-gotchas)
- [Component Development](#component-development)
- [Debugging Tips](#debugging-tips)
- [Testing Guidelines](#testing-guidelines)
- [Performance Considerations](#performance-considerations)

## Blazor Render Modes

In .NET 8+, Blazor introduced new render modes. Understanding these is crucial for component interactivity:

### Static Server-Side Rendering (SSR)
- **Default mode** - No `@rendermode` specified
- No interactivity, pure HTML generation
- Best for static content, SEO-friendly pages

### Interactive Server
- Add `@rendermode="InteractiveServer"` to enable
- Required for:
  - Click handlers (`@onclick`)
  - Form inputs (`@bind`)
  - Real-time updates
  - JavaScript interop

```razor
@* Example: Interactive component *@
@page "/interactive"
@rendermode InteractiveServer

<button @onclick="HandleClick">Click me!</button>

@code {
    private void HandleClick() => Console.WriteLine("Clicked!");
}
```

### Interactive WebAssembly
- Not used in this project
- Would require `@rendermode="InteractiveWebAssembly"`

### Auto Mode
- Not used in this project
- Would use `@rendermode="InteractiveAuto"`

## Common Gotchas

### 1. Component Not Responding to Clicks
**Problem**: Click handlers not working
**Solution**: Add `@rendermode="InteractiveServer"` to the component or its parent

### 2. JavaScript Errors After Updates
**Problem**: Browser caching old JavaScript files
**Solutions**:
- Clear browser cache (Ctrl+Shift+R)
- Use cache busting: `<script src="file.js?v=@Version"></script>`
- Enable "Disable cache" in browser DevTools

### 3. Blazor Initialization Errors
**Problem**: "Blazor has already started" error
**Cause**: Manual Blazor.start() call when Blazor auto-starts
**Solution**: Remove manual initialization for .NET 9+

### 4. Component Parameter Mismatch
**Problem**: Runtime error about missing properties
**Causes**:
- Renamed parameter not updated everywhere
- Case sensitivity issues
**Solution**: Use Find/Replace All, check parameter names carefully

### 5. CSS Not Applying
**Problem**: Styles not affecting components
**Causes**:
- CSS isolation (styles scoped to component)
- Missing `::deep` for child components
**Solutions**:
```css
/* Global styles in app.css */
.my-global-class { }

/* Component-isolated styles */
::deep .child-component-class { }
```

### 6. State Not Updating
**Problem**: UI not reflecting state changes
**Solution**: Call `StateHasChanged()` after async operations:
```csharp
private async Task LoadData()
{
    data = await DataService.GetDataAsync();
    StateHasChanged(); // Force UI update
}
```

### 7. Null Reference in OnInitialized
**Problem**: Services or parameters null during initialization
**Solution**: Use `OnInitializedAsync()` or `OnParametersSet()`:
```csharp
protected override async Task OnInitializedAsync()
{
    // Parameters and services are available here
    await base.OnInitializedAsync();
}
```

## Component Development

### Component Structure Template

```razor
@* ComponentName.razor *@
@* Purpose: Brief description of what this component does *@
@page "/route" @* Only if routable *@
@rendermode InteractiveServer @* Only if interactive *@
@implements IDisposable @* If needed *@
@inject ILogger<ComponentName> Logger
@inject IRequiredService Service

<div class="component-name">
    @if (IsLoading)
    {
        <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    }
    else if (HasError)
    {
        <div class="alert alert-danger">
            @ErrorMessage
        </div>
    }
    else
    {
        <!-- Main component content -->
    }
</div>

@code {
    #region Parameters
    
    /// <summary>
    /// Required parameter description.
    /// </summary>
    [Parameter, EditorRequired] 
    public string RequiredParam { get; set; } = default!;
    
    /// <summary>
    /// Optional parameter with default.
    /// </summary>
    [Parameter] 
    public string? OptionalParam { get; set; }
    
    /// <summary>
    /// Event callback for parent communication.
    /// </summary>
    [Parameter] 
    public EventCallback<string> OnSomethingChanged { get; set; }
    
    #endregion
    
    #region Private Fields
    
    private bool IsLoading = true;
    private bool HasError = false;
    private string? ErrorMessage;
    
    #endregion
    
    #region Lifecycle Methods
    
    protected override async Task OnInitializedAsync()
    {
        Logger.LogDebug("Initializing {ComponentName}", nameof(ComponentName));
        
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing component");
            HasError = true;
            ErrorMessage = "Failed to load data";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    protected override void OnParametersSet()
    {
        // Validate parameters
        if (string.IsNullOrEmpty(RequiredParam))
        {
            throw new ArgumentException($"{nameof(RequiredParam)} is required");
        }
    }
    
    public void Dispose()
    {
        // Clean up resources
        Logger.LogDebug("Disposing {ComponentName}", nameof(ComponentName));
    }
    
    #endregion
    
    #region Private Methods
    
    private async Task LoadDataAsync()
    {
        // Implementation
    }
    
    #endregion
}
```

### Component Best Practices

1. **Always use XML documentation** for public parameters
2. **Use `[EditorRequired]`** for mandatory parameters
3. **Implement proper loading states** with spinners
4. **Handle errors gracefully** with user-friendly messages
5. **Log component lifecycle** for debugging
6. **Dispose resources properly** (timers, subscriptions)
7. **Validate parameters** in `OnParametersSet()`

## Debugging Tips

### Browser Console

1. **Check for Blazor errors**: Look for red errors in console
2. **Enable Blazor logging**: Add to `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.Components": "Debug"
    }
  }
}
```

### Visual Studio / VS Code

1. **Breakpoint debugging**: Works with `@rendermode="InteractiveServer"`
2. **Hot Reload**: Make changes without restarting
3. **Browser Link**: Real-time CSS updates

### Useful Debugging Components

```razor
@* DebugInfo.razor - Show component state *@
@if (IsDevelopment)
{
    <div class="debug-panel">
        <h6>Debug Info</h6>
        <pre>@JsonSerializer.Serialize(DebugData, new JsonSerializerOptions { WriteIndented = true })</pre>
    </div>
}

@code {
    [Parameter] public object? DebugData { get; set; }
    private bool IsDevelopment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
}
```

## Testing Guidelines

### Component Testing

1. **Use bUnit** for component testing:
```csharp
[Fact]
public void Component_RendersCorrectly()
{
    // Arrange
    using var ctx = new TestContext();
    
    // Act
    var component = ctx.RenderComponent<MyComponent>(parameters => parameters
        .Add(p => p.Title, "Test Title"));
    
    // Assert
    Assert.Equal("Test Title", component.Find("h1").TextContent);
}
```

2. **Test parameter validation**:
```csharp
[Fact]
public void Component_ThrowsWhenRequiredParameterMissing()
{
    using var ctx = new TestContext();
    
    Assert.Throws<ArgumentException>(() =>
        ctx.RenderComponent<MyComponent>());
}
```

3. **Test interactivity**:
```csharp
[Fact]
public async Task Component_HandlesClickCorrectly()
{
    using var ctx = new TestContext();
    var component = ctx.RenderComponent<MyComponent>();
    
    await component.Find("button").ClickAsync();
    
    Assert.Equal("Clicked", component.Instance.State);
}
```

## Performance Considerations

### 1. Minimize Re-renders

```csharp
// Bad - Re-renders on every change
<div>@DateTime.Now</div>

// Good - Only updates when needed
<div>@LastUpdateTime</div>

@code {
    private DateTime LastUpdateTime;
    
    private void UpdateTime()
    {
        LastUpdateTime = DateTime.Now;
        StateHasChanged();
    }
}
```

### 2. Use Virtualization for Large Lists

```razor
<Virtualize Items="@LargeList" Context="item">
    <div>@item.Name</div>
</Virtualize>
```

### 3. Lazy Load Heavy Components

```razor
@if (ShowHeavyComponent)
{
    <HeavyComponent />
}

<button @onclick="() => ShowHeavyComponent = true">
    Load Component
</button>
```

### 4. Dispose Timers and Subscriptions

```csharp
@implements IDisposable

@code {
    private Timer? _timer;
    
    protected override void OnInitialized()
    {
        _timer = new Timer(Callback, null, 0, 1000);
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

### 5. Use Cascading Values Sparingly

```razor
@* Only cascade values that many components need *@
<CascadingValue Value="@UserContext">
    @ChildContent
</CascadingValue>
```

## Development Workflow

### Local Development

1. **Run with hot reload**:
```bash
dotnet watch run --project ConduitLLM.WebUI
```

2. **Enable detailed errors** in `appsettings.Development.json`:
```json
{
  "DetailedErrors": true
}
```

### Docker Development

1. **Use development compose file**:
```bash
docker-compose -f docker-compose.dev.yml up
```

2. **View container logs**:
```bash
docker logs -f conduit2-webui-1
```

3. **Attach debugger** to container (VS Code):
```json
{
    "name": "Docker .NET Attach",
    "type": "coreclr",
    "request": "attach",
    "processId": "${command:pickRemoteProcess}",
    "pipeTransport": {
        "pipeProgram": "docker",
        "pipeArgs": ["exec", "-i", "conduit2-webui-1"],
        "debuggerPath": "/vsdbg/vsdbg",
        "pipeCwd": "${workspaceRoot}"
    }
}
```

## Troubleshooting Checklist

When something isn't working:

- [ ] Check browser console for errors
- [ ] Verify component has correct `@rendermode`
- [ ] Clear browser cache
- [ ] Check if parameters are correctly named and cased
- [ ] Verify services are registered in DI
- [ ] Check if `StateHasChanged()` is needed
- [ ] Look for null reference exceptions
- [ ] Verify async operations are awaited
- [ ] Check component disposal for memory leaks
- [ ] Review logs for detailed error messages

## Additional Resources

- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor/)
- [Blazor University](https://blazor-university.com/)
- [bUnit Testing](https://bunit.dev/)
- [Blazor Performance Best Practices](https://docs.microsoft.com/aspnet/core/blazor/performance)