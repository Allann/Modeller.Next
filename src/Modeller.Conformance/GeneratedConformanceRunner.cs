namespace Modeller.Conformance;

public interface IConformanceFixtureGenerator
{
    string Version { get; }
    ConformanceFixture Generate(long seed);
}

public delegate ValueTask<ConformanceFixture> GeneratedFailureMinimizer(
    ConformanceFixture fixture,
    IConformanceAdapter adapter,
    CancellationToken cancellationToken);

public sealed record GeneratedConformanceFailure(
    string SchemaVersion,
    string GeneratorVersion,
    long Seed,
    ConformanceFixture MinimizedFixture,
    ConformanceReport Report);

public sealed record GeneratedConformanceResult(
    ConformanceFixture Fixture,
    ConformanceReport Report,
    GeneratedConformanceFailure? Failure);

public static class GeneratedConformanceRunner
{
    public static async ValueTask<GeneratedConformanceResult> RunAsync(
        IConformanceFixtureGenerator generator,
        long seed,
        IConformanceAdapter adapter,
        GeneratedFailureMinimizer minimizer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(minimizer);

        var fixture = generator.Generate(seed);
        var report = await ConformanceRunner.RunAsync(fixture, adapter, cancellationToken);
        if (report.Status == ConformanceStatus.Passed)
        {
            return new GeneratedConformanceResult(fixture, report, null);
        }

        var minimized = await minimizer(fixture, adapter, cancellationToken);
        var minimizedReport = await ConformanceRunner.RunAsync(minimized, adapter, cancellationToken);
        var failure = new GeneratedConformanceFailure(
            "1.0",
            generator.Version,
            seed,
            minimized,
            minimizedReport);
        return new GeneratedConformanceResult(fixture, report, failure);
    }
}
