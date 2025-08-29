using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Repositories;

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
        private readonly Mock<ILogger<ModelController>> _mockLogger;

        public ModelControllerTests()
        {
            _mockRepository = new Mock<IModelRepository>();
            _mockMappingService = new Mock<IAdminModelProviderMappingService>();
            _mockLogger = new Mock<ILogger<ModelController>>();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldCreateController()
        {
            // Act & Assert
            var controller = new ModelController(_mockRepository.Object, _mockMappingService.Object, _mockLogger.Object);
            Assert.NotNull(controller);
        }

        [Fact]
        public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(null!, _mockMappingService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullMappingService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelController(_mockRepository.Object, _mockMappingService.Object, null!));
        }

        #endregion

        /* Additional test files:
         * - ModelControllerTests.GetOperations.cs - GET operations (GetAll, GetById, Search, GetIdentifiers)
         * - ModelControllerTests.ProviderOperations.cs - Provider-related operations (GetModelsByProvider)
         * - ModelControllerTests.CrudOperations.cs - CREATE, UPDATE, DELETE operations
         */
    }
}