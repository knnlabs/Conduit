using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

using ConduitLLM.Configuration.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Base unit tests for ModelController
    /// Constructor tests and shared functionality
    /// Other tests are split into separate files by functionality
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelControllerTests
    {
        private readonly Mock<IModelRepository> _mockRepository;
        private readonly Mock<IAdminModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderRepository> _mockProviderRepository;
        private readonly Mock<IEventBus> _mockPublishEndpoint;
        private readonly Mock<ILogger<ModelController>> _mockLogger;

        public ModelControllerTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockProviderRepository = new Mock<IProviderRepository>();
            _mockPublishEndpoint = new Mock<IEventBus>();
            _mockLogger = new Mock<ILogger<ModelController>>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldCreateController()
        {
            // Act & Assert
            var controller = new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object);
            Assert.NotNull(controller);
        }

        [Fact]
        public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(null!, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullMappingService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, null!, _mockProviderRepository.Object, _mockPublishEndpoint.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullProviderRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, _mockMappingService.Object, null!, _mockPublishEndpoint.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullPublishEndpoint_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockProviderRepository.Object, _mockPublishEndpoint.Object, null!));
        }

        #endregion

        /* Additional test files:
         * - ModelControllerTests.GetOperations.cs - GET operations (GetAll, GetById, Search, GetIdentifiers)
         * - ModelControllerTests.ProviderOperations.cs - Provider-related operations (GetModelsByProvider)
         * - ModelControllerTests.CrudOperations.cs - CREATE, UPDATE, DELETE operations
         */
    }
}