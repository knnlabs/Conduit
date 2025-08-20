using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Consumes ModelMappingChanged events and pushes real-time updates through SignalR
    /// </summary>
    public class ModelMappingChangedNotificationConsumer : IConsumer<ModelMappingChanged>
    {
        private readonly INavigationStateNotificationService _notificationService;
        private readonly ILogger<ModelMappingChangedNotificationConsumer> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelMappingChangedNotificationConsumer
        /// </summary>
        /// <param name="notificationService">Navigation state notification service</param>
        /// <param name="logger">Logger instance</param>
        public ModelMappingChangedNotificationConsumer(
            INavigationStateNotificationService notificationService,
            ILogger<ModelMappingChangedNotificationConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events by pushing updates through SignalR
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                await _notificationService.NotifyModelMappingChangedAsync(
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType);
                
                _logger.LogInformation(
                    "Pushed real-time update for model mapping change: {ModelAlias} ({ChangeType})",
                    @event.ModelAlias,
                    @event.ChangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to push real-time update for model mapping change: {ModelAlias}", 
                    @event.ModelAlias);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }


}