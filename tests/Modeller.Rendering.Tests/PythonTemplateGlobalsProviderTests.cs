using System.Collections.Immutable;
using Modeller.Generation;
using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class PythonTemplateGlobalsProviderTests
{
    [Fact]
    public void Entity_projection_snake_cases_fields_and_resolves_every_property_and_relationship_shape()
    {
        var revision = ChildCareRevision(out var status, out var booking);
        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");

        var globals = provider.GetGlobals(Context(revision, booking));

        var definition = Assert.IsAssignableFrom<object>(globals["definition"]);
        Assert.Equal("Booking", Get<string>(definition, "name"));
        Assert.Equal("booking", Get<string>(definition, "module_name"));

        var properties = Get<System.Collections.IEnumerable>(definition, "properties").Cast<object>().ToArray();
        Assert.Contains(properties, p => Get<string>(p, "name") == "status" && Get<string>(p, "type") == "BookingStatus" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "note" && Get<string>(p, "type") == "str" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "grace_period" && Get<string>(p, "type") == "int" && Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "room" && Get<string>(p, "type") == "UUID" && !Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "absence" && Get<string>(p, "type") == "UUID" && Get<bool>(p, "nullable"));
        Assert.Contains(properties, p => Get<string>(p, "name") == "attendances" && Get<string>(p, "type") == "list[UUID]" && !Get<bool>(p, "nullable"));

        var imports = Get<System.Collections.IEnumerable>(definition, "imports").Cast<string>().ToArray();
        Assert.Contains("from ..enumerations.booking_status import BookingStatus", imports);
    }

    [Fact]
    public void Behaviour_projection_supports_multiple_transitions_each_with_its_own_guard_rule()
    {
        var (revision, behaviour) = MultiTransitionBehaviour();
        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        var transitions = Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>().ToArray();
        Assert.Equal(2, transitions.Length);
        Assert.Equal("first_guard(facts)", Get<string>(transitions[0], "guard"));
        Assert.Equal("second_guard(facts)", Get<string>(transitions[1], "guard"));
    }

    [Fact]
    public void Rule_projection_walks_nested_and_expressions_instead_of_flattening_input_facts()
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");
        var factA = new FactDefinition(SemanticId.New(), new SemanticName("Fact A"), new SemanticSlug("fact-a"), FactType.Truth);
        var factB = new FactDefinition(SemanticId.New(), new SemanticName("Fact B"), new SemanticSlug("fact-b"), FactType.Truth);
        var factC = new FactDefinition(SemanticId.New(), new SemanticName("Fact C"), new SemanticSlug("fact-c"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(factA));
        revision = Apply(revision, new AddDefinition(factB));
        revision = Apply(revision, new AddDefinition(factC));

        var rule = new RuleDefinition(SemanticId.New(), new SemanticName("Determine Eligibility"), new SemanticSlug("determine-eligibility"),
            [new FactReference(factA.Id), new FactReference(factB.Id), new FactReference(factC.Id)], [],
            new AndExpression([
                new FactExpression(new FactReference(factA.Id)),
                new AndExpression([new FactExpression(new FactReference(factB.Id)), new FactExpression(new FactReference(factC.Id))])
            ]));
        revision = Apply(revision, new AddDefinition(rule));

        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");
        var globals = provider.GetGlobals(Context(revision, rule));
        var definition = globals["definition"]!;

        var terms = Get<System.Collections.IEnumerable>(definition, "expression_terms").Cast<string>().ToArray();
        Assert.Equal(["facts.fact_a", "(facts.fact_b and facts.fact_c)"], terms);
    }

    [Fact]
    public void Behaviour_projection_supports_zero_transitions_when_a_rule_binding_still_exists()
    {
        var (revision, behaviour) = ZeroTransitionBehaviour(withBinding: true);
        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        Assert.Empty(Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>());
        Assert.Equal("OnlyRuleFacts", Get<string>(definition, "facts_type"));
    }

    [Fact]
    public void Behaviour_projection_renders_a_zero_transition_unguarded_behaviour_with_an_object_facts_type()
    {
        var (revision, behaviour) = ZeroTransitionBehaviour(withBinding: false);
        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        Assert.Empty(Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>());
        Assert.Equal("object", Get<string>(definition, "facts_type"));
        Assert.Null(Get<string?>(definition, "facts_module_name"));
        Assert.Empty(Get<System.Collections.IEnumerable>(definition, "imports").Cast<object>());
    }

    [Fact]
    public void Behaviour_projection_imports_every_distinct_guard_rule_module_not_just_the_primary_rule()
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");
        var factA = new FactDefinition(SemanticId.New(), new SemanticName("Fact A"), new SemanticSlug("fact-a"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(factA));

        var firstGuard = new RuleDefinition(SemanticId.New(), new SemanticName("Determine first guard"), new SemanticSlug("first-guard"),
            [new FactReference(factA.Id)], [], new FactExpression(new FactReference(factA.Id)));
        var secondGuard = new RuleDefinition(SemanticId.New(), new SemanticName("Determine second guard"), new SemanticSlug("second-guard"),
            [new FactReference(factA.Id)], [], new FactExpression(new FactReference(factA.Id)));
        revision = Apply(revision, new AddDefinition(firstGuard));
        revision = Apply(revision, new AddDefinition(secondGuard));

        var draft = new LifecycleStage(SemanticId.New(), new SemanticName("Draft"), new SemanticSlug("draft"));
        var submitted = new LifecycleStage(SemanticId.New(), new SemanticName("Submitted"), new SemanticSlug("submitted"));
        var approved = new LifecycleStage(SemanticId.New(), new SemanticName("Approved"), new SemanticSlug("approved"));
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("Application lifecycle"), new SemanticSlug("application-lifecycle"),
            [draft, submitted, approved]);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Application"), new SemanticSlug("application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var outcome = new OutcomeDefinition(SemanticId.New(), new SemanticName("Advanced"), new SemanticSlug("advanced"));
        var firstTransition = new TransitionDefinition(SemanticId.New(), new SemanticName("Submit"), new SemanticSlug("submit"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(draft.Id), new LifecycleStageReference(submitted.Id),
            new OutcomeReference(outcome.Id), new RuleReference(firstGuard.Id));
        var secondTransition = new TransitionDefinition(SemanticId.New(), new SemanticName("Approve"), new SemanticSlug("approve"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(submitted.Id), new LifecycleStageReference(approved.Id),
            new OutcomeReference(outcome.Id), new RuleReference(secondGuard.Id));
        var behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Progress application"), new SemanticSlug("progress-application"),
            new EntityReference(entity.Id), [outcome], [], [], [firstTransition, secondTransition], []);
        revision = Apply(revision, new AddDefinition(behaviour));

        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");
        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        var imports = Get<System.Collections.IEnumerable>(definition, "imports").Cast<object>().ToArray();
        Assert.Equal(2, imports.Length);
        Assert.Contains(imports, item => Get<string>(item, "module_name") == "determine_first_guard" &&
            Get<string>(item, "symbols") == "FirstGuardFacts, determine_first_guard");
        Assert.Contains(imports, item => Get<string>(item, "module_name") == "determine_second_guard" &&
            Get<string>(item, "symbols") == "determine_second_guard");
        Assert.Equal("determine_first_guard", Get<string>(definition, "facts_module_name"));
    }

    [Fact]
    public void Rule_projection_carries_canonical_conclusions()
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");
        var factA = new FactDefinition(SemanticId.New(), new SemanticName("Fact A"), new SemanticSlug("fact-a"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(factA));

        var rule = new RuleDefinition(SemanticId.New(), new SemanticName("Determine Eligibility"), new SemanticSlug("determine-eligibility"),
            [new FactReference(factA.Id)],
            [new ConclusionDefinition(SemanticId.New(), new SemanticName("Eligible"), new SemanticSlug("eligible")),
             new ConclusionDefinition(SemanticId.New(), new SemanticName("Requires review"), new SemanticSlug("requires-review"))],
            new FactExpression(new FactReference(factA.Id)));
        revision = Apply(revision, new AddDefinition(rule));

        var provider = new PythonTemplateGlobalsProvider(revision, "pkg", "Proj", "3.13");
        var globals = provider.GetGlobals(Context(revision, rule));
        var definition = globals["definition"]!;

        var conclusions = Get<System.Collections.IEnumerable>(definition, "conclusions").Cast<object>()
            .Select(item => Get<string>(item, "name")).ToArray();
        Assert.Equal(["ELIGIBLE", "REQUIRES_REVIEW"], conclusions);
    }

    private static (AuthoredContextRevision Revision, BehaviourDefinition Behaviour) ZeroTransitionBehaviour(bool withBinding)
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");
        var factA = new FactDefinition(SemanticId.New(), new SemanticName("Fact A"), new SemanticSlug("fact-a"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(factA));

        var onlyRule = new RuleDefinition(SemanticId.New(), new SemanticName("Only rule"), new SemanticSlug("only-rule"),
            [new FactReference(factA.Id)], [], new FactExpression(new FactReference(factA.Id)));
        revision = Apply(revision, new AddDefinition(onlyRule));

        var draft = new LifecycleStage(SemanticId.New(), new SemanticName("Draft"), new SemanticSlug("draft"));
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("Application lifecycle"), new SemanticSlug("application-lifecycle"), [draft]);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Application"), new SemanticSlug("application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var bindings = withBinding
            ? (ImmutableArray<RuleBinding>)[new RuleBinding(new RuleReference(onlyRule.Id), RuleBindingPurpose.Invariant, ImmutableDictionary<FactReference, FactReference>.Empty)]
            : [];
        var behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Record decision"), new SemanticSlug("record-decision"),
            new EntityReference(entity.Id), [], [], [], [], bindings);
        revision = Apply(revision, new AddDefinition(behaviour));
        return (revision, behaviour);
    }

    private static (AuthoredContextRevision Revision, BehaviourDefinition Behaviour) MultiTransitionBehaviour()
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");
        var factA = new FactDefinition(SemanticId.New(), new SemanticName("Fact A"), new SemanticSlug("fact-a"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(factA));

        var firstGuard = new RuleDefinition(SemanticId.New(), new SemanticName("First guard"), new SemanticSlug("first-guard"),
            [new FactReference(factA.Id)], [], new FactExpression(new FactReference(factA.Id)));
        var secondGuard = new RuleDefinition(SemanticId.New(), new SemanticName("Second guard"), new SemanticSlug("second-guard"),
            [new FactReference(factA.Id)], [], new FactExpression(new FactReference(factA.Id)));
        revision = Apply(revision, new AddDefinition(firstGuard));
        revision = Apply(revision, new AddDefinition(secondGuard));

        var draft = new LifecycleStage(SemanticId.New(), new SemanticName("Draft"), new SemanticSlug("draft"));
        var submitted = new LifecycleStage(SemanticId.New(), new SemanticName("Submitted"), new SemanticSlug("submitted"));
        var approved = new LifecycleStage(SemanticId.New(), new SemanticName("Approved"), new SemanticSlug("approved"));
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("Application lifecycle"), new SemanticSlug("application-lifecycle"),
            [draft, submitted, approved]);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Application"), new SemanticSlug("application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var outcome = new OutcomeDefinition(SemanticId.New(), new SemanticName("Advanced"), new SemanticSlug("advanced"));
        var firstTransition = new TransitionDefinition(SemanticId.New(), new SemanticName("Submit"), new SemanticSlug("submit"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(draft.Id), new LifecycleStageReference(submitted.Id),
            new OutcomeReference(outcome.Id), new RuleReference(firstGuard.Id));
        var secondTransition = new TransitionDefinition(SemanticId.New(), new SemanticName("Approve"), new SemanticSlug("approve"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(submitted.Id), new LifecycleStageReference(approved.Id),
            new OutcomeReference(outcome.Id), new RuleReference(secondGuard.Id));

        var behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Progress application"), new SemanticSlug("progress-application"),
            new EntityReference(entity.Id), [outcome], [], [], [firstTransition, secondTransition], []);
        revision = Apply(revision, new AddDefinition(behaviour));
        return (revision, behaviour);
    }

    private static AuthoredContextRevision Apply(AuthoredContextRevision revision, ModelOperation operation)
    {
        var result = CanonicalModel.Apply(revision, operation);
        Assert.True(result.Succeeded, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        return result.Revision;
    }

    private static ArtifactRenderingContext Context(AuthoredContextRevision revision, ISemanticConcept entity)
    {
        var artifact = new ProposedArtifact(
            0, "entity:booking", "entities/booking.py",
            new ArtifactOwnership("test", "test", "1.0.0", "entity"),
            [new PlannedSemanticInput(entity.Slug.Value, revision.Id.ToString(), "sha256:booking")],
            "sha256:template", "sha256:input");
        var plan = new GenerationPlan("1.0", "generated", [artifact], "sha256:plan");
        return new ArtifactRenderingContext(plan, artifact);
    }

    private static AuthoredContextRevision ChildCareRevision(out EnumerationDefinition status, out EntityDefinition booking)
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");

        status = new EnumerationDefinition(SemanticId.New(), new SemanticName("Booking status"), new SemanticSlug("booking-status"),
            [new EnumerationMember(SemanticId.New(), new SemanticName("Active"), new SemanticSlug("active"), 1)]);
        var afterEnum = CanonicalModel.Apply(revision, new AddDefinition(status));
        Assert.True(afterEnum.Succeeded);

        var room = new EntityDefinition(SemanticId.New(), new SemanticName("Room"), new SemanticSlug("room"), Lifecycle: null);
        var afterRoom = CanonicalModel.Apply(afterEnum.Revision, new AddDefinition(room));
        Assert.True(afterRoom.Succeeded);

        var absence = new EntityDefinition(SemanticId.New(), new SemanticName("Absence"), new SemanticSlug("absence"), Lifecycle: null);
        var afterAbsence = CanonicalModel.Apply(afterRoom.Revision, new AddDefinition(absence));
        Assert.True(afterAbsence.Succeeded);

        var attendance = new EntityDefinition(SemanticId.New(), new SemanticName("Attendance"), new SemanticSlug("attendance"), Lifecycle: null);
        var afterAttendance = CanonicalModel.Apply(afterAbsence.Revision, new AddDefinition(attendance));
        Assert.True(afterAttendance.Succeeded);

        booking = new EntityDefinition(SemanticId.New(), new SemanticName("Booking"), new SemanticSlug("booking"), Lifecycle: null)
        {
            Fields =
            [
                new FieldDefinition(SemanticId.New(), new SemanticName("Status"), new SemanticSlug("status"), new EnumerationDataType(status.Id)),
                new FieldDefinition(SemanticId.New(), new SemanticName("Note"), new SemanticSlug("note"), new StringDataType(), IsOptional: true),
                new FieldDefinition(SemanticId.New(), new SemanticName("Grace period"), new SemanticSlug("grace-period"), new Int32DataType(), IsOptional: true)
            ],
            Relationships =
            [
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Room"), new SemanticSlug("room"), room.Id, RelationshipCardinality.One),
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Absence"), new SemanticSlug("absence"), absence.Id, RelationshipCardinality.One, IsOptional: true),
                new RelationshipDefinition(SemanticId.New(), new SemanticName("Attendances"), new SemanticSlug("attendances"), attendance.Id, RelationshipCardinality.Many)
            ]
        };
        var afterBooking = CanonicalModel.Apply(afterAttendance.Revision, new AddDefinition(booking));
        Assert.True(afterBooking.Succeeded);

        return afterBooking.Revision;
    }

    private static T Get<T>(object source, string property)
    {
        var value = source.GetType().GetProperty(property)!.GetValue(source);
        return (T)value!;
    }
}
