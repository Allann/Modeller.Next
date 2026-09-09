namespace EveryService;

public static class CompleteJob
{
    public static JobStage Apply(
        JobStage current,
        object facts) =>
        current == JobStage.Scheduled && 
            ? JobStage.InProgress
            : 
        current == JobStage.InProgress && 
            ? JobStage.CompletedByProvider
            : 
        current == JobStage.CompletedByProvider && 
            ? JobStage.Confirmed
            : current;
}
