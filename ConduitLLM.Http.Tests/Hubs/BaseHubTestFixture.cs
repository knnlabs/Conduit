using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Http.Authentication;

namespace ConduitLLM.Http.Tests.Hubs
{
    /// <summary>
    /// Base fixture for hub tests that provides common setup for SecureHub-based tests
    /// </summary>
    public abstract class BaseHubTestFixture<THub> where THub : Hub
    {
        protected readonly Mock<HubCallerContext> ContextMock;
        protected readonly Mock<IGroupManager> GroupsMock;
        protected readonly Mock<IHubCallerClients> ClientsMock;
        protected readonly Mock<ISingleClientProxy> CallerMock;
        protected readonly Mock<ISignalRAuthenticationService> AuthServiceMock;
        protected readonly IServiceProvider ServiceProvider;

        protected BaseHubTestFixture()
        {
            ContextMock = new Mock<HubCallerContext>();
            GroupsMock = new Mock<IGroupManager>();
            ClientsMock = new Mock<IHubCallerClients>();
            CallerMock = new Mock<ISingleClientProxy>();
            AuthServiceMock = new Mock<ISignalRAuthenticationService>();

            // Setup clients
            ClientsMock.Setup(x => x.Caller).Returns(CallerMock.Object);

            // Build service provider with auth service
            var services = new ServiceCollection();
            services.AddSingleton(AuthServiceMock.Object);
            AddAdditionalServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Override to add additional services to the service collection
        /// </summary>
        protected virtual void AddAdditionalServices(ServiceCollection services)
        {
        }

        /// <summary>
        /// Creates a hub instance with the test setup
        /// </summary>
        protected THub CreateHub(ILogger<THub> logger)
        {
            var hub = CreateHubInstance(logger);
            hub.Context = ContextMock.Object;
            hub.Groups = GroupsMock.Object;
            hub.Clients = ClientsMock.Object;
            return hub;
        }

        /// <summary>
        /// Override to create the specific hub instance
        /// </summary>
        protected abstract THub CreateHubInstance(ILogger<THub> logger);
    }
}