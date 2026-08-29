using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Modeller.Api.Contracts;
using Modeller.Api.Initiative;
using Modeller.Projections;

namespace Modeller.Api.OpenApi;

/// <summary>Attaches one representative example to every DTO schema the generated OpenAPI
/// document references, keyed by CLR type. Scalar (and any other OpenAPI viewer) renders
/// <see cref="OpenApiSchema.Example"/> wherever that schema appears — request body or response —
/// so one example per type covers every endpoint that uses it, without repeating per-operation
/// JSON by hand. Registered via <c>AddOpenApi(options => options.AddSchemaTransformer&lt;...&gt;())</c>
/// in Program.cs.</summary>
public sealed class ExampleSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (Examples.TryGetValue(context.JsonTypeInfo.Type, out var example))
            schema.Example = JsonSerializer.SerializeToNode(example, context.JsonTypeInfo.Type, context.JsonTypeInfo.Options) as JsonNode;
        return Task.CompletedTask;
    }

    private static readonly WorkspaceDocumentDto SampleDocument = new(
        "entities/child.modeller",
        "rml 1.0\nentity Child\n  field First name\n    type string\n  end\nend\n");

    private static readonly ConfigurationDto SampleConfiguration = new("1.0", "generated");

    private static readonly WorkspaceAnalyzeRequest SampleAnalyzeRequest = new(
        [SampleDocument],
        new EphemeralIdentityDto(),
        SampleConfiguration,
        [new ProjectionRequestDto("active", ViewKind.Lifecycle, ["child"])]);

    private static readonly DurableIdentityDto SampleDurableIdentity = new(
        "1.0", new Dictionary<string, List<string>> { ["entities/child.modeller"] = ["child-01hx8k2z"] });

    private static readonly ApiDiagnostic SampleDiagnostic = new(
        "rml.field.type-unknown", "Unknown field type 'strng'. Did you mean 'string'?",
        new ApiSourceSpan("entities/child.modeller", 5, 10, 5));

    private static readonly RootSummaryDto SampleRoot = new("child", ViewKind.Lifecycle, "Child", "child");

    private static readonly SemanticOutlineItemDto SampleOutlineItem = new(
        "child", "Entity", "Child", null, new ApiSourceSpan("entities/child.modeller", 3, 1, 5));

    private static readonly ApiProjectionGraph SampleGraph = new(
        1, ViewKind.Lifecycle,
        [new ApiProjectionNode("child.draft", "state", "Draft", ["child"])],
        [new ApiProjectionEdge("child.draft-to-enrolled", "transition", "Enrol", "child.draft", "child.enrolled", ["child"])]);

    private static readonly ProjectionResponseDto SampleProjection = new("active", true, SampleGraph, []);

    private static readonly WorkspaceAnalyzeResponse SampleAnalyzeResponse = new(
        "1.0", [], [SampleRoot], [SampleOutlineItem], [new SemanticCountDto("Entity", 1)], [SampleProjection], SampleDurableIdentity);

    private static readonly SupportedViewsResponse SampleSupportedViews = new(
        "1.0", [ViewKind.Lifecycle, ViewKind.Structural, ViewKind.BehaviourMap]);

    private static readonly WorkspaceCompletionRequest SampleCompletionRequest = new(SampleAnalyzeRequest, "entities/child.modeller", 5, 10);

    private static readonly CompletionItemDto SampleCompletionItem = new("string", "type", "The built-in text type", "string", 5);

    private static readonly WorkspaceCompletionResponse SampleCompletionResponse = new("1.0", [SampleCompletionItem], []);

    private static readonly WorkspaceExportResponse SampleExportResponse = new(
        "1.0", [], [SampleDocument with { Content = SampleDocument.Content.TrimEnd('\n') + "\n# @id=child-01hx8k2z\n" }], SampleDurableIdentity);

    private static readonly Guid SampleInitiativeId = Guid.Parse("b6f1c8b0-5a1e-4c2a-9d3f-2a7e6c1d4f90");
    private static readonly Guid SampleQuestionId = Guid.Parse("a1e2c3d4-5f60-4718-9a2b-3c4d5e6f7081");
    private static readonly Guid SampleResponseId = Guid.Parse("c9d8e7f6-1a2b-4c3d-8e9f-0a1b2c3d4e5f");
    private static readonly Guid SampleParticipantId = Guid.Parse("11112222-3333-4444-5555-666677778888");

    private static readonly CreateInitiativeRequest SampleCreateInitiativeRequest = new(
        "Add a subsidised absence reason for pupil-free days.", "Priya Facilitator", "Alex Domain Expert");

    private static readonly ProposeQuestionRequestDto SampleProposeQuestionRequest = new(
        "BusinessStatement", "Does this apply to casual bookings too?");

    private static readonly InitiativeCredentialsDto SampleInitiativeCredentials = new(
        "eyJTZXNzaW9uSWQiOiJiNmYxYzhiMC01YTFlLTRjMmEtOWQzZi0yYTdlNmMxZDRmOTAiLCJSb2xlIjowLCJFeHBpcmVzQXRVbml4U2Vjb25kcyI6MTc5MjM3NjAwMH0.c2lnbmF0dXJl",
        "eyJTZXNzaW9uSWQiOiJiNmYxYzhiMC01YTFlLTRjMmEtOWQzZi0yYTdlNmMxZDRmOTAiLCJSb2xlIjoxLCJFeHBpcmVzQXRVbml4U2Vjb25kcyI6MTc5MjM3NjAwMH0.c2lnbmF0dXJl");

    private static readonly SubmitResponseRequestDto SampleSubmitResponseRequest = new(
        "Yes — casual bookings use the same non-chargeable reason.");

    private static readonly SelectInterventionRequestDto SampleSelectInterventionRequest = new(
        "RuleChange", "Add PupilFreeDay to AbsenceNonChargeableReason.", "Matches the existing Absence pattern.", true);

    private static readonly LinkDesignWorkspaceRequestDto SampleLinkDesignWorkspaceRequest = new("https://modeller.website/playground#session=abc123");

    private static readonly RecordGateEvaluationRequestDto SampleRecordGateEvaluationRequest = new(
        "Discovery", [new GateCheckResultDto("business-statement-present", true, "A Business Statement was recorded.")]);

    private static readonly DismissGateFindingRequestDto SampleDismissGateFindingRequest = new(
        "business-statement-present", "Confirmed with the Domain Expert as intentionally deferred.");

    private static readonly FinalizeRequestDto SampleFinalizeRequest = new("Both gates passed; ready for design.");

    private static readonly QuestionDto SampleQuestion = new(
        SampleQuestionId, "Does this apply to casual bookings too?", SampleParticipantId, "DomainExpert", "BusinessStatement", "Sent");

    private static readonly ResponseDto SampleResponse = new(
        SampleResponseId, SampleQuestionId, "Yes — casual bookings use the same non-chargeable reason.", "Accepted");

    private static readonly GateEvaluationDto SampleGateEvaluation = new(
        "Discovery", [new GateCheckResultDto("business-statement-present", true, "A Business Statement was recorded.")],
        null, DateTimeOffset.Parse("2026-08-20T09:15:00Z"), "HumanOnly");

    private static readonly InitiativeSessionDto SampleInitiativeSession = new(
        SampleInitiativeId,
        "Add a subsidised absence reason for pupil-free days.",
        [new ParticipantDto(SampleParticipantId, "Priya Facilitator", "Facilitator")],
        [SampleQuestion],
        [SampleResponse],
        [],
        [],
        SampleGateEvaluation,
        null,
        null);

    private static readonly CreateInitiativeResponseDto SampleCreateInitiativeResponse = new(SampleInitiativeSession, SampleInitiativeCredentials);

    private static readonly AgentInterventionSuggestionsResponse SampleInterventionSuggestions = new(
        [new AgentInterventionSuggestionDto("RuleChange", "Add PupilFreeDay to AbsenceNonChargeableReason.", "Matches the existing Absence pattern.")]);

    private static readonly AgentAdvisorStatusResponse SampleAgentAdvisorStatus = new(true, "gpt-4o-mini", false, "gpt-4o-mini");

    private static readonly InitiativeErrorResponse SampleInitiativeError = new(
        "initiative.gate.not-satisfied", "The Discovery gate has not passed and no override was recorded.");

    private static readonly Dictionary<Type, object> Examples = new()
    {
        [typeof(WorkspaceDocumentDto)] = SampleDocument,
        [typeof(ConfigurationDto)] = SampleConfiguration,
        [typeof(ProjectionRequestDto)] = new ProjectionRequestDto("active", ViewKind.Lifecycle, ["child"]),
        [typeof(EphemeralIdentityDto)] = new EphemeralIdentityDto(),
        [typeof(DurableIdentityDto)] = SampleDurableIdentity,
        [typeof(WorkspaceAnalyzeRequest)] = SampleAnalyzeRequest,
        [typeof(WorkspaceAnalyzeResponse)] = SampleAnalyzeResponse,
        [typeof(ApiDiagnostic)] = SampleDiagnostic,
        [typeof(ApiSourceSpan)] = new ApiSourceSpan("entities/child.modeller", 5, 10, 5),
        [typeof(RootSummaryDto)] = SampleRoot,
        [typeof(SemanticOutlineItemDto)] = SampleOutlineItem,
        [typeof(SemanticCountDto)] = new SemanticCountDto("Entity", 1),
        [typeof(ProjectionResponseDto)] = SampleProjection,
        [typeof(ApiProjectionGraph)] = SampleGraph,
        [typeof(SupportedViewsResponse)] = SampleSupportedViews,
        [typeof(WorkspaceCompletionRequest)] = SampleCompletionRequest,
        [typeof(CompletionItemDto)] = SampleCompletionItem,
        [typeof(WorkspaceCompletionResponse)] = SampleCompletionResponse,
        [typeof(WorkspaceExportResponse)] = SampleExportResponse,

        [typeof(CreateInitiativeRequest)] = SampleCreateInitiativeRequest,
        [typeof(ProposeQuestionRequestDto)] = SampleProposeQuestionRequest,
        [typeof(SubmitResponseRequestDto)] = SampleSubmitResponseRequest,
        [typeof(SelectInterventionRequestDto)] = SampleSelectInterventionRequest,
        [typeof(LinkDesignWorkspaceRequestDto)] = SampleLinkDesignWorkspaceRequest,
        [typeof(RecordGateEvaluationRequestDto)] = SampleRecordGateEvaluationRequest,
        [typeof(DismissGateFindingRequestDto)] = SampleDismissGateFindingRequest,
        [typeof(FinalizeRequestDto)] = SampleFinalizeRequest,
        [typeof(GateCheckResultDto)] = new GateCheckResultDto("business-statement-present", true, "A Business Statement was recorded."),
        [typeof(AgentInterventionSuggestionDto)] = new AgentInterventionSuggestionDto(
            "RuleChange", "Add PupilFreeDay to AbsenceNonChargeableReason.", "Matches the existing Absence pattern."),
        [typeof(AgentInterventionSuggestionsResponse)] = SampleInterventionSuggestions,
        [typeof(AgentAdvisorStatusResponse)] = SampleAgentAdvisorStatus,
        [typeof(InitiativeErrorResponse)] = SampleInitiativeError,
        [typeof(ParticipantDto)] = new ParticipantDto(SampleParticipantId, "Priya Facilitator", "Facilitator"),
        [typeof(QuestionDto)] = SampleQuestion,
        [typeof(ResponseDto)] = SampleResponse,
        [typeof(GateEvaluationDto)] = SampleGateEvaluation,
        [typeof(InitiativeSessionDto)] = SampleInitiativeSession,
        [typeof(InitiativeCredentialsDto)] = SampleInitiativeCredentials,
        [typeof(CreateInitiativeResponseDto)] = SampleCreateInitiativeResponse,
    };
}
