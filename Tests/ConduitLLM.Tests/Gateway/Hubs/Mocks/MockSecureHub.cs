using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Gateway.Hubs;

namespace ConduitLLM.Tests.Gateway.Hubs.Mocks
{
    /// <summary>
    /// Concrete implementation of SecureHub for testing purposes.
    /// Exposes protected methods via public wrappers to enable testing of the abstract base class.
    /// </summary>
    public class MockSecureHub : SecureHub
    {
        public MockSecureHub(ILogger<MockSecureHub> logger, IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        /// <summary>
        /// Returns the hub name for logging purposes.
        /// </summary>
        protected override string GetHubName() => "MockSecureHub";

        // Expose protected methods for testing

        /// <summary>
        /// Gets the virtual key ID from the connection context.
        /// Exposes the protected GetVirtualKeyId method.
        /// </summary>
        public new int? GetVirtualKeyId() => base.GetVirtualKeyId();

        /// <summary>
        /// Gets the virtual key name from the connection context.
        /// Exposes the protected GetVirtualKeyName method.
        /// </summary>
        public new string GetVirtualKeyName() => base.GetVirtualKeyName();

        /// <summary>
        /// Ensures the current connection has a valid virtual key ID.
        /// Exposes the protected RequireVirtualKeyId method.
        /// </summary>
        public new int RequireVirtualKeyId() => base.RequireVirtualKeyId();

        /// <summary>
        /// Verifies if the current virtual key can access a specific task.
        /// Exposes the protected CanAccessTaskAsync method.
        /// </summary>
        public new Task<bool> CanAccessTaskAsync(string taskId) => base.CanAccessTaskAsync(taskId);

        /// <summary>
        /// Checks if the current virtual key has admin privileges.
        /// Exposes the protected IsAdminAsync method.
        /// </summary>
        public new Task<bool> IsAdminAsync() => base.IsAdminAsync();

        /// <summary>
        /// Gets the authenticated virtual key entity.
        /// Exposes the protected GetVirtualKeyAsync method.
        /// </summary>
        public new Task<VirtualKey?> GetVirtualKeyAsync(int virtualKeyId) => base.GetVirtualKeyAsync(virtualKeyId);

        /// <summary>
        /// Gets or creates a correlation ID for the current connection.
        /// Exposes the protected GetOrCreateCorrelationId method.
        /// </summary>
        public new string GetOrCreateCorrelationId() => base.GetOrCreateCorrelationId();

        /// <summary>
        /// Adds the current connection to the virtual key's group.
        /// Exposes the protected AddToVirtualKeyGroupAsync method.
        /// </summary>
        public new Task AddToVirtualKeyGroupAsync() => base.AddToVirtualKeyGroupAsync();

        /// <summary>
        /// Gets the virtual key ID as a string property.
        /// Exposes the protected VirtualKeyId property.
        /// </summary>
        public new string? VirtualKeyId => base.VirtualKeyId;

        /// <summary>
        /// Exposes the Logger for testing purposes.
        /// </summary>
        public ILogger ExposedLogger => Logger;
    }
}
