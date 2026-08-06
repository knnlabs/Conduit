using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AwesomeAssertions;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ConduitLLM.IntegrationTests.Tests
{
    [Collection("Sequential")]
    [Trait("Category", "Integration")]
    [Trait("Component", "SignalR")]
    public class SignalRMessagePackIntegrationTests : IAsyncLifetime
    {
        private IHost _host = null!;
        private string _serverUrl = null!;
        private const int Port = 5555;

        public class TestHub : Hub
        {
            public async Task<string> Echo(string message)
            {
                return await Task.FromResult($"Echo: {message}");
            }

            public async Task BroadcastMessage(string message)
            {
                await Clients.All.SendAsync("ReceiveMessage", message);
            }

            public async Task SendLargePayload(int size)
            {
                var payload = new string('x', size);
                await Clients.Caller.SendAsync("ReceiveLargePayload", payload);
            }
        }

        public async Task InitializeAsync()
        {
            _serverUrl = $"http://localhost:{Port}";

            var builder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseUrls(_serverUrl)
                        .ConfigureServices(services =>
                        {
                            services.AddSignalR()
                                .AddMessagePackProtocol(options =>
                                {
                                    options.SerializerOptions = CreateMessagePackSerializerOptions();
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

            _host = builder.Build();
            await _host.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        [Fact]
        public async Task Client_Should_Connect_With_MessagePack_Protocol()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .Build();

            // Act
            await connection.StartAsync();

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Client_Should_Connect_With_Json_Protocol()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .Build(); // Default JSON protocol

            // Act
            await connection.StartAsync();

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task MessagePack_Should_Send_And_Receive_Messages()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .Build();

            await connection.StartAsync();

            // Act
            var result = await connection.InvokeAsync<string>("Echo", "Hello MessagePack");

            // Assert
            result.Should().Be("Echo: Hello MessagePack");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Json_Should_Send_And_Receive_Messages()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .Build();

            await connection.StartAsync();

            // Act
            var result = await connection.InvokeAsync<string>("Echo", "Hello JSON");

            // Assert
            result.Should().Be("Echo: Hello JSON");

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Server_Should_Support_Both_Protocols_Simultaneously()
        {
            // Arrange
            var jsonConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .Build();

            var messagePackConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .Build();

            var jsonReceived = new TaskCompletionSource<string>();
            var messagePackReceived = new TaskCompletionSource<string>();

            jsonConnection.On<string>("ReceiveMessage", message =>
            {
                jsonReceived.TrySetResult(message);
            });

            messagePackConnection.On<string>("ReceiveMessage", message =>
            {
                messagePackReceived.TrySetResult(message);
            });

            // Act
            await jsonConnection.StartAsync();
            await messagePackConnection.StartAsync();

            // Send broadcast from JSON client
            await jsonConnection.InvokeAsync("BroadcastMessage", "Cross-protocol test");

            // Assert - Both clients should receive the message
            var jsonResult = await Task.WhenAny(jsonReceived.Task, Task.Delay(5000));
            var messagePackResult = await Task.WhenAny(messagePackReceived.Task, Task.Delay(5000));

            jsonResult.Should().Be(jsonReceived.Task);
            messagePackResult.Should().Be(messagePackReceived.Task);

            (await jsonReceived.Task).Should().Be("Cross-protocol test");
            (await messagePackReceived.Task).Should().Be("Cross-protocol test");

            // Cleanup
            await jsonConnection.StopAsync();
            await messagePackConnection.StopAsync();
            await jsonConnection.DisposeAsync();
            await messagePackConnection.DisposeAsync();
        }

        [Theory]
        [InlineData(100)]   // Small payload (below compression threshold)
        [InlineData(500)]   // Medium payload (above compression threshold)
        [InlineData(2000)]  // Large payload
        public async Task MessagePack_Should_Handle_Various_Payload_Sizes(int size)
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .Build();

            await connection.StartAsync();

            var received = new TaskCompletionSource<string>();
            connection.On<string>("ReceiveLargePayload", payload =>
            {
                received.TrySetResult(payload);
            });

            // Act
            await connection.InvokeAsync("SendLargePayload", size);

            // Assert
            var result = await Task.WhenAny(received.Task, Task.Delay(10000));
            result.Should().Be(received.Task, $"Should receive payload of size {size}");

            var payload = await received.Task;
            payload.Length.Should().Be(size);

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task MessagePack_Should_Reconnect_After_Disconnection()
        {
            // Arrange
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .WithAutomaticReconnect()
                .Build();

            var reconnected = new TaskCompletionSource<bool>();
            connection.Reconnected += _ =>
            {
                reconnected.TrySetResult(true);
                return Task.CompletedTask;
            };

            await connection.StartAsync();
            connection.State.Should().Be(HubConnectionState.Connected);

            // Act - Force stop and wait for reconnect
            await connection.StopAsync();
            await connection.StartAsync();

            // Assert
            connection.State.Should().Be(HubConnectionState.Connected);

            // Cleanup
            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task MessagePack_And_Json_Should_Have_Same_Functional_Behavior()
        {
            // Arrange
            var jsonConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .Build();

            var messagePackConnection = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/testhub")
                .AddMessagePackProtocol(options =>
                    options.SerializerOptions = CreateMessagePackSerializerOptions())
                .Build();

            await jsonConnection.StartAsync();
            await messagePackConnection.StartAsync();

            // Act
            var jsonResult = await jsonConnection.InvokeAsync<string>("Echo", "Test message");
            var messagePackResult = await messagePackConnection.InvokeAsync<string>("Echo", "Test message");

            // Assert
            jsonResult.Should().Be(messagePackResult,
                "Both protocols should return identical results for the same operation");

            // Cleanup
            await jsonConnection.StopAsync();
            await messagePackConnection.StopAsync();
            await jsonConnection.DisposeAsync();
            await messagePackConnection.DisposeAsync();
        }

        private static MessagePack.MessagePackSerializerOptions CreateMessagePackSerializerOptions() =>
            MessagePack.MessagePackSerializerOptions.Standard
                .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                .WithCompressionMinLength(256);
    }
}
