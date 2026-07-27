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

        return app;
    }
}
