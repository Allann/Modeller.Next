using System.Collections.Immutable;
using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Modeller.Projections;
using Reqnroll;
using Xunit;

namespace Modeller.Parsing.Acceptance.Features;

[Binding]
public sealed class ContextMapCrossContextDependenciesSteps
{
    private readonly WorkspaceCompilationContext _context;
    private readonly List<ContextSpec> _contexts = [];
    private WorkspaceParseResult? _compileResult;
    private ProjectionResult? _projectionResult;

    public ContextMapCrossContextDependenciesSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            Compile();
            _context.IsSuccess = _compileResult!.IsSuccess;
            _context.FailureSummary = FailureSummary();
        };
    }

    [Given("the workspace is empty")]
    public void GivenTheWorkspaceIsEmpty() => _contexts.Clear();

    [Given("a workspace declaring the bounded context {string}")]
    [Given("the workspace also declares the bounded context {string}")]
    public void GivenAWorkspaceDeclaringTheBoundedContext(string name) => DeclareContext(name);

    [Given("a workspace declaring two bounded contexts both named {string}")]
    public void GivenAWorkspaceDeclaringTwoBoundedContextsBothNamed(string name)
    {
        DeclareContext(name);
        DeclareContext(name);
    }

    [Given("a workspace declaring the bounded context {string} that exports the fact {string}")]
    public void GivenAWorkspaceDeclaringABoundedContextThatExportsAFact(string contextName, string factName) =>
        DeclareContext(contextName).Facts.Add((factName, true));

    [Given("a workspace declaring the bounded context {string} with the fact {string} that is not exported")]
    public void GivenAWorkspaceDeclaringABoundedContextWithAFactThatIsNotExported(string contextName, string factName) =>
        DeclareContext(contextName).Facts.Add((factName, false));

    [Given("the workspace declares the bounded context {string} that imports the fact {string} from the bounded context {string}")]
    [Given("a workspace declaring the bounded context {string} that imports the fact {string} from the bounded context {string}")]
    public void GivenTheWorkspaceDeclaresABoundedContextThatImportsAFact(string contextName, string factName, string fromContext) =>
        DeclareContext(contextName).Imports.Add((factName, fromContext));

    [Given("no bounded context named {string} is declared in the workspace")]
    public void GivenNoBoundedContextIsDeclaredInTheWorkspace(string name) =>
        Assert.DoesNotContain(_contexts, context => context.Name == name);

    [Given("a compiled workspace where the bounded context {string} imports the fact {string} from the bounded context {string}")]
    public void GivenACompiledWorkspaceWhereABoundedContextImportsAFact(string importingContext, string factName, string exportingContext)
    {
        DeclareContext(exportingContext).Facts.Add((factName, true));
        DeclareContext(importingContext).Imports.Add((factName, exportingContext));
        Compile();
        Assert.True(_compileResult!.IsSuccess, FailureSummary());
    }

    [Given("a compiled workspace declaring only the bounded context {string}")]
    public void GivenACompiledWorkspaceDeclaringOnlyTheBoundedContext(string name)
    {
        DeclareContext(name);
        Compile();
        Assert.True(_compileResult!.IsSuccess, FailureSummary());
    }

    [When("the context map view is projected rooted at the bounded context {string}")]
    public void WhenTheContextMapViewIsProjectedRootedAtTheBoundedContext(string rootContextName)
    {
        var root = _compileResult!.Contexts.Single(context => context.AuthoredRevision.Name.Value == rootContextName);
        var view = new ViewDefinition("context-map", 1, ViewKind.ContextMap, [root.AuthoredRevision.Id]);
        _projectionResult = DiagramProjector.ProjectContextMap(
            [.. _compileResult.Contexts.Select(context => context.AuthoredRevision)], _compileResult.Dependencies, view);
    }

    [Then("the compiled model contains the bounded context {string}")]
    public void ThenTheCompiledModelContainsTheBoundedContext(string name) =>
        Assert.Contains(_compileResult!.Contexts, context => context.AuthoredRevision.Name.Value == name);

    [Then("the compiled model records {string} as depending on {string} for the fact {string}")]
    public void ThenTheCompiledModelRecordsADependency(string importingContext, string exportingContext, string factName)
    {
        var importing = _compileResult!.Contexts.Single(context => context.AuthoredRevision.Name.Value == importingContext);
        var exporting = _compileResult.Contexts.Single(context => context.AuthoredRevision.Name.Value == exportingContext);
        Assert.Contains(_compileResult.Dependencies, dependency =>
            dependency.ImportingContextId == importing.AuthoredRevision.Id &&
            dependency.ExportingContextId == exporting.AuthoredRevision.Id &&
            dependency.FactName.Value == factName);
    }

    [Then("compilation fails with a diagnostic explaining the fact is not exported")]
    public void ThenCompilationFailsBecauseTheFactIsNotExported() => AssertFailureCode("rml.import.not-exported");

    [Then("compilation fails with a diagnostic explaining the bounded context cannot be resolved")]
    public void ThenCompilationFailsBecauseTheBoundedContextCannotBeResolved() => AssertFailureCode("rml.import.context-unresolved");

    [Then("compilation fails with a diagnostic explaining bounded context names must be unique")]
    public void ThenCompilationFailsBecauseNamesMustBeUnique() => AssertFailureCode("rml.name.duplicate");

    [Then("the context map shows a node for the bounded context {string}")]
    public void ThenTheContextMapShowsANodeForTheBoundedContext(string name) =>
        Assert.Contains(_projectionResult!.Graph!.Nodes, node => node.Role == "context" && node.Label == name);

    [Then("the context map shows a dependency edge from {string} to {string} for the fact {string}")]
    public void ThenTheContextMapShowsADependencyEdge(string from, string to, string factName)
    {
        var fromId = _compileResult!.Contexts.Single(context => context.AuthoredRevision.Name.Value == from).AuthoredRevision.Id;
        var toId = _compileResult.Contexts.Single(context => context.AuthoredRevision.Name.Value == to).AuthoredRevision.Id;
        var fromNode = _projectionResult!.Graph!.Nodes.Single(node => node.SemanticIds.Contains(fromId));
        var toNode = _projectionResult.Graph.Nodes.Single(node => node.SemanticIds.Contains(toId));
        Assert.Contains(_projectionResult.Graph.Edges, edge =>
            edge.SourceId == fromNode.Id && edge.TargetId == toNode.Id && edge.Label == factName);
    }

    [Then("the context map shows no dependency edges")]
    public void ThenTheContextMapShowsNoDependencyEdges() => Assert.Empty(_projectionResult!.Graph!.Edges);

    private ContextSpec DeclareContext(string name)
    {
        var spec = new ContextSpec(name);
        _contexts.Add(spec);
        return spec;
    }

    private void Compile()
    {
        var source = new SourceDocument("workspace.rml", BuildSource());
        _compileResult = RmlCompiler.CompileWorkspace([source], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
    }

    private string BuildSource()
    {
        var source = new StringBuilder().AppendLine("rml 1.0");
        foreach (var context in _contexts)
        {
            source.AppendLine($"context {context.Name}");
            source.AppendLine("  version 1.0.0");
            foreach (var (fact, from) in context.Imports)
            {
                source.AppendLine($"  import \"{fact}\"");
                source.AppendLine($"    from \"{from}\"");
                source.AppendLine("  end");
            }

            source.AppendLine("end");
            foreach (var (factName, exported) in context.Facts)
            {
                source.AppendLine($"fact {factName}");
                source.AppendLine("  type truth");
                if (exported) source.AppendLine("  export");
                source.AppendLine("end");
            }
        }

        return source.ToString();
    }

    private void AssertFailureCode(string code)
    {
        Assert.False(_compileResult!.IsSuccess);
        Assert.Contains(_compileResult.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private string FailureSummary() =>
        string.Join("; ", _compileResult!.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

    private sealed class ContextSpec(string name)
    {
        public string Name { get; } = name;
        public List<(string Fact, bool Exported)> Facts { get; } = [];
        public List<(string Fact, string FromContext)> Imports { get; } = [];
    }
}
