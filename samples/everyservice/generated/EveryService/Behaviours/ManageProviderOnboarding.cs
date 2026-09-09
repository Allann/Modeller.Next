namespace EveryService;

public static class ManageProviderOnboarding
{
    public static ProviderOnboardingStage Apply(
        ProviderOnboardingStage current,
        ProfessionalVerificationFacts facts) =>
        current == ProviderOnboardingStage.OnboardingInProgress && ProfessionalVerification.Determine(facts)
            ? ProviderOnboardingStage.OnboardingPendingReview
            : 
        current == ProviderOnboardingStage.OnboardingPendingReview && ProfessionalVerification.Determine(facts)
            ? ProviderOnboardingStage.OnboardingActivated
            : current;
}
