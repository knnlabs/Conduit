using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Middleware
{
    /// <summary>
    /// Base middleware for cleaning up ephemeral keys after request completion.
    /// Subclasses define the context item keys and provide the deletion service via method injection.
    /// </summary>
    public abstract class EphemeralKeyCleanupMiddlewareBase
    {
        private readonly RequestDelegate _next;
        protected readonly ILogger Logger;

        /// <summary>
        /// The HttpContext.Items key that flags whether the key should be deleted (expects bool value).
        /// </summary>
        protected abstract string DeleteFlagKey { get; }

        /// <summary>
        /// The HttpContext.Items key that stores the ephemeral key string to delete.
        /// </summary>
        protected abstract string KeyStorageKey { get; }

        protected EphemeralKeyCleanupMiddlewareBase(RequestDelegate next, ILogger logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the request pipeline and cleans up the ephemeral key afterward using the provided deletion function.
        /// </summary>
        protected async Task InvokeAsync(HttpContext context, Func<string, Task> deleteKeyAsync)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                await CleanupKeyIfNeededAsync(context, deleteKeyAsync);
            }
        }

        private async Task CleanupKeyIfNeededAsync(HttpContext context, Func<string, Task> deleteKeyAsync)
        {
            if (context.Items.TryGetValue(DeleteFlagKey, out var shouldDelete) &&
                shouldDelete is bool delete && delete &&
                context.Items.TryGetValue(KeyStorageKey, out var keyObj) &&
                keyObj is string ephemeralKey)
            {
                try
                {
                    await deleteKeyAsync(ephemeralKey);
                    Logger.LogDebug("Cleaned up ephemeral key after request completion");
                }
                catch (Exception ex)
                {
                    // Best effort - don't let cleanup failures affect the response
                    Logger.LogWarning(ex, "Failed to clean up ephemeral key after request");
                }
            }
        }
    }
}
