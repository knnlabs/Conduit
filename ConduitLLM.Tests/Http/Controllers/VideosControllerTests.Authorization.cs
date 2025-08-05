using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class VideosControllerTests
    {
        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void Controller_ShouldHaveRateLimiting()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var rateLimitAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute));

            // Assert
            Assert.NotNull(rateLimitAttribute);
            var attr = (Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute)rateLimitAttribute;
            Assert.Equal("VirtualKeyPolicy", attr.PolicyName);
        }

        #endregion
    }
}