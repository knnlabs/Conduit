using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class MediaControllerTests
    {
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<MediaController>> _mockLogger;
        private readonly MediaController _controller;

        public MediaControllerTests()
        {
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = new Mock<ILogger<MediaController>>();
            _controller = new MediaController(_mockStorageService.Object, _mockLogger.Object);
        }
    }
}