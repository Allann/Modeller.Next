using Modeller.Configuration;
using Modeller.Workspace;
using Xunit;

namespace Modeller.Workspace.Tests;

public sealed class WorkspaceInputTests
{
    [Fact]
    public void WorkspaceDocument_cannot_be_constructed_with_an_unconfined_path()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceDocument(LogicalPath.Create("../escape.rml"), "content"));
    }

    [Fact]
    public void WorkspaceConfigurationInput_ToRequest_produces_a_base_source_with_the_required_fields()
    {
        var input = new WorkspaceConfigurationInput("1.0", "generated/");

        var request = input.ToRequest();
        var resolved = ConfigurationResolver.Resolve(request, TestContext.Current.CancellationToken);

        Assert.True(resolved.IsSuccess);
        Assert.Equal("1.0", resolved.Configuration!.GenerationContractVersion);
        Assert.Equal("generated/", resolved.Configuration.LogicalOutputRoot);
    }

    [Fact]
    public void IdentityStrategy_Ephemeral_Instance_is_a_singleton()
    {
        Assert.Same(IdentityStrategy.Ephemeral.Instance, IdentityStrategy.Ephemeral.Instance);
    }

    [Fact]
    public void IdentityStrategy_is_exhaustively_matchable()
    {
        IdentityStrategy strategy = IdentityStrategy.Ephemeral.Instance;

        var description = strategy switch
        {
            IdentityStrategy.Ephemeral => "ephemeral",
            IdentityStrategy.Durable durable => $"durable:{durable.Registry.Version}",
            _ => throw new InvalidOperationException("unreachable"),
        };

        Assert.Equal("ephemeral", description);
    }

    [Fact]
    public void WorkspaceIdentityRegistry_Empty_has_no_documents()
    {
        Assert.Empty(WorkspaceIdentityRegistry.Empty.Documents);
        Assert.Equal("1.0", WorkspaceIdentityRegistry.Empty.Version);
    }

    [Fact]
    public void WorkspaceInput_is_directly_constructible_in_memory()
    {
        var input = new WorkspaceInput(
            [new(LogicalPath.Create("entities/customer.rml"), "rml 1.0\ncontext Customer\n  version 1.0.0\nend\n")],
            IdentityStrategy.Ephemeral.Instance,
            new WorkspaceConfigurationInput("1.0", "generated/"));

        Assert.Single(input.Documents);
        Assert.IsType<IdentityStrategy.Ephemeral>(input.Identity);
    }
}
