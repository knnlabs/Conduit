using ConduitLLM.Core.Middleware;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Middleware that cleans up ephemeral keys after request completion.
    /// </summary>
    public class EphemeralKeyCleanupMiddleware : EphemeralKeyCleanupMiddlewareBase
    {
        protected override string DeleteFlagKey => "DeleteEphemeralKey";
        protected override string KeyStorageKey => "EphemeralKey";

        public EphemeralKeyCleanupMiddleware(
            RequestDelegate next,
            ILogger<EphemeralKeyCleanupMiddleware> logger)
            : base(next, logger)
        {
        }

        public Task InvokeAsync(HttpContext context, IEphemeralKeyService ephemeralKeyService)
            => InvokeAsync(context, ephemeralKeyService.DeleteKeyAsync);
    }
}
