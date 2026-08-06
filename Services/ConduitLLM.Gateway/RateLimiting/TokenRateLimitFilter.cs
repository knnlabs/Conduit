using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// Enforces the rate limits that need the request body: token ceilings and per-model overrides.
/// </summary>
/// <remarks>
/// <para>
/// This runs as an endpoint filter rather than middleware because both need something only the
/// bound request has — a token estimate needs the prompt, and a per-model limit needs the model
/// alias. Doing it here costs no body buffering and no second parse. Request-counting windows
/// stay in <c>VirtualKeyRateLimitMiddleware</c>, which can decide without reading the body.
/// </para>
/// <para>
/// The 429 it produces is byte-identical to the middleware's, so a client cannot tell which
/// layer rejected it — only the <c>X-RateLimit-Scope</c> header distinguishes the reason.
/// </para>
/// </remarks>
public sealed class TokenRateLimitFilter : IEndpointFilter
{
    private readonly ITokenRateLimitService _tokenRateLimitService;
    private readonly RequestTokenEstimator _estimator;
    private readonly IRateLimitFailurePolicy _failurePolicy;

    public TokenRateLimitFilter(
        ITokenRateLimitService tokenRateLimitService,
        RequestTokenEstimator estimator,
        IRateLimitFailurePolicy failurePolicy)
    {
        _tokenRateLimitService = tokenRateLimitService;
        _estimator = estimator;
        _failurePolicy = failurePolicy;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        var request = FindTokenBearingRequest(context);
        if (request is null)
        {
            return await next(context);
        }

        var modelAlias = RequestTokenEstimator.TryGetModel(request);

        // Only tokenise when some token window actually applies. A key limited purely by
        // requests per minute should not pay for an estimate nobody reads.
        var estimatedTokens = 0L;
        if (NeedsTokenEstimate(http, modelAlias))
        {
            var estimate = await _estimator.EstimateAsync(request);
            estimatedTokens = estimate?.Total ?? 0;
        }

        var decision = await _tokenRateLimitService.ReserveAsync(http, modelAlias, estimatedTokens);
        if (decision is null)
        {
            return await next(context);
        }

        if (decision.Degraded &&
            _failurePolicy.ShouldReject(decision.Scope, "the rate limit store was unreachable"))
        {
            return RateLimitResponse.DegradedResult(http, decision.Scope);
        }

        RateLimitResponse.SetHeaders(http, decision.Limit, decision.Remaining, decision.ResetsAt, decision.Scope);

        if (!decision.IsAllowed)
        {
            return RateLimitResponse.AsResult(
                http,
                decision.Scope,
                decision.Limit,
                decision.ResetsAt);
        }

        return await next(context);
    }

    /// <summary>
    /// Whether any configured window charges this request in tokens rather than requests.
    /// </summary>
    private static bool NeedsTokenEstimate(HttpContext http, string? modelAlias)
    {
        if (http.Items[RateLimitContextKeys.Tpm] is int keyTpm && keyTpm > 0)
        {
            return true;
        }

        if (http.Items[RateLimitContextKeys.GroupId] is int &&
            http.Items[RateLimitContextKeys.GroupTpm] is int groupTpm && groupTpm > 0)
        {
            return true;
        }

        var rules = ModelRateLimitPolicy.Parse(http.Items[RateLimitContextKeys.ModelRateLimits] as string);
        return ModelRateLimitPolicy.Resolve(rules, modelAlias)?.Tpm is > 0;
    }

    /// <summary>
    /// Picks the bound request DTO out of the route's arguments; which position it occupies
    /// varies by route.
    /// </summary>
    private static object? FindTokenBearingRequest(EndpointFilterInvocationContext context)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is not null && RequestTokenEstimator.IsTokenBearing(argument))
            {
                return argument;
            }
        }

        return null;
    }
}
