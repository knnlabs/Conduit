using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Middleware;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Middleware to clean up ephemeral master keys after request completion.
    /// </summary>
    public class EphemeralMasterKeyCleanupMiddleware : EphemeralKeyCleanupMiddlewareBase
    {
        protected override string DeleteFlagKey => "DeleteEphemeralMasterKey";
        protected override string KeyStorageKey => "EphemeralMasterKey";

        /// <inheritdoc />
        public EphemeralMasterKeyCleanupMiddleware(
            RequestDelegate next,
            ILogger<EphemeralMasterKeyCleanupMiddleware> logger)
            : base(next, logger)
        {
        }

        /// <summary>
        /// Processes the HTTP request and cleans up ephemeral master keys after completion.
        /// </summary>
        public Task InvokeAsync(HttpContext context, IEphemeralMasterKeyService ephemeralMasterKeyService)
            => InvokeAsync(context, ephemeralMasterKeyService.DeleteKeyAsync);
    }
}
