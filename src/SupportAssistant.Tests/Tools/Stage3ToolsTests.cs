using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SupportAssistant.Core.Tools;
using Xunit;

namespace SupportAssistant.Tests.Tools;

/// <summary>
/// Phase 4 Stage 3 (T3.1) tests for the three new additive agent tools:
/// ListDirectoryTool, GetSystemInfoTool, and WriteFileContentsTool.
/// </summary>
public class Stage3ToolsTests
{
    private static ToolExecutionContext Context(Dictionary<string, object>? parameters = null, bool approved = true)
    {
        return new ToolExecutionContext(
            parameters ?? new Dictionary<string, object>(),
            userId: "test-user",
            executionId: Guid.NewGuid().ToString("N"),
            userApproved: approved,
            workingDirectory: AppDomain.CurrentDomain.BaseDirectory);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sa-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---------------------------------------------------------------------
    // Parameterless constructors / reflection discovery
    // ---------------------------------------------------------------------

    [Fact]
    public void AllThreeTools_HaveParameterlessConstructor_AndConstructWithoutThrowing()
    {
        var list = new ListDirectoryTool();
        var sys = new GetSystemInfoTool();
        var write = new WriteFileContentsTool();

        list.Name.Should().Be("ListDirectory");
        sys.Name.Should().Be("GetSystemInfo");
        write.Name.Should().Be("WriteFileContents");
    }

    [Fact]
    public void ToolRegistry_AutoDiscoversAllThreeTools_WithParameterlessCtor()
    {
        var registry = new ToolRegistry();
        registry.DiscoverAndRegisterTools();

        registry.IsToolRegistered("ListDirectory").Should().BeTrue();
        registry.IsToolRegistered("GetSystemInfo").Should().BeTrue();
        registry.IsToolRegistered("WriteFileContents").Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // ListDirectoryTool
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ListDirectoryTool_ListsFilesAndDirectories_InTempDir()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "alpha.txt"), "a");
            File.WriteAllText(Path.Combine(dir, "beta.log"), "b");
            Directory.CreateDirectory(Path.Combine(dir, "sub"));

            var tool = new ListDirectoryTool();

            var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["path"] = dir
            }), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();

            var listing = result.Data.Should().BeOfType<ListDirectoryResult>().Subject;
            listing.EntryCount.Should().Be(3);
            listing.Entries.Should().Contain(e => e.Name == "alpha.txt" && !e.IsDirectory && e.Size == 1);
            listing.Entries.Should().Contain(e => e.Name == "beta.log" && !e.IsDirectory && e.Size == 1);
            listing.Entries.Should().Contain(e => e.Name == "sub" && e.IsDirectory && e.Size == null);
            result.ModifiedFiles.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListDirectoryTool_RespectsMaxEntriesCap()
    {
        var dir = CreateTempDir();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                File.WriteAllText(Path.Combine(dir, $"f{i}.txt"), "x");
            }

            var tool = new ListDirectoryTool();

            var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["path"] = dir,
                ["maxEntries"] = 2
            }), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var listing = result.Data.Should().BeOfType<ListDirectoryResult>().Subject;
            listing.EntryCount.Should().Be(2);
            listing.IsTruncated.Should().BeTrue();
            listing.MaxEntries.Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListDirectoryTool_ReturnsFailure_ForNonExistentPath()
    {
        var tool = new ListDirectoryTool();
        var missing = Path.Combine(Path.GetTempPath(), "definitely-does-not-exist-" + Path.GetRandomFileName());

        var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
        {
            ["path"] = missing
        }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ListDirectoryTool_IsReadOnlyAndDoesNotRequireApproval()
    {
        var tool = new ListDirectoryTool();
        tool.IsModifying.Should().BeFalse();
        tool.RequiresApproval.Should().BeFalse();
        tool.Category.Should().Be(ToolCategory.FileSystem);
        tool.RequiredPermission.Should().Be(ToolPermissionLevel.Read);
    }

    [Fact]
    public void ListDirectoryTool_ValidateParameters_RejectsMissingPath()
    {
        var tool = new ListDirectoryTool();
        var result = tool.ValidateParameters(new Dictionary<string, object>());
        result.IsValid.Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // GetSystemInfoTool
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetSystemInfoTool_ReturnsStructuredData_WithOsAndMachine()
    {
        var tool = new GetSystemInfoTool();

        var result = await tool.ExecuteAsync(Context(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var info = result.Data.Should().BeOfType<SystemInfoResult>().Subject;
        info.MachineName.Should().NotBeNullOrEmpty();
        info.OperatingSystem.Should().NotBeNullOrEmpty();
        info.OsPlatform.Should().NotBeNullOrEmpty();
        info.ProcessorCount.Should().BeGreaterThan(0);
        info.FrameworkDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSystemInfoTool_IncludesMemoryAndStorage_ByDefault()
    {
        var tool = new GetSystemInfoTool();

        var result = await tool.ExecuteAsync(Context(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var info = result.Data.Should().BeOfType<SystemInfoResult>().Subject;
        info.Memory.Should().NotBeNull();
        info.Storage.Should().NotBeNull();
        info.Storage!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSystemInfoTool_RespectsExcludeFlags()
    {
        var tool = new GetSystemInfoTool();

        var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
        {
            ["includeMemory"] = false,
            ["includeStorage"] = false
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var info = result.Data.Should().BeOfType<SystemInfoResult>().Subject;
        info.Memory.Should().BeNull();
        info.Storage.Should().BeNull();
    }

    [Fact]
    public void GetSystemInfoTool_IsReadOnlyAndDoesNotRequireApproval()
    {
        var tool = new GetSystemInfoTool();
        tool.IsModifying.Should().BeFalse();
        tool.RequiresApproval.Should().BeFalse();
        tool.Category.Should().Be(ToolCategory.System);
        tool.RequiredPermission.Should().Be(ToolPermissionLevel.Read);
    }

    // ---------------------------------------------------------------------
    // WriteFileContentsTool
    // ---------------------------------------------------------------------

    [Fact]
    public void WriteFileContentsTool_IsModifyingAndRequiresApproval()
    {
        var tool = new WriteFileContentsTool();
        tool.IsModifying.Should().BeTrue();
        tool.RequiresApproval.Should().BeTrue();
        tool.Category.Should().Be(ToolCategory.FileSystem);
        tool.RequiredPermission.Should().Be(ToolPermissionLevel.Administrator);
    }

    [Fact]
    public void WriteFileContentsTool_ValidateParameters_RejectsEmptyPathOrContent()
    {
        var tool = new WriteFileContentsTool();

        var missingPath = tool.ValidateParameters(new Dictionary<string, object>
        {
            ["content"] = "x"
        });
        missingPath.IsValid.Should().BeFalse();

        var missingContent = tool.ValidateParameters(new Dictionary<string, object>
        {
            ["filePath"] = "/tmp/x.txt"
        });
        missingContent.IsValid.Should().BeFalse();

        var emptyContent = tool.ValidateParameters(new Dictionary<string, object>
        {
            ["filePath"] = "/tmp/x.txt",
            ["content"] = ""
        });
        emptyContent.IsValid.Should().BeFalse();
    }

    [Fact]
    public void WriteFileContentsTool_ValidateParameters_AcceptsValidParameters()
    {
        var tool = new WriteFileContentsTool();
        var result = tool.ValidateParameters(new Dictionary<string, object>
        {
            ["filePath"] = "/tmp/x.txt",
            ["content"] = "hello"
        });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task WriteFileContentsTool_WritesContent_ThatCanBeReadBack()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "out.txt");
            var tool = new WriteFileContentsTool();

            var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["filePath"] = file,
                ["content"] = "line one"
            }), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            tool.IsModifying.Should().BeTrue();
            result.ModifiedFiles.Should().ContainSingle().Which.Should().Be(file);
            File.Exists(file).Should().BeTrue();
            (await File.ReadAllTextAsync(file)).Should().Be("line one");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteFileContentsTool_Append_AddsRatherThanOverwrites()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "append.txt");
            var tool = new WriteFileContentsTool();

            var first = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["filePath"] = file,
                ["content"] = "first"
            }), CancellationToken.None);
            first.IsSuccess.Should().BeTrue();

            var second = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["filePath"] = file,
                ["content"] = "second",
                ["append"] = true
            }), CancellationToken.None);
            second.IsSuccess.Should().BeTrue();

            (await File.ReadAllTextAsync(file)).Should().Be("firstsecond");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteFileContentsTool_Overwrite_ReplacesExistingContent()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "overwrite.txt");
            File.WriteAllText(file, "original");
            var tool = new WriteFileContentsTool();

            var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["filePath"] = file,
                ["content"] = "replaced",
                ["append"] = false
            }), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            (await File.ReadAllTextAsync(file)).Should().Be("replaced");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteFileContentsTool_CreatesParentDirectory_WhenMissing()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "nested", "deep", "out.txt");
            var tool = new WriteFileContentsTool();

            var result = await tool.ExecuteAsync(Context(new Dictionary<string, object>
            {
                ["filePath"] = file,
                ["content"] = "nested"
            }), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(file).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
