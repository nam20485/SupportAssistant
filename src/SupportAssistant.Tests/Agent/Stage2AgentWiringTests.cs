using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SupportAssistant.Core.Agent;
using SupportAssistant.Core.Models;
using SupportAssistant.Core.Security;
using SupportAssistant.Core.Services;
using SupportAssistant.Core.Tools;
using Xunit;

namespace SupportAssistant.Tests.Agent;

/// <summary>
/// Phase 4 Stage 2 wiring tests: the orchestrator consumes the RAG pipeline (T2.3) and degrades
/// gracefully when it is absent, and the agent services resolve through DI (T2.2).
/// </summary>
public class Stage2AgentWiringTests
{
    private static (Mock<IToolRegistry> Tools, Mock<ISecurityManager> Security) MinimalDeps()
    {
        var tools = new Mock<IToolRegistry>();
        tools.Setup(t => t.GetAuthorizedTools(It.IsAny<ToolPermissionLevel>()))
            .Returns(new List<ITool>());
        tools.Setup(t => t.GetTool(It.IsAny<string>())).Returns((ITool?)null);

        var security = new Mock<ISecurityManager>();
        security.Setup(s => s.GetUserPermissionLevel(It.IsAny<string>()))
            .Returns(ToolPermissionLevel.Administrator);
        return (tools, security);
    }

    [Fact]
    public async Task ProcessQuery_ConsumesRagContext_WhenServicesWired()
    {
        var (tools, security) = MinimalDeps();
        var queryProcessing = new Mock<IQueryProcessingService>();
        queryProcessing.Setup(q => q.ProcessQueryAsync(It.IsAny<string>()))
            .ReturnsAsync((ProcessedQuery)null!);
        var contextRetrieval = new Mock<IContextRetrievalService>();
        // The orchestrator tolerates a null retrieval result (no KB grounding) without throwing.
        contextRetrieval.Setup(c => c.RetrieveContextAsync(It.IsAny<ProcessedQuery>()))
            .ReturnsAsync((ContextRetrievalResult)null!);

        var orchestrator = new AgentOrchestrator(
            tools.Object, security.Object, contextRetrieval.Object, queryProcessing.Object);

        var response = await orchestrator.ProcessQueryAsync(
            "user", "How do I configure SSL certificates?", CancellationToken.None);

        response.Should().NotBeNull();
        response.ResponseText.Should().NotBeNullOrEmpty();
        response.Errors.Should().BeEmpty();

        // Phase 4 T2.3: the knowledge-base context pipeline must be consulted.
        queryProcessing.Verify(q => q.ProcessQueryAsync("How do I configure SSL certificates?"), Times.Once);
        contextRetrieval.Verify(c => c.RetrieveContextAsync(It.IsAny<ProcessedQuery>()), Times.Once);
    }

    [Fact]
    public async Task ProcessQuery_StillAnswers_WhenRagServicesAbsent()
    {
        var (tools, security) = MinimalDeps();

        // Two-argument constructor: no RAG wiring. The agent must still answer (ungrounded) and not throw.
        var orchestrator = new AgentOrchestrator(tools.Object, security.Object);

        var response = await orchestrator.ProcessQueryAsync("user", "Hello", CancellationToken.None);

        response.Should().NotBeNull();
        response.ResponseText.Should().NotBeNullOrEmpty();
        response.Errors.Should().BeEmpty();
    }
}
