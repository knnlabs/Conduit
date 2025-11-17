using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Middleware
{
    /// <summary>
    /// Middleware to clean up ephemeral master keys after request completion
    /// </summary>
    public class EphemeralMasterKeyCleanupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<EphemeralMasterKeyCleanupMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EphemeralMasterKeyCleanupMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">The logger</param>
        public EphemeralMasterKeyCleanupMiddleware(
            RequestDelegate next,
            ILogger<EphemeralMasterKeyCleanupMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Processes the HTTP request and cleans up ephemeral master keys after completion
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <param name="ephemeralMasterKeyService">The ephemeral master key service</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InvokeAsync(HttpContext context, IEphemeralMasterKeyService ephemeralMasterKeyService)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                // Clean up ephemeral master key if marked for deletion
                if (context.Items.TryGetValue("DeleteEphemeralMasterKey", out var shouldDelete) &&
                    shouldDelete is bool delete && delete)
                {
                    if (context.Items.TryGetValue("EphemeralMasterKey", out var keyObj) &&
                        keyObj is string ephemeralKey)
                    {
                        try
                        {
                            await ephemeralMasterKeyService.DeleteKeyAsync(ephemeralKey);
                            _logger.LogDebug("Cleaned up ephemeral master key after request completion");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to clean up ephemeral master key");
                            // Don't throw - we don't want cleanup failures to affect the response
                        }
                    }
                }
            }
        }
    }
}