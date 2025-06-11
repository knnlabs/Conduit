using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Hybrid implementation of real-time session storage using in-memory cache and Redis.
    /// </summary>
    public class RealtimeSessionStore : IRealtimeSessionStore
    {
        private readonly ILogger<RealtimeSessionStore> _logger;
        private readonly ICacheService _cacheService;
        private readonly ConcurrentDictionary<string, RealtimeSession> _localCache = new();
        private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(2);
        private readonly string _keyPrefix = "realtime:session:";
        private readonly string _indexPrefix = "realtime:index:";

        /// <summary>
        /// Initializes a new instance of the <see cref="RealtimeSessionStore"/> class.
        /// </summary>
        public RealtimeSessionStore(
            ILogger<RealtimeSessionStore> logger,
            ICacheService cacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <inheritdoc />
        public async Task StoreSessionAsync(
            RealtimeSession session,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveTtl = ttl ?? _defaultTtl;
            var key = GetSessionKey(session.Id);

            // Store in local cache
            _localCache[session.Id] = session;

            // Store in distributed cache
            _cacheService.Set(key, session, effectiveTtl);

            // Update indices
            await UpdateIndicesAsync(session, effectiveTtl, cancellationToken);
            _logger.LogDebug("Stored session {SessionId} with TTL {TTL}", session.Id, effectiveTtl);
        }

        /// <inheritdoc />
        public async Task<RealtimeSession?> GetSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            // Check local cache first
            if (_localCache.TryGetValue(sessionId, out var session))
            {
                return session;
            }

            // Check distributed cache
            var key = GetSessionKey(sessionId);
            session = _cacheService.Get<RealtimeSession>(key);

            if (session != null)
            {
                // Populate local cache
                _localCache[sessionId] = session;
            }

            return await Task.FromResult(session);
        }

        /// <inheritdoc />
        public async Task UpdateSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetSessionAsync(session.Id, cancellationToken);
            if (existing == null)
            {
                _logger.LogWarning("Attempted to update non-existent session {SessionId}", session.Id);
                return;
            }

            // Calculate remaining TTL
            var age = DateTime.UtcNow - existing.CreatedAt;
            var remainingTtl = _defaultTtl - age;

            if (remainingTtl > TimeSpan.Zero)
            {
                await StoreSessionAsync(session, remainingTtl, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            // Remove from local cache
            _localCache.TryRemove(sessionId, out _);

            // Get session for cleanup
            var key = GetSessionKey(sessionId);
            var session = _cacheService.Get<RealtimeSession>(key);

            if (session != null)
            {
                // Remove from indices
                await RemoveFromIndicesAsync(session, cancellationToken);
            }

            // Remove from distributed cache
            _cacheService.Remove(key);
            _logger.LogDebug("Removed session {SessionId}", sessionId);

            return true;
        }

        /// <inheritdoc />
        public async Task<List<RealtimeSession>> GetActiveSessionsAsync(
            CancellationToken cancellationToken = default)
        {
            var sessions = new List<RealtimeSession>();
            var indexKey = $"{_indexPrefix}active";

            // Get session IDs from index
            var sessionIds = _cacheService.Get<List<string>>(indexKey) ?? new List<string>();

            foreach (var sessionId in sessionIds)
            {
                var session = await GetSessionAsync(sessionId, cancellationToken);
                if (session != null && session.State != SessionState.Closed)
                {
                    sessions.Add(session);
                }
            }

            return sessions.OrderByDescending(s => s.CreatedAt).ToList();
        }

        /// <inheritdoc />
        public async Task<List<RealtimeSession>> GetSessionsByVirtualKeyAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var sessions = new List<RealtimeSession>();
            var indexKey = $"{_indexPrefix}vkey:{virtualKey}";

            // Get session IDs from index
            var sessionIds = _cacheService.Get<List<string>>(indexKey) ?? new List<string>();

            foreach (var sessionId in sessionIds)
            {
                var session = await GetSessionAsync(sessionId, cancellationToken);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }

            return sessions.OrderByDescending(s => s.CreatedAt).ToList();
        }

        /// <inheritdoc />
        public async Task UpdateSessionMetricsAsync(
            string sessionId,
            SessionStatistics metrics,
            CancellationToken cancellationToken = default)
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                _logger.LogWarning("Cannot update metrics for non-existent session {SessionId}", sessionId);
                return;
            }

            session.Statistics = metrics;
            await UpdateSessionAsync(session, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> CleanupExpiredSessionsAsync(
            TimeSpan maxAge,
            CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var cleaned = 0;

            // Get all active sessions
            var sessions = await GetActiveSessionsAsync(cancellationToken);

            foreach (var session in sessions)
            {
                if (session.CreatedAt < cutoff || session.State == SessionState.Closed)
                {
                    if (await RemoveSessionAsync(session.Id, cancellationToken))
                    {
                        cleaned++;
                    }
                }
            }

            if (cleaned > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", cleaned);
            }

            return cleaned;
        }

        private string GetSessionKey(string sessionId) => $"{_keyPrefix}{sessionId}";

        private async Task UpdateIndicesAsync(
            RealtimeSession session,
            TimeSpan ttl,
            CancellationToken cancellationToken)
        {
            // Update active sessions index
            var activeKey = $"{_indexPrefix}active";
            var activeList = _cacheService.Get<List<string>>(activeKey) ?? new List<string>();

            if (!activeList.Contains(session.Id))
            {
                activeList.Add(session.Id);
                _cacheService.Set(activeKey, activeList, ttl);
            }

            // Update virtual key index if available
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString();
            if (!string.IsNullOrEmpty(virtualKey))
            {
                var vkeyKey = $"{_indexPrefix}vkey:{virtualKey}";
                var vkeyList = _cacheService.Get<List<string>>(vkeyKey) ?? new List<string>();

                if (!vkeyList.Contains(session.Id))
                {
                    vkeyList.Add(session.Id);
                    _cacheService.Set(vkeyKey, vkeyList, ttl);
                }
            }

            await Task.CompletedTask;
        }

        private async Task RemoveFromIndicesAsync(
            RealtimeSession session,
            CancellationToken cancellationToken)
        {
            // Remove from active sessions index
            var activeKey = $"{_indexPrefix}active";
            var activeList = _cacheService.Get<List<string>>(activeKey) ?? new List<string>();
            activeList.Remove(session.Id);

            if (activeList.Count > 0)
            {
                _cacheService.Set(activeKey, activeList, _defaultTtl);
            }
            else
            {
                _cacheService.Remove(activeKey);
            }

            // Remove from virtual key index
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString();
            if (!string.IsNullOrEmpty(virtualKey))
            {
                var vkeyKey = $"{_indexPrefix}vkey:{virtualKey}";
                var vkeyList = _cacheService.Get<List<string>>(vkeyKey) ?? new List<string>();
                vkeyList.Remove(session.Id);

                if (vkeyList.Count > 0)
                {
                    _cacheService.Set(vkeyKey, vkeyList, _defaultTtl);
                }
                else
                {
                    _cacheService.Remove(vkeyKey);
                }
            }

            await Task.CompletedTask;
        }
    }
}
