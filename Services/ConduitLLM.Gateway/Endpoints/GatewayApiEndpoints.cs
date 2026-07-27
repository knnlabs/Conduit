using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.BatchOperations;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Gateway.Services;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Responses;
using ConduitLLM.Core.Models.Rerank;
using ConduitLLM.Gateway.DTOs;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services.Strategies;
using ConduitLLM.Gateway.RateLimiting;

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
        services.AddScoped<FunctionsEndpoints>();
        services.AddScoped<RerankEndpoints>();
        services.AddScoped<EmbeddingsEndpoints>();
        services.AddScoped<AudioEndpoints>();
        services.AddScoped<MediaEndpoints>();
        services.AddScoped<DownloadsEndpoints>();
        services.AddScoped<ImagesEndpoints>();
        services.AddScoped<Base64MediaProcessor>();
        services.AddScoped<UrlMediaProcessor>();
        services.AddScoped<VideosEndpoints>();
        services.AddScoped<ChatEndpoints>();
        services.AddScoped<IResponsesChatExecutor, ResponsesChatExecutor>();
        services.AddScoped<ResponsesEndpoints>();
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

        var functions = app.MapGroup("/v1/conduit/functions")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Functions");
        functions.MapPost("/execute", ([FromServices] FunctionsEndpoints endpoints, FunctionsEndpoints.FunctionExecutionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken) => endpoints.ExecuteFunction(request, idempotencyKey, cancellationToken))
            .WithName("Functions_Execute").Produces<FunctionExecutionDto>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(404).Produces(500);
        functions.MapGet("/executions/{executionId}", ([FromServices] FunctionsEndpoints endpoints, Guid executionId, CancellationToken cancellationToken) => endpoints.GetExecution(executionId, cancellationToken))
            .WithName("Functions_GetExecution").Produces<FunctionExecutionDto>().Produces<OpenAIErrorResponse>(404).Produces(500);

        app.MapPost("/v1/conduit/rerank", ([FromServices] RerankEndpoints endpoints, RerankRequest request, CancellationToken cancellationToken) => endpoints.CreateRerank(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication").AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Rerank").WithName("Rerank_Create")
            .Produces<RerankResponse>();

        app.MapPost("/v1/embeddings", ([FromServices] EmbeddingsEndpoints endpoints, EmbeddingRequest request, CancellationToken cancellationToken) => endpoints.CreateEmbedding(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication").AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<TokenRateLimitFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>().WithTags("Embeddings").WithName("Embeddings_Create")
            .Produces<EmbeddingResponse>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(500);

        var audio = app.MapGroup("/v1/audio")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Audio");
        audio.MapPost("/transcriptions", ([FromServices] AudioEndpoints endpoints,
                [FromForm] IFormFile file, [FromForm] string model, [FromForm] string? language,
                [FromForm] string? prompt, [FromForm(Name = "response_format")] string? responseFormat,
                [FromForm] double? temperature,
                [FromForm(Name = "chunking_strategy")] string? chunkingStrategy,
                [FromForm] bool? stream,
                CancellationToken cancellationToken) =>
                endpoints.CreateTranscription(
                    file, model, language, prompt, responseFormat, temperature,
                    chunkingStrategy, stream, cancellationToken))
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
            .AllowAnonymous().WithName("Media_Get").Produces<byte[]>(200, "application/octet-stream");
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
            .WithName("Downloads_Get").Produces<byte[]>(200, "application/octet-stream");
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
            .WithName("Videos_GenerateAsync").Produces<AsyncTaskResponse>(202).Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(403).Produces<OpenAIErrorResponse>(429).Produces<OpenAIErrorResponse>(500);
        videos.MapGet("/generations/tasks/{taskId}", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.GetTaskStatus(taskId, cancellationToken))
            .WithName("Videos_GetTaskStatus").Produces<VideoGenerationTaskStatus>().Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(500);
        videos.MapPost("/generations/tasks/{taskId}/retry", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.RetryTask(taskId, cancellationToken))
            .WithName("Videos_RetryTask").Produces<VideoGenerationTaskStatus>().Produces<OpenAIErrorResponse>(400).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(500);
        videos.MapDelete("/generations/{taskId}", ([FromServices] VideosEndpoints endpoints, string taskId, CancellationToken cancellationToken) => endpoints.CancelTask(taskId, cancellationToken))
            .WithName("Videos_CancelTask").Produces(204).Produces<OpenAIErrorResponse>(401).Produces<OpenAIErrorResponse>(404).Produces<OpenAIErrorResponse>(409).Produces<OpenAIErrorResponse>(500);

        app.MapPost("/v1/chat/completions", ([FromServices] ChatEndpoints endpoints, ChatCompletionRequest request, CancellationToken cancellationToken) => endpoints.CreateChatCompletion(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<TokenRateLimitFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Chat").WithName("Chat_CreateCompletion")
            .Produces<ChatCompletionResponse>(200, "application/json")
            .Produces<OpenAIErrorResponse>(400)
            .Produces<OpenAIErrorResponse>(404)
            .Produces<OpenAIErrorResponse>(500);

        app.MapPost("/v1/responses", ([FromServices] ResponsesEndpoints endpoints, CreateResponseRequest request, CancellationToken cancellationToken) => endpoints.CreateResponse(request, cancellationToken))
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<RequireBalanceEndpointFilter>()
            .AddEndpointFilter<TokenRateLimitFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Responses").WithName("Responses_Create")
            .WithSummary("Create a stateless model response")
            .Produces<ResponseObject>(200, "application/json")
            .Produces<OpenAIErrorResponse>(400)
            .Produces<OpenAIErrorResponse>(401)
            .Produces<OpenAIErrorResponse>(402)
            .Produces<OpenAIErrorResponse>(403)
            .Produces<OpenAIErrorResponse>(404)
            .Produces<OpenAIErrorResponse>(429)
            .Produces<OpenAIErrorResponse>(500);

        return app;
    }

    private sealed record RequestSizeLimitMetadata(long? MaxRequestBodySize) : Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata;
}
