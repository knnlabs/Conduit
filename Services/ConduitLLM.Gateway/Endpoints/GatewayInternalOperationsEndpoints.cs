using ConduitLLM.Configuration.DTOs.BatchOperations;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>
/// Maps operational endpoints that must never be part of the virtual-key-facing Gateway API.
/// These routes use service-to-service backend authentication and are excluded from OpenAPI.
/// </summary>
public static class GatewayInternalOperationsEndpoints
{
    public static IEndpointRouteBuilder MapGatewayInternalOperationsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var internalApi = app.MapGroup("/internal")
            .RequireAuthorization("AdminOnly")
            .ExcludeFromDescription();

        internalApi.MapGet(
                "/provider-models/{providerId:int}",
                ([FromServices] ProviderModelsEndpoints endpoints, int providerId) =>
                    endpoints.GetProviderModels(providerId))
            .WithName("InternalProviderModels_List");

        var batch = internalApi.MapGroup("/operations/batch");
        batch.MapPost(
                "/spend-updates",
                ([FromServices] BatchOperationsEndpoints endpoints,
                    BatchSpendUpdateRequest request,
                    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey) =>
                    endpoints.StartBatchSpendUpdate(request, idempotencyKey))
            .WithName("InternalBatchOperations_StartSpendUpdates");
        batch.MapPost(
                "/virtual-key-updates",
                ([FromServices] BatchOperationsEndpoints endpoints,
                    BatchVirtualKeyUpdateRequest request) =>
                    endpoints.StartBatchVirtualKeyUpdate(request))
            .WithName("InternalBatchOperations_StartVirtualKeyUpdates");
        batch.MapPost(
                "/webhook-sends",
                ([FromServices] BatchOperationsEndpoints endpoints,
                    BatchWebhookSendRequest request) =>
                    endpoints.StartBatchWebhookSend(request))
            .WithName("InternalBatchOperations_StartWebhookSends");
        batch.MapGet(
                "/{operationId}",
                ([FromServices] BatchOperationsEndpoints endpoints, string operationId) =>
                    endpoints.GetOperationStatus(operationId))
            .WithName("InternalBatchOperations_GetStatus");
        batch.MapPost(
                "/{operationId}/cancel",
                ([FromServices] BatchOperationsEndpoints endpoints, string operationId) =>
                    endpoints.CancelOperation(operationId))
            .WithName("InternalBatchOperations_Cancel");

        var batching = internalApi.MapGroup("/diagnostics/signalr/batching");
        batching.MapGet(
                "/statistics",
                ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.GetStatistics())
            .WithName("InternalSignalRBatching_GetStatistics");
        batching.MapPost(
                "/pause",
                ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.PauseBatching())
            .WithName("InternalSignalRBatching_Pause");
        batching.MapPost(
                "/resume",
                ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.ResumeBatching())
            .WithName("InternalSignalRBatching_Resume");
        batching.MapPost(
                "/flush",
                ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.FlushBatches())
            .WithName("InternalSignalRBatching_Flush");
        batching.MapGet(
                "/efficiency",
                ([FromServices] SignalRBatchingEndpoints endpoints) =>
                    endpoints.GetEfficiencyMetrics())
            .WithName("InternalSignalRBatching_GetEfficiency");

        var health = internalApi.MapGroup("/diagnostics/signalr");
        health.MapGet(
                "/connections",
                ([FromServices] SignalRHealthEndpoints endpoints) =>
                    endpoints.GetConnectionStatistics())
            .WithName("InternalSignalRHealth_GetConnections");
        health.MapGet(
                "/queue",
                ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetQueueStatistics())
            .WithName("InternalSignalRHealth_GetQueue");
        health.MapGet(
                "/connections/details",
                ([FromServices] SignalRHealthEndpoints endpoints) =>
                    endpoints.GetConnectionDetails())
            .WithName("InternalSignalRHealth_GetConnectionDetails");
        health.MapGet(
                "/connections/hub/{hubName}",
                ([FromServices] SignalRHealthEndpoints endpoints, string hubName) =>
                    endpoints.GetHubConnections(hubName))
            .WithName("InternalSignalRHealth_GetHubConnections");
        health.MapGet(
                "/connections/key/{virtualKeyId:int}",
                ([FromServices] SignalRHealthEndpoints endpoints, int virtualKeyId) =>
                    endpoints.GetVirtualKeyConnections(virtualKeyId))
            .WithName("InternalSignalRHealth_GetVirtualKeyConnections");
        health.MapGet(
                "/connections/group/{groupName}",
                ([FromServices] SignalRHealthEndpoints endpoints, string groupName) =>
                    endpoints.GetGroupConnections(groupName))
            .WithName("InternalSignalRHealth_GetGroupConnections");
        health.MapGet(
                "/queue/deadletter",
                ([FromServices] SignalRHealthEndpoints endpoints) =>
                    endpoints.GetDeadLetterMessages())
            .WithName("InternalSignalRHealth_GetDeadLetters");
        health.MapPost(
                "/queue/deadletter/{messageId}/requeue",
                ([FromServices] SignalRHealthEndpoints endpoints, string messageId) =>
                    endpoints.RequeueDeadLetter(messageId))
            .WithName("InternalSignalRHealth_RequeueDeadLetter");
        health.MapGet(
                "",
                ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetHealthStatus())
            .WithName("InternalSignalRHealth_GetHealth");

        return app;
    }
}
