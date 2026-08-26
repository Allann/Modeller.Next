using System.Collections.Immutable;
using Modeller.Generation;
using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

/// <summary>
/// Proves the shared canonical projection (expression-tree walking, multi-transition/multi-binding handling)
/// still renders the real Child Care C# templates byte-identical to the checked-in golden output, for the
/// current single-transition/single-binding/flat-AND sample data.
/// </summary>
public sealed class GoldenTemplateOutputTests
{
    [Fact]
    public void Rule_template_renders_the_golden_ACCS_eligibility_rule()
    {
        var revision = AccsRevision(out var rule, out _, out _);
        var provider = new CSharpTemplateGlobalsProvider(revision, "ChildCare", "ChildCare", "net10.0");
        var content = Render(TemplatePath("csharp/domain-project/Rule.cs.sbn"), provider, Context(revision, rule, "rule.cs"));

        Assert.Equal(ReadGolden("Rules/DetermineACCSEligibility.cs"), content);
    }

    [Fact]
    public void Behaviour_template_renders_the_golden_submit_ACCS_determination_application_behaviour()
    {
        var revision = AccsRevision(out _, out var behaviour, out _);
        var provider = new CSharpTemplateGlobalsProvider(revision, "ChildCare", "ChildCare", "net10.0");
        var content = Render(TemplatePath("csharp/domain-project/Behaviour.cs.sbn"), provider, Context(revision, behaviour, "behaviour.cs"));

        Assert.Equal(ReadGolden("Behaviours/SubmitACCSDeterminationApplication.cs"), content);
    }

    [Fact]
    public void CSharp_api_templates_emit_one_stage_type_for_behaviours_that_share_a_lifecycle()
    {
        var revision = AccsRevision(out var rule, out var firstBehaviour, out var entity);
        var lifecycle = entity.Lifecycle!;
        var outcome = new OutcomeDefinition(SemanticId.New(), new SemanticName("Application reviewed"),
            new SemanticSlug("application-reviewed"));
        var transition = new TransitionDefinition(SemanticId.New(), new SemanticName("Review application"),
            new SemanticSlug("review-application"), new LifecycleReference(lifecycle.Id),
            new LifecycleStageReference(lifecycle.Stages[0].Id), new LifecycleStageReference(lifecycle.Stages[1].Id),
            new OutcomeReference(outcome.Id));
        var binding = new RuleBinding(new RuleReference(rule.Id), RuleBindingPurpose.Requirement,
            ImmutableDictionary<FactReference, FactReference>.Empty);
        var secondBehaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Review ACCS application"),
            new SemanticSlug("review-accs-application"), new EntityReference(entity.Id), [outcome], [], [], [transition], [binding]);
        revision = Apply(revision, new AddDefinition(secondBehaviour));
        var provider = new CSharpTemplateGlobalsProvider(revision, "ChildCare", "ChildCare", "net10.0");

        var outputs = new[]
        {
            Render(TemplatePath("csharp/api-project/Entity.cs.sbn"), provider, Context(revision, entity, "entity.cs")),
            Render(TemplatePath("csharp/api-project/Behaviour.cs.sbn"), provider, Context(revision, firstBehaviour, "first.cs")),
            Render(TemplatePath("csharp/api-project/Behaviour.cs.sbn"), provider, Context(revision, secondBehaviour, "second.cs"))
        };

        Assert.Equal(1, outputs.Sum(output => output.Split("public enum ACCSDeterminationApplicationStage", StringSplitOptions.None).Length - 1));
        Assert.Contains("    Draft,", outputs[0], StringComparison.Ordinal);
        Assert.Contains("    Submitted", outputs[0], StringComparison.Ordinal);
    }

    [Fact]
    public void CSharp_api_entity_template_omits_stage_type_when_entity_has_no_lifecycle()
    {
        var revision = AccsRevision(out _, out _, out _);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Provider"),
            new SemanticSlug("provider"), null);
        revision = Apply(revision, new AddDefinition(entity));
        var provider = new CSharpTemplateGlobalsProvider(revision, "ChildCare", "ChildCare", "net10.0");

        var output = Render(TemplatePath("csharp/api-project/Entity.cs.sbn"), provider,
            Context(revision, entity, "provider.cs"));

        Assert.DoesNotContain("public enum ProviderStage", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Python_behaviour_template_renders_an_unconditional_transition_without_a_dangling_and()
    {
        var revision = UnconditionalTransitionRevision(out var behaviour);
        var provider = new PythonTemplateGlobalsProvider(revision, "child_care", "child_care", "3.13");

        var content = Render(TemplatePath("python/api-project/behaviour.py.sbn"), provider,
            Context(revision, behaviour, "behaviour.py"));

        Assert.Contains("if current == ApplicationStage.DRAFT", content, StringComparison.Ordinal);
        Assert.DoesNotContain("and :", content, StringComparison.Ordinal);
    }

    private static string Render(string templatePath, ITemplateGlobalsProvider provider, ArtifactRenderingContext context)
    {
        var content = File.ReadAllText(templatePath);
        var digest = $"sha256:{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)))}";
        var templates = ImmutableDictionary<string, ScribanTemplateSource>.Empty.Add(context.Artifact.Ownership.TemplateId, new ScribanTemplateSource(digest, content));
        var adapter = new ScribanRendererAdapter("scriban", "1.0", templates, globalsProvider: provider);
        var artifact = context.Artifact with { TemplateDigest = digest };
        var plan = context.Plan with { Artifacts = [artifact] };
        var result = adapter.RenderAsync(new ArtifactRenderingContext(plan, artifact), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Empty(result.Diagnostics);
        return result.Content!;
    }

    private static ArtifactRenderingContext Context(AuthoredContextRevision revision, ISemanticConcept definition, string templateId)
    {
        var artifact = new ProposedArtifact(
            0, "definition", "output.cs",
            new ArtifactOwnership("test", "test", "1.0.0", templateId),
            [new PlannedSemanticInput(definition.Slug.Value, revision.Id.ToString(), "sha256:definition")],
            "sha256:template", "sha256:input");
        var plan = new GenerationPlan("1.0", "generated", [artifact], "sha256:plan");
        return new ArtifactRenderingContext(plan, artifact);
    }

    private static AuthoredContextRevision AccsRevision(out RuleDefinition rule, out BehaviourDefinition behaviour, out EntityDefinition entity)
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"), new SemanticSlug("child-care"), "1.0.0");

        var activeEnrolment = new FactDefinition(SemanticId.New(), new SemanticName("Active enrolment exists"), new SemanticSlug("active-enrolment-exists"), FactType.Truth);
        var supportingEvidence = new FactDefinition(SemanticId.New(), new SemanticName("Supporting evidence is held"), new SemanticSlug("supporting-evidence-is-held"), FactType.Truth);
        revision = Apply(revision, new AddDefinition(activeEnrolment));
        revision = Apply(revision, new AddDefinition(supportingEvidence));

        rule = new RuleDefinition(SemanticId.New(), new SemanticName("Determine ACCS eligibility"), new SemanticSlug("determine-accs-eligibility"),
            [new FactReference(activeEnrolment.Id), new FactReference(supportingEvidence.Id)], [],
            new AndExpression([new FactExpression(new FactReference(activeEnrolment.Id)), new FactExpression(new FactReference(supportingEvidence.Id))]));
        revision = Apply(revision, new AddDefinition(rule));

        var draft = new LifecycleStage(SemanticId.New(), new SemanticName("Draft"), new SemanticSlug("draft"));
        var submitted = new LifecycleStage(SemanticId.New(), new SemanticName("Submitted"), new SemanticSlug("submitted"));
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("ACCS determination application lifecycle"),
            new SemanticSlug("accs-determination-application-lifecycle"), [draft, submitted]);
        entity = new EntityDefinition(SemanticId.New(), new SemanticName("ACCS determination application"),
            new SemanticSlug("accs-determination-application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var applicationSubmitted = new OutcomeDefinition(SemanticId.New(), new SemanticName("Application submitted"), new SemanticSlug("application-submitted"));
        var applicationRejected = new OutcomeDefinition(SemanticId.New(), new SemanticName("Application rejected"), new SemanticSlug("application-rejected"));
        var transition = new TransitionDefinition(SemanticId.New(), new SemanticName("Submit application"), new SemanticSlug("submit-application"),
            new LifecycleReference(lifecycle.Id), new LifecycleStageReference(draft.Id), new LifecycleStageReference(submitted.Id),
            new OutcomeReference(applicationSubmitted.Id));

        var binding = new RuleBinding(new RuleReference(rule.Id), RuleBindingPurpose.Requirement, ImmutableDictionary<FactReference, FactReference>.Empty);
        behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Submit ACCS determination application"),
            new SemanticSlug("submit-accs-determination-application"), new EntityReference(entity.Id),
            [applicationSubmitted, applicationRejected], [], [], [transition], [binding]);
        revision = Apply(revision, new AddDefinition(behaviour));

        return revision;
    }

    private static AuthoredContextRevision UnconditionalTransitionRevision(out BehaviourDefinition behaviour)
    {
        var revision = AuthoredContextRevision.Create(SemanticId.New(), new SemanticName("Child Care"),
            new SemanticSlug("child-care"), "1.0.0");
        var draft = new LifecycleStage(SemanticId.New(), new SemanticName("Draft"), new SemanticSlug("draft"));
        var submitted = new LifecycleStage(SemanticId.New(), new SemanticName("Submitted"), new SemanticSlug("submitted"));
        var lifecycle = new LifecycleDefinition(SemanticId.New(), new SemanticName("Application lifecycle"),
            new SemanticSlug("application-lifecycle"), [draft, submitted]);
        var entity = new EntityDefinition(SemanticId.New(), new SemanticName("Application"),
            new SemanticSlug("application"), lifecycle);
        revision = Apply(revision, new AddDefinition(entity));

        var submittedOutcome = new OutcomeDefinition(SemanticId.New(), new SemanticName("Submitted"),
            new SemanticSlug("submitted"));
        var transition = new TransitionDefinition(SemanticId.New(), new SemanticName("Submit"),
            new SemanticSlug("submit"), new LifecycleReference(lifecycle.Id),
            new LifecycleStageReference(draft.Id), new LifecycleStageReference(submitted.Id),
            new OutcomeReference(submittedOutcome.Id));
        behaviour = new BehaviourDefinition(SemanticId.New(), new SemanticName("Submit application"),
            new SemanticSlug("submit-application"), new EntityReference(entity.Id), [submittedOutcome], [], [],
            [transition], []);
        revision = Apply(revision, new AddDefinition(behaviour));
        return revision;
    }

    private static AuthoredContextRevision Apply(AuthoredContextRevision revision, ModelOperation operation)
    {
        var result = CanonicalModel.Apply(revision, operation);
        Assert.True(result.Succeeded, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        return result.Revision;
    }

    private static string TemplatePath(string relative) => Path.Combine(RepositoryRoot(), "samples", "child-care", "templates", relative.Replace('/', Path.DirectorySeparatorChar));
    private static string ReadGolden(string relative) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "samples", "child-care", "expected", "ChildCare", relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Modeller.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
