namespace ChildCare;

public sealed record ACCSEligibilityFacts(
    bool ActiveEnrolmentExists,
    bool SupportingEvidenceIsHeld
);

public static class ACCSEligibility
{
    public static bool Determine(ACCSEligibilityFacts facts) =>
        facts.ActiveEnrolmentExists &&
        facts.SupportingEvidenceIsHeld;
}
