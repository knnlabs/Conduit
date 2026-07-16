using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Configuration.Messaging
{
    /// <summary>
    /// The messaging backend hosting the <see cref="IEventBus"/> / <see cref="IEventHandler{TEvent}"/>
    /// abstraction (epic #909). Selected at startup by <c>ConduitLLM:Messaging:Backend</c>;
    /// both backends bind the same contracts and consume the same
    /// <see cref="EndpointPolicy"/> descriptors, so domain code is backend-agnostic.
    /// </summary>
    public enum MessagingBackend
    {
        /// <summary>MassTransit over RabbitMQ (multi-instance) or in-memory (single-instance). The Phase 1 default.</summary>
        MassTransit,

        /// <summary>Wolverine on the PostgreSQL transport with transactional durability (Phase 2).</summary>
        Wolverine
    }

    /// <summary>
    /// Resolves the active <see cref="MessagingBackend"/> from configuration.
    /// </summary>
    public static class MessagingBackendResolver
    {
        /// <summary>Configuration key selecting the backend. Absent/empty means <see cref="MessagingBackend.MassTransit"/>.</summary>
        public const string ConfigurationKey = "ConduitLLM:Messaging:Backend";

        /// <summary>
        /// Reads <see cref="ConfigurationKey"/> and returns the selected backend.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The value is present but not a recognized backend name — misconfiguring the
        /// cutover flag must fail the boot loudly rather than silently fall back.
        /// </exception>
        public static MessagingBackend Resolve(IConfiguration configuration)
        {
            var value = configuration[ConfigurationKey];
            if (string.IsNullOrWhiteSpace(value))
            {
                return MessagingBackend.MassTransit;
            }

            if (Enum.TryParse<MessagingBackend>(value.Trim(), ignoreCase: true, out var backend))
            {
                return backend;
            }

            throw new InvalidOperationException(
                $"Unrecognized messaging backend '{value}' in '{ConfigurationKey}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<MessagingBackend>())}.");
        }
    }
}
