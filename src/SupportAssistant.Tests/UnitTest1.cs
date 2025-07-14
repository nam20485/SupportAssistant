using FluentAssertions;
using SupportAssistant.Core.Services;

namespace SupportAssistant.Tests;

public class OnnxRuntimeServiceTests
{
    [Fact]
    public void Initialize_ShouldComplete()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act & Assert - Should not throw an exception
        var initialize = () => service.Initialize();
        
        initialize.Should().NotThrow();
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnProviders()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act
        var providers = service.GetAvailableProviders();

        // Assert
        providers.Should().NotBeNull();
        providers.Should().NotBeEmpty();
        providers.Should().Contain("CPUExecutionProvider");
    }

    [Fact]
    public void CreateSessionOptions_ShouldReturnValidSessionOptions()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act
        var sessionOptions = service.CreateSessionOptions();

        // Assert
        sessionOptions.Should().NotBeNull();
    }

    [Fact]
    public void IsDirectMLAvailable_ShouldComplete()
    {
        // Arrange
        var service = new OnnxRuntimeService();

        // Act & Assert - Should not throw an exception
        var checkDirectMl = () => service.IsDirectMLAvailable();
        
        checkDirectMl.Should().NotThrow();
    }
}
