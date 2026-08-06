using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// What a request is expected to cost against a token window, before the provider is called.
/// </summary>
/// <param name="Model">Model alias the caller asked for.</param>
/// <param name="PromptTokens">Estimated prompt size.</param>
/// <param name="CompletionBudget">Output tokens reserved on the caller's behalf.</param>
public readonly record struct TokenEstimate(string Model, int PromptTokens, int CompletionBudget)
{
    public int Total => PromptTokens + CompletionBudget;
}

/// <summary>
/// Estimates the token cost of a bound request DTO.
/// </summary>
/// <remarks>
/// Runs after model binding, so the messages are already parsed — no body buffering and no
/// second JSON parse. Request shapes with no token semantics (image, video, audio, media)
/// return null and are simply not subject to a token window.
/// </remarks>
public sealed class RequestTokenEstimator
{
    private readonly ITokenCounter _tokenCounter;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RequestTokenEstimator> _logger;

    public RequestTokenEstimator(
        ITokenCounter tokenCounter,
        RateLimitOptions options,
        ILogger<RequestTokenEstimator> logger)
    {
        _tokenCounter = tokenCounter;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Whether this request shape is measured in tokens at all. Image, video and audio requests
    /// are not, so they carry no token window.
    /// </summary>
    public static bool IsTokenBearing(object request) =>
        request is ChatCompletionRequest or EmbeddingRequest;

    /// <summary>
    /// The model alias the caller asked for, used to resolve per-model overrides without
    /// paying for a token estimate.
    /// </summary>
    public static string? TryGetModel(object? request) => request switch
    {
        ChatCompletionRequest chat => chat.Model,
        EmbeddingRequest embedding => embedding.Model,
        _ => null
    };

    /// <summary>
    /// Returns the estimated cost of <paramref name="request"/>, or null when the request has
    /// no token semantics or its size cannot be determined.
    /// </summary>
    public async Task<TokenEstimate?> EstimateAsync(object? request)
    {
        try
        {
            return request switch
            {
                ChatCompletionRequest chat => await EstimateChatAsync(chat),
                EmbeddingRequest embedding => await EstimateEmbeddingAsync(embedding),
                _ => null
            };
        }
        catch (Exception ex)
        {
            // An estimate is not worth failing a request over; fall back to no token window.
            _logger.LogWarning(ex, "Token estimation failed for {RequestType}; skipping the token window for this request",
                request?.GetType().Name ?? "null");
            return null;
        }
    }

    private async Task<TokenEstimate> EstimateChatAsync(ChatCompletionRequest chat)
    {
        // Rate-limit windows are estimate-then-reconcile, so the raw count is used without a
        // fidelity buffer: padding here would only throttle callers earlier than their limit.
        var prompt = (await _tokenCounter.EstimateTokenCountAsync(chat.Model, chat.Messages, chat.Tools)).Tokens;

        // max_completion_tokens supersedes the legacy max_tokens where both are present.
        var declared = chat.MaxCompletionTokens ?? chat.MaxTokens;
        return new TokenEstimate(chat.Model, prompt, CompletionBudget(declared));
    }

    private async Task<TokenEstimate> EstimateEmbeddingAsync(EmbeddingRequest embedding)
    {
        var text = FlattenEmbeddingInput(embedding.Input);
        var prompt = (await _tokenCounter.EstimateTokenCountAsync(embedding.Model, text)).Tokens;

        // Embeddings produce vectors, not tokens, so only the input counts.
        return new TokenEstimate(embedding.Model, prompt, 0);
    }

    private int CompletionBudget(int? declaredMaxTokens)
    {
        // No declared ceiling means the caller could consume the whole window; reserve the
        // configured default instead and let reconciliation correct it.
        var budget = declaredMaxTokens is > 0 ? declaredMaxTokens.Value : _options.DefaultCompletionTokenBudget;
        return Math.Min(budget, _options.MaxCompletionTokenReservation);
    }

    /// <summary>
    /// Embedding input is a string, an array of strings, or an array of token ids. Flattening
    /// to text is enough for an estimate that reconciliation will correct.
    /// </summary>
    private static string FlattenEmbeddingInput(object? input)
    {
        switch (input)
        {
            case null:
                return string.Empty;
            case string single:
                return single;
            case IEnumerable<string> many:
                return string.Join(' ', many);
            case JsonElement { ValueKind: JsonValueKind.String } element:
                return element.GetString() ?? string.Empty;
            case JsonElement { ValueKind: JsonValueKind.Array } array:
                {
                    var builder = new StringBuilder();
                    foreach (var item in array.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            builder.Append(item.GetString()).Append(' ');
                        }
                        else
                        {
                            // Pre-tokenised input: each element already is one token.
                            builder.Append("x ");
                        }
                    }

                    return builder.ToString();
                }
            default:
                return input.ToString() ?? string.Empty;
        }
    }
}
