using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class HybridAudioService
    {
        /// <inheritdoc />
        public Task<string> CreateSessionAsync(
            HybridSessionConfig config,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(config);

            var sessionId = Guid.NewGuid().ToString();
            var session = new HybridSession
            {
                Id = sessionId,
                Config = config,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _sessions[sessionId] = session;
            _logger.LogInformation("Created hybrid audio session: {SessionId}", sessionId);

            return Task.FromResult(sessionId);
        }

        /// <inheritdoc />
        public Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (_sessions.TryRemove(sessionId, out var session))
            {
                _logger.LogInformation("Closed hybrid audio session: {SessionId}", sessionId.Replace(Environment.NewLine, ""));
            }

            return Task.CompletedTask;
        }

        private Task<List<Message>> BuildMessagesAsync(
            string? sessionId,
            string userInput,
            string? systemPrompt)
        {
            var messages = new List<Message>();

            // Add system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new Message
                {
                    Role = "system",
                    Content = systemPrompt
                });
            }
            else if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
            {
                // Use session's system prompt
                if (!string.IsNullOrEmpty(session.Config.SystemPrompt))
                {
                    messages.Add(new Message
                    {
                        Role = "system",
                        Content = session.Config.SystemPrompt
                    });
                }

                // Add conversation history
                foreach (var turn in session.GetRecentTurns())
                {
                    messages.Add(new Message { Role = "user", Content = turn.UserInput });
                    messages.Add(new Message { Role = "assistant", Content = turn.AssistantResponse });
                }
            }

            // Add current user input
            messages.Add(new Message
            {
                Role = "user",
                Content = userInput
            });

            return Task.FromResult(messages);
        }

        private void CleanupExpiredSessions(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredSessions = _sessions
                .Where(kvp => now - kvp.Value.LastActivity > kvp.Value.Config.SessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                if (_sessions.TryRemove(sessionId, out _))
                {
                    _logger.LogDebug("Cleaned up expired session: {SessionId}", sessionId);
                }
            }
        }

        /// <summary>
        /// Disposes of the service and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            _sessionCleanupTimer?.Dispose();
            _sessions.Clear();
        }

        /// <summary>
        /// Represents a hybrid audio conversation session.
        /// </summary>
        private class HybridSession
        {
            public string Id { get; set; } = string.Empty;
            public HybridSessionConfig Config { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime LastActivity { get; set; }
            private readonly Queue<ConversationTurn> _history = new();

            public void AddTurn(string userInput, string assistantResponse)
            {
                _history.Enqueue(new ConversationTurn
                {
                    UserInput = userInput,
                    AssistantResponse = assistantResponse,
                    Timestamp = DateTime.UtcNow
                });

                // Maintain history limit
                while (_history.Count() > Config.MaxHistoryTurns)
                {
                    _history.Dequeue();
                }
            }

            public IEnumerable<ConversationTurn> GetRecentTurns()
            {
                return _history.ToList();
            }
        }

        /// <summary>
        /// Represents a single turn in a conversation.
        /// </summary>
        private class ConversationTurn
        {
            public string UserInput { get; set; } = string.Empty;
            public string AssistantResponse { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}