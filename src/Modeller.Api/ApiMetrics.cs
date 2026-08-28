using System.Diagnostics.Metrics;

namespace Modeller.Api;

/// <summary>Custom OpenTelemetry meter, separate from the automatic ASP.NET Core/HttpClient
/// instrumentation registered in <c>Program.cs</c>. <see cref="ProcessStarts"/> increments once
/// per process lifetime — on a host that scales to zero between requests, a graph of this counter
/// climbing without matching request volume is the signature of a reconnect/cold-start loop.</summary>
public static class ApiMetrics
{
    public const string MeterName = "Modeller.Api";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> ProcessStartsCounter =
        Meter.CreateCounter<long>("modeller_api.process_starts_total", description: "Incremented once per process start.");

    public static void RecordProcessStart() => ProcessStartsCounter.Add(1);
}
