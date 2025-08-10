using System;
using ConduitLLM.Core.Services;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    public partial class RouterServiceTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRouter_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(null!, _repositoryMock.Object, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(_routerMock.Object, null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouterService(_routerMock.Object, _repositoryMock.Object, null!));
        }

        #endregion
    }
}