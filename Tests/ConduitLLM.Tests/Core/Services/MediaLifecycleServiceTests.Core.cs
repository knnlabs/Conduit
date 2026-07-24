using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<ILogger<MediaLifecycleService>> _mockLogger;
        private readonly MediaLifecycleService _service;

        public MediaLifecycleServiceTests()
        {
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockLogger = new Mock<ILogger<MediaLifecycleService>>();
            _mockMediaRepository
                .Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            _service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockLogger.Object);
        }
    }
}
