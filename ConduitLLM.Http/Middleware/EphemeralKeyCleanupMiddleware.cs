using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware that cleans up ephemeral keys after request completion
    /// </summary>
    public class EphemeralKeyCleanupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<EphemeralKeyCleanupMiddleware> _logger;

        public EphemeralKeyCleanupMiddleware(
            RequestDelegate next,
            ILogger<EphemeralKeyCleanupMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context, IEphemeralKeyService ephemeralKeyService)
        {
            try
            {
                // Process the request
                await _next(context);
            }
            finally
            {
                // After request completes (success or failure), clean up ephemeral key if needed
                if (context.Items.TryGetValue("DeleteEphemeralKey", out var shouldDelete) && 
                    shouldDelete is bool delete && delete)
                {
                    if (context.Items.TryGetValue("EphemeralKey", out var keyObj) && 
                        keyObj is string ephemeralKey)
                    {
                        try
                        {
                            await ephemeralKeyService.DeleteKeyAsync(ephemeralKey);
                            _logger.LogDebug("Deleted ephemeral key after request completion");
                        }
                        catch (Exception ex)
                        {
                            // Log but don't throw - cleanup is best effort
                            _logger.LogWarning(ex, "Failed to delete ephemeral key after request");
                        }
                    }
                }
            }
        }
    }
}