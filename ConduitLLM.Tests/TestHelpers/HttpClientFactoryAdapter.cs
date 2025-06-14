using System;
using System.Net.Http;

using Moq;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Adapter that creates an IHttpClientFactory mock that returns a specified HttpClient instance.
    /// This helps test classes that were previously directly using HttpClient but now require IHttpClientFactory.
    /// </summary>
    public static class HttpClientFactoryAdapter
    {
        /// <summary>
        /// Creates a mock IHttpClientFactory that returns the specified HttpClient.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to return from the factory.</param>
        /// <returns>A mock IHttpClientFactory that returns the specified HttpClient.</returns>
        public static IHttpClientFactory CreateFactory(HttpClient httpClient)
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            return mockFactory.Object;
        }

        /// <summary>
        /// Adapts a test that previously directly passed an HttpClient to now use an IHttpClientFactory parameter.
        /// </summary>
        /// <param name="httpClient">The HttpClient to be wrapped in a factory.</param>
        /// <returns>An IHttpClientFactory that returns the specified HttpClient.</returns>
        public static IHttpClientFactory AdaptHttpClient(HttpClient httpClient)
        {
            return CreateFactory(httpClient);
        }
    }
}
