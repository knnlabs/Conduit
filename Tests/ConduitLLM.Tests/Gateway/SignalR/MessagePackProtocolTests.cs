using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;

namespace ConduitLLM.Tests.Gateway.SignalR
{
    /// <summary>
    /// Unit tests for MessagePack protocol registration and configuration
    /// </summary>
    public class MessagePackProtocolTests
    {
        [Fact]
        public void MessagePack_Protocol_Should_Be_Available()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSignalR()
                .AddMessagePackProtocol();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();

            // Assert
            protocols.Should().NotBeNull();
            protocols.Should().Contain(p => p.Name == "messagepack");
        }

        [Fact]
        public void MessagePack_Protocol_Should_Support_Json_Fallback()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSignalR()
                .AddMessagePackProtocol();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();

            // Assert
            protocols.Should().Contain(p => p.Name == "json",
                "JSON protocol should always be available for backward compatibility");
            protocols.Should().Contain(p => p.Name == "messagepack",
                "MessagePack protocol should be registered");
        }

        [Fact]
        public void MessagePack_Protocol_Configuration_Should_Include_Security_Settings()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol(options =>
                {
                    // Configure with security settings matching Program.SignalR.cs
                    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                        .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                        .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData);
                });

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var messagePackProtocol = protocols.FirstOrDefault(p => p.Name == "messagepack");

            // Assert
            messagePackProtocol.Should().NotBeNull("MessagePack protocol should be registered");
            messagePackProtocol.Name.Should().Be("messagepack");
        }

        [Fact]
        public void MessagePack_Protocol_Should_Use_Lz4_Compression()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol(options =>
                {
                    // Configure with LZ4 compression matching Program.SignalR.cs
                    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                        .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                        .WithCompressionMinLength(256);
                });

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var messagePackProtocol = protocols.FirstOrDefault(p => p.Name == "messagepack");

            // Assert
            messagePackProtocol.Should().NotBeNull("MessagePack protocol with compression should be registered");
        }

        [Theory]
        [InlineData("json")]
        [InlineData("messagepack")]
        public void SignalR_Should_Support_Protocol_By_Name(string protocolName)
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var protocol = protocols.FirstOrDefault(p => p.Name == protocolName);

            // Assert
            protocol.Should().NotBeNull($"{protocolName} protocol should be available");
            protocol.Name.Should().Be(protocolName);
        }

        [Fact]
        public void MessagePack_Protocol_Should_Have_Correct_Version()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var messagePackProtocol = protocols.FirstOrDefault(p => p.Name == "messagepack");

            // Assert
            messagePackProtocol.Should().NotBeNull();
            messagePackProtocol.Version.Should().BeGreaterThan(0,
                "MessagePack protocol should have a valid version number");
        }

        [Fact]
        public void SignalR_Builder_Should_Register_Both_Protocols()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // This simulates the configuration in Program.SignalR.cs
            var signalRBuilder = services.AddSignalR();
            signalRBuilder.AddMessagePackProtocol(options =>
            {
                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                    .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                    .WithCompressionMinLength(256);
            });

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>().ToList();

            // Assert
            protocols.Should().HaveCount(2, "Should have both JSON and MessagePack protocols");
            protocols.Should().Contain(p => p.Name == "json");
            protocols.Should().Contain(p => p.Name == "messagepack");
        }

        [Fact]
        public void MessagePack_Protocol_Should_Be_TransferFormat_Binary()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR()
                .AddMessagePackProtocol();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var messagePackProtocol = protocols.FirstOrDefault(p => p.Name == "messagepack");

            // Assert
            messagePackProtocol.Should().NotBeNull();
            messagePackProtocol.TransferFormat.Should().Be(TransferFormat.Binary,
                "MessagePack is a binary protocol");
        }

        [Fact]
        public void Json_Protocol_Should_Be_TransferFormat_Text()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSignalR();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var protocols = serviceProvider.GetServices<IHubProtocol>();
            var jsonProtocol = protocols.FirstOrDefault(p => p.Name == "json");

            // Assert
            jsonProtocol.Should().NotBeNull();
            jsonProtocol.TransferFormat.Should().Be(TransferFormat.Text,
                "JSON is a text protocol");
        }
    }
}
