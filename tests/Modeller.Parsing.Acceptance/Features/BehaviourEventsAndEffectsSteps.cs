using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Modeller.Projections;
using Reqnroll;
using Xunit;

namespace Modeller.Parsing.Acceptance.Features;

[Binding]
public sealed class BehaviourEventsAndEffectsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string _contextName = "";
    private string _entityName = "";
    private readonly List<BehaviourSpec> _behaviours = [];
    private ParseResult? _compileResult;
    private ProjectionResult? _projectionResult;

    public BehaviourEventsAndEffectsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            Compile();
            _context.IsSuccess = _compileResult!.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a workspace declaring the context {string}, the entity {string}, and the behaviour {string} for {string}, which publishes the event {string}")]
    public void GivenAWorkspaceWithABehaviourThatPublishesAnEvent(string contextName, string entityName, string behaviourName, string forEntity, string eventName)
    {
        DeclareWorkspace(contextName, entityName);
        DeclareBehaviour(behaviourName, forEntity).Events.Add(eventName);
    }

    [Given("a workspace declaring the context {string}, the entity {string}, the behaviour {string} for {string} which publishes the event {string}, and the behaviour {string} for {string} which publishes the event {string}")]
    public void GivenAWorkspaceWithTwoBehavioursThatEachPublishAnEvent(
        string contextName, string entityName,
        string firstBehaviour, string firstFor, string firstEvent,
        string secondBehaviour, string secondFor, string secondEvent)
    {
        DeclareWorkspace(contextName, entityName);
        DeclareBehaviour(firstBehaviour, firstFor).Events.Add(firstEvent);
        DeclareBehaviour(secondBehaviour, secondFor).Events.Add(secondEvent);
    }

    [Given("a workspace declaring the context {string}, the entity {string}, and the behaviour {string} for {string}, which causes the effect {string}")]
    public void GivenAWorkspaceWithABehaviourThatCausesAnEffect(string contextName, string entityName, string behaviourName, string forEntity, string effectName)
    {
        DeclareWorkspace(contextName, entityName);
        DeclareBehaviour(behaviourName, forEntity).Effects.Add(effectName);
    }

    [Given("a workspace declaring the context {string}, the entity {string}, and the behaviour {string} for {string}, which publishes the event {string} and causes the effect {string}")]
    public void GivenAWorkspaceWithABehaviourThatPublishesAnEventAndCausesAnEffect(string contextName, string entityName, string behaviourName, string forEntity, string eventName, string effectName)
    {
        DeclareWorkspace(contextName, entityName);
        var behaviour = DeclareBehaviour(behaviourName, forEntity);
        behaviour.Events.Add(eventName);
        behaviour.Effects.Add(effectName);
    }

    [Given("a workspace declaring the context {string}, the entity {string}, and the behaviour {string} for {string}, with no published event and no effect")]
    public void GivenAWorkspaceWithABehaviourThatDeclaresNeither(string contextName, string entityName, string behaviourName, string forEntity)
    {
        DeclareWorkspace(contextName, entityName);
        DeclareBehaviour(behaviourName, forEntity);
    }

    [When("the causality and event-flow view is projected rooted at the context {string}")]
    public void WhenTheCausalityAndEventFlowViewIsProjectedRootedAtTheContext(string contextName)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        Assert.Equal(contextName, revision.Name.Value);
        _projectionResult = DiagramProjector.Project(revision, new("event-flow", 1, ViewKind.CausalityAndEventFlow, [revision.Id]));
    }

    [When("the behaviour map view is projected rooted at the entity {string}")]
    public void WhenTheBehaviourMapViewIsProjectedRootedAtTheEntity(string entityName)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        var entity = revision.Definitions.OfType<EntityDefinition>().Single(item => item.Name.Value == entityName);
        _projectionResult = DiagramProjector.Project(revision, new("behaviour-map", 1, ViewKind.BehaviourMap, [entity.Id]));
    }

    [Then("the view shows a node for the behaviour {string}")]
    public void ThenTheViewShowsANodeForTheBehaviour(string name) =>
        Assert.Contains(_projectionResult!.Graph!.Nodes, node => node.Role == "behaviour" && node.Label == name);

    [Then("the view shows a node for the event {string}")]
    public void ThenTheViewShowsANodeForTheEvent(string name) =>
        Assert.Contains(_projectionResult!.Graph!.Nodes, node => node.Role == "event" && node.Label == name);

    [Then("the view shows a node for the effect {string}")]
    public void ThenTheViewShowsANodeForTheEffect(string name) =>
        Assert.Contains(_projectionResult!.Graph!.Nodes, node => node.Role == "effect" && node.Label == name);

    [Then("the view shows a \"publishes\" relationship from {string} to {string}")]
    public void ThenTheViewShowsAPublishesRelationshipFromTo(string from, string to)
    {
        var fromNode = _projectionResult!.Graph!.Nodes.Single(node => node.Role == "behaviour" && node.Label == from);
        var toNode = _projectionResult.Graph.Nodes.Single(node => node.Role == "event" && node.Label == to);
        Assert.Contains(_projectionResult.Graph.Edges, edge => edge.Role == "publishes" && edge.SourceId == fromNode.Id && edge.TargetId == toNode.Id);
    }

    [Then("the view shows no nodes and no relationships")]
    public void ThenTheViewShowsNoNodesAndNoRelationships()
    {
        Assert.Empty(_projectionResult!.Graph!.Nodes);
        Assert.Empty(_projectionResult.Graph.Edges);
    }

    private void Compile() =>
        _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

    private void DeclareWorkspace(string contextName, string entityName)
    {
        _contextName = contextName;
        _entityName = entityName;
    }

    private BehaviourSpec DeclareBehaviour(string name, string forEntity)
    {
        var spec = new BehaviourSpec(name, forEntity);
        _behaviours.Add(spec);
        return spec;
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine($"context {_contextName}")
            .AppendLine("  version 1.0.0")
            .AppendLine("end")
            .AppendLine($"entity {_entityName}")
            .AppendLine("end");
        foreach (var behaviour in _behaviours)
        {
            source.AppendLine($"behaviour {behaviour.Name}");
            source.AppendLine($"  for \"{behaviour.ForEntity}\"");
            foreach (var eventName in behaviour.Events)
            {
                source.AppendLine($"  event {eventName}");
                source.AppendLine("  end");
            }
            foreach (var effectName in behaviour.Effects)
            {
                source.AppendLine($"  effect {effectName}");
                source.AppendLine("  end");
            }
            source.AppendLine("end");
        }
        return source.ToString();
    }

    private sealed class BehaviourSpec(string name, string forEntity)
    {
        public string Name { get; } = name;
        public string ForEntity { get; } = forEntity;
        public List<string> Events { get; } = [];
        public List<string> Effects { get; } = [];
    }
}
