using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class FamilyAndRelatedAdultsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string _family = "Smith, Jane";
    private string? _pathway;
    private string? _referralSource;
    private readonly List<string> _children = [];
    private readonly List<string> _relatedAdults = [];
    private readonly List<string> _authorisations = [];
    private readonly List<(string Adult, int Rank)> _accountHolders = [];
    private string? _relationshipType;
    private int _displayPriority;
    private string? _familyAccount;
    private string? _account;
    private string? _centre;
    private string? _arrangement;
    private string? _payee;
    private ParseResult? _compileResult;

    public FamilyAndRelatedAdultsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the family {string} came to the centre through {string}")]
    public void GivenTheFamilyCameToTheCentreThrough(string family, string pathway) =>
        (_family, _pathway) = (family, pathway);

    [Given("its referral source is {string}")]
    public void GivenItsReferralSourceIs(string referralSource) => _referralSource = referralSource;

    [Given("the family {string} includes the children {string} and {string}")]
    public void GivenTheFamilyIncludesTheChildren(string family, string first, string second)
    {
        _family = family;
        _children.AddRange([first, second]);
    }

    [Given("the adult {string} is related to the family {string}")]
    public void GivenTheAdultIsRelatedToTheFamily(string adult, string family)
    {
        _family = family;
        _relatedAdults.Add(adult);
    }

    [Given("the relationship type is {string}")]
    public void GivenTheRelationshipTypeIs(string relationshipType) => _relationshipType = relationshipType;

    [Given("the display priority is {int}")]
    public void GivenTheDisplayPriorityIs(int priority) => _displayPriority = priority;

    [Given("the related adult has the authorisations {string} and {string}")]
    public void GivenTheRelatedAdultHasTheAuthorisations(string first, string second) =>
        _authorisations.AddRange([first, second]);

    [Given("the family {string} owns the family account {string}")]
    public void GivenTheFamilyOwnsTheFamilyAccount(string family, string familyAccount) =>
        (_family, _familyAccount) = (family, familyAccount);

    [Given("the family account uses the account {string}")]
    public void GivenTheFamilyAccountUsesTheAccount(string account) => _account = account;

    [Given("the adult {string} is its first account holder")]
    public void GivenTheAdultIsItsFirstAccountHolder(string adult) => _accountHolders.Add((adult, 1));

    [Given("the adult {string} is its second account holder")]
    public void GivenTheAdultIsItsSecondAccountHolder(string adult) => _accountHolders.Add((adult, 2));

    [Given("the child {string} belongs to the family {string}")]
    public void GivenTheChildBelongsToTheFamily(string child, string family)
    {
        _family = family;
        if (!_children.Contains(child, StringComparer.Ordinal)) _children.Add(child);
    }

    [Given("that child has an enrolment at the centre {string}")]
    public void GivenThatChildHasAnEnrolmentAtTheCentre(string centre) => _centre = centre;

    [Given("the enrolment has the arrangement {string}")]
    public void GivenTheEnrolmentHasTheArrangement(string arrangement) => _arrangement = arrangement;

    [Given("that arrangement is paid by the account {string}")]
    public void GivenThatArrangementIsPaidByTheAccount(string account) => _payee = account;

    [When("the family is reviewed")]
    [When("the family's related adults are reviewed")]
    [When("the family account is reviewed")]
    [When("the family's care and financial relationships are reviewed")]
    public void WhenTheFamilyIsReviewed() => Compile();

    [Then("its family name is {string}")]
    public void ThenItsFamilyNameIs(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, _family);
        Assert.Contains(_compileResult!.FindEntity(_family).Fields, field => field.Name.Value == "Family name" && field.IsOptional);
    }

    [Then("its pathway to the centre is {string}")]
    public void ThenItsPathwayToTheCentreIs(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, _pathway);
        Assert.Equal(expected, RelationshipTarget(_family, "Pathway to centre"));
    }

    [Then("its referral source is {string}")]
    public void ThenItsReferralSourceIs(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, _referralSource);
        Assert.Equal(expected, RelationshipTarget(_family, "Referral source"));
    }

    [Then("both children belong to that family")]
    public void ThenBothChildrenBelongToThatFamily()
    {
        EnsureCompiled();
        Assert.Equal(_children, RelationshipTargets(_family, "Children"));
    }

    [Then("{string} and {string} remain distinct children")]
    public void ThenTheChildrenRemainDistinct(string first, string second) => Assert.NotEqual(first, second);

    [Then("{string} is the first related adult displayed for that family")]
    public void ThenTheAdultIsTheFirstRelatedAdultDisplayed(string adult)
    {
        EnsureCompiled();
        var relatedAdult = RelationshipTarget(_family, "Related adults");
        Assert.Equal(adult, RelationshipTarget(relatedAdult, "Adult"));
        Assert.Equal(1, _displayPriority);
    }

    [Then("the relationship type is {string}")]
    public void ThenTheRelationshipTypeIs(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, RelationshipTarget($"{_relatedAdults[0]} relationship", "Relationship type"));
    }

    [Then("both authorisations belong to that related adult relationship")]
    public void ThenBothAuthorisationsBelongToThatRelatedAdultRelationship()
    {
        EnsureCompiled();
        Assert.Equal(_authorisations, RelationshipTargets($"{_relatedAdults[0]} relationship", "Authorisations"));
    }

    [Then("{string} remains an adult independently of that family relationship")]
    public void ThenTheAdultRemainsIndependent(string adult)
    {
        EnsureCompiled();
        Assert.NotEqual(_compileResult!.FindEntity(adult).Id, _compileResult!.FindEntity($"{adult} relationship").Id);
    }

    [Then("{string} and {string} are jointly responsible through distinct ranked account-holder records")]
    public void ThenTheAdultsAreJointlyResponsible(string first, string second)
    {
        EnsureCompiled();
        Assert.Equal([(first, 1), (second, 2)], _accountHolders);
        Assert.Equal(2, RelationshipTargets(_familyAccount!, "Account holders").Distinct().Count());
    }

    [Then("neither account holder is made a related adult by financial responsibility alone")]
    public void ThenNeitherAccountHolderIsMadeARelatedAdult() => Assert.Empty(_relatedAdults);

    [Then("the enrolment identifies the child {string}")]
    public void ThenTheEnrolmentIdentifiesTheChild(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, RelationshipTarget("Enrolment", "Child"));
    }

    [Then("the enrolment identifies the family {string}")]
    public void ThenTheEnrolmentIdentifiesTheFamily(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, RelationshipTarget("Enrolment", "Family"));
    }

    [Then("{string} belongs to that enrolment")]
    public void ThenTheArrangementBelongsToThatEnrolment(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, RelationshipTarget("Enrolment", "Arrangements"));
    }

    [Then("the arrangement payee is the account {string}")]
    public void ThenTheArrangementPayeeIsTheAccount(string expected)
    {
        EnsureCompiled();
        Assert.Equal(expected, RelationshipTarget(_arrangement!, "Payee"));
    }

    [Then("the arrangement does not become the family account")]
    public void ThenTheArrangementDoesNotBecomeTheFamilyAccount()
    {
        EnsureCompiled();
        Assert.NotEqual(RelationshipTarget(_arrangement!, "Payee"), _familyAccount);
    }

    private void EnsureCompiled()
    {
        if (_compileResult is null) Compile();
    }

    private void Compile()
    {
        _compileResult = RmlCompiler.Compile(
            [new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1,
            TestContext.Current.CancellationToken);
        _context.IsSuccess = _compileResult.IsSuccess;
        _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
    }

    private string RelationshipTarget(string entity, string relationship) => RelationshipTargets(entity, relationship).Single();

    private IReadOnlyList<string> RelationshipTargets(string entity, string relationshipPrefix)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        return _compileResult.FindEntity(entity).Relationships
            .Where(item => item.Name.Value.StartsWith(relationshipPrefix, StringComparison.Ordinal))
            .Select(item => revision.Definitions.OfType<EntityDefinition>().Single(target => target.Id == item.TargetId).Name.Value)
            .ToArray();
    }

    private string BuildSource()
    {
        var source = new StringBuilder("rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n");
        AddPeopleAndReferenceData(source);
        AddRelatedAdultRelationships(source);
        AddAccountHolders(source);
        AddFamilyAccount(source);
        AddFamily(source);
        AddEnrolment(source);
        return source.ToString();
    }

    private void AddPeopleAndReferenceData(StringBuilder source)
    {
        AddEntity(source, "Adult");
        foreach (var adult in _relatedAdults.Concat(_accountHolders.Select(holder => holder.Adult)).Distinct()) AddEntity(source, adult);
        foreach (var child in _children) AddEntity(source, child);
        if (_pathway is not null) AddDescribedEntity(source, _pathway);
        if (_referralSource is not null) AddDescribedEntity(source, _referralSource);
        if (_relationshipType is not null) AddDescribedEntity(source, _relationshipType);
        foreach (var authorisation in _authorisations) AddDescribedEntity(source, authorisation);
    }

    private void AddRelatedAdultRelationships(StringBuilder source)
    {
        foreach (var adult in _relatedAdults)
        {
            source.AppendLine($"entity \"{adult} relationship\"")
                .AppendLine("  field Display priority").AppendLine("    type integer").AppendLine("  end");
            AddRelationship(source, "Adult", adult, "one");
            if (_relationshipType is not null) AddRelationship(source, "Relationship type", _relationshipType, "one");
            for (var index = 0; index < _authorisations.Count; index++) AddRelationship(source, $"Authorisations {index + 1}", _authorisations[index], "many");
            source.AppendLine("end");
        }
    }

    private void AddAccountHolders(StringBuilder source)
    {
        foreach (var holder in _accountHolders)
        {
            source.AppendLine($"entity \"{holder.Adult} account holder\"")
                .AppendLine("  field Account holder rank").AppendLine("    type integer").AppendLine("  end");
            AddRelationship(source, "Adult", holder.Adult, "one");
            source.AppendLine("end");
        }
    }

    private void AddFamilyAccount(StringBuilder source)
    {
        if (_account is not null) AddEntity(source, _account);
        if (_familyAccount is not null)
        {
            source.AppendLine($"entity \"{_familyAccount}\"");
            if (_account is not null) AddRelationship(source, "Account", _account, "one");
            for (var index = 0; index < _accountHolders.Count; index++) AddRelationship(source, $"Account holders {index + 1}", $"{_accountHolders[index].Adult} account holder", "many");
            source.AppendLine("end");
        }
    }

    private void AddFamily(StringBuilder source)
    {
        source.AppendLine($"entity \"{_family}\"")
            .AppendLine("  field Family name").AppendLine("    type string").AppendLine("    optional").AppendLine("  end");
        if (_pathway is not null) AddRelationship(source, "Pathway to centre", _pathway, "one", optional: true);
        if (_referralSource is not null) AddRelationship(source, "Referral source", _referralSource, "one", optional: true);
        for (var index = 0; index < _children.Count; index++) AddRelationship(source, $"Children {index + 1}", _children[index], "many");
        for (var index = 0; index < _relatedAdults.Count; index++) AddRelationship(source, $"Related adults {index + 1}", $"{_relatedAdults[index]} relationship", "many");
        if (_familyAccount is not null) AddRelationship(source, "Family account", _familyAccount, "one");
        source.AppendLine("end");
    }

    private void AddEnrolment(StringBuilder source)
    {
        if (_centre is not null) AddEntity(source, _centre);
        if (_arrangement is not null)
        {
            source.AppendLine($"entity \"{_arrangement}\"");
            if (_payee is not null) AddRelationship(source, "Payee", _payee, "one");
            source.AppendLine("end");
        }
        if (_children.Count > 0 && _centre is not null && _arrangement is not null)
        {
            source.AppendLine("entity Enrolment").AppendLine($"  owner \"{_centre}\"");
            AddRelationship(source, "Child", _children[0], "one");
            AddRelationship(source, "Family", _family, "one");
            AddRelationship(source, "Arrangements", _arrangement, "many");
            source.AppendLine("end");
        }
    }

    private static void AddEntity(StringBuilder source, string name) => source.AppendLine($"entity \"{name}\"").AppendLine("end");

    private static void AddDescribedEntity(StringBuilder source, string name) => source.AppendLine($"entity \"{name}\"")
        .AppendLine("  field Description").AppendLine("    type string").AppendLine("  end").AppendLine("end");

    private static void AddRelationship(StringBuilder source, string name, string target, string cardinality, bool optional = false)
    {
        source.AppendLine($"  relationship {name}").AppendLine($"    target \"{target}\"").AppendLine($"    cardinality {cardinality}");
        if (optional) source.AppendLine("    optional");
        source.AppendLine("  end");
    }
}
