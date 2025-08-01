# SignalR Quick Start Guide for Conduit

## Overview

This guide provides practical examples for implementing new SignalR features in Conduit. It complements the comprehensive implementation plan with ready-to-use code snippets.

## Quick Implementation Examples

### 1. Adding Real-Time Spend Notifications

#### Backend: Create SpendNotificationHub

```csharp
// ConduitLLM.Http/Hubs/SpendNotificationHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace ConduitLLM.Http.Hubs
{
    [Authorize(AuthenticationSchemes = "VirtualKeySignalR")]
    public class SpendNotificationHub : Hub
    {
        private readonly IAsyncTaskService _taskService;
        private readonly ILogger<SpendNotificationHub> _logger;

        public SpendNotificationHub(
            IAsyncTaskService taskService,
            ILogger<SpendNotificationHub> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var virtualKeyId = Context.User?.FindFirst("VirtualKeyId")?.Value;
            if (!string.IsNullOrEmpty(virtualKeyId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
                _logger.LogInformation("Client {ConnectionId} joined spend group for vkey-{VirtualKeyId}", 
                    Context.ConnectionId, virtualKeyId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyId = Context.User?.FindFirst("VirtualKeyId")?.Value;
            if (!string.IsNullOrEmpty(virtualKeyId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
```

#### Backend: Create SpendNotificationService

```csharp
// ConduitLLM.Http/Services/SpendNotificationService.cs
namespace ConduitLLM.Http.Services
{
    public interface ISpendNotificationService
    {
        Task NotifySpendUpdate(string virtualKeyId, decimal newSpend, decimal totalSpend, decimal? budget);
        Task NotifyBudgetAlert(string virtualKeyId, decimal percentage, decimal remaining);
    }

    public class SpendNotificationService : ISpendNotificationService
    {
        private readonly IHubContext<SpendNotificationHub> _hubContext;
        private readonly ILogger<SpendNotificationService> _logger;

        public SpendNotificationService(
            IHubContext<SpendNotificationHub> hubContext,
            ILogger<SpendNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifySpendUpdate(string virtualKeyId, decimal newSpend, decimal totalSpend, decimal? budget)
        {
            var notification = new
            {
                timestamp = DateTime.UtcNow,
                newSpend,
                totalSpend,
                budget,
                budgetPercentage = budget.HasValue && budget.Value > 0 
                    ? (totalSpend / budget.Value) * 100 
                    : (decimal?)null
            };

            await _hubContext.Clients
                .Group($"vkey-{virtualKeyId}")
                .SendAsync("SpendUpdate", notification);

            // Check for budget alerts
            if (budget.HasValue && budget.Value > 0)
            {
                var percentage = (totalSpend / budget.Value) * 100;
                if (percentage >= 80 && percentage < 100)
                {
                    await NotifyBudgetAlert(virtualKeyId, percentage, budget.Value - totalSpend);
                }
                else if (percentage >= 100)
                {
                    await NotifyBudgetAlert(virtualKeyId, percentage, 0);
                }
            }
        }

        public async Task NotifyBudgetAlert(string virtualKeyId, decimal percentage, decimal remaining)
        {
            var alert = new
            {
                timestamp = DateTime.UtcNow,
                percentage,
                remaining,
                severity = percentage >= 100 ? "critical" : percentage >= 90 ? "warning" : "info"
            };

            await _hubContext.Clients
                .Group($"vkey-{virtualKeyId}")
                .SendAsync("BudgetAlert", alert);
        }
    }
}
```

#### Backend: Wire up in SpendUpdateEventHandler

```csharp
// Add to existing SpendUpdateEventHandler.cs
public class SpendUpdatedEventHandler : IConsumer<SpendUpdated>
{
    private readonly ISpendNotificationService _notificationService;
    // ... other dependencies

    public async Task Consume(ConsumeContext<SpendUpdated> context)
    {
        // ... existing cache invalidation logic

        // Add real-time notification
        await _notificationService.NotifySpendUpdate(
            context.Message.KeyId,
            context.Message.Amount,
            context.Message.NewTotalSpend,
            context.Message.Budget);
    }
}
```

#### Frontend: JavaScript Client

```javascript
// wwwroot/js/spend-notifications.js
class SpendNotificationClient {
    constructor(dotNetReference, virtualKey) {
        this.dotNetReference = dotNetReference;
        this.virtualKey = virtualKey;
        this.connection = null;
        this.isConnected = false;
    }

    async connect() {
        try {
            const apiBaseUrl = window.conduitConfig?.apiBaseUrl || 'http://localhost:5000';
            const hubUrl = `${apiBaseUrl}/hubs/spend-notifications`;
            
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, {
                    accessTokenFactory: () => this.virtualKey,
                    withCredentials: false
                })
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                    }
                })
                .build();

            // Set up event handlers
            this.connection.on('SpendUpdate', (notification) => {
                this.dotNetReference.invokeMethodAsync('OnSpendUpdate', notification);
            });

            this.connection.on('BudgetAlert', (alert) => {
                this.dotNetReference.invokeMethodAsync('OnBudgetAlert', alert);
            });

            this.connection.onreconnecting(() => {
                this.isConnected = false;
                this.dotNetReference.invokeMethodAsync('OnConnectionStatusChanged', false);
            });

            this.connection.onreconnected(() => {
                this.isConnected = true;
                this.dotNetReference.invokeMethodAsync('OnConnectionStatusChanged', true);
            });

            await this.connection.start();
            this.isConnected = true;
            this.dotNetReference.invokeMethodAsync('OnConnectionStatusChanged', true);
            
        } catch (err) {
            console.error('Failed to connect to spend notifications hub:', err);
            throw err;
        }
    }

    async disconnect() {
        if (this.connection) {
            await this.connection.stop();
            this.connection = null;
            this.isConnected = false;
        }
    }
}

// Export for Blazor
window.SpendNotificationClient = SpendNotificationClient;
```

#### Frontend: Blazor Component

```razor
@* Components/Shared/SpendNotificationListener.razor *@
@implements IAsyncDisposable
@inject IJSRuntime JS

<div class="spend-notifications">
    @if (!IsConnected)
    {
        <div class="alert alert-warning">
            <i class="fas fa-spinner fa-spin"></i> Connecting to real-time updates...
        </div>
    }
    
    @if (LatestSpendUpdate != null)
    {
        <div class="spend-update-toast @(IsVisible ? "show" : "")">
            <div class="toast-header">
                <strong>Spend Update</strong>
                <small>@LatestSpendUpdate.Timestamp.ToString("HH:mm:ss")</small>
            </div>
            <div class="toast-body">
                <p>New spend: $@LatestSpendUpdate.NewSpend.ToString("F4")</p>
                <p>Total: $@LatestSpendUpdate.TotalSpend.ToString("F2")</p>
                @if (LatestSpendUpdate.BudgetPercentage.HasValue)
                {
                    <div class="progress">
                        <div class="progress-bar @GetProgressBarClass(LatestSpendUpdate.BudgetPercentage.Value)" 
                             style="width: @Math.Min(100, LatestSpendUpdate.BudgetPercentage.Value)%">
                            @LatestSpendUpdate.BudgetPercentage.Value.ToString("F1")%
                        </div>
                    </div>
                }
            </div>
        </div>
    }

    @if (LatestBudgetAlert != null && ShowBudgetAlert)
    {
        <div class="alert alert-@GetAlertClass(LatestBudgetAlert.Severity) alert-dismissible fade show">
            <h5>Budget Alert!</h5>
            <p>You've used @LatestBudgetAlert.Percentage.ToString("F1")% of your budget.</p>
            <p>Remaining: $@LatestBudgetAlert.Remaining.ToString("F2")</p>
            <button type="button" class="btn-close" @onclick="DismissBudgetAlert"></button>
        </div>
    }
</div>

@code {
    [Parameter] public string VirtualKey { get; set; } = null!;
    [Parameter] public EventCallback<SpendUpdate> OnSpendUpdate { get; set; }
    [Parameter] public EventCallback<BudgetAlert> OnBudgetAlert { get; set; }

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SpendNotificationListener>? _dotNetRef;
    private bool IsConnected = false;
    private SpendUpdate? LatestSpendUpdate;
    private BudgetAlert? LatestBudgetAlert;
    private bool IsVisible = false;
    private bool ShowBudgetAlert = false;
    private System.Timers.Timer? _hideTimer;

    protected override async Task OnInitializedAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./js/spend-notifications.js");
        
        await Connect();
    }

    private async Task Connect()
    {
        if (_jsModule != null && _dotNetRef != null)
        {
            await _jsModule.InvokeVoidAsync("connect", _dotNetRef, VirtualKey);
        }
    }

    [JSInvokable]
    public async Task OnSpendUpdate(JsonElement notification)
    {
        LatestSpendUpdate = notification.Deserialize<SpendUpdate>();
        ShowToast();
        
        if (OnSpendUpdate.HasDelegate)
        {
            await OnSpendUpdate.InvokeAsync(LatestSpendUpdate);
        }
        
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnBudgetAlert(JsonElement alert)
    {
        LatestBudgetAlert = alert.Deserialize<BudgetAlert>();
        ShowBudgetAlert = true;
        
        if (OnBudgetAlert.HasDelegate)
        {
            await OnBudgetAlert.InvokeAsync(LatestBudgetAlert);
        }
        
        StateHasChanged();
    }

    [JSInvokable]
    public Task OnConnectionStatusChanged(bool connected)
    {
        IsConnected = connected;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ShowToast()
    {
        IsVisible = true;
        _hideTimer?.Stop();
        _hideTimer = new System.Timers.Timer(5000);
        _hideTimer.Elapsed += (s, e) => 
        {
            IsVisible = false;
            InvokeAsync(StateHasChanged);
        };
        _hideTimer.Start();
    }

    private void DismissBudgetAlert()
    {
        ShowBudgetAlert = false;
    }

    private string GetProgressBarClass(decimal percentage)
    {
        return percentage switch
        {
            >= 90 => "bg-danger",
            >= 80 => "bg-warning",
            _ => "bg-success"
        };
    }

    private string GetAlertClass(string severity)
    {
        return severity switch
        {
            "critical" => "danger",
            "warning" => "warning",
            _ => "info"
        };
    }

    public async ValueTask DisposeAsync()
    {
        _hideTimer?.Dispose();
        
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("disconnect");
            await _jsModule.DisposeAsync();
        }
        
        _dotNetRef?.Dispose();
    }

    public record SpendUpdate(
        DateTime Timestamp,
        decimal NewSpend,
        decimal TotalSpend,
        decimal? Budget,
        decimal? BudgetPercentage);

    public record BudgetAlert(
        DateTime Timestamp,
        decimal Percentage,
        decimal Remaining,
        string Severity);
}
```

### 2. Integration in VirtualKeyDashboard

```razor
@* Add to VirtualKeyDashboard.razor *@
<SpendNotificationListener 
    VirtualKey="@VirtualKey"
    OnSpendUpdate="HandleSpendUpdate"
    OnBudgetAlert="HandleBudgetAlert" />

@code {
    private async Task HandleSpendUpdate(SpendNotificationListener.SpendUpdate update)
    {
        // Update local state with real-time data
        if (selectedKey != null)
        {
            selectedKey.CurrentSpend = update.TotalSpend;
            // Refresh UI
            await LoadVirtualKeyDetails(selectedKey.Id);
        }
    }

    private async Task HandleBudgetAlert(SpendNotificationListener.BudgetAlert alert)
    {
        // Show toast or modal for budget alerts
        toastService.ShowWarning($"Budget Alert: {alert.Percentage:F1}% used!");
    }
}
```

### 3. Configuration Updates

#### Add to Program.cs

```csharp
// Register SignalR hub
builder.Services.AddSignalR(options =>
{
    // ... existing configuration
}).AddHub<SpendNotificationHub>("/hubs/spend-notifications");

// Register notification service
builder.Services.AddScoped<ISpendNotificationService, SpendNotificationService>();

// Map hub endpoint
app.MapHub<SpendNotificationHub>("/hubs/spend-notifications")
   .RequireAuthorization("VirtualKeySignalR");
```

#### Add to App.razor

```html
<!-- Add script reference -->
<script src="js/spend-notifications.js?v=@FileVersionService.GetFileVersion("js/spend-notifications.js")"></script>
```

## Testing the Implementation

### 1. Unit Test Example

```csharp
[Test]
public async Task SpendNotificationService_Should_Send_Budget_Alert_At_80_Percent()
{
    // Arrange
    var mockHubContext = new Mock<IHubContext<SpendNotificationHub>>();
    var mockClients = new Mock<IHubClients>();
    var mockGroup = new Mock<IClientProxy>();
    
    mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
    mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockGroup.Object);
    
    var service = new SpendNotificationService(mockHubContext.Object, Mock.Of<ILogger<SpendNotificationService>>());
    
    // Act
    await service.NotifySpendUpdate("test-key", 10m, 80m, 100m);
    
    // Assert
    mockGroup.Verify(x => x.SendAsync(
        "BudgetAlert",
        It.Is<object>(o => o.GetType().GetProperty("percentage").GetValue(o).Equals(80m)),
        default), 
        Times.Once);
}
```

### 2. Integration Test Example

```csharp
[Test]
public async Task SpendUpdate_Should_Trigger_SignalR_Notification()
{
    // Arrange
    var connection = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/spend-notifications", options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(_testVirtualKey);
        })
        .Build();
    
    var tcs = new TaskCompletionSource<SpendUpdate>();
    connection.On<SpendUpdate>("SpendUpdate", update => tcs.SetResult(update));
    
    await connection.StartAsync();
    
    // Act - Trigger spend update through API
    var response = await _httpClient.PostAsync("/v1/chat/completions", 
        new StringContent(JsonSerializer.Serialize(new { /* request */ })));
    
    // Assert
    var notification = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.That(notification.NewSpend, Is.GreaterThan(0));
}
```

## Best Practices Summary

1. **Always validate task/resource ownership** in hub methods
2. **Use groups for virtual key isolation** to prevent data leakage
3. **Implement automatic reconnection** in JavaScript clients
4. **Add connection status indicators** in UI
5. **Log all SignalR events** for debugging
6. **Use structured event data** (not just strings)
7. **Handle graceful degradation** when SignalR is unavailable
8. **Test with multiple concurrent connections**
9. **Monitor hub performance metrics**
10. **Document all hub methods and events**

## Next Steps

1. Review the comprehensive implementation plan
2. Choose specific features to implement
3. Follow the examples in this guide
4. Test thoroughly with multiple clients
5. Monitor performance and user feedback