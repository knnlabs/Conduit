using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Redis-based implementation of batch operation idempotency tracking.
    /// Uses SHA256 hashing to generate deterministic tokens from operation parameters.
    /// </summary>
    public class BatchOperationIdempotencyService : IBatchOperationIdempotencyService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<BatchOperationIdempotencyService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string KeyPrefix = "batch:idempotency:";
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

        public BatchOperationIdempotencyService(
            IConnectionMultiplexer redis,
            ILogger<BatchOperationIdempotencyService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Checks if an operation with the given token has already been processed
        /// </summary>
        public async Task<bool> IsOperationProcessedAsync(
            string idempotencyToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyToken))
            {
                throw new ArgumentException("Idempotency token cannot be null or empty", nameof(idempotencyToken));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetRedisKey(idempotencyToken);
                var exists = await db.KeyExistsAsync(key);

                if (exists)
                {
                    _logger.LogInformation(
                        "Idempotency check: Operation {Token} already processed",
                        idempotencyToken);
                }

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to check idempotency for token {Token}",
                    idempotencyToken);

                // Fail open - allow operation to proceed if Redis is unavailable
                return false;
            }
        }

        /// <summary>
        /// Stores the result of a batch operation for idempotency checking
        /// </summary>
        public async Task StoreOperationResultAsync(
            string idempotencyToken,
            object result,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyToken))
            {
                throw new ArgumentException("Idempotency token cannot be null or empty", nameof(idempotencyToken));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetRedisKey(idempotencyToken);
                var serialized = JsonSerializer.Serialize(result, _jsonOptions);
                var expiry = ttl ?? DefaultTtl;

                await db.StringSetAsync(key, serialized, expiry, false, When.Always, CommandFlags.None);

                _logger.LogInformation(
                    "Stored idempotency result for token {Token} with TTL {TTL}",
                    idempotencyToken,
                    expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to store idempotency result for token {Token}",
                    idempotencyToken);

                // Don't throw - failure to cache should not break the operation
            }
        }

        /// <summary>
        /// Retrieves the cached result of a previously processed operation
        /// </summary>
        public async Task<T?> GetOperationResultAsync<T>(
            string idempotencyToken,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(idempotencyToken))
            {
                throw new ArgumentException("Idempotency token cannot be null or empty", nameof(idempotencyToken));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetRedisKey(idempotencyToken);
                var cached = await db.StringGetAsync(key);

                if (!cached.HasValue)
                {
                    return null;
                }

                var result = JsonSerializer.Deserialize<T>(cached.ToString(), _jsonOptions);

                _logger.LogInformation(
                    "Retrieved cached result for idempotency token {Token}",
                    idempotencyToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve cached result for token {Token}",
                    idempotencyToken);

                return null;
            }
        }

        /// <summary>
        /// Generates a unique idempotency token based on operation parameters.
        /// Uses SHA256 to create a deterministic hash from the input parameters.
        /// </summary>
        public string GenerateToken(string operationType, params object[] parameters)
        {
            if (string.IsNullOrWhiteSpace(operationType))
            {
                throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));
            }

            try
            {
                // Combine operation type with serialized parameters
                var components = new List<string> { operationType };

                foreach (var param in parameters)
                {
                    if (param != null)
                    {
                        var serialized = JsonSerializer.Serialize(param, _jsonOptions);
                        components.Add(serialized);
                    }
                }

                var combined = string.Join("|", components);
                var bytes = Encoding.UTF8.GetBytes(combined);
                var hash = SHA256.HashData(bytes);
                var token = Convert.ToBase64String(hash)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');

                _logger.LogDebug(
                    "Generated idempotency token {Token} for operation {OperationType}",
                    token,
                    operationType);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate idempotency token for operation {OperationType}",
                    operationType);

                // Return a random token as fallback
                return $"{operationType}:{Guid.NewGuid():N}";
            }
        }

        /// <summary>
        /// Removes the cached result for an idempotency token
        /// </summary>
        public async Task InvalidateTokenAsync(
            string idempotencyToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyToken))
            {
                throw new ArgumentException("Idempotency token cannot be null or empty", nameof(idempotencyToken));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetRedisKey(idempotencyToken);
                var deleted = await db.KeyDeleteAsync(key);

                if (deleted)
                {
                    _logger.LogInformation(
                        "Invalidated idempotency token {Token}",
                        idempotencyToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate idempotency token {Token}",
                    idempotencyToken);
            }
        }

        private static string GetRedisKey(string idempotencyToken)
        {
            return $"{KeyPrefix}{idempotencyToken}";
        }
    }
}
