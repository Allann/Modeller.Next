using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Parsing.Acceptance.Features;

/// <summary>Steps for EntityOwnership.feature: an entity's optional 'owner "&lt;EntityName&gt;"'
/// clause, the DDD aggregate-root / consistency-boundary fact. Each Given step matches one of the
/// feature's literal sentence shapes and feeds the entities it names (and, where the sentence says
/// so, an owner) into <see cref="_entities"/>; <see cref="Compile"/> then renders that list as RML
/// source and runs it through <see cref="RmlCompiler.CompileWorkspace"/>. A step's "where no entity
/// named ... is declared" clause is deliberately not turned into an action — the entity it names is
/// simply never added to <see cref="_entities"/>, matching the scenario's intent that it stays
/// undeclared.</summary>
[Binding]
public sealed class EntityOwnershipSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string _contextName = "";
    private readonly List<(string Name, string? Owner)> _entities = [];
    private WorkspaceParseResult? _compileResult;

    public EntityOwnershipSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            Compile();
            _context.IsSuccess = _compileResult!.IsSuccess;
            _context.FailureSummary = FailureSummary();
        };
    }

    [Given("a workspace declaring the context {string}, the entity {string}, and the entity {string} owned by {string}")]
    public void GivenAWorkspaceWithTwoEntitiesTheSecondOwnedByTheFirst(string contextName, string firstEntity, string secondEntity, string owner)
    {
        SetContext(contextName);
        DeclareEntity(firstEntity);
        DeclareEntity(secondEntity, owner);
    }

    [Given("a workspace declaring the context {string} and the entity {string} with no declared owner")]
    public void GivenAWorkspaceWithAnEntityWithNoDeclaredOwner(string contextName, string entityName)
    {
        SetContext(contextName);
        DeclareEntity(entityName);
    }

    [Given("a workspace declaring the context {string}, the entity {string}, the entity {string} owned by {string}, and the entity {string} owned by {string}")]
    public void GivenAWorkspaceWithAThreeLevelOwnershipChain(
        string contextName, string firstEntity, string secondEntity, string secondOwner, string thirdEntity, string thirdOwner)
    {
        SetContext(contextName);
        DeclareEntity(firstEntity);
        DeclareEntity(secondEntity, secondOwner);
        DeclareEntity(thirdEntity, thirdOwner);
    }

    [Given("a workspace declaring the context {string} and the entity {string} owned by {string}, where no entity named {string} is declared")]
    public void GivenAWorkspaceWithAnEntityOwnedByAnUndeclaredEntity(string contextName, string entityName, string owner, string undeclaredName)
    {
        Assert.Equal(owner, undeclaredName);
        SetContext(contextName);
        DeclareEntity(entityName, owner);
    }

    [Given("a workspace declaring the context {string} and the entity {string} owned by {string}")]
    public void GivenAWorkspaceWithAnEntityOwnedBy(string contextName, string entityName, string owner)
    {
        SetContext(contextName);
        DeclareEntity(entityName, owner);
    }

    [Given("a workspace declaring the context {string}, the entity {string} owned by {string}, and the entity {string} owned by {string}")]
    public void GivenAWorkspaceWithTwoEntitiesEachOwnedByTheOther(
        string contextName, string firstEntity, string firstOwner, string secondEntity, string secondOwner)
    {
        SetContext(contextName);
        DeclareEntity(firstEntity, firstOwner);
        DeclareEntity(secondEntity, secondOwner);
    }

    [Given("a workspace declaring the context {string}, the entity {string} owned by {string}, the entity {string} owned by {string}, and the entity {string} owned by {string}")]
    public void GivenAWorkspaceWithALongerOwnershipCycle(
        string contextName, string firstEntity, string firstOwner, string secondEntity, string secondOwner, string thirdEntity, string thirdOwner)
    {
        SetContext(contextName);
        DeclareEntity(firstEntity, firstOwner);
        DeclareEntity(secondEntity, secondOwner);
        DeclareEntity(thirdEntity, thirdOwner);
    }

    [Given("a workspace declaring the context {string}, the entity {string} owned by {string}, the entity {string} owned by {string}, and the entity {string} with no declared owner")]
    public void GivenAWorkspaceWithAThreeLevelOwnershipChainDeclaredLeafFirst(
        string contextName, string firstEntity, string firstOwner, string secondEntity, string secondOwner, string thirdEntity)
    {
        SetContext(contextName);
        DeclareEntity(firstEntity, firstOwner);
        DeclareEntity(secondEntity, secondOwner);
        DeclareEntity(thirdEntity);
    }

    [Then("the compiled model records {string} as the aggregate-root owner of {string}")]
    public void ThenTheCompiledModelRecordsAsTheAggregateRootOwnerOf(string ownerName, string ownedName)
    {
        var owner = Entity(ownerName);
        var owned = Entity(ownedName);
        Assert.Equal(owner.Id, owned.OwnerId);
    }

    [Then("the compiled model records that {string} has no aggregate-root owner")]
    public void ThenTheCompiledModelRecordsThatHasNoAggregateRootOwner(string name) =>
        Assert.Null(Entity(name).OwnerId);

    [Then("compilation fails with a diagnostic explaining the owner entity cannot be resolved")]
    public void ThenCompilationFailsBecauseTheOwnerEntityCannotBeResolved() => AssertFailureCode("rml.reference.unresolved");

    [Then("compilation fails with a diagnostic explaining an entity cannot own itself")]
    public void ThenCompilationFailsBecauseAnEntityCannotOwnItself() => AssertFailureCode("rml.entity.owner-self");

    [Then("compilation fails with a diagnostic explaining aggregate ownership cannot be circular")]
    public void ThenCompilationFailsBecauseAggregateOwnershipCannotBeCircular() => AssertFailureCode("rml.entity.owner-cycle");

    private void SetContext(string contextName)
    {
        _contextName = contextName;
        _entities.Clear();
    }

    private void DeclareEntity(string name, string? owner = null) => _entities.Add((name, owner));

    private EntityDefinition Entity(string name) =>
        _compileResult!.Contexts
            .SelectMany(context => context.AuthoredRevision.Definitions.OfType<EntityDefinition>())
            .Single(entity => entity.Name.Value == name);

    private void Compile()
    {
        var source = new SourceDocument("workspace.rml", BuildSource());
        _compileResult = RmlCompiler.CompileWorkspace([source], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine($"context {_contextName}")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        foreach (var (name, owner) in _entities)
        {
            source.AppendLine($"entity {name}");
            if (owner is not null) source.AppendLine($"  owner \"{owner}\"");
            source.AppendLine("end");
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
}
