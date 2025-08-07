using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for managing task-scoped authentication tokens for SignalR connections
    /// </summary>
    public interface ITaskAuthenticationService
    {
        /// <summary>
        /// Creates a secure token for accessing task updates via SignalR
        /// </summary>
        /// <param name="taskId">The task ID</param>
        /// <param name="virtualKeyId">The virtual key that created the task</param>
        /// <returns>A secure token for SignalR authentication</returns>
        Task<string> CreateTaskTokenAsync(string taskId, int virtualKeyId);

        /// <summary>
        /// Validates a task token
        /// </summary>
        /// <param name="taskId">The task ID</param>
        /// <param name="token">The token to validate</param>
        /// <returns>The virtual key ID if valid, null otherwise</returns>
        Task<int?> ValidateTaskTokenAsync(string taskId, string token);

        /// <summary>
        /// Revokes a task token (typically when task completes)
        /// </summary>
        /// <param name="taskId">The task ID</param>
        Task RevokeTaskTokenAsync(string taskId);
    }

    public class TaskAuthenticationService : ITaskAuthenticationService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<TaskAuthenticationService> _logger;
        private const string TokenPrefix = "task:auth:";
        private const int TokenExpirationHours = 4; // Tokens expire after 4 hours

        public TaskAuthenticationService(
            IDistributedCache cache,
            ILogger<TaskAuthenticationService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> CreateTaskTokenAsync(string taskId, int virtualKeyId)
        {
            if (string.IsNullOrEmpty(taskId))
                throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));

            // Generate a cryptographically secure token
            var token = GenerateSecureToken();

            // Store token metadata in Redis
            var tokenData = new TaskTokenData
            {
                Token = token,
                VirtualKeyId = virtualKeyId,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(TokenExpirationHours)
            };

            var cacheKey = $"{TokenPrefix}{taskId}";
            var serializedData = JsonSerializer.Serialize(tokenData);
            
            await _cache.SetStringAsync(
                cacheKey,
                serializedData,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(TokenExpirationHours)
                });

            _logger.LogDebug("Created task token for task {TaskId} with virtual key {VirtualKeyId}", 
                taskId, virtualKeyId);

            return token;
        }

        public async Task<int?> ValidateTaskTokenAsync(string taskId, string token)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(token))
                return null;

            var cacheKey = $"{TokenPrefix}{taskId}";
            var serializedData = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(serializedData))
            {
                _logger.LogDebug("No token found for task {TaskId}", taskId);
                return null;
            }

            try
            {
                var tokenData = JsonSerializer.Deserialize<TaskTokenData>(serializedData);
                
                if (tokenData == null || tokenData.Token != token)
                {
                    _logger.LogWarning("Invalid token provided for task {TaskId}", taskId);
                    return null;
                }

                if (tokenData.ExpiresAt < DateTimeOffset.UtcNow)
                {
                    _logger.LogDebug("Token expired for task {TaskId}", taskId);
                    await RevokeTaskTokenAsync(taskId); // Clean up expired token
                    return null;
                }

                _logger.LogDebug("Token validated for task {TaskId} with virtual key {VirtualKeyId}", 
                    taskId, tokenData.VirtualKeyId);
                
                return tokenData.VirtualKeyId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token for task {TaskId}", taskId);
                return null;
            }
        }

        public async Task RevokeTaskTokenAsync(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return;

            var cacheKey = $"{TokenPrefix}{taskId}";
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogDebug("Revoked token for task {TaskId}", taskId);
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[32]; // 256 bits
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('='); // URL-safe base64
        }

        private class TaskTokenData
        {
            public string Token { get; set; } = string.Empty;
            public int VirtualKeyId { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }
    }
}