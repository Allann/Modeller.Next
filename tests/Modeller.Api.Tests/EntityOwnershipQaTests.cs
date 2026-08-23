using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Contracts;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// Executes tests/Modeller.Parsing.Acceptance/Features/EntityOwnership.qa.md against the real
/// product surface (the hosted API's /v1/workspace/analyze endpoint) rather than against
/// Modeller.Parsing directly, proving the "owner" aggregate-root fact is actually reachable
/// through the same request shape Modeller Studio's playground sends — not just implemented and
/// unit-tested in isolation. One test per numbered QA part.
///
/// The compiled model's "aggregate-root owner" fact is inspected the way the playground's
/// details panel would: via each entity's <see cref="SemanticOutlineItemDto.OwnerId"/> in the
/// analyze response's Outline. For an Entity-kind outline item, OwnerId is null when the entity
/// declares no owner (it is its own aggregate root) and is the declared owner entity's id
/// otherwise — see WorkspaceAnalysisPipeline.ComputeOutline.
/// </summary>
public sealed class EntityOwnershipQaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EntityOwnershipQaTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private const string ContextHeader = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        """;

    private static WorkspaceAnalyzeRequest WorkspaceRequest(string document) => new(
        [new("model/child-care.rml", document)],
        new EphemeralIdentityDto(),
        new ConfigurationDto("1.0", "generated/"),
        null);

    private async Task<WorkspaceAnalyzeResponse> Analyze(string document)
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/v1/workspace/analyze", WorkspaceRequest(document), ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    /// <summary>QA Part 1: an entity can declare which other entity owns it, and the compiled
    /// model records that fact against the owned entity.</summary>
    [Fact]
    public async Task Analyze_records_the_declared_owner_as_the_entitys_aggregate_root()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Centre
            end
            entity Absence
              owner "Centre"
            end
            """);

        Assert.Empty(body.Diagnostics);
        var centre = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Centre");
        var absence = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Absence");
        Assert.Equal(centre.Id, absence.OwnerId);
    }

    /// <summary>QA Part 2: declaring an owner is optional — an entity with no owner clause
    /// compiles cleanly and is recorded as having no aggregate-root owner, not as an error.</summary>
    [Fact]
    public async Task Analyze_records_no_owner_for_an_entity_with_no_owner_clause()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Centre
            end
            """);

        Assert.Empty(body.Diagnostics);
        var centre = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Centre");
        Assert.Null(centre.OwnerId);
    }

    /// <summary>QA Part 3: ownership chains through more than one level — each link in the chain
    /// is recorded independently.</summary>
    [Fact]
    public async Task Analyze_records_a_multi_level_ownership_chain()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Centre
            end
            entity Room
              owner "Centre"
            end
            entity Absence
              owner "Room"
            end
            """);

        Assert.Empty(body.Diagnostics);
        var centre = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Centre");
        var room = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Room");
        var absence = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Absence");
        Assert.Equal(centre.Id, room.OwnerId);
        Assert.Equal(room.Id, absence.OwnerId);
    }

    /// <summary>QA Part 4: an owner that does not resolve to any declared entity is rejected with
    /// a diagnostic explaining the owner cannot be resolved.</summary>
    [Fact]
    public async Task Analyze_rejects_an_owner_that_does_not_resolve_to_a_declared_entity()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Absence
              owner "Centre"
            end
            """);

        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.reference.unresolved");
    }

    /// <summary>QA Part 5: an entity cannot declare itself as its own owner.</summary>
    [Fact]
    public async Task Analyze_rejects_an_entity_that_declares_itself_as_its_own_owner()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Absence
              owner "Absence"
            end
            """);

        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.entity.owner-self");
    }

    /// <summary>QA Part 6: two entities cannot own each other — a direct 2-cycle is rejected with
    /// the circular-ownership diagnostic.</summary>
    [Fact]
    public async Task Analyze_rejects_two_entities_that_own_each_other()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Centre
              owner "Absence"
            end
            entity Absence
              owner "Centre"
            end
            """);

        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.entity.owner-cycle");
    }

    /// <summary>QA Part 7: a longer ownership chain that loops back on itself is rejected with the
    /// same circular-ownership diagnostic as a direct 2-cycle, and — crucially — the request
    /// completes deterministically rather than hanging or overflowing the stack.</summary>
    [Fact]
    public async Task Analyze_rejects_a_longer_ownership_chain_that_loops_back_on_itself()
    {
        var body = await Analyze($$"""
            {{ContextHeader}}
            entity Centre
              owner "Room"
            end
            entity Room
              owner "Absence"
            end
            entity Absence
              owner "Centre"
            end
            """);

        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.entity.owner-cycle");
    }
}
