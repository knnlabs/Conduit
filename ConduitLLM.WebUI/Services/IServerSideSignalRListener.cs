using System.Text.Json;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Interface for components that want to receive SignalR events
    /// </summary>
    public interface IServerSideSignalRListener
    {
        Task OnConnectionStateChanged(string hubName, ConnectionState state);
        Task OnSystemNotificationReceived(JsonElement notification);
        Task OnModelMappingChanged(JsonElement data);
        Task OnProviderHealthChanged(JsonElement data);
        Task OnNavigationStateChanged(JsonElement data);
        Task OnVideoGenerationProgress(string taskId, JsonElement progress);
        Task OnVideoGenerationCompleted(string taskId, JsonElement result);
        Task OnVideoGenerationFailed(string taskId, string error);
        Task OnImageGenerationProgress(string taskId, JsonElement progress);
        Task OnImageGenerationCompleted(string taskId, JsonElement result);
        Task OnImageGenerationFailed(string taskId, string error);
        
        // Spend notification events
        Task OnSpendUpdate(JsonElement notification);
        Task OnBudgetAlert(JsonElement notification);
        Task OnSpendSummary(JsonElement notification);
        Task OnUnusualSpending(JsonElement notification);
        
        // Model discovery events
        Task OnNewModelsDiscovered(JsonElement notification);
        Task OnModelCapabilitiesChanged(JsonElement notification);
        Task OnModelPricingUpdated(JsonElement notification);
        Task OnModelDeprecated(JsonElement notification);
        
        // Batch operation events
        Task OnBatchOperationProgress(string operationId, JsonElement progress);
        Task OnBatchOperationCompleted(string operationId, JsonElement result);
        Task OnBatchOperationFailed(string operationId, string error);
        
        // Admin notification events
        Task OnAdminNotificationReceived(JsonElement notification);
    }
}