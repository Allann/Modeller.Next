namespace EveryService;

public enum ProviderOnboardingStage
{
    OnboardingInProgress,
    OnboardingPendingReview,
    OnboardingActivated
}



public abstract record ProviderOnboarding(
    Guid Member
);

public sealed record ProviderOnboardingOnboardingInProgress(
    Guid Member,
    int IntentStep
) : ProviderOnboarding(
    Member
);

public sealed record ProviderOnboardingOnboardingPendingReview(
    Guid Member
) : ProviderOnboarding(
    Member
);

public sealed record ProviderOnboardingOnboardingActivated(
    Guid Member,
    DateTimeOffset ActivatedAt
) : ProviderOnboarding(
    Member
);


