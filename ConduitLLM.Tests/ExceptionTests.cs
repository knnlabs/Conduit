using ConduitLLM.Core.Exceptions;
using System;
using Xunit;

namespace ConduitLLM.Tests;

public class ExceptionTests
{
    private const string TestMessage = "This is a test exception message.";

    [Fact]
    public void ConfigurationException_Constructors_SetPropertiesCorrectly()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");

        // Act
        var ex1 = new ConfigurationException();
        var ex2 = new ConfigurationException(TestMessage);
        var ex3 = new ConfigurationException(TestMessage, innerEx);

        // Assert
        Assert.NotNull(ex1); // Default message might vary, just check instantiation
        Assert.Equal(TestMessage, ex2.Message);
        Assert.Null(ex2.InnerException);
        Assert.Equal(TestMessage, ex3.Message);
        Assert.Same(innerEx, ex3.InnerException);
    }

    [Fact]
    public void ConduitException_Constructors_SetPropertiesCorrectly()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");

        // Act
        var ex1 = new ConduitException();
        var ex2 = new ConduitException(TestMessage);
        var ex3 = new ConduitException(TestMessage, innerEx);

        // Assert
        Assert.NotNull(ex1);
        Assert.Equal(TestMessage, ex2.Message);
        Assert.Null(ex2.InnerException);
        Assert.Equal(TestMessage, ex3.Message);
        Assert.Same(innerEx, ex3.InnerException);
    }

    [Fact]
    public void LLMCommunicationException_Constructors_SetPropertiesCorrectly()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");

        // Act
        var ex1 = new LLMCommunicationException();
        var ex2 = new LLMCommunicationException(TestMessage);
        var ex3 = new LLMCommunicationException(TestMessage, innerEx);

        // Assert
        Assert.NotNull(ex1);
        Assert.Equal(TestMessage, ex2.Message);
        Assert.Null(ex2.InnerException);
        Assert.Equal(TestMessage, ex3.Message);
        Assert.Same(innerEx, ex3.InnerException);
    }

    [Fact]
    public void UnsupportedProviderException_Constructors_SetPropertiesCorrectly()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");
        var providerName = "UnsupportedProvider123";

        // Act
        var ex1 = new UnsupportedProviderException();
        var ex2 = new UnsupportedProviderException(providerName);
        var ex3 = new UnsupportedProviderException(providerName, TestMessage); // Assuming a constructor like this exists or should exist
        var ex4 = new UnsupportedProviderException(TestMessage, innerEx); // Standard exception constructor

        // Assert
        Assert.NotNull(ex1);
        Assert.Contains(providerName, ex2.Message); // Check if provider name is in the message
        Assert.Null(ex2.InnerException);

        // Assert for ex3 depends on its specific constructor implementation
        // If it combines providerName and message:
        Assert.Contains(providerName, ex3.Message);
        Assert.Contains(TestMessage, ex3.Message);
        Assert.Null(ex3.InnerException);

        // Assert for standard constructor
        Assert.Equal(TestMessage, ex4.Message);
        Assert.Same(innerEx, ex4.InnerException);
    }
}
