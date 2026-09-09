namespace EveryService;

public sealed record ProfessionalVerificationFacts(
    bool RegisteredBusinessIdentityIsValid,
    bool ProviderOnboardingIsActivated
);

public static class ProfessionalVerification
{
    public static bool Determine(ProfessionalVerificationFacts facts) =>
        facts.RegisteredBusinessIdentityIsValid &&
        facts.ProviderOnboardingIsActivated;
}
