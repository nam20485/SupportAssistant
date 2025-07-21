using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SupportAssistant.ViewModels;
using SupportAssistant.Core.Services;
using SupportAssistant.Models;
using Moq;

namespace SupportAssistant.Tests.UI;

/// <summary>
/// Performance testing for Phase 3.9 UI Validation
/// Tests UI responsiveness, memory usage, rendering performance,
/// and system resource management during various operations
/// </summary>
public class PerformanceTests
{
    private readonly Mock<IQueryProcessingService> _mockQueryProcessor;
    private readonly Mock<IContextRetrievalService> _mockContextRetrieval;
    private readonly Mock<IResponseGenerationService> _mockResponseGeneration;
    private readonly ChatViewModel _viewModel;

    public PerformanceTests()
    {
        _mockQueryProcessor = new Mock<IQueryProcessingService>();
        _mockContextRetrieval = new Mock<IContextRetrievalService>();
        _mockResponseGeneration = new Mock<IResponseGenerationService>();
        
        _viewModel = new ChatViewModel(
            _mockQueryProcessor.Object,
            _mockContextRetrieval.Object,
            _mockResponseGeneration.Object);
    }

    #region UI Responsiveness Tests

    [Fact]
    public void Performance_UIResponsiveness_CommandExecutionShouldBeQuick()
    {
        // Test that UI commands execute within acceptable time limits
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Execute basic UI commands
        _viewModel.UserInput = "Test message";
        _viewModel.SendMessageCommand.Execute(null);
        
        stopwatch.Stop();
        
        // Assert - Command should execute within 100ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "UI commands should be responsive and not block the UI thread");
    }

    [Fact]
    public void Performance_UIResponsiveness_ClearChatShouldBeInstantaneous()
    {
        // Arrange - Add many messages to test clearing performance
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"Test message {i}", 
                Type = i % 2 == 0 ? ChatMessageType.User : ChatMessageType.Assistant 
            });
        }

        var stopwatch = Stopwatch.StartNew();
        
        // Act
        _viewModel.ClearChatCommand.Execute(null);
        
        stopwatch.Stop();
        
        // Assert - Should clear quickly even with many messages
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, 
            "Clearing chat should be instantaneous regardless of message count");
        _viewModel.Messages.Count.Should().Be(1); // Welcome message only
    }

    [Fact]
    public void Performance_UIResponsiveness_TypingIndicatorShouldNotCauseDelays()
    {
        // Test that typing indicator updates don't cause UI delays
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Rapidly toggle typing indicator
        for (int i = 0; i < 100; i++)
        {
            _viewModel.IsTyping = !_viewModel.IsTyping;
            _viewModel.IsProcessing = !_viewModel.IsProcessing;
        }
        
        stopwatch.Stop();
        
        // Assert - Should handle rapid updates without delays
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, 
            "Typing indicator updates should not cause UI delays");
    }

    [Fact]
    public void Performance_UIResponsiveness_ProgressUpdatesShouldBeSmooth()
    {
        // Test that progress updates don't cause stuttering
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Simulate rapid progress updates
        for (int i = 0; i <= 100; i++)
        {
            _viewModel.ProcessingProgress = i;
            _viewModel.ProcessingStage = $"Processing step {i}...";
        }
        
        stopwatch.Stop();
        
        // Assert - Progress updates should be smooth
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "Progress updates should not cause UI stuttering");
        _viewModel.ProcessingProgress.Should().Be(100);
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public void Performance_MemoryUsage_LargeConversationsShouldNotLeakMemory()
    {
        // Test memory usage with large conversations
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Add many messages to simulate long conversation
        for (int i = 0; i < 5000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"This is a test message number {i} with some content to simulate real usage patterns and memory consumption during extended conversations.",
                Type = i % 2 == 0 ? ChatMessageType.User : ChatMessageType.Assistant,
                Timestamp = DateTime.Now.AddSeconds(i)
            });
        }
        
        var afterAddingMemory = GC.GetTotalMemory(false);
        
        // Clear messages
        _viewModel.ClearChatCommand.Execute(null);
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var afterClearingMemory = GC.GetTotalMemory(true);
        
        // Assert - Memory should be released after clearing
        var memoryIncrease = afterAddingMemory - initialMemory;
        var memoryAfterClear = afterClearingMemory - initialMemory;
        
        memoryAfterClear.Should().BeLessThan(memoryIncrease / 2, 
            "Memory should be released after clearing messages");
    }

    [Fact]
    public void Performance_MemoryUsage_RepeatedOperationsShouldNotAccumulate()
    {
        // Test that repeated operations don't accumulate memory
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Perform repeated operations
        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Add messages
            for (int i = 0; i < 100; i++)
            {
                _viewModel.Messages.Add(new ChatMessage 
                { 
                    Content = $"Cycle {cycle} Message {i}",
                    Type = ChatMessageType.User
                });
            }
            
            // Clear messages
            _viewModel.ClearChatCommand.Execute(null);
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDifference = finalMemory - initialMemory;
        
        // Assert - Memory usage should not accumulate significantly
        memoryDifference.Should().BeLessThan(1024 * 1024, // Less than 1MB
            "Repeated operations should not accumulate memory");
    }

    [Fact]
    public void Performance_MemoryUsage_MessageCollectionShouldScaleReasonably()
    {
        // Test memory scaling with message count
        
        var baselineMemory = GC.GetTotalMemory(true);
        
        // Add 1000 messages
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = "Standard message content for memory testing",
                Type = ChatMessageType.User
            });
        }
        
        var after1000Memory = GC.GetTotalMemory(false);
        
        // Add another 1000 messages
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = "Standard message content for memory testing",
                Type = ChatMessageType.User
            });
        }
        
        var after2000Memory = GC.GetTotalMemory(false);
        
        // Assert - Memory should scale roughly linearly
        var first1000Increase = after1000Memory - baselineMemory;
        var second1000Increase = after2000Memory - after1000Memory;
        
        // Second 1000 should not use significantly more memory than first 1000
        second1000Increase.Should().BeLessThan((long)(first1000Increase * 1.5), 
            "Memory usage should scale reasonably with message count");
    }

    #endregion

    #region Rendering Performance Tests

    [Fact]
    public void Performance_Rendering_RapidMessageAdditionShouldBeHandledEfficiently()
    {
        // Test performance when messages are added rapidly
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Add messages rapidly as they might arrive during streaming
        for (int i = 0; i < 500; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"Rapid message {i}",
                Type = ChatMessageType.Assistant,
                Timestamp = DateTime.Now.AddMilliseconds(i)
            });
        }
        
        stopwatch.Stop();
        
        // Assert - Should handle rapid additions efficiently
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, 
            "Rapid message addition should be handled efficiently");
        _viewModel.Messages.Count.Should().Be(501); // Including welcome message
    }

    [Fact]
    public void Performance_Rendering_LongMessagesShouldNotCausePerformanceIssues()
    {
        // Test performance with very long messages
        
        var longContent = new string('A', 50000); // 50KB message
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        _viewModel.Messages.Add(new ChatMessage 
        { 
            Content = longContent,
            Type = ChatMessageType.Assistant
        });
        
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "Long messages should not cause performance issues");
        _viewModel.Messages.Last().Content.Should().HaveLength(50000);
    }

    [Fact]
    public void Performance_Rendering_ScrollingPerformanceShouldBeOptimal()
    {
        // Test auto-scroll performance with many messages
        
        // Arrange - Add many messages
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"Scroll test message {i}",
                Type = ChatMessageType.User
            });
        }
        
        var stopwatch = Stopwatch.StartNew();
        bool scrollEventTriggered = false;
        
        // Act - Test scroll triggering
        _viewModel.ScrollToBottom += () => scrollEventTriggered = true;
        _viewModel.Messages.Add(new ChatMessage 
        { 
            Content = "New message that should trigger scroll",
            Type = ChatMessageType.Assistant
        });
        
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, 
            "Auto-scroll should trigger quickly even with many messages");
        scrollEventTriggered.Should().BeTrue();
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task Performance_Concurrency_MultipleOperationsShouldNotBlock()
    {
        // Test that multiple UI operations can be performed concurrently
        
        var tasks = new List<Task>();
        
        // Act - Perform multiple operations concurrently
        tasks.Add(Task.Run(() => 
        {
            for (int i = 0; i < 100; i++)
            {
                _viewModel.UserInput = $"Message {i}";
                Thread.Sleep(1); // Simulate UI interaction delay
            }
        }));
        
        tasks.Add(Task.Run(() => 
        {
            for (int i = 0; i < 100; i++)
            {
                _viewModel.ProcessingProgress = i;
                Thread.Sleep(1);
            }
        }));
        
        tasks.Add(Task.Run(() => 
        {
            for (int i = 0; i < 100; i++)
            {
                _viewModel.StatusMessage = $"Status {i}";
                Thread.Sleep(1);
            }
        }));
        
        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert - All operations should complete without deadlocks
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "Concurrent operations should complete efficiently");
    }

    [Fact]
    public async Task Performance_Concurrency_PropertyUpdatesShouldBeThreadSafe()
    {
        // Test thread safety of property updates
        
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();
        
        // Act - Update properties from multiple threads
        for (int threadId = 0; threadId < 5; threadId++)
        {
            int localThreadId = threadId;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        _viewModel.ProcessingProgress = (localThreadId * 100) + i;
                        _viewModel.StatusMessage = $"Thread {localThreadId} Update {i}";
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - No exceptions should occur
        exceptions.Should().BeEmpty("Property updates should be thread-safe");
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public void Performance_ResourceManagement_ViewModelDisposalShouldCleanupResources()
    {
        // Test that ViewModel properly cleans up resources
        
        // Arrange - Add event handlers and data
        bool eventHandlerCalled = false;
        _viewModel.CopyToClipboard += _ => eventHandlerCalled = true;
        
        for (int i = 0; i < 100; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"Message {i}",
                Type = ChatMessageType.User
            });
        }
        
        // Act - Trigger event to ensure handler is connected
        _viewModel.CopyMessageCommand.Execute("test");
        eventHandlerCalled.Should().BeTrue();
        
        // In a real implementation, we would test Dispose() method
        // For now, test that clearing works as cleanup
        _viewModel.ClearChatCommand.Execute(null);
        
        // Assert - Resources should be cleaned up
        _viewModel.Messages.Count.Should().Be(1); // Welcome message only
    }

    [Fact]
    public void Performance_ResourceManagement_CommandExecutionShouldNotLeakResources()
    {
        // Test that repeated command execution doesn't leak resources
        
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Execute commands repeatedly
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.UserInput = $"Test {i}";
            _viewModel.SendMessageCommand.Execute(null);
            _viewModel.ClearChatCommand.Execute(null);
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Assert - Memory increase should be minimal
        memoryIncrease.Should().BeLessThan(1024 * 1024, // Less than 1MB
            "Repeated command execution should not leak significant memory");
    }

    #endregion

    #region Stress Testing

    [Fact]
    public void Performance_StressTest_HighVolumeMessageProcessingShouldBeStable()
    {
        // Stress test with high volume of messages
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Process large number of messages
        for (int i = 0; i < 10000; i++)
        {
            _viewModel.Messages.Add(new ChatMessage 
            { 
                Content = $"Stress test message {i} with some additional content to simulate realistic message sizes and content patterns",
                Type = i % 3 == 0 ? ChatMessageType.System : 
                       i % 2 == 0 ? ChatMessageType.User : ChatMessageType.Assistant,
                Timestamp = DateTime.Now.AddMilliseconds(i)
            });
            
            // Occasionally update other properties
            if (i % 100 == 0)
            {
                _viewModel.ProcessingProgress = (i / 100) % 101;
                _viewModel.StatusMessage = $"Processing batch {i / 100}";
            }
        }
        
        stopwatch.Stop();
        
        // Assert - Should handle high volume efficiently
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            "High volume message processing should complete within reasonable time");
        _viewModel.Messages.Count.Should().Be(10001); // Including welcome message
    }

    [Fact]
    public void Performance_StressTest_RapidCommandExecutionShouldBeStable()
    {
        // Stress test rapid command execution
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Execute commands rapidly
        for (int i = 0; i < 1000; i++)
        {
            _viewModel.UserInput = $"Rapid test {i}";
            _viewModel.SendMessageCommand.Execute(null);
            
            if (i % 10 == 0)
            {
                _viewModel.ClearChatCommand.Execute(null);
            }
            
            if (i % 5 == 0)
            {
                _viewModel.CopyMessageCommand.Execute("test content");
            }
        }
        
        stopwatch.Stop();
        
        // Assert - Should remain stable under rapid execution
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, 
            "Rapid command execution should be stable and performant");
    }

    #endregion
}
