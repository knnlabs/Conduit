namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// Enforces per-key token-per-minute ceilings on the endpoints that consume tokens.
/// </summary>
/// <remarks>
/// <para>
/// This runs as an endpoint filter rather than middleware because a token estimate needs the
/// prompt, and the prompt only exists once the request has been bound. Doing it here costs no
/// body buffering and no second parse. Request-counting windows stay in
/// <c>VirtualKeyRateLimitMiddleware</c>, which can decide without reading the body at all.
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

    public TokenRateLimitFilter(
        ITokenRateLimitService tokenRateLimitService,
        RequestTokenEstimator estimator)
    {
        _tokenRateLimitService = tokenRateLimitService;
        _estimator = estimator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        // No token ceiling on this key: skip estimation entirely rather than tokenising a
        // prompt whose result nobody will use.
        if (http.Items[RateLimitContextKeys.Tpm] is not int tpmLimit || tpmLimit <= 0)
        {
            return await next(context);
        }

        var estimate = await EstimateAsync(context);
        if (estimate is null)
        {
            return await next(context);
        }

        var decision = await _tokenRateLimitService.ReserveAsync(http, estimate.Value.Total);
        if (decision is null)
        {
            return await next(context);
        }

        RateLimitResponse.SetHeaders(http, decision.Limit, decision.Remaining, decision.ResetsAt, decision.Scope);

        if (!decision.IsAllowed)
        {
            return RateLimitResponse.AsResult(
                http,
                decision.Scope,
                decision.Limit,
                decision.ResetsAt,
                $"Token rate limit exceeded ({decision.Limit} tokens per minute). " +
                $"Retry after {RateLimitResponse.RetryAfterSeconds(decision.ResetsAt)} seconds.");
        }

        return await next(context);
    }

    private async Task<TokenEstimate?> EstimateAsync(EndpointFilterInvocationContext context)
    {
        // The bound request DTO is one of the route's arguments; which position varies by route.
        foreach (var argument in context.Arguments)
        {
            var estimate = await _estimator.EstimateAsync(argument);
            if (estimate is not null)
            {
                return estimate;
            }
        }

        return null;
    }
}
