using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Configuration.Messaging
{
    /// <summary>
    /// The messaging backend hosting the <see cref="IEventBus"/> / <see cref="IEventHandler{TEvent}"/>
    /// abstraction (epic #909). Wolverine on the PostgreSQL transport is the only supported
    /// backend as of I3.1 (#932); the former MassTransit/RabbitMQ backend was removed after the
    /// Wolverine cutover (#930) completed its soak. Selected at startup by
    /// <c>ConduitLLM:Messaging:Backend</c>, which now defaults to (and only accepts) Wolverine.
    /// </summary>
    public enum MessagingBackend
    {
        /// <summary>Wolverine on the PostgreSQL transport with transactional durability.</summary>
        Wolverine
    }

    /// <summary>
    /// Resolves the active <see cref="MessagingBackend"/> from configuration.
    /// </summary>
    public static class MessagingBackendResolver
    {
        /// <summary>Configuration key selecting the backend. Absent/empty means <see cref="MessagingBackend.Wolverine"/>.</summary>
        public const string ConfigurationKey = "ConduitLLM:Messaging:Backend";

        /// <summary>
        /// The name of the backend removed in #932. Matched case-insensitively so a stale
        /// rollback configuration (<c>CONDUIT_MESSAGING_BACKEND=MassTransit</c>) fails the boot
        /// with a clear pointer to Wolverine rather than the generic "unrecognized value" error.
        /// </summary>
        private const string RemovedMassTransitBackend = "MassTransit";

        /// <summary>
        /// Reads <see cref="ConfigurationKey"/> and returns the selected backend.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The value names the removed backend, or is otherwise not a recognized backend name —
        /// misconfiguring the flag must fail the boot loudly rather than silently fall back.
        /// </exception>
        public static MessagingBackend Resolve(IConfiguration configuration)
        {
            var value = configuration[ConfigurationKey];
            if (string.IsNullOrWhiteSpace(value))
            {
                return MessagingBackend.Wolverine;
            }

            var trimmed = value.Trim();

            if (trimmed.Equals(RemovedMassTransitBackend, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Messaging backend 'MassTransit' was removed in #932 (epic #909) after the " +
                    $"Wolverine cutover (#930) soaked. Use Wolverine: remove '{ConfigurationKey}' " +
                    "(Wolverine is the default) or set it to 'Wolverine'.");
            }

            if (Enum.TryParse<MessagingBackend>(trimmed, ignoreCase: true, out var backend))
            {
                return backend;
            }

            throw new InvalidOperationException(
                $"Unrecognized messaging backend '{value}' in '{ConfigurationKey}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<MessagingBackend>())}.");
        }
    }
}
