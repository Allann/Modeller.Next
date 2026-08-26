using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class ChildMedicalConsentAndConfirmationSteps
{
    private readonly WorkspaceCompilationContext _context;
    private ParseResult? _result;

    public ChildMedicalConsentAndConfirmationSteps(WorkspaceCompilationContext context)
    {
        _context = context;
    }

    [Given("the legacy-derived Child wellbeing and support model")]
    public void GivenTheLegacyDerivedChildWellbeingAndSupportModel()
    {
        _context.Compile = () =>
        {
            _result = RmlCompiler.Compile(
                [new SourceDocument("child-wellbeing.modeller", Source)],
                ParseOptions.EditorLanguage1,
                TestContext.Current.CancellationToken);
            _context.IsSuccess = _result.IsSuccess;
            _context.FailureSummary = string.Join("; ", _result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Then("the child has consent, medical record, additional needs, and CCSS confirmation relationships")]
    public void ThenTheChildHasTheSupportRelationships()
    {
        var child = _result!.FindEntity("Child");
        child.AssertRelationship("Consents", RelationshipCardinality.Many, optional: true);
        child.AssertRelationship("Medical record", RelationshipCardinality.One, optional: true);
        child.AssertRelationship("Additional needs", RelationshipCardinality.Many, optional: true);
        child.AssertRelationship("CCSS confirmed child", RelationshipCardinality.One, optional: true);
    }

    [Then("the medical record retains its alerts, review date, dietary requirements, conditions, and immunisation statuses")]
    public void ThenTheMedicalRecordRetainsItsLegacyShape()
    {
        var record = _result!.FindEntity("Medical record");
        record.AssertField("Medical support plan", type => type is StringDataType, optional: true);
        record.AssertField("Has medical alert", type => type is BooleanDataType, optional: false);
        record.AssertField("Immunisation review date", type => type is DateDataType, optional: true);
        record.AssertField("Dietary requirements", type => type is StringDataType, optional: true);
        record.AssertField("Has additional needs alert", type => type is BooleanDataType, optional: false);
        record.AssertRelationship("Medical conditions", RelationshipCardinality.Many, optional: true);
        record.AssertRelationship("Immunisation statuses", RelationshipCardinality.Many, optional: true);
    }

    [Then("an additional need retains its dates, diagnosis, comments, and specialised support")]
    public void ThenAnAdditionalNeedRetainsItsLegacyShape()
    {
        var need = _result!.FindEntity("Child additional need");
        need.AssertField("Comments", type => type is StringDataType, optional: true);
        need.AssertField("Date advised", type => type is DateDataType, optional: false);
        need.AssertField("Review date", type => type is DateDataType, optional: true);
        need.AssertField("End date", type => type is DateDataType, optional: true);
        need.AssertRelationship("Additional need", RelationshipCardinality.One, optional: false);
        need.AssertRelationship("Specialised support required", RelationshipCardinality.Many, optional: true);
        need.AssertRelationship("Diagnosed", RelationshipCardinality.One, optional: false);
    }

    [Then("the CCSS confirmation retains its service identifier, CRN, and date of birth")]
    public void ThenTheCcssConfirmationRetainsItsLegacyShape()
    {
        var confirmation = _result!.FindEntity("CCSS confirmed child");
        confirmation.AssertField("Service identifier", type => type is StringDataType, optional: true);
        confirmation.AssertField("CRN", type => type is StringDataType, optional: true);
        confirmation.AssertField("Date of birth", type => type is DateDataType, optional: true);
    }

    private const string Source = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity Consent
        end
        entity Medical condition
        end
        entity Immunisation status
        end
        entity Child specialised support required
        end
        entity Child additional need type
        end
        entity Child additional needs diagnosed
        end
        entity Medical record
          field Medical support plan
            type string
            optional
          end
          field Has medical alert
            type boolean
          end
          field Immunisation review date
            type date
            optional
          end
          field Dietary requirements
            type string
            optional
          end
          field Has additional needs alert
            type boolean
          end
          relationship Medical conditions
            target "Medical condition"
            cardinality many
            optional
          end
          relationship Immunisation statuses
            target "Immunisation status"
            cardinality many
            optional
          end
        end
        entity Child additional need
          field Comments
            type string
            optional
          end
          field Date advised
            type date
          end
          field Review date
            type date
            optional
          end
          field End date
            type date
            optional
          end
          relationship Additional need
            target "Child additional need type"
            cardinality one
          end
          relationship Specialised support required
            target "Child specialised support required"
            cardinality many
            optional
          end
          relationship Diagnosed
            target "Child additional needs diagnosed"
            cardinality one
          end
        end
        entity CCSS confirmed child
          field Service identifier
            type string
            optional
          end
          field CRN
            type string
            optional
          end
          field Date of birth
            type date
            optional
          end
        end
        entity Child
          relationship Consents
            target "Consent"
            cardinality many
            optional
          end
          relationship Medical record
            target "Medical record"
            cardinality one
            optional
          end
          relationship Additional needs
            target "Child additional need"
            cardinality many
            optional
          end
          relationship CCSS confirmed child
            target "CCSS confirmed child"
            cardinality one
            optional
          end
        end
        """;
}
