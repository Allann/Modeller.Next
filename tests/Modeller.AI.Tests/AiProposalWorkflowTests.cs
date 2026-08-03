using System.Collections.Immutable;
using Modeller.AI;
using Modeller.Model;
using Xunit;

namespace Modeller.AI.Tests;

public sealed class AiProposalWorkflowTests
{
    private static AuthoredContextRevision NewRevision() =>
        AuthoredContextRevision.Create(
            SemanticId.New(),
            new SemanticName("Child Care"),
            new SemanticSlug("child-care"),
            "1.0.0");

    private static AiProposalRequest RequestFor(
        AuthoredContextRevision revision,
        ImmutableArray<SemanticId> permittedConcepts = default,
        string intent = "Add eligibility fact") =>
        new(revision, revision.Revision, intent, permittedConcepts.IsDefault ? [] : permittedConcepts);

    private static FactDefinition NewFact(string name = "Active enrolment exists") =>
        new(SemanticId.New(), new SemanticName(name), new SemanticSlug(name.ToLowerInvariant().Replace(' ', '-')), FactType.Truth);

    private static IAiProposalProvider NeverCalledProvider() =>
        new DeterministicAiProposalProvider("never", "0", _ => throw new InvalidOperationException("Provider should not be called."));

    private static IAiProposalProvider ProviderReturning(ProviderProposal proposal) =>
        new DeterministicAiProposalProvider("fixture", "1", _ => proposal);

    [Fact]
    public async Task ProposeAsync_returns_cancelled_diagnostic_when_token_already_cancelled()
    {
        var revision = NewRevision();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), NeverCalledProvider(), cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.cancelled", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_stale_revision_diagnostic_when_expected_revision_mismatches()
    {
        var revision = NewRevision();
        var request = new AiProposalRequest(revision, revision.Revision + 1, "Add eligibility fact", []);

        var result = await AiProposalWorkflow.ProposeAsync(request, NeverCalledProvider(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.stale-revision", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_intent_required_diagnostic_when_intent_is_blank()
    {
        var revision = NewRevision();
        var request = RequestFor(revision, intent: "   ");

        var result = await AiProposalWorkflow.ProposeAsync(request, NeverCalledProvider(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.intent-required", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_cancelled_diagnostic_when_provider_cancels_the_token_and_throws()
    {
        var revision = NewRevision();
        using var cts = new CancellationTokenSource();
        var provider = new DeterministicAiProposalProvider("cancels", "1", _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), provider, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.cancelled", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_provider_failed_diagnostic_when_provider_throws_unexpectedly()
    {
        var revision = NewRevision();
        var provider = new DeterministicAiProposalProvider("broken", "1", _ => throw new InvalidOperationException("boom"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.provider.failed", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_operations_invalid_diagnostic_when_provider_returns_no_operations()
    {
        var revision = NewRevision();
        var provider = ProviderReturning(new ProviderProposal([], "no changes needed"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.operations-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_operations_invalid_diagnostic_when_provider_returns_default_operations()
    {
        var revision = NewRevision();
        var provider = ProviderReturning(new ProviderProposal(default, "no changes needed"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.operations-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_operations_invalid_diagnostic_when_provider_exceeds_the_operation_limit()
    {
        var revision = NewRevision();
        var operations = Enumerable.Range(0, 65)
            .Select(_ => (ModelOperation)new AddDefinition(NewFact(Guid.NewGuid().ToString())))
            .ToImmutableArray();
        var provider = ProviderReturning(new ProviderProposal(operations, "too many changes"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.operations-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_scope_violation_diagnostic_when_operation_targets_a_concept_outside_permitted_scope()
    {
        var revision = NewRevision();
        var outOfScopeConceptId = SemanticId.New();
        var provider = ProviderReturning(new ProviderProposal(
            [new RenameConcept(outOfScopeConceptId, new SemanticName("New name"), new SemanticSlug("new-name"))],
            "rename"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision, []), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.scope-violation", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_scope_violation_diagnostic_for_an_operation_type_the_scope_check_does_not_recognise()
    {
        var revision = NewRevision();
        var provider = ProviderReturning(new ProviderProposal([new UnrecognisedOperation()], "unknown op"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision, []), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.scope-violation", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_operation_invalid_diagnostic_when_canonical_model_rejects_the_operations()
    {
        var revision = NewRevision();
        var missingConceptId = SemanticId.New();
        var provider = ProviderReturning(new ProviderProposal(
            [new RenameConcept(missingConceptId, new SemanticName("New name"), new SemanticSlug("new-name"))],
            "rename missing concept"));

        var result = await AiProposalWorkflow.ProposeAsync(
            RequestFor(revision, [missingConceptId]),
            provider,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.operation-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_validation_failed_diagnostic_when_the_applied_model_fails_canonical_validation()
    {
        var revision = NewRevision();
        var rule = new RuleDefinition(
            SemanticId.New(),
            new SemanticName("Eligibility rule"),
            new SemanticSlug("eligibility-rule"),
            [new FactReference(SemanticId.New())],
            []);
        var provider = ProviderReturning(new ProviderProposal([new AddDefinition(rule)], "add rule referencing a missing fact"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision, []), provider, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.proposal.validation-failed", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task ProposeAsync_returns_a_successful_proposal_with_provenance_on_the_happy_path()
    {
        var revision = NewRevision();
        var fact = NewFact();
        var provider = ProviderReturning(new ProviderProposal([new AddDefinition(fact)], "add a fact"));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision, []), provider, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.Equal(revision.Revision, result.Proposal.SourceRevision);
        Assert.Equal("add a fact", result.Proposal.Explanation);
        Assert.Equal(new AiProposalProvenance("fixture", "1", revision.Revision), result.Proposal.Provenance);
        Assert.Equal($"ai:fixture:{revision.Revision}:1", result.Proposal.Id);
    }

    [Fact]
    public async Task ProposeAsync_defaults_explanation_to_empty_when_provider_returns_null()
    {
        var revision = NewRevision();
        var fact = NewFact();
        var provider = ProviderReturning(new ProviderProposal([new AddDefinition(fact)], null!));

        var result = await AiProposalWorkflow.ProposeAsync(RequestFor(revision, []), provider, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Proposal!.Explanation);
    }

    [Fact]
    public void Accept_returns_stale_revision_diagnostic_when_the_revision_has_moved_on()
    {
        var revision = NewRevision();
        var fact = NewFact();
        var applied = CanonicalModel.Apply(revision, new AddDefinition(fact));
        var proposal = new AiProposal("ai:x", revision.Revision, [new AddDefinition(fact)], "add", new("x", "1", revision.Revision));

        var result = AiProposalWorkflow.Accept(applied.Revision, proposal);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.acceptance.stale-revision", Assert.Single(result.Diagnostics).Code);
        Assert.Same(applied.Revision, result.Revision);
    }

    [Fact]
    public void Accept_returns_operation_invalid_diagnostic_when_canonical_model_rejects_the_proposal()
    {
        var revision = NewRevision();
        var missingConceptId = SemanticId.New();
        var proposal = new AiProposal(
            "ai:x",
            revision.Revision,
            [new RenameConcept(missingConceptId, new SemanticName("New name"), new SemanticSlug("new-name"))],
            "rename",
            new("x", "1", revision.Revision));

        var result = AiProposalWorkflow.Accept(revision, proposal);

        Assert.False(result.IsSuccess);
        Assert.Equal("ai.acceptance.operation-invalid", Assert.Single(result.Diagnostics).Code);
        Assert.Same(revision, result.Revision);
    }

    [Fact]
    public void Accept_applies_the_proposal_and_returns_the_next_revision_on_success()
    {
        var revision = NewRevision();
        var fact = NewFact();
        var proposal = new AiProposal("ai:x", revision.Revision, [new AddDefinition(fact)], "add", new("x", "1", revision.Revision));

        var result = AiProposalWorkflow.Accept(revision, proposal);

        Assert.True(result.IsSuccess);
        Assert.Equal(revision.Revision + 1, result.Revision.Revision);
        Assert.Contains(result.Revision.Definitions, definition => definition.Id == fact.Id);
    }

    private sealed record UnrecognisedOperation : ModelOperation;
}
