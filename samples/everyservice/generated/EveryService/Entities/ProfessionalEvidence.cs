namespace EveryService;



public sealed record ProfessionalEvidence(
    string EvidenceType,
    string Identifier,
    DateOnly? ExpiresOn,
    Guid Business
);

