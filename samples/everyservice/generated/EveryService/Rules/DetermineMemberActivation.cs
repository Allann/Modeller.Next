namespace EveryService;

public sealed record MemberActivationFacts(
    bool MemberContactIsValid
);

public static class MemberActivation
{
    public static bool Determine(MemberActivationFacts facts) =>
        facts.MemberContactIsValid;
}
