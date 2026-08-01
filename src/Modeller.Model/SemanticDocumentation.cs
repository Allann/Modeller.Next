namespace Modeller.Model;

public sealed record SemanticDocumentation(
    string? Purpose = null,
    string? OwnershipAndIdentity = null,
    string? SemanticContract = null,
    string? Relationships = null,
    string? InvariantsAndConstraints = null,
    string? AcceptanceExamples = null,
    string? EvolutionAndCompatibility = null,
    string? ImplementationGuidance = null);
