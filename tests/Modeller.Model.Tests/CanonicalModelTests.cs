using System.Collections.Immutable;
using System.Text.Json;
using Modeller.Model;
using Xunit;

namespace Modeller.Model.Tests;

public sealed class CanonicalModelTests
{
    [Fact]
    public void Child_care_slice_is_created_through_one_immutable_operation_seam()
    {
        var original = ChildCareFixture.EmptyContext();

        var result = CanonicalModel.Apply(
            original,
            new AddDefinition(ChildCareFixture.ApplicationEntity()),
            new AddDefinition(ChildCareFixture.ActiveEnrolmentFact()),
            new AddDefinition(ChildCareFixture.EligibilityRule()),
            new AddDefinition(ChildCareFixture.SubmitBehaviour()));

        Assert.True(result.Succeeded);
        Assert.Empty(original.Definitions);
        Assert.Equal(0, original.Revision);
        Assert.Equal(4, result.Revision.Definitions.Length);
        Assert.Equal(1, result.Revision.Revision);

        var behaviour = Assert.IsType<BehaviourDefinition>(result.Revision.Definitions[3]);
        Assert.Equal("Submit ACCS determination application", behaviour.Name.Value);
        Assert.Equal(ChildCareFixture.ApplicationId, behaviour.Entity.TargetId);
        Assert.Equal(2, behaviour.Outcomes.Length);
        Assert.Single(behaviour.Transitions);
        Assert.Equal(
            "Moves a draft application to submitted after the behaviour succeeds.",
            behaviour.Transitions[0].Documentation?.Purpose);
        Assert.Single(behaviour.RuleBindings);
        Assert.Equal(RuleBindingPurpose.Requirement, behaviour.RuleBindings[0].Purpose);
        Assert.Equal(ChildCareFixture.EligibilityRuleId, behaviour.RuleBindings[0].Rule.TargetId);
        Assert.Equal(
            ChildCareFixture.ActiveEnrolmentFactId,
            Assert.Single(behaviour.RuleBindings[0].FactBindings).Key.TargetId);
        var entity = Assert.IsType<EntityDefinition>(result.Revision.Definitions[0]);
        Assert.Equal(
            "Records an application for additional child care subsidy.",
            entity.Documentation?.Purpose);
    }

    [Fact]
    public void Failed_operation_batch_returns_the_original_revision_with_diagnostic()
    {
        var original = ChildCareFixture.EmptyContext();
        var fact = ChildCareFixture.ActiveEnrolmentFact();

        var result = CanonicalModel.Apply(
            original,
            new AddDefinition(fact),
            new AddDefinition(fact));

        Assert.False(result.Succeeded);
        Assert.Same(original, result.Revision);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("model.identity.duplicate", diagnostic.Code);
        Assert.Equal(fact.Id, diagnostic.SubjectId);
    }

    [Fact]
    public void Rename_preserves_identity_and_does_not_mutate_the_previous_revision()
    {
        var added = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(ChildCareFixture.ActiveEnrolmentFact())).Revision;

        var renamed = CanonicalModel.Apply(
            added,
            new RenameConcept(
                ChildCareFixture.ActiveEnrolmentFactId,
                new SemanticName("Child has active enrolment"),
                new SemanticSlug("child-has-active-enrolment")));

        Assert.True(renamed.Succeeded);
        Assert.Equal("active-enrolment", added.Definitions[0].Slug.Value);
        Assert.Equal(ChildCareFixture.ActiveEnrolmentFactId, renamed.Revision.Definitions[0].Id);
        Assert.Equal("child-has-active-enrolment", renamed.Revision.Definitions[0].Slug.Value);
        Assert.Equal(
            ["child-care.active-enrolment"],
            renamed.Revision.Definitions[0].FormerQualifiedNames);
    }

    [Fact]
    public void Sibling_slugs_are_unique()
    {
        var original = ChildCareFixture.EmptyContext();
        var first = ChildCareFixture.ActiveEnrolmentFact();
        var duplicateSlug = new FactDefinition(
            ChildCareFixture.SupportingEvidenceFactId,
            new SemanticName("Supporting evidence is held"),
            first.Slug,
            FactType.Truth);

        var result = CanonicalModel.Apply(
            original,
            new AddDefinition(first),
            new AddDefinition(duplicateSlug));

        Assert.False(result.Succeeded);
        Assert.Equal("model.slug.duplicate", Assert.Single(result.Diagnostics).Code);
        Assert.Same(original, result.Revision);
    }

    [Fact]
    public void Nested_semantic_identities_are_unique_across_the_context()
    {
        var entity = ChildCareFixture.ApplicationEntity();
        var collidingFact = new FactDefinition(
            ChildCareFixture.DraftStageId,
            new SemanticName("Application is draft"),
            new SemanticSlug("application-is-draft"),
            FactType.Truth);

        var result = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(entity),
            new AddDefinition(collidingFact));

        Assert.False(result.Succeeded);
        Assert.Equal("model.identity.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Definition_cannot_reuse_its_bounded_context_identity()
    {
        var collidingFact = new FactDefinition(
            ChildCareFixture.ContextId,
            new SemanticName("Context collision"),
            new SemanticSlug("context-collision"),
            FactType.Truth);

        var result = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(collidingFact));

        Assert.False(result.Succeeded);
        Assert.Equal("model.identity.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Unknown_semantic_definition_kind_is_rejected()
    {
        var unsupported = new UnsupportedDefinition(
            Id("0191f6d4-4ea0-7000-8000-00000000000f"),
            new SemanticName("Unsupported"),
            new SemanticSlug("unsupported"));

        var result = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(unsupported));

        Assert.False(result.Succeeded);
        Assert.Equal("model.definition.unsupported", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Nested_sibling_slugs_are_unique()
    {
        var entity = ChildCareFixture.ApplicationEntity();
        var invalidEntity = entity with
        {
            Lifecycle = entity.Lifecycle with
            {
                Stages = entity.Lifecycle.Stages.Add(
                    new LifecycleStage(
                        Id("0191f6d4-4ea0-7000-8000-00000000000e"),
                        new SemanticName("Another draft stage"),
                        new SemanticSlug("draft")))
            }
        };

        var result = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(invalidEntity));

        Assert.False(result.Succeeded);
        Assert.Equal("model.slug.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Nested_concepts_are_found_by_identity_with_a_derived_qualified_name()
    {
        var revision = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(ChildCareFixture.ApplicationEntity()),
            new AddDefinition(ChildCareFixture.SubmitBehaviour())).Revision;

        var stage = revision.FindConcept(ChildCareFixture.SubmittedStageId);
        var transition = revision.FindConcept(ChildCareFixture.TransitionId);

        Assert.NotNull(stage);
        Assert.Equal(
            "child-care.accs-determination-application.accs-determination-application-lifecycle.submitted",
            stage.QualifiedName);
        Assert.NotNull(transition);
        Assert.Equal(
            "child-care.submit-accs-determination-application.submit-application",
            transition.QualifiedName);
    }

    [Fact]
    public void Nested_concept_rename_preserves_identity_and_former_qualified_name()
    {
        var added = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(ChildCareFixture.ApplicationEntity())).Revision;

        var renamed = CanonicalModel.Apply(
            added,
            new RenameConcept(
                ChildCareFixture.SubmittedStageId,
                new SemanticName("Application submitted"),
                new SemanticSlug("application-submitted")));

        Assert.True(renamed.Succeeded);
        Assert.Equal("submitted", added.FindConcept(ChildCareFixture.SubmittedStageId)?.Slug.Value);
        var stage = renamed.Revision.FindConcept(ChildCareFixture.SubmittedStageId);
        Assert.NotNull(stage);
        Assert.Equal("application-submitted", stage.Slug.Value);
        Assert.Equal(
            ["child-care.accs-determination-application.accs-determination-application-lifecycle.submitted"],
            stage.FormerQualifiedNames);
    }

    [Fact]
    public void Incomplete_authored_collections_are_normalized_to_empty()
    {
        var incompleteRule = new RuleDefinition(
            ChildCareFixture.EligibilityRuleId,
            new SemanticName("Determine ACCS eligibility"),
            new SemanticSlug("determine-accs-eligibility"),
            default,
            default);

        var result = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(incompleteRule));

        Assert.True(result.Succeeded);
        var rule = Assert.IsType<RuleDefinition>(Assert.Single(result.Revision.Definitions));
        Assert.False(rule.InputFacts.IsDefault);
        Assert.False(rule.Conclusions.IsDefault);
        Assert.Empty(rule.InputFacts);
        Assert.Empty(rule.Conclusions);
    }

    [Fact]
    public void Child_care_conformance_observation_matches_the_public_model()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "child-care-accs-model.expected.json");
        var expected = JsonSerializer.Deserialize<ConformanceObservation>(
            File.ReadAllText(fixturePath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(expected);

        var revision = CanonicalModel.Apply(
            ChildCareFixture.EmptyContext(),
            new AddDefinition(ChildCareFixture.ApplicationEntity()),
            new AddDefinition(ChildCareFixture.ActiveEnrolmentFact()),
            new AddDefinition(ChildCareFixture.EligibilityRule()),
            new AddDefinition(ChildCareFixture.SubmitBehaviour())).Revision;

        var actual = expected.Concepts.Select(item =>
        {
            var concept = revision.FindConcept(SemanticId.Parse(item.Id));
            Assert.NotNull(concept);
            return new ConceptObservation(
                concept.Id.ToString(),
                concept.Kind.ToString(),
                concept.QualifiedName,
                concept.OwnerId.ToString());
        });

        Assert.Equal(expected.Concepts, actual);
    }

    private static SemanticId Id(string value) => SemanticId.Parse(value);

    private sealed record UnsupportedDefinition(
        SemanticId Id,
        SemanticName Name,
        SemanticSlug Slug)
        : SemanticDefinition(Id, Name, Slug);

    private sealed record ConformanceObservation(IReadOnlyList<ConceptObservation> Concepts);

    private sealed record ConceptObservation(
        string Id,
        string Kind,
        string QualifiedName,
        string OwnerId);
}

internal static class ChildCareFixture
{
    internal static readonly SemanticId ContextId = Id("0191f6d4-4ea0-7000-8000-000000000001");
    internal static readonly SemanticId ApplicationId = Id("0191f6d4-4ea0-7000-8000-000000000002");
    internal static readonly SemanticId LifecycleId = Id("0191f6d4-4ea0-7000-8000-000000000003");
    internal static readonly SemanticId DraftStageId = Id("0191f6d4-4ea0-7000-8000-000000000004");
    internal static readonly SemanticId SubmittedStageId = Id("0191f6d4-4ea0-7000-8000-000000000005");
    internal static readonly SemanticId ActiveEnrolmentFactId = Id("0191f6d4-4ea0-7000-8000-000000000006");
    internal static readonly SemanticId SupportingEvidenceFactId = Id("0191f6d4-4ea0-7000-8000-000000000007");
    internal static readonly SemanticId EligibilityRuleId = Id("0191f6d4-4ea0-7000-8000-000000000008");
    internal static readonly SemanticId EligibleConclusionId = Id("0191f6d4-4ea0-7000-8000-000000000009");
    internal static readonly SemanticId SubmitBehaviourId = Id("0191f6d4-4ea0-7000-8000-00000000000a");
    internal static readonly SemanticId SubmittedOutcomeId = Id("0191f6d4-4ea0-7000-8000-00000000000b");
    internal static readonly SemanticId RejectedOutcomeId = Id("0191f6d4-4ea0-7000-8000-00000000000c");
    internal static readonly SemanticId TransitionId = Id("0191f6d4-4ea0-7000-8000-00000000000d");

    internal static AuthoredContextRevision EmptyContext() => AuthoredContextRevision.Create(
        ContextId,
        new SemanticName("Child Care"),
        new SemanticSlug("child-care"),
        "1.0.0");

    internal static EntityDefinition ApplicationEntity() => new(
        ApplicationId,
        new SemanticName("ACCS determination application"),
        new SemanticSlug("accs-determination-application"),
        new LifecycleDefinition(
            LifecycleId,
            new SemanticName("ACCS determination application lifecycle"),
            new SemanticSlug("accs-determination-application-lifecycle"),
            [
                new LifecycleStage(DraftStageId, new SemanticName("Draft"), new SemanticSlug("draft")),
                new LifecycleStage(SubmittedStageId, new SemanticName("Submitted"), new SemanticSlug("submitted"))
            ]),
        new SemanticDocumentation(
            Purpose: "Records an application for additional child care subsidy.",
            OwnershipAndIdentity: "Owned by the Child Care bounded context.",
            SemanticContract: "Retains identity throughout its determination lifecycle."));

    internal static FactDefinition ActiveEnrolmentFact() => new(
        ActiveEnrolmentFactId,
        new SemanticName("Active enrolment exists"),
        new SemanticSlug("active-enrolment"),
        FactType.Truth);

    internal static RuleDefinition EligibilityRule() => new(
        EligibilityRuleId,
        new SemanticName("Determine ACCS eligibility"),
        new SemanticSlug("determine-accs-eligibility"),
        [new FactReference(ActiveEnrolmentFactId)],
        [new ConclusionDefinition(
            EligibleConclusionId,
            new SemanticName("Eligible"),
            new SemanticSlug("eligible"))]);

    internal static BehaviourDefinition SubmitBehaviour() => new(
        SubmitBehaviourId,
        new SemanticName("Submit ACCS determination application"),
        new SemanticSlug("submit-accs-determination-application"),
        new EntityReference(ApplicationId),
        [
            new OutcomeDefinition(SubmittedOutcomeId, new SemanticName("Application submitted"), new SemanticSlug("application-submitted")),
            new OutcomeDefinition(RejectedOutcomeId, new SemanticName("Application rejected"), new SemanticSlug("application-rejected"))
        ],
        [],
        [],
        [new TransitionDefinition(
            TransitionId,
            new SemanticName("Submit application"),
            new SemanticSlug("submit-application"),
            new LifecycleReference(LifecycleId),
            new LifecycleStageReference(DraftStageId),
            new LifecycleStageReference(SubmittedStageId),
            new OutcomeReference(SubmittedOutcomeId),
            Documentation: new SemanticDocumentation(
                Purpose: "Moves a draft application to submitted after the behaviour succeeds."))],
        [new RuleBinding(
            new RuleReference(EligibilityRuleId),
            RuleBindingPurpose.Requirement,
            ImmutableDictionary<FactReference, FactReference>.Empty.Add(
                new FactReference(ActiveEnrolmentFactId),
                new FactReference(ActiveEnrolmentFactId)))]);

    private static SemanticId Id(string value) => SemanticId.Parse(value);
}
