using System.Threading.Tasks;

namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Interface for handling domain events
    /// </summary>
    /// <typeparam name="TEvent">The type of event to handle</typeparam>
    public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        /// <summary>
        /// Handles the specified domain event
        /// </summary>
        /// <param name="domainEvent">The domain event to handle</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task Handle(TEvent domainEvent);
    }
}