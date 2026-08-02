using Modeller.Generation;
using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class CSharpTemplateGlobalsProviderTests
{
    [Fact]
    public void Behaviour_projection_supports_multiple_transitions_each_with_its_own_guard_rule()
    {
        var (revision, behaviour) = MultiTransitionBehaviour();
        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        var transitions = Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>().ToArray();
        Assert.Equal(2, transitions.Length);
        Assert.Equal("Draft", Get<string>(transitions[0], "source_stage"));
        Assert.Equal("Submitted", Get<string>(transitions[0], "target_stage"));
        Assert.Equal("FirstGuard.Determine(facts)", Get<string>(transitions[0], "guard"));
        Assert.Equal("Submitted", Get<string>(transitions[1], "source_stage"));
        Assert.Equal("Approved", Get<string>(transitions[1], "target_stage"));
        Assert.Equal("SecondGuard.Determine(facts)", Get<string>(transitions[1], "guard"));
    }

    [Fact]
    public void Behaviour_projection_supports_multiple_rule_bindings_marked_as_transition_guards()
    {
        var (revision, behaviour) = MultiBindingBehaviour();
        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        var transitions = Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>().ToArray();
        var transition = Assert.Single(transitions);
        Assert.Equal("FirstGuard.Determine(facts) && SecondGuard.Determine(facts)", Get<string>(transition, "guard"));
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

        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");
        var globals = provider.GetGlobals(Context(revision, rule));
        var definition = globals["definition"]!;

        var terms = Get<System.Collections.IEnumerable>(definition, "expression_terms").Cast<string>().ToArray();
        Assert.Equal(["facts.FactA", "(facts.FactB && facts.FactC)"], terms);
    }

    [Fact]
    public void Behaviour_projection_supports_zero_transitions_when_a_rule_binding_still_exists()
    {
        var (revision, behaviour) = ZeroTransitionBehaviour(withBinding: true);
        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        Assert.Empty(Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>());
        Assert.Equal("OnlyRuleFacts", Get<string>(definition, "facts_type"));
    }

    [Fact]
    public void Behaviour_projection_renders_a_zero_transition_unguarded_behaviour_with_an_object_facts_type()
    {
        var (revision, behaviour) = ZeroTransitionBehaviour(withBinding: false);
        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");

        var globals = provider.GetGlobals(Context(revision, behaviour));
        var definition = globals["definition"]!;

        Assert.Empty(Get<System.Collections.IEnumerable>(definition, "transitions").Cast<object>());
        Assert.Equal("object", Get<string>(definition, "facts_type"));
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

        var provider = new CSharpTemplateGlobalsProvider(revision, "ns", "Proj", "net10.0");
        var globals = provider.GetGlobals(Context(revision, rule));
        var definition = globals["definition"]!;

        var conclusions = Get<System.Collections.IEnumerable>(definition, "conclusions").Cast<object>()
            .Select(item => Get<string>(item, "name")).ToArray();
        Assert.Equal(["Eligible", "RequiresReview"], conclusions);
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
            ? (System.Collections.Immutable.ImmutableArray<RuleBinding>)
                [new RuleBinding(new RuleReference(onlyRule.Id), RuleBindingPurpose.Invariant, System.Collections.Immutable.ImmutableDictionary<FactReference, FactReference>.Empty)]
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

    private static (AuthoredContextRevision Revision, BehaviourDefinition Behaviour) MultiBindingBehaviour()
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
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("Application lifecycle"), new SemanticSlug("application-lifecycle"),
            [draft, submitted]);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Application"), new SemanticSlug("application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var outcome = new OutcomeDefinition(SemanticId.New(), new SemanticName("Advanced"), new SemanticSlug("advanced"));
        var transition = new TransitionDefinition(SemanticId.New(), new SemanticName("Submit"), new SemanticSlug("submit"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(draft.Id), new LifecycleStageReference(submitted.Id),
            new OutcomeReference(outcome.Id));

        var bindings = new[]
        {
            new RuleBinding(new RuleReference(firstGuard.Id), RuleBindingPurpose.TransitionGuard, System.Collections.Immutable.ImmutableDictionary<FactReference, FactReference>.Empty),
            new RuleBinding(new RuleReference(secondGuard.Id), RuleBindingPurpose.TransitionGuard, System.Collections.Immutable.ImmutableDictionary<FactReference, FactReference>.Empty)
        };
        var behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Progress application"), new SemanticSlug("progress-application"),
            new EntityReference(entity.Id), [outcome], [], [], [transition], [.. bindings]);
        revision = Apply(revision, new AddDefinition(behaviour));
        return (revision, behaviour);
    }

    private static AuthoredContextRevision Apply(AuthoredContextRevision revision, ModelOperation operation)
    {
        var result = CanonicalModel.Apply(revision, operation);
        Assert.True(result.Succeeded, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        return result.Revision;
    }

    private static ArtifactRenderingContext Context(AuthoredContextRevision revision, ISemanticConcept definition)
    {
        var artifact = new ProposedArtifact(
            0, "definition", "output.cs",
            new ArtifactOwnership("test", "test", "1.0.0", "template"),
            [new PlannedSemanticInput(definition.Slug.Value, revision.Id.ToString(), "sha256:definition")],
            "sha256:template", "sha256:input");
        var plan = new GenerationPlan("1.0", "generated", [artifact], "sha256:plan");
        return new ArtifactRenderingContext(plan, artifact);
    }

    private static T Get<T>(object source, string property)
    {
        var value = source.GetType().GetProperty(property)!.GetValue(source);
        return (T)value!;
    }
}
