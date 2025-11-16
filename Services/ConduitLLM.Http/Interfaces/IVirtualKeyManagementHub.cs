using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Interface for the VirtualKeyManagementHub that provides real-time virtual key management updates.
    /// </summary>
    public interface IVirtualKeyManagementHub
    {
        /// <summary>
        /// Notifies when a new virtual key is created.
        /// </summary>
        /// <param name="notification">The key creation notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task VirtualKeyCreated(VirtualKeyCreatedNotification notification);

        /// <summary>
        /// Notifies when a virtual key is updated.
        /// </summary>
        /// <param name="notification">The key update notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task VirtualKeyUpdated(VirtualKeyUpdatedNotification notification);

        /// <summary>
        /// Notifies when a virtual key is deleted.
        /// </summary>
        /// <param name="notification">The key deletion notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task VirtualKeyDeleted(VirtualKeyDeletedNotification notification);

        /// <summary>
        /// Notifies when a virtual key's status changes.
        /// </summary>
        /// <param name="notification">The status change notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task VirtualKeyStatusChanged(VirtualKeyStatusChangedNotification notification);

        /// <summary>
        /// Sends the current status of a virtual key.
        /// </summary>
        /// <param name="status">The virtual key status.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task VirtualKeyStatus(VirtualKeyStatusNotification status);

        /// <summary>
        /// Notifies about successful subscription to key management updates.
        /// </summary>
        /// <param name="keyId">The subscribed key ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SubscribedToKeyManagement(int keyId);

        /// <summary>
        /// Notifies about successful unsubscription from key management updates.
        /// </summary>
        /// <param name="keyId">The unsubscribed key ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnsubscribedFromKeyManagement(int keyId);

        /// <summary>
        /// Sends error messages to the client.
        /// </summary>
        /// <param name="error">The error object containing message details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Error(object error);
    }
}