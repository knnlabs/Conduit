using ConduitLLM.Configuration.DTOs.BatchOperations;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Rerank;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Gateway.DTOs;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Registers Gateway endpoint handlers and maps their HTTP contracts.</summary>
public static class GatewayApiEndpoints
{
    public static IServiceCollection AddGatewayEndpointHandlers(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuthEndpoints>();
        services.AddScoped<CompletionsEndpoints>();
        services.AddScoped<ProviderModelsEndpoints>();
        services.AddScoped<DiscoveryEndpoints>();
        services.AddScoped<TasksEndpoints>();
        services.AddScoped<BatchOperationsEndpoints>();
        services.AddScoped<SignalRBatchingEndpoints>();
        services.AddScoped<SignalRHealthEndpoints>();
        services.AddScoped<FunctionsEndpoints>();
        services.AddScoped<RerankEndpoints>();
        services.AddScoped<EmbeddingsEndpoints>();
        services.AddScoped<AudioEndpoints>();
        services.AddScoped<MediaEndpoints>();
        services.AddScoped<DownloadsEndpoints>();
        services.AddScoped<ImagesEndpoints>();
        services.AddScoped<VideosEndpoints>();
        services.AddScoped<ChatEndpoints>();
        return services;
    }

    public static IEndpointRouteBuilder MapGatewayApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/conduit/auth/ephemeral-key", ([FromServices] AuthEndpoints endpoints, GenerateEphemeralKeyRequest? request) => endpoints.GenerateEphemeralKey(request))
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Authentication").WithName("Auth_GenerateEphemeralKey")
            .Produces<EphemeralKeyResponse>(StatusCodes.Status200OK)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status500InternalServerError);

        app.MapPost("/v1/completions", ([FromServices] CompletionsEndpoints endpoints) => endpoints.CreateCompletion())
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Completions").WithName("Completions_Create")
            .Produces<OpenAIErrorResponse>(StatusCodes.Status501NotImplemented)
            .ExcludeFromDescription();

        app.MapGet("/api/provider-models/{providerId:int}", ([FromServices] ProviderModelsEndpoints endpoints, int providerId) => endpoints.GetProviderModels(providerId))
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Provider Models").WithName("ProviderModels_List")
            .Produces<List<string>>(StatusCodes.Status200OK)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        var discovery = app.MapGroup("/v1/conduit/discovery")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Discovery");
        discovery.MapGet("/models", ([FromServices] DiscoveryEndpoints endpoints, string? capability) => endpoints.GetModels(capability))
            .WithName("Discovery_GetModels").Produces<DiscoveryModelsResponse>();
        discovery.MapGet("/capabilities", ([FromServices] DiscoveryEndpoints endpoints) => endpoints.GetCapabilities())
            .WithName("Discovery_GetCapabilities").Produces<DiscoveryCapabilitiesResponse>();
        discovery.MapGet("/models/{model}/parameters", ([FromServices] DiscoveryEndpoints endpoints, string model) => endpoints.GetModelParameters(model))
            .WithName("Discovery_GetModelParameters").Produces<ModelParametersResponse>();
        discovery.MapGet("/functions", ([FromServices] DiscoveryEndpoints endpoints, string? purpose, string? providerType) => endpoints.GetFunctions(purpose, providerType))
            .WithName("Discovery_GetFunctions").Produces<FunctionDiscoveryResponse>();
        discovery.MapGet("/functions/{functionConfigurationId}/parameters", ([FromServices] DiscoveryEndpoints endpoints, int functionConfigurationId) => endpoints.GetFunctionParameters(functionConfigurationId))
            .WithName("Discovery_GetFunctionParameters").Produces<FunctionParametersResponseDto>();

        var tasks = app.MapGroup("/v1/conduit/tasks")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Tasks");
        tasks.MapGet("/{taskId}", ([FromServices] TasksEndpoints endpoints, string taskId) => endpoints.GetTaskStatus(taskId))
            .WithName("Tasks_GetStatus").Produces<AsyncTaskStatus>();
        tasks.MapPost("/{taskId}/cancel", ([FromServices] TasksEndpoints endpoints, string taskId) => endpoints.CancelTask(taskId))
            .WithName("Tasks_Cancel").Produces(StatusCodes.Status204NoContent);
        tasks.MapGet("/{taskId}/poll", ([FromServices] TasksEndpoints endpoints, string taskId, int timeout = 300, int interval = 2) => endpoints.PollTask(taskId, timeout, interval))
            .WithName("Tasks_Poll").Produces<AsyncTaskStatus>();

        var batch = app.MapGroup("/v1/conduit/batch")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Batch Operations");
        batch.MapPost("/spend-updates", ([FromServices] BatchOperationsEndpoints endpoints, BatchSpendUpdateRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey) => endpoints.StartBatchSpendUpdate(request, idempotencyKey))
            .WithName("BatchOperations_StartSpendUpdates").Produces<BatchOperationStartResponse>(202).Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401);
        batch.MapPost("/virtual-key-updates", ([FromServices] BatchOperationsEndpoints endpoints, BatchVirtualKeyUpdateRequest request) => endpoints.StartBatchVirtualKeyUpdate(request))
            .WithName("BatchOperations_StartVirtualKeyUpdates").Produces<BatchOperationStartResponse>(202).Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401);
        batch.MapPost("/webhook-sends", ([FromServices] BatchOperationsEndpoints endpoints, BatchWebhookSendRequest request) => endpoints.StartBatchWebhookSend(request))
            .WithName("BatchOperations_StartWebhookSends").Produces<BatchOperationStartResponse>(202).Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401);
        batch.MapGet("/operations/{operationId}", ([FromServices] BatchOperationsEndpoints endpoints, string operationId) => endpoints.GetOperationStatus(operationId))
            .WithName("BatchOperations_GetStatus").Produces<BatchOperationStatusResponse>().Produces<OpenAIErrorResponse>(404);
        batch.MapPost("/operations/{operationId}/cancel", ([FromServices] BatchOperationsEndpoints endpoints, string operationId) => endpoints.CancelOperation(operationId))
            .WithName("BatchOperations_Cancel").Produces(204).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(409);

        var batching = app.MapGroup("/api/signalr/batching").WithTags("SignalR Batching");
        batching.MapGet("/statistics", ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.GetStatistics())
            .AllowAnonymous().WithName("SignalRBatching_GetStatistics").Produces<BatchingStatistics>();
        batching.MapPost("/pause", ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.PauseBatching())
            .RequireAuthorization("AdminOnly").WithName("SignalRBatching_Pause").Produces<MessageResponse>();
        batching.MapPost("/resume", ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.ResumeBatching())
            .RequireAuthorization("AdminOnly").WithName("SignalRBatching_Resume").Produces<MessageResponse>();
        batching.MapPost("/flush", ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.FlushBatches())
            .RequireAuthorization("AdminOnly").WithName("SignalRBatching_Flush").Produces<MessageResponse>();
        batching.MapGet("/efficiency", ([FromServices] SignalRBatchingEndpoints endpoints) => endpoints.GetEfficiencyMetrics())
            .AllowAnonymous().WithName("SignalRBatching_GetEfficiency").Produces<BatchingEfficiencyResponse>();

        var signalRHealth = app.MapGroup("/health/signalr").WithTags("SignalR Health");
        signalRHealth.MapGet("/connections", ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetConnectionStatistics())
            .AllowAnonymous().WithName("SignalRHealth_GetConnections").Produces<ConnectionStatistics>();
        signalRHealth.MapGet("/queue", ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetQueueStatistics())
            .AllowAnonymous().WithName("SignalRHealth_GetQueue").Produces<QueueStatistics>();
        signalRHealth.MapGet("/connections/details", ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetConnectionDetails())
            .RequireAuthorization("AdminOnly").WithName("SignalRHealth_GetConnectionDetails").Produces<ConnectionDetailsResponse>();
        signalRHealth.MapGet("/connections/hub/{hubName}", ([FromServices] SignalRHealthEndpoints endpoints, string hubName) => endpoints.GetHubConnections(hubName))
            .AllowAnonymous().WithName("SignalRHealth_GetHubConnections").Produces<HubConnectionsResponse>();
        signalRHealth.MapGet("/connections/key/{virtualKeyId}", ([FromServices] SignalRHealthEndpoints endpoints, int virtualKeyId) => endpoints.GetVirtualKeyConnections(virtualKeyId))
            .RequireAuthorization("VirtualKeyAuthentication").WithName("SignalRHealth_GetVirtualKeyConnections").Produces<VirtualKeyConnectionsResponse>();
        signalRHealth.MapGet("/connections/group/{groupName}", ([FromServices] SignalRHealthEndpoints endpoints, string groupName) => endpoints.GetGroupConnections(groupName))
            .AllowAnonymous().WithName("SignalRHealth_GetGroupConnections").Produces<GroupConnectionsResponse>();
        signalRHealth.MapGet("/queue/deadletter", ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetDeadLetterMessages())
            .RequireAuthorization("AdminOnly").WithName("SignalRHealth_GetDeadLetters").Produces<DeadLetterMessagesResponse>();
        signalRHealth.MapPost("/queue/deadletter/{messageId}/requeue", ([FromServices] SignalRHealthEndpoints endpoints, string messageId) => endpoints.RequeueDeadLetter(messageId))
            .RequireAuthorization("AdminOnly").WithName("SignalRHealth_RequeueDeadLetter").Produces<MessageResponse>();
        signalRHealth.MapGet("", ([FromServices] SignalRHealthEndpoints endpoints) => endpoints.GetHealthStatus())
            .AllowAnonymous().WithName("SignalRHealth_GetHealth").Produces<SignalRHealthResponse>();

        var functions = app.MapGroup("/v1/conduit/functions")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Functions");
        functions.MapPost("/execute", ([FromServices] FunctionsEndpoints endpoints, FunctionsEndpoints.FunctionExecutionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) => endpoints.ExecuteFunction(request, idempotencyKey, cancellationToken))
            .WithName("Functions_Execute").Produces<FunctionsEndpoints.FunctionExecutionResponse>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(404).Produces(500);
        functions.MapGet("/executions/{executionId}", ([FromServices] FunctionsEndpoints endpoints, Guid executionId, CancellationToken cancellationToken) => endpoints.GetExecution(executionId, cancellationToken))
            .WithName("Functions_GetExecution").Produces<FunctionsEndpoints.FunctionExecutionResponse>().Produces<OpenAIErrorResponse>(404).Produces(500);

        app.MapPost("/v1/conduit/rerank", ([FromServices] RerankEndpoints endpoints, RerankRequest request, CancellationToken cancellationToken) => endpoints.CreateRerank(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication").AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Rerank").WithName("Rerank_Create")
            .Produces<RerankResponse>();

        app.MapPost("/v1/embeddings", ([FromServices] EmbeddingsEndpoints endpoints, EmbeddingRequest request, CancellationToken cancellationToken) => endpoints.CreateEmbedding(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication").AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Embeddings").WithName("Embeddings_Create")
            .Produces<EmbeddingResponse>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(500);

        var audio = app.MapGroup("/v1/audio")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Audio");
        audio.MapPost("/transcriptions", ([FromServices] AudioEndpoints endpoints,
                [FromForm] IFormFile file, [FromForm] string model, [FromForm] string? language,
                [FromForm] string? prompt, [FromForm(Name = "response_format")] string? responseFormat,
                [FromForm] double? temperature, CancellationToken cancellationToken) =>
                endpoints.CreateTranscription(file, model, language, prompt, responseFormat, temperature, cancellationToken))
            .WithName("Audio_CreateTranscription").DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitMetadata(26_214_400))
            .Produces<AudioTranscriptionResponse>();
        audio.MapPost("/speech", ([FromServices] AudioEndpoints endpoints, TextToSpeechRequest request, CancellationToken cancellationToken) => endpoints.CreateSpeech(request, cancellationToken))
            .WithName("Audio_CreateSpeech").Produces<byte[]>(200, "application/octet-stream");

        var media = app.MapGroup("/v1/conduit/media").WithTags("Media");
        media.MapPost("/upload", ([FromServices] MediaEndpoints endpoints, [FromForm] IFormFile file, [FromForm(Name = "media_type")] string? mediaType) => endpoints.UploadMedia(file, mediaType))
            .RequireAuthorization("VirtualKeyAuthentication").AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithName("Media_Upload").DisableAntiforgery().WithMetadata(new RequestSizeLimitMetadata(524_288_000))
            .Produces<MediaUploadResponse>();
        media.MapGet("/info/{**storageKey}", ([FromServices] MediaEndpoints endpoints, string storageKey) => endpoints.GetMediaInfo(storageKey))
            .RequireAuthorization("VirtualKeyAuthentication").WithName("Media_GetInfo").Produces<MediaInfo>();
        media.MapGet("/{**storageKey}", ([FromServices] MediaEndpoints endpoints, string storageKey) => endpoints.GetMedia(storageKey))
            .AllowAnonymous().WithName("Media_Get").Produces<Stream>(200, "application/octet-stream");
        media.MapMethods("/{**storageKey}", [HttpMethods.Head], ([FromServices] MediaEndpoints endpoints, string storageKey) => endpoints.CheckMediaExists(storageKey))
            .AllowAnonymous().WithName("Media_Head").Produces(200);

        var downloads = app.MapGroup("/v1/conduit/downloads")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Downloads");
        downloads.MapGet("/metadata/{**fileId}", ([FromServices] DownloadsEndpoints endpoints, string fileId) => endpoints.GetFileMetadata(fileId))
            .WithName("Downloads_GetMetadata").Produces<FileMetadataResponse>();
        downloads.MapPost("/generate-url", ([FromServices] DownloadsEndpoints endpoints, GenerateUrlRequest request) => endpoints.GenerateDownloadUrl(request))
            .WithName("Downloads_GenerateUrl").Produces<DownloadUrlResponse>();
        downloads.MapGet("/{**fileId}", ([FromServices] DownloadsEndpoints endpoints, string fileId, bool inline = false) => endpoints.DownloadFile(fileId, inline))
            .WithName("Downloads_Get").Produces<Stream>(200, "application/octet-stream");
        downloads.MapMethods("/{**fileId}", [HttpMethods.Head], ([FromServices] DownloadsEndpoints endpoints, string fileId) => endpoints.CheckFileExists(fileId))
            .WithName("Downloads_Head").Produces(200);

        var images = app.MapGroup("/v1/images")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Images");
        images.MapPost("/generations", ([FromServices] ImagesEndpoints endpoints, ImageGenerationRequest request, CancellationToken cancellationToken) => endpoints.CreateImage(request, cancellationToken))
            .WithName("Images_Create").Produces<ImageGenerationResponse>();
        var conduitImages = app.MapGroup("/v1/conduit/images")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Images");
        conduitImages.MapPost("/generations/async", ([FromServices] ImagesEndpoints endpoints, ImageGenerationRequest request) => endpoints.CreateImageAsync(request))
            .WithName("Images_CreateImageAsync").Produces<AsyncTaskResponse>(StatusCodes.Status202Accepted);
        conduitImages.MapGet("/generations/{taskId}/status", ([FromServices] ImagesEndpoints endpoints, string taskId) => endpoints.GetGenerationStatus(taskId))
            .WithName("Images_GetStatus").Produces<AsyncTaskStatusResponse>();
        conduitImages.MapDelete("/generations/{taskId}", ([FromServices] ImagesEndpoints endpoints, string taskId) => endpoints.CancelGeneration(taskId))
            .WithName("Images_Cancel").Produces<TaskCancellationResponse>();

        var videos = app.MapGroup("/v1/conduit/videos")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Videos");
        videos.MapPost("/generations/async", ([FromServices] VideosEndpoints endpoints, VideoGenerationRequest request, CancellationToken cancellationToken) => endpoints.GenerateVideoAsync(request, cancellationToken))
            .WithName("Videos_GenerateAsync").Produces<VideoGenerationTaskResponse>(202).Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(403).Produces<OpenAIErrorResponse>(429).Produces<OpenAIErrorResponse>(500);
        videos.MapGet("/generations/tasks/{taskId}", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.GetTaskStatus(taskId, cancellationToken))
            .WithName("Videos_GetTaskStatus").Produces<VideoGenerationTaskStatus>().Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(500);
        videos.MapPost("/generations/tasks/{taskId}/retry", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.RetryTask(taskId, cancellationToken))
            .WithName("Videos_RetryTask").Produces<VideoGenerationTaskStatus>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(500);
        videos.MapDelete("/generations/{taskId}", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.CancelTask(taskId, cancellationToken))
            .WithName("Videos_CancelTask").Produces(204).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(409).Produces<OpenAIErrorResponse>(500);

        app.MapPost("/v1/chat/completions", ([FromServices] ChatEndpoints endpoints, ChatCompletionRequest request, CancellationToken cancellationToken) => endpoints.CreateChatCompletion(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Chat").WithName("Chat_CreateCompletion")
            .Produces<ChatCompletionResponse>(200, "application/json")
            .Produces<OpenAIErrorResponse>(400)
            .Produces<OpenAIErrorResponse>(500);

        return app;
    }

    private sealed record RequestSizeLimitMetadata(long? MaxRequestBodySize) : Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata;
}
