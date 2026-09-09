namespace EveryService;

public sealed record ProviderCapabilityPublicationFacts(
    bool ProviderOnboardingIsComplete,
    bool ProfessionalEvidenceIsCurrent
);

public static class ProviderCapabilityPublication
{
    public static bool Determine(ProviderCapabilityPublicationFacts facts) =>
        facts.ProviderOnboardingIsComplete &&
        facts.ProfessionalEvidenceIsCurrent;
}
