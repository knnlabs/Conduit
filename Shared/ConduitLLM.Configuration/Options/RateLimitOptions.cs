using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Deployment-wide knobs for rate-limit enforcement. Per-key ceilings live on the
    /// VirtualKey entity; these govern how those ceilings are applied.
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "RateLimiting";

        /// <summary>
        /// Completion tokens reserved when a request does not cap its own output.
        /// </summary>
        /// <remarks>
        /// Token limits are enforced by reserving an estimate before the provider is called and
        /// correcting it afterwards. A request that specifies no <c>max_tokens</c> has no
        /// declared ceiling, so without this cap a single call would reserve — and therefore
        /// block — the whole window. The reservation is corrected down to actual usage as soon
        /// as the response is billed, so a generous default costs little.
        /// </remarks>
        [Range(1, 1_000_000)]
        public int DefaultCompletionTokenBudget { get; set; } = 1024;

        /// <summary>
        /// Upper bound on the completion budget a request may reserve by declaring a large
        /// <c>max_tokens</c>. Prevents one request from parking the entire window.
        /// </summary>
        [Range(1, 10_000_000)]
        public int MaxCompletionTokenReservation { get; set; } = 32_768;
    }
}
