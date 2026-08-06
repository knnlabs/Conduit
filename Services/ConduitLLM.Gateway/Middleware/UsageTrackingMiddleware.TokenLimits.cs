using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.RateLimiting;

namespace ConduitLLM.Gateway.Middleware;

public partial class UsageTrackingMiddleware
{
    /// <summary>
    /// Corrects the request's token reservation to what it actually consumed.
    /// </summary>
    /// <remarks>
    /// Admission reserved an estimate — prompt tokens plus a completion budget — because the
    /// real cost cannot be known before the provider answers. Streaming and non-streaming both
    /// land here once usage is resolved, so the correction happens exactly once per request.
    /// A failure to reconcile is never allowed to affect billing or the response: the estimate
    /// simply stands until the entry ages out of the rolling minute.
    /// </remarks>
    private async Task ReconcileTokenReservationAsync(HttpContext context, Usage usage)
    {
        if (!context.Items.ContainsKey(RateLimitContextKeys.TokenReservation))
        {
            return;
        }

        try
        {
            var service = context.RequestServices.GetService<ITokenRateLimitService>();
            if (service is null)
            {
                return;
            }

            var actual = (usage.PromptTokens ?? 0) + (usage.CompletionTokens ?? 0);
            await service.ReconcileAsync(context, actual);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to reconcile the token rate-limit reservation; the estimate stands until the window slides");
        }
    }
}
