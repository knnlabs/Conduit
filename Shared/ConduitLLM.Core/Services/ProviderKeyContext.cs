using System;
using System.Threading;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides ambient context for provider key information during HTTP requests
    /// </summary>
    public static class ProviderKeyContext
    {
        private static readonly AsyncLocal<ProviderKeyInfo?> _current = new();

        /// <summary>
        /// Gets the current provider key information
        /// </summary>
        public static ProviderKeyInfo? Current
        {
            get => _current.Value;
            private set => _current.Value = value;
        }

        /// <summary>
        /// Sets the provider key context for the current async flow
        /// </summary>
        public static IDisposable Set(int keyId, int providerId)
        {
            var previous = Current;
            Current = new ProviderKeyInfo(keyId, providerId);
            return new ContextScope(previous);
        }

        /// <summary>
        /// Clears the current context
        /// </summary>
        public static void Clear()
        {
            Current = null;
        }

        private class ContextScope : IDisposable
        {
            private readonly ProviderKeyInfo? _previous;

            public ContextScope(ProviderKeyInfo? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                Current = _previous;
            }
        }
    }

    /// <summary>
    /// Information about the provider key being used
    /// </summary>
    public class ProviderKeyInfo
    {
        /// <summary>
        /// The key credential ID
        /// </summary>
        public int KeyId { get; }

        /// <summary>
        /// The provider ID
        /// </summary>
        public int ProviderId { get; }

        /// <summary>
        /// When the context was created
        /// </summary>
        public DateTime CreatedAt { get; }

        public ProviderKeyInfo(int keyId, int providerId)
        {
            KeyId = keyId;
            ProviderId = providerId;
            CreatedAt = DateTime.UtcNow;
        }
    }
}