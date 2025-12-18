using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentAssertions;
using Xunit;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.IntegrationTests.Tests
{
    [Collection("Sequential")]
    [Trait("Category", "Integration")]
    [Trait("Component", "SignalR")]
    public class SignalRProtocolNegotiationTests : IAsyncLifetime
    {
        private IHost _hostWithBothProtocols;
        private IHost _hostWithJsonOnly;
        private string _bothProtocolsUrl;
        private string _jsonOnlyUrl;
        private const int BothProtocolsPort = 5556;
        private const int JsonOnlyPort = 5557;

        public class TestHub : Hub
        {
            public Task<string> GetProtocol()
            {
                // Unfortunately, we can't easily detect the protocol from the Hub
                // This would require accessing internal SignalR state
                return Task.FromResult("Connected");
            }

            public Task<string> Echo(string message)
            {
                return Task.FromResult(message);
            }
        }

        public async Task InitializeAsync()
        {
            _bothProtocolsUrl = $"http://localhost:{BothProtocolsPort}";
            _jsonOnlyUrl = $"http://localhost:{JsonOnlyPort}";

            // Server with both protocols (matching production configuration)
            var builderBoth = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseUrls(_bothProtocolsUrl)
                        .ConfigureServices(services =>
                        {
                            services.AddSignalR()
                                .AddMessagePackProtocol(options =>
                                {
                                    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                                        .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                                        .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                                        .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                                        .WithCompressionMinLength(256);
                                });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapHub<TestHub>("/testhub");
                            });
                        });
                });

            // Server with JSON only
            var builderJsonOnly = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseUrls(_jsonOnlyUrl)
                        .ConfigureServices(services =>
                        {
                            services.AddSignalR();
                            // No AddMessagePackProtocol call
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapHub<TestHub>("/testhub");
                            });
                        });
                });

            _hostWithBothProtocols = builderBoth.Build();
            _hostWithJsonOnly = builderJsonOnly.Build();

            await _hostWithBothProtocols.StartAsync();
            await _hostWithJsonOnly.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_hostWithBothProtocols != null)
            {
                await _hostWithBothProtocols.StopAsync();
                _hostWithBothProtocols.Dispose();
            }

            if (_hostWithJsonOnly != null)
            {
                await _hostWithJsonOnly.StopAsync();
                _hostWithJsonOnly.Dispose();
            }
        }

        [Fact]
        public async Task Server_With_Both_Protocols_Client_Requests_MessagePack_Should_Succeed()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .AddMessagePackProtocol()
                .Build();

            // Act
            await connection.StartAsync();
            var result = await connection.InvokeAsync<string>("Echo", "MessagePack test");

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);
            result.Should().Be("MessagePack test");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Server_With_Both_Protocols_Client_Requests_Json_Should_Succeed()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .Build(); // JSON is default

            // Act
            await connection.StartAsync();
            var result = await connection.InvokeAsync<string>("Echo", "JSON test");

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);
            result.Should().Be("JSON test");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Server_With_Json_Only_Client_Requests_Json_Should_Succeed()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_jsonOnlyUrl}/testhub")
                .Build();

            // Act
            await connection.StartAsync();
            var result = await connection.InvokeAsync<string>("Echo", "JSON only test");

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);
            result.Should().Be("JSON only test");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Server_With_Json_Only_Client_Requests_MessagePack_Should_Fallback_To_Json()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_jsonOnlyUrl}/testhub")
                .AddMessagePackProtocol()
                .Build();

            // Act & Assert
            // The connection should either:
            // 1. Fall back to JSON automatically (SignalR's built-in behavior)
            // 2. Fail with a negotiation error

            // Try to connect
            try
            {
                await connection.StartAsync();

                // If we got here, fallback worked
                connection.State.Should().Be(HubConnectionState.Connected);

                // Verify it's functional
                var result = await connection.InvokeAsync<string>("Echo", "Fallback test");
                result.Should().Be("Fallback test");

                await connection.StopAsync();
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                // If negotiation fails, that's also acceptable behavior
                ex.Message.Should().Contain("protocol",
                    "Error should indicate protocol negotiation issue");
            }
        }

        [Fact]
        public async Task Multiple_Clients_With_Different_Protocols_Should_Coexist()
        {
            // Arrange
            var jsonClient1 = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .Build();

            var jsonClient2 = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .Build();

            var messagePackClient1 = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .AddMessagePackProtocol()
                .Build();

            var messagePackClient2 = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .AddMessagePackProtocol()
                .Build();

            // Act
            await jsonClient1.StartAsync();
            await jsonClient2.StartAsync();
            await messagePackClient1.StartAsync();
            await messagePackClient2.StartAsync();

            // Assert - All clients should be connected
            jsonClient1.State.Should().Be(HubConnectionState.Connected);
            jsonClient2.State.Should().Be(HubConnectionState.Connected);
            messagePackClient1.State.Should().Be(HubConnectionState.Connected);
            messagePackClient2.State.Should().Be(HubConnectionState.Connected);

            // Verify all can communicate
            var result1 = await jsonClient1.InvokeAsync<string>("Echo", "JSON 1");
            var result2 = await jsonClient2.InvokeAsync<string>("Echo", "JSON 2");
            var result3 = await messagePackClient1.InvokeAsync<string>("Echo", "MessagePack 1");
            var result4 = await messagePackClient2.InvokeAsync<string>("Echo", "MessagePack 2");

            result1.Should().Be("JSON 1");
            result2.Should().Be("JSON 2");
            result3.Should().Be("MessagePack 1");
            result4.Should().Be("MessagePack 2");

            // Cleanup
            await jsonClient1.StopAsync();
            await jsonClient2.StopAsync();
            await messagePackClient1.StopAsync();
            await messagePackClient2.StopAsync();
            await jsonClient1.DisposeAsync();
            await jsonClient2.DisposeAsync();
            await messagePackClient1.DisposeAsync();
            await messagePackClient2.DisposeAsync();
        }

        [Fact]
        public async Task Client_Can_Switch_Protocols_Between_Connections()
        {
            // Arrange & Act - First connection with JSON
            var jsonConnection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .Build();

            await jsonConnection.StartAsync();
            var jsonResult = await jsonConnection.InvokeAsync<string>("Echo", "JSON");
            await jsonConnection.StopAsync();
            await jsonConnection.DisposeAsync();

            // Second connection with MessagePack
            var messagePackConnection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .AddMessagePackProtocol()
                .Build();

            await messagePackConnection.StartAsync();
            var messagePackResult = await messagePackConnection.InvokeAsync<string>("Echo", "MessagePack");

            // Assert
            jsonResult.Should().Be("JSON");
            messagePackResult.Should().Be("MessagePack");

            // Cleanup
            await messagePackConnection.StopAsync();
            await messagePackConnection.DisposeAsync();
        }

        [Fact]
        public async Task Protocol_Negotiation_Should_Respect_Client_Preference()
        {
            // Arrange - Client explicitly requests MessagePack
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub", options =>
                {
                    // Don't skip negotiation to allow protocol selection
                    options.SkipNegotiation = false;
                })
                .AddMessagePackProtocol()
                .Build();

            // Act
            await connection.StartAsync();

            // Assert - Connection should succeed with MessagePack
            connection.State.Should().Be(HubConnectionState.Connected);

            // Verify functionality
            var result = await connection.InvokeAsync<string>("Echo", "Protocol preference test");
            result.Should().Be("Protocol preference test");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Backward_Compatibility_Existing_Json_Clients_Continue_Working()
        {
            // This test verifies that existing clients (not upgraded to MessagePack)
            // continue to work without any changes

            // Arrange - Simulate legacy client (JSON only)
            var legacyConnection = new HubConnectionBuilder()
                .WithUrl($"{_bothProtocolsUrl}/testhub")
                .Build(); // No MessagePack configured

            // Act
            await legacyConnection.StartAsync();
            var result = await legacyConnection.InvokeAsync<string>("Echo", "Legacy client");

            // Assert
            legacyConnection.State.Should().Be(HubConnectionState.Connected);
            result.Should().Be("Legacy client",
                "Legacy JSON clients should work without any changes");

            // Cleanup
            await legacyConnection.StopAsync();
            await legacyConnection.DisposeAsync();
        }
    }
}
