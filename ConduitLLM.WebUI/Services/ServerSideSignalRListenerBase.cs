using System.Text.Json;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Base implementation of IServerSideSignalRListener with default no-op implementations
    /// Components can inherit from this and override only the events they care about
    /// </summary>
    public abstract class ServerSideSignalRListenerBase : IServerSideSignalRListener
    {
        public virtual Task OnConnectionStateChanged(string hubName, ConnectionState state) => Task.CompletedTask;
        public virtual Task OnSystemNotificationReceived(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnModelMappingChanged(JsonElement data) => Task.CompletedTask;
        public virtual Task OnProviderHealthChanged(JsonElement data) => Task.CompletedTask;
        public virtual Task OnNavigationStateChanged(JsonElement data) => Task.CompletedTask;
        public virtual Task OnVideoGenerationProgress(string taskId, JsonElement progress) => Task.CompletedTask;
        public virtual Task OnVideoGenerationCompleted(string taskId, JsonElement result) => Task.CompletedTask;
        public virtual Task OnVideoGenerationFailed(string taskId, string error) => Task.CompletedTask;
        public virtual Task OnImageGenerationProgress(string taskId, JsonElement progress) => Task.CompletedTask;
        public virtual Task OnImageGenerationCompleted(string taskId, JsonElement result) => Task.CompletedTask;
        public virtual Task OnImageGenerationFailed(string taskId, string error) => Task.CompletedTask;
        
        // Spend notification events
        public virtual Task OnSpendUpdate(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnBudgetAlert(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnSpendSummary(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnUnusualSpending(JsonElement notification) => Task.CompletedTask;
        
        // Model discovery events
        public virtual Task OnNewModelsDiscovered(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnModelCapabilitiesChanged(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnModelPricingUpdated(JsonElement notification) => Task.CompletedTask;
        public virtual Task OnModelDeprecated(JsonElement notification) => Task.CompletedTask;
        
        // Batch operation events
        public virtual Task OnBatchOperationProgress(string operationId, JsonElement progress) => Task.CompletedTask;
        public virtual Task OnBatchOperationCompleted(string operationId, JsonElement result) => Task.CompletedTask;
        public virtual Task OnBatchOperationFailed(string operationId, string error) => Task.CompletedTask;
        
        // Admin notification events
        public virtual Task OnAdminNotificationReceived(JsonElement notification) => Task.CompletedTask;
    }
}