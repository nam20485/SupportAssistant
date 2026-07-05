using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SupportAssistant.Core.Agent;
using SupportAssistant.Core.Security;
using SupportAssistant.Core.Tools;
using Xunit;

namespace SupportAssistant.Tests.Security
{
    /// <summary>
    /// Phase 4 Stage 3 T3.2/T3.3: real human-in-the-loop approval delegation and real file
    /// backup/restore, plus the orchestrator-level approve/deny + backup flow.
    /// </summary>
    public class Stage3ApprovalBackupTests
    {
        private static ITool WriteTool => new WriteFileContentsTool();

        private static Dictionary<string, object> ParamsFor(string path, string content = "x") =>
            new() { ["filePath"] = path, ["content"] = content };

        [Fact]
        public async Task RequestApproval_DelegatesToUserInteraction_WhenWired()
        {
            var ui = new Mock<IUserInteraction>();
            ui.Setup(u => u.RequestApprovalAsync(
                    It.IsAny<ITool>(), It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ToolApprovalResult.Approved("approval-1", "yes"));

            var security = new SecurityManager(ui.Object);

            var result = await security.RequestApprovalAsync("user", WriteTool, ParamsFor("/tmp/a.txt", "z"));

            result.IsApproved.Should().BeTrue();
            ui.Verify(u => u.RequestApprovalAsync(
                It.IsAny<ITool>(), It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RequestApproval_FallsBackToSimulation_WhenNoUserInteraction()
        {
            var security = new SecurityManager(); // headless: no HITL surface

            // Non-modifying tool is auto-approved by the simulation; a modifying tool is denied.
            var read = new ListDirectoryTool();
            var approved = await security.RequestApprovalAsync("user", read, ParamsFor("/tmp"));
            approved.IsApproved.Should().BeTrue();

            var denied = await security.RequestApprovalAsync("user", WriteTool, ParamsFor("/tmp/a.txt", "z"));
            denied.IsApproved.Should().BeFalse();
        }

        [Fact]
        public async Task Backup_RestoresOriginalContent_AfterModification()
        {
            var path = Path.Combine(Path.GetTempPath(), $"sa-backup-{System.Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(path, "ORIGINAL");

            try
            {
                var security = new SecurityManager();
                var backup = await security.CreateBackupAsync(WriteTool, ParamsFor(path, "MODIFIED"));
                backup.BackedUpFiles.Should().Contain(path);
                backup.CanRestore.Should().BeTrue();

                await File.WriteAllTextAsync(path, "MODIFIED");
                (await security.RestoreBackupAsync(backup.BackupId)).Should().BeTrue();
                (await File.ReadAllTextAsync(path)).Should().Be("ORIGINAL");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task Backup_RemovesNewlyCreatedFile_OnRestore()
        {
            var path = Path.Combine(Path.GetTempPath(), $"sa-new-{System.Guid.NewGuid():N}.txt");
            File.Exists(path).Should().BeFalse();

            try
            {
                var security = new SecurityManager();
                // The file does not exist yet at backup time -> recorded as a new file.
                var backup = await security.CreateBackupAsync(WriteTool, ParamsFor(path, "hello"));

                await File.WriteAllTextAsync(path, "hello");

                (await security.RestoreBackupAsync(backup.BackupId)).Should().BeTrue();
                File.Exists(path).Should().BeFalse();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task Orchestrator_ApprovedWrite_WritesFile_AndDenyDoesNot()
        {
            var approvePath = Path.Combine(Path.GetTempPath(), $"sa-approve-{System.Guid.NewGuid():N}.txt");
            var denyPath = Path.Combine(Path.GetTempPath(), $"sa-deny-{System.Guid.NewGuid():N}.txt");

            try
            {
                var registry = new ToolRegistry();
                registry.RegisterTool(WriteTool);

                // Approved -> file written.
                var approveCall = new ToolCall { ToolName = "WriteFileContents", Parameters = ParamsFor(approvePath, "written") };
                // Inject an approving HITL surface by reconstructing with a mock UI.
                var approveUi = new Mock<IUserInteraction>();
                approveUi.Setup(u => u.RequestApprovalAsync(
                        It.IsAny<ITool>(), It.IsAny<Dictionary<string, object>>(),
                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ToolApprovalResult.Approved("a"));
                var approvingSecurity = new SecurityManager(approveUi.Object);
                await approvingSecurity.SetUserPermissionLevelAsync("admin", ToolPermissionLevel.Administrator);
                var orchestratorApprove = new AgentOrchestrator(registry, approvingSecurity);

                var approved = await orchestratorApprove.ExecuteToolCallAsync("admin", approveCall);
                approved.ToolResult.IsSuccess.Should().BeTrue();
                File.Exists(approvePath).Should().BeTrue();
                (await File.ReadAllTextAsync(approvePath)).Should().Be("written");

                // Denied -> no write, failure reported.
                var denyUi = new Mock<IUserInteraction>();
                denyUi.Setup(u => u.RequestApprovalAsync(
                        It.IsAny<ITool>(), It.IsAny<Dictionary<string, object>>(),
                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ToolApprovalResult.Denied("d", "no"));
                var denyingSecurity = new SecurityManager(denyUi.Object);
                await denyingSecurity.SetUserPermissionLevelAsync("admin", ToolPermissionLevel.Administrator);
                var orchestratorDeny = new AgentOrchestrator(registry, denyingSecurity);

                var denyCall = new ToolCall { ToolName = "WriteFileContents", Parameters = ParamsFor(denyPath, "nope") };
                var denied = await orchestratorDeny.ExecuteToolCallAsync("admin", denyCall);
                denied.ToolResult.IsSuccess.Should().BeFalse();
                File.Exists(denyPath).Should().BeFalse();
            }
            finally
            {
                if (File.Exists(approvePath)) File.Delete(approvePath);
                if (File.Exists(denyPath)) File.Delete(denyPath);
            }
        }

        /// <summary>An SLM that always reports unavailable, so the orchestrator uses its simulation
        /// path and the approval/backup flow is exercised independently of generation.</summary>
        private sealed class AlwaysFailSlm : ISLMService
        {
            public bool IsAvailable => false;
            public Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default) =>
                throw new System.NotSupportedException("test SLM");
        }
    }
}
