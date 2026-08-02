namespace ChildCare;

public enum ACCSDeterminationApplicationStage
{
    Draft,
    Submitted
}

public static class SubmitACCSDeterminationApplication
{
    public static ACCSDeterminationApplicationStage Apply(
        ACCSDeterminationApplicationStage current,
        ACCSEligibilityFacts facts) =>
        current == ACCSDeterminationApplicationStage.Draft && ACCSEligibility.Determine(facts)
            ? ACCSDeterminationApplicationStage.Submitted
            : current;
}
